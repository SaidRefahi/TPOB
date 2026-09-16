---
title: Two Pilots, One Robot - Informe de Arquitectura de Software
tags:
  - architecture
  - purrnet
  - vcontainer
  - unitask
  - cinemachine
  - solid
  - zerogc
created: 2026-09-16
updated: 2026-09-16
---

# 🏛️ Informe de Arquitectura de Software: Two Pilots, One Robot (TPOB)

Este documento define la arquitectura técnica del proyecto, diseñada bajo el **Master Engineering Manifesto** (Cero Garbage Collection en rutas calientes, encapsulación estricta, desacoplamiento por interfaces y Ultra-KISS) e integrando de forma cohesiva la suite de addons instalados: **PurrNet**, **VContainer**, **UniTask**, **Cinemachine 3.x**, **DOTween** y **Tri-Inspector**.

---

## 🌐 1. Topología de Red y Física con PurrNet

TPOB adopta un modelo **Servidor-Autoritativo con Simulación de Física Centralizada**. Ningún cliente simula la física autoritativa localmente para evitar discrepancias numéricas en PhysX.

```
       CLIENTE 1 (Piernas)                   SERVIDOR PURRNET                   CLIENTE 2 (Torso)
   ┌───────────────────────────┐         ┌───────────────────────┐         ┌───────────────────────────┐
   │ UnityEngine.InputSystem   │         │ PurrNet onTick Engine │         │ UnityEngine.InputSystem   │
   │  ↳ ReadValue<Vector2>()   │         │  ↳ Procesa InputData  │         │  ↳ ReadValue<Vector2>()   │
   │  ↳ Empaqueta struct       │         │  ↳ Simula PhysX World │         │  ↳ Empaqueta struct       │
   └─────────────┬─────────────┘         └───────────┬───────────┘         └─────────────┬─────────────┘
                 │ ServerRpc(Unreliable)             │                           │ ServerRpc(Unreliable)
                 ▼                                   ▼                           ▼
          [InputData Struct] ────────────────► (Ejecuta Comandos) ◄────────────── [InputData Struct]
                                                     │
                                                     ▼
                                    ┌─────────────────────────────────┐
                                    │ NetworkTransform / NetworkRigid │
                                    │ (Replica estados interpolados)  │
                                    └────────────────┬────────────────┘
                                                     │
                             ┌───────────────────────┴───────────────────────┐
                             ▼                                               ▼
               ┌───────────────────────────┐                   ┌───────────────────────────┐
               │ Cliente 1: Interpolación  │                   │ Cliente 2: Interpolación  │
               │ (Rigidbody.isKinematic)   │                   │ (Rigidbody.isKinematic)   │
               └───────────────────────────┘                   └───────────────────────────┘
```

### Reglas de Implementación en Red:
1. **Input Structs (Zero Allocations):**
   * Cada cliente lee el hardware en `Update()` a través del `UnityEngine.InputSystem`.
   * En el evento de tick de red (`onTick`), se empaquetan en un `struct LegsInputData` o `struct TorsoInputData` serializable por valor (cero asignación en heap).
   * Se transmiten mediante `[ServerRpc(Channel.Unreliable)]` para minimizar la latencia.
2. **Kinematic en Clientes:**
   * En los clientes que no son servidor, todos los `Rigidbody` de entidades interactivas y jugadores se marcan como `isKinematic = !isServer`.
   * La posición y rotación se suavizan mediante el componente nativo `NetworkTransform` de PurrNet (interpolación de buffers).
3. **Fusión en Servidor:**
   * Al fusionarse, el servidor emparenta el transform de Torso al socket receptor de Piernas.
   * El `Rigidbody` de Torso se desactiva o congela, sumando su masa al `Rigidbody` de Piernas.
   * El servidor continúa recibiendo los comandos de apuntado y agarre del Jugador 2 y los aplica localmente en el sub-transform del Torso montado.

---

## 💉 2. Inyección de Dependencias con VContainer

Siguiendo el principio del Manifiesto de Ingeniería: *"Prefer Service Locator or DI over Singletons"*, se eliminan los Singletons estáticos con `DontDestroyOnLoad`. La resolución de dependencias se gestiona mediante la jerarquía de scopes de **VContainer**:

```
                       ┌──────────────────────────────────────┐
                       │          GameLifetimeScope           │  (Root Scope - DontDestroyOnLoad)
                       │  • INetworkManagerWrapper            │
                       │  • IPlayerRegistry                   │
                       │  • IAudioService                     │
                       │  • GameEventBus                      │
                       └──────────────────┬───────────────────┘
                                          │
                                          ▼ (Hereda e inyecta en cada escena)
                       ┌──────────────────────────────────────┐
                       │          RoomLifetimeScope           │  (Scene Scope - Nivel de Sala)
                       │  • RoomController                    │
                       │  • IRoomCompletionEvaluator          │
                       │  • CinemachineTargetGroupCoordinator │
                       │  • SpawnPointManager                 │
                       └──────────────────────────────────────┘
```

### Ventajas de VContainer frente a Singletons:
* **Desacoplamiento Total:** Los controladores de sala y jugadores reciben interfaces (`IPlayerRegistry`, `GameEventBus`) por constructor o método `[Inject]`, facilitando pruebas unitarias y mocking.
* **Ciclo de Vida Limpio:** Al descargar una sala y cargar la siguiente, el `RoomLifetimeScope` destruye limpiamente sus suscripciones sin dejar referencias colgantes en memoria.

---

## ⚡ 3. Asincronismo de Alto Rendimiento con UniTask

Las corrutinas tradicionales de Unity (`IEnumerator`) generan recolección de basura innecesaria por la creación de objetos de yield (`new WaitForSeconds(...)`).

TPOB adopta **UniTask** en todas las operaciones asíncronas:
* **Carga de Escenas en Red:**
  ```csharp
  public async UniTask LoadRoomAsync(string roomSceneName, CancellationToken ct) {
      await _networkManager.sceneModule.LoadSceneAsync(roomSceneName, _sceneSettings);
      await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
  }
  ```
* **Transiciones y Delays Zero-GC:** Las transiciones de pantalla, secuencias de respawn y pausas de puertas mecánicas utilizan `UniTask.Delay(TimeSpan, cancellationToken: this.GetCancellationTokenOnDestroy())`.
* **Cancelación Garantizada:** Cualquier tarea en vuelo se cancela automáticamente cuando el objeto se desactiva o destruye (`OnDisable`/`OnDestroy`), previniendo errores de referencia nula.

---

## 🎥 4. Sistema de Cámara Dinámico con Cinemachine 3.x

El juego presenta un desafío visual único: alternar entre un único avatar (Robot Fusionado) y dos entidades que pueden distanciarse por extremos opuestos de la sala (Piernas y Torso Separados).

```
   ESTADO FUSIONADO                                 ESTADO SEPARADO
┌───────────────────────┐              ┌─────────────────────────────────────────┐
│ CinemachineCamera     │              │ CinemachineCamera                       │
│  ↳ Target: Robot      │              │  ↳ Target: CinemachineTargetGroup       │
│  ↳ Zoom cercano       │              │      ├─ Target A: Piernas (Weight: 1)   │
│  ↳ Encuadre centrado  │              │      └─ Target B: Torso   (Weight: 1)   │
│                       │              │  ↳ Zoom dinámico según separación       │
└───────────────────────┘              └─────────────────────────────────────────┘
```

1. **`CinemachineTargetGroup`:**
   * En **Separación**, tanto Piernas como Torso son miembros activos del `TargetGroup` con peso `1.0`. La cámara adapta automáticamente el encuadre (FOV o tamaño ortográfico) para mantener a ambos jugadores siempre visibles en pantalla.
   * En **Fusión**, el peso de Torso pasa a `0` y la cámara se enfoca con un encuadre más ajustado e inmersivo en el Robot.
2. **Screen Shake con `CinemachineImpulseSource`:**
   * La patada de Piernas (`KickCommand`), el impacto de caídas pesadas y el golpe de compuertas emiten impulsos físicos sin escribir código custom de sacudida de cámara.

---

## ✨ 5. Juicing Procedural con DOTween

Las retroalimentaciones estéticas y táctiles que no involucran colisiones físicas de PhysX se delegan a **DOTween**:
* **Sensación de Pulsadores:** Efecto de hundimiento elástico (`transform.DOMoveY(...)`) con easing `EaseOutQuad`.
* **Compuertas y Puentes:** Animación de apertura mediante interpolación procedural precisa iniciada tras el evento de red.
* **Haz Magnético de Torso:** Pulsación de escala en el indicador visual mientras atrae objetos metálicos.
* **Efecto Snap de Fusión:** Un ligero escalado tipo `PunchScale` al completarse el acople para otorgar peso y satisfacción al impacto visual.

---

## 🛠️ 6. Ergonomía del Inspector con Tri-Inspector

Para evitar la creación de custom inspectors farragosos y acelerar el testeo directo:
* **Agrupación Limpia:** Uso de `[Title]`, `[Group("Física")]` y `[Group("Red")]` en scripts de gameplay.
* **Botones de Runtime:**
  ```csharp
  [TriInspector.Button("Simular Fusión")]
  private void DebugForceFuse() => RequestFusionServerRpc();

  [TriInspector.Button("Completar Sala")]
  private void DebugCompleteRoom() => ForceCompleteRoom();
  ```
* **Inspección en Tiempo Real:** Visualización de estados (`SyncVar`) con `[ShowInInspector]` sin romper la encapsulación de variables privadas.

---

## 🧩 7. Patrones de Diseño de Software

### 1. Patrón Command (Encapsulamiento de Acciones)
* **Objetivo:** Desacoplar la recepción de entradas del transporte de red y de la ejecución física.
* **Contrato:**
  ```csharp
  public interface IPlayerCommand {
      void Execute(PlayerContext context);
  }
  ```
* **Comandos Clave:** `MoveCommand`, `JumpCommand`, `KickCommand`, `GrabCommand`, `ThrowCommand`, `ClimbCommand`, `FuseCommand`, `SeparateCommand`.
* Permite añadir nuevas habilidades creando una clase/struct que implemente `IPlayerCommand` sin modificar el código de red.

### 2. Patrón Strategy (Fusión y Comportamiento)
* `RobotCoordinator` conmuta la estrategia activa mediante `IRobotStrategy`:
  * `FusionStrategy`: Unifica los inputs en una entidad motriz única.
  * `SeparationStrategy`: Desglosa los inputs en canales físicos independientes.

### 3. Patrón Observer (Bus de Eventos de Red)
* **Local:** `GameEventBus` centralizado en VContainer emite eventos de C# desacoplados.
* **Red:** `NetworkEventRelay` intercepta eventos clave y los retransmite mediante PurrNet `[ObserversRpc(BufferLast = true)]` o `Broadcasts` (`IPackedAuto`).
* Eventos: `PlayerDiedEvent`, `PlayerRespawnedEvent`, `RoomCompletedEvent`, `RobotFusedEvent`, `RobotSeparatedEvent`.

### 4. Patrón Composite (Criterios de Sala)
* `RoomCompletion` actúa como nodo raíz que evalúa un árbol lógico de condiciones (`ICompletionCondition`).
* Soporta composiciones `AndCondition` y `OrCondition`:
  * Ej: (Puerta Abierta **AND** Dos Jugadores en la Salida) **OR** (Interruptor Maestro Activado).

### 5. Patrón State (Ciclo de Vida de Sala)
* `RoomController` implementa una máquina de estados determinista:
  `RoomInactive ➔ RoomActive ➔ RoomCompleted ➔ RoomTransitioning`.
* Replicado en red mediante PurrNet `NetworkStateMachine` o `SyncVar<RoomState>`.

---

## 🚫 8. Directrices del Master Engineering Manifesto (Zero GC)

1. **Cero `new` en Bucles Frecuentes:** Ni en `Update`, `FixedUpdate`, `onTick` ni en callbacks de eventos.
2. **Queries Físicas `NonAlloc`:**
   * Utilizar buffers preasignados: `Physics.OverlapSphereNonAlloc(center, radius, _hitBuffer, layerMask)`.
   * Comprobación de suelo con `Physics.RaycastNonAlloc`.
3. **Optimización Matemática:** Comparaciones de distancia con `.sqrMagnitude` en lugar de `Vector3.Distance`.
4. **Comparación de Tags:** Utilizar `.CompareTag(...)` en lugar de `== "Tag"`.
5. **Object Pooling Nativo:** Para partículas, fragmentos de muros destruibles y proyectiles, utilizar estrictamente `UnityEngine.Pool.ObjectPool<T>`.

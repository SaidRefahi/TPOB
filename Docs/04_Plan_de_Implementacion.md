---
title: Two Pilots, One Robot - Plan de Implementación por Fases (Producción)
tags:
  - roadmap
  - implementation
  - purrnet
  - vcontainer
  - unitask
  - cinemachine
  - dotween
  - tri-inspector
created: 2026-09-16
updated: 2026-09-16
---

# 🚀 Plan de Implementación de Producción: Two Pilots, One Robot (TPOB)

Este plan de implementación toma como base el roadmap inicial de 14 fases y lo eleva a estándar de producción, incorporando de manera explícita cada uno de los addons del proyecto (**PurrNet**, **VContainer**, **UniTask**, **Cinemachine 3.x**, **DOTween**, **Tri-Inspector**) y garantizando el cumplimiento irrestricto de las reglas del **Master Engineering Manifesto** (Cero asignaciones en hot paths, inyección de dependencias y desacoplamiento por interfaces).

---

## 📋 Resumen Ejecutivo del Roadmap

| Fase | Título | Stack / Addons Protagonistas | Entregable Principal |
| :---: | :--- | :--- | :--- |
| **01** | Infraestructura Core, Asmdefs y PurrNet Setup | PurrNet, VContainer, Tri-Inspector | Conexión local de 2 clientes con `NetworkManager` y Root Scope. `[COMPLETADA]` |
| **02** | Capa Global: GameManager y LevelManager | VContainer, UniTask, PurrNet SceneModule | Carga asíncrona de escenas con UniTask y gestión de estados de partida. `[COMPLETADA]` |
| **03** | Capa de Sala: RoomController y State Pattern | VContainer, PurrNet SyncVar, Tri-Inspector | Ciclo de vida determinista de sala con spawning sincronizado. `[COMPLETADA]` |
| **04** | Jugador 1: Locomoción y Habilidades (Piernas) | InputSystem, PurrNet Physics, Tri-Inspector | Piernas camina, salta, patea y empuja cajas con física autoritativa. `[COMPLETADA]` |
| **05** | Jugador 2: Manipulación y Habilidades (Torso) | InputSystem, PurrNet Physics, DOTween | Torso agarra, lanza, atrae magnéticamente y opera mecanismos. |
| **06** | Sistema de Comandos (Command Pattern) | C# Structs (Zero GC), PurrNet ServerRpc | Comandos polimórficos de jugador desacoplados de la red. |
| **07** | Fusión y Separación del Robot | PurrNet Hierarchy Sync, Cinemachine 3.x, DOTween | Alternancia fluida entre 1 y 2 avatares físicos en red. |
| **08** | Cámara Cinemachine 3.x Adaptativa e Impulsos | Cinemachine 3.x, CinemachineTargetGroup | Encuadre adaptativo dinámico y screen shake por impulsos físicos. |
| **09** | Bus de Eventos Desacoplado (Observer Pattern) | VContainer, C# Events, PurrNet ObserversRpc | Comunicación entre subsistemas sin llamadas cruzadas directas. |
| **10** | Composite Pattern y Mecanismos de Puzzle | Interfaces, DOTween, Tri-Inspector | Árbol lógico de finalización y objetos interactivos reactivos. |
| **11** | Ciclo de Muerte, Checkpoints y Respawn | UniTask, PurrNet SyncVar | Caídas y muertes con reaparición rápida sin reiniciar la sala. |
| **12** | Producción de Contenido: Diseño de 10 Salas | Tri-Inspector, Cinemachine, PurrNet | 4 salas simples, 4 intermedias y 2 avanzadas encadenadas. |
| **13** | Comedia Física, Ragdolls y Jugo Audiovisual | DOTween, Cinemachine Impulses, Audio Network | Sensación de impacto, fallos cómicos y audio sincronizado. |
| **14** | Optimización Zero-GC, Simulación de Red y QA Final | PurrNet Latency Sim, Unity Profiler | Juego fluido bajo 150 ms de ping sin picos de GC en hot paths. |

---

## 🛠️ Desglose Detallado por Fase

### 🔹 Fase 1: Infraestructura Core, Asmdefs e Integración de PurrNet
* **Objetivo:** Establecer la arquitectura modular de ensamblados y la tubería básica de red.
* **Integración de Addons:**
  * **Asmdefs:** Configurar `Game.Core`, `Game.Network`, `Game.Gameplay`, `Game.UI` y `Game.Editor`.
  * **PurrNet:** Configurar `NetworkManager`, transportes de red locales y registro de tipos básicos.
  * **VContainer:** Crear `GameLifetimeScope` en la escena inicial (`Bootstrapper`) para resolver servicios globales.
  * **Tri-Inspector:** Validar configuración de atributos en inspectores core.
* **Scripts Clave:**
  * `Bootstrapper.cs`
  * `GameLifetimeScope.cs`
  * `NetworkManagerWrapper.cs` (implementa `INetworkService`)
* **Criterio de Aceptación:** Dos instancias locales (Editor + Clones/Build) conectan con éxito y sincronizan un `NetworkIdentity` de prueba.

---

### 🔹 Fase 2: Capa Global: GameManager y LevelManager
* **Objetivo:** Coordinar los estados macro del juego (Menú, EnPartida, Transición, FinDePartida) y la carga de salas.
* **Integración de Addons:**
  * **VContainer:** Inyección de `LevelManager` y `GameManager` desde el Root Scope sin Singletons estáticos.
  * **UniTask:** Carga de escenas de sala mediante `await networkManager.sceneModule.LoadSceneAsync(...)` encapsulada en `UniTask` con manejo de cancelación.
  * **PurrNet:** `PurrSceneSettings` configurado en modo `Single` y público para sincronizar a todos los clientes.
* **Scripts Clave:**
  * `GameManager.cs`
  * `LevelManager.cs` (implementa `ILevelManager`)
  * `GameState.cs` (Enum)
* **Criterio de Aceptación:** El servidor comanda el cambio de nivel y ambos clientes cargan la escena de sala de forma sincronizada, admitiendo late-join sin desincronización de escena.

---

### 🔹 Fase 3: Capa de Sala: RoomController y State Pattern
* **Objetivo:** Encapsular la lógica, el spawning y la evaluación de finalización de cada sala individual.
* **Integración de Addons:**
  * **VContainer:** `RoomLifetimeScope` en cada escena de sala que hereda del `GameLifetimeScope`.
  * **PurrNet:** Sincronización del estado de la sala mediante `SyncVar<RoomState>`.
  * **Tri-Inspector:** Botones de depuración `[Button("Reiniciar Sala")]` y `[Button("Forzar Completado")]`.
* **Scripts Clave:**
  * `RoomLifetimeScope.cs`
  * `RoomController.cs` (implementa `IRoomController`)
  * `SpawnPoint.cs`
  * `RoomState.cs` (Inactive, Active, Completed, Transitioning)
* **Criterio de Aceptación:** Cargar una sala vacía posiciona a ambos jugadores en sus respectivos `SpawnPoint` y transiciona el estado a `RoomActive`.

---

### 🔹 Fase 4: Jugador 1: Locomoción y Habilidades (Piernas) `[COMPLETADA]`
* **Objetivo:** Implementar el control físico autoritativo del Jugador 1.
* **Integración de Addons:**
  * **Input System:** Lectura de ejes analógicos y botones de salto/patada con eventos dumb.
  * **PurrNet:** Transmisión de inputs en `onTick` vía `[ServerRpc(Channel.Unreliable)]`. El servidor simula el `Rigidbody`; los clientes usan `NetworkTransform` y `isKinematic = !isServer`.
  * **Tri-Inspector:** Parámetros de velocidad, fuerza de salto y cooldowns agrupados con `[Group("Locomoción")]`.
  * **Zero-GC Manifesto:** Chequeo de suelo mediante `Physics.RaycastNonAlloc` con buffers preasignados.
* **Scripts Clave:**
  * `LegsInputReader.cs`
  * `LegsController.cs` (implementa `IMoveable`, `IKicker`)
  * `LegsPhysics.cs`
* **Criterio de Aceptación:** Piernas camina con aceleración fluida, salta y patea obstáculos con simulación física autoritativa en el servidor y réplica suave en el cliente.

---

### 🔹 Fase 5: Jugador 2: Manipulación y Habilidades (Torso)
* **Objetivo:** Implementar las capacidades de interacción precisa del Jugador 2.
* **Integración de Addons:**
  * **Input System:** Lectura de stick derecho/ratón para apuntado en 360° y botones de agarre/lanzamiento/imán.
  * **PurrNet:** Interacción autoritativa con objetos `IGrabbable` y `IMagnetic`. El servidor valida distancias y crea el joint o constraint físico.
  * **DOTween:** Animación de pulsación del rayo magnético y balanceo visual del objeto sostenido.
  * **Zero-GC Manifesto:** Detección de objetos cercanos con `Physics.OverlapSphereNonAlloc`.
* **Scripts Clave:**
  * `TorsoInputReader.cs`
  * `TorsoController.cs` (implementa `IGrabber`, `IThrower`, `IMagnetOperator`)
  * `GrabSystem.cs`
  * `MagnetSystem.cs`
* **Criterio de Aceptación:** Torso apunta en cualquier ángulo, agarra cajas, las lanza con trayectoria parabólica y atrae llaves metálicas sin atravesar la geometría del nivel.

---

### 🔹 Fase 6: Sistema de Comandos (Command Pattern)
* **Objetivo:** Desacoplar la recepción de inputs del transporte de red y de la ejecución física de las acciones.
* **Integración de Addons:**
  * **PurrNet:** Envío de identificadores de comando serializables por valor en lugar de invocaciones directas de RPCs por cada botón.
  * **Zero-GC Manifesto:** Structs de comando en lugar de clases para evitar asignaciones en cada tick de entrada.
* **Scripts Clave:**
  * `IPlayerCommand.cs`
  * `MoveCommand.cs`, `JumpCommand.cs`, `KickCommand.cs`, `GrabCommand.cs`, `ThrowCommand.cs`, `ClimbCommand.cs`
  * `CommandInvoker.cs`
* **Criterio de Aceptación:** Es posible crear una nueva habilidad de gameplay implementando `IPlayerCommand` sin modificar el código de transporte de red.

---

### 🔹 Fase 7: Fusión y Separación del Robot
* **Objetivo:** Gestionar la alternancia entre las dos entidades físicas separadas y el robot combinado.
* **Integración de Addons:**
  * **PurrNet:** Validación de distancia en el servidor (`.sqrMagnitude < maxDistance`). Al fusionar, el servidor emparenta Torso al socket de Piernas, activa `isKinematic` en Torso y transfiere la masa total a Piernas.
  * **DOTween:** Juicing de acople con un ligero `PunchScale` procedural en la unión metálica.
  * **Tri-Inspector:** Botón `[Button("Simular Fusión")]` en el `RobotCoordinator`.
* **Scripts Clave:**
  * `RobotCoordinator.cs`
  * `IRobotStrategy.cs` (`FusionStrategy.cs`, `SeparationStrategy.cs`)
  * `FusionSocket.cs`
* **Criterio de Aceptación:** Ambos clientes ven la fusión y separación en el mismo fotograma; el robot fusionado camina con las órdenes de Piernas mientras Torso apunta y dispara simultáneamente.

---

### 🔹 Fase 8: Cámara Cinemachine 3.x Adaptativa e Impulsos
* **Objetivo:** Proveer una experiencia visual cinematográfica y adaptativa al estado de Fusión/Separación.
* **Integración de Addons:**
  * **Cinemachine 3.x:** Uso de `CinemachineCamera` y `CinemachineTargetGroup`.
    * En **Separación:** Ponderación dinámica `Weight = 1.0` para Piernas y Torso, con zoom automático según la distancia mutua.
    * En **Fusión:** Ponderación `Weight = 0` para Torso y encuadre cerrado sobre el Robot.
  * **Cinemachine Impulse:** `CinemachineImpulseSource` en la patada de Piernas, caídas de gran altura e impactos de compuertas.
* **Scripts Clave:**
  * `CameraController.cs`
  * `TargetGroupCoordinator.cs`
* **Criterio de Aceptación:** La cámara amplía y reduce su encuadre suavemente al separarse los jugadores, y tiembla contundentemente al propinar una patada fuerte.

---

### 🔹 Fase 9: Bus de Eventos de Red Desacoplado (Observer Pattern)
* **Objetivo:** Eliminar el acoplamiento directo entre subsistemas (puzzles, audio, UI, jugadores).
* **Integración de Addons:**
  * **VContainer:** `GameEventBus` registrado como Singleton de servicio en el Root Scope.
  * **PurrNet:** `NetworkEventRelay` que retransmite eventos globales mediante PurrNet `Broadcasts` (`IPackedAuto`) o `[ObserversRpc(BufferLast = true)]` para eventos atados a salas.
* **Scripts Clave:**
  * `GameEventBus.cs`
  * `NetworkEventRelay.cs`
  * Eventos: `PlayerDiedEvent`, `PlayerRespawnedEvent`, `RoomCompletedEvent`, `RobotFusedEvent`, `RobotSeparatedEvent`
* **Criterio de Aceptación:** Ningún componente de gameplay invoca RPCs cruzados directos hacia otros sistemas; toda la comunicación reactiva fluye por el bus.

---

### 🔹 Fase 10: Composite Pattern y Mecanismos de Puzzle
* **Objetivo:** Construir la lógica modular de puzzles y los objetos físicos interactivos.
* **Integración de Addons:**
  * **Patrón Composite:** `RoomCompletion` evalúa un árbol de `ICompletionCondition` (`AndCondition`, `OrCondition`).
  * **DOTween:** Animación procedural de compuertas deslizantes, hundimiento elástico de pulsadores y rotación de palancas.
  * **Tri-Inspector:** Visualización clara de condiciones en el inspector con `[ShowInInspector]`.
* **Scripts Clave:**
  * `RoomCompletion.cs`
  * `ICompletionCondition.cs`
  * `PushableBox.cs`, `BreakableWall.cs`, `TargetButton.cs`, `MetalKey.cs`, `MagnetTarget.cs`, `LeverMechanism.cs`, `WeightPlatform.cs`
* **Criterio de Aceptación:** Al resolverse todas las condiciones del árbol Composite, se dispara `RoomCompletedEvent` y la puerta de salida se abre con animación de DOTween.

---

### 🔹 Fase 11: Ciclo de Muerte, Checkpoints y Respawn
* **Objetivo:** Garantizar un flujo de reaparición rápido y sin frustración ante caídas o trampas.
* **Integración de Addons:**
  * **UniTask:** Secuencia de reaparición sin corrutinas (`await UniTask.Delay(1000, cancellationToken: ct)`).
  * **PurrNet:** Respawn autoritativo reseteando la posición del `NetworkTransform` y las velocidades del `NetworkRigidbody`.
* **Scripts Clave:**
  * `PlayerDeathHandler.cs`
  * `CheckpointSystem.cs`
  * `RespawnCoordinator.cs`
* **Criterio de Aceptación:** Al caer a un foso, el jugador reaparece en el último checkpoint activo en menos de 1.5 segundos sin reiniciar los mecanismos ya completados de la sala.

---

### 🔹 Fase 12: Producción de Contenido: Diseño de 10 Salas
* **Objetivo:** Construir y conectar las 10 salas del arco de dificultad.
* **Integración de Addons:**
  * **LevelManager + UniTask:** Encadenamiento fluido de escenas mediante `LoadRoomAsync`.
  * **Tri-Inspector:** Fichas de inspección en cada sala para setear `CompletionConditions` sin tocar código.
* **Contenido a Entregar:**
  * Salas Simples (1 a 4): Movimiento, Fusión Básica, Patada y Magnetismo.
  * Salas Intermedias (5 a 8): El Abismo, Doble Interruptor, La Torre y El Calzo.
  * Salas Avanzadas (9 y 10): Balancines Dinámicos y El Reactor.
* **Criterio de Aceptación:** Partida continua de inicio a fin recorriendo las 10 salas con progresión guardada por el `LevelManager`.

---

### 🔹 Fase 13: Comedia Física, Ragdolls y Jugo Audiovisual
* **Objetivo:** Potenciar el núcleo cómico del juego ("fallar es divertido") mediante respuesta física exagerada.
* **Integración de Addons:**
  * **PhysX:** Materiales físicos de alto rebote y baja fricción para deslices cómicos.
  * **Cinemachine:** Impulsos de choque ante colisiones a alta velocidad.
  * **DOTween:** Escalamientos y deformaciones cómicas (Squash & Stretch).
  * **PurrNet Audio:** Sincronización de efectos sonoros mecánicos y choques en red.
* **Scripts Clave:**
  * `ImpactFeedbackSystem.cs`
  * `NetworkAudioRelay.cs`
* **Criterio de Aceptación:** Descoordinaciones entre jugadores provocan caídas y choques cómicos idénticos en ambos clientes, con respuesta auditiva y háptica inmediata.

---

### 🔹 Fase 14: Optimización Zero-GC, Simulación de Red y QA Final
* **Objetivo:** Certificar el rendimiento competitivo y la estabilidad de red en condiciones adversas.
* **Integración de Addons:**
  * **PurrNet Simulation:** Pruebas con simulación de latencia de 100–150 ms y 2% de pérdida de paquetes.
  * **Unity Profiler:** Comprobación estricta de CERO asignaciones de GC en `Update`, `FixedUpdate` y `onTick`.
  * **Build Standalone:** Generación de ejecutable para dos puestos de juego independientes.
* **Checklist de QA:**
  1. Latencia de entrada imperceptible en el cliente local.
  2. Ausencia de "rubberbanding" o saltos bruscos en `NetworkTransform`.
  3. Fusión y separación sin bloqueos de colisión.
  4. Ningún `NullReferenceException` al desconectarse un cliente.
* **Criterio de Aceptación:** Build de producción testeada en dos equipos remotos completando las 10 salas de forma fluida y estable.

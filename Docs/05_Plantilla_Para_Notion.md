# 🚀 Programa de Implementación: Two Pilots, One Robot (TPOB)

> **Resumen del Proyecto:** Two Pilots, One Robot es un juego cooperativo asimétrico 2P donde los jugadores controlan las mitades física de un robot (Piernas y Torso), alternando entre estados de Fusión y Separación.
> **Tecnologías Clave:** Unity URP, PurrNet (Física Server-Autoritativa), VContainer (DI), UniTask (Zero GC), Cinemachine 3.x, DOTween, Tri-Inspector, Input System.

---

## 📊 Dashboard de Fases y Progreso

- [x] **Fase 1:** Configuración inicial del proyecto e integración de PurrNet & VContainer (Completada)
- [x] **Fase 2:** Capa Global: GameManager y LevelManager con UniTask (Completada)
- [x] **Fase 3:** Capa de Sala: RoomController, Spawning y State Pattern (Completada)
- [x] **Fase 4:** Movimiento y habilidades Jugador 1 (Piernas: Locomoción y Patada) (Completada)
- [x] **Fase 5:** Movimiento y habilidades Jugador 2 (Torso: Agarre, Imán y Palancas) (Completada)
- [x] **Fase 6:** Sistema de Comandos desacoplado de red (Command Pattern & Structs) (Completada)
- [x] **Fase 7:** Fusión y Separación del Robot (Hierarchy & Rigidbody Sync) (Completada)
- [x] **Fase 8:** Cinemachine 3.x Adaptativa (TargetGroup Fusión/Separación e Impulsos) (Completada)
- [x] **Fase 9:** Sistema de Eventos en Red (GameEventBus + NetworkEventRelay) (Completada)
- [x] **Fase 10:** RoomCompletion (Composite Pattern) y Objetos de Puzzle con DOTween (Completada)
- [ ] **Fase 11:** Muerte, Reaparición y Checkpoints (UniTask Respawn Flow)
- [ ] **Fase 12:** Diseño de Niveles y Progresión (10 Salas de Prueba)
- [ ] **Fase 13:** Física Emergente, Comedia, Ragdolls y Jugo Audiovisual
- [ ] **Fase 14:** Testing de Red, Optimización Zero-GC y Build Final

---

## 🛠️ Detalle de las Fases de Desarrollo

### 🔹 Fase 1: Infraestructura Core y Red Inicial
* **Objetivo:** Ensamblados modulares (`Game.Core`, `Game.Network`, `Game.Gameplay`) y conexión básica de red con PurrNet.
* **Integración:** `GameLifetimeScope` (VContainer) para servicios globales sin Singletons estáticos.
* **Entregable:** 2 instancias sincronizando un `NetworkIdentity` en local.

### 🔹 Fase 2: Capa Global (Game & Level Managers)
* **Objetivo:** Orquestación macro del ciclo de juego y carga asíncrona de escenas.
* **Integración:** `networkManager.sceneModule.LoadSceneAsync` encapsulado con `UniTask`.
* **Entregable:** Servidor comanda avance de salas con ambos clientes cargando la escena de forma sincronizada.

### 🔹 Fase 3: Capa de Sala (RoomController)
* **Objetivo:** Ciclo de vida determinista de sala (`RoomInactive -> RoomActive -> RoomCompleted -> RoomTransitioning`).
* **Integración:** `RoomLifetimeScope` de VContainer y `SyncVar<RoomState>`. Botones `[Button]` de Tri-Inspector para depuración rápida.
* **Entregable:** Carga de sala posiciona a ambos jugadores en `SpawnPoint` y activa la sala.

### 🔹 Fase 4: Jugador 1 (Piernas: Locomoción y Fuerza)
* **Objetivo:** Control físico autoritativo del movimiento, salto y patada contundente.
* **Integración:** `UnityEngine.InputSystem`, transmisión de structs `LegsInputData` en `onTick` vía `[ServerRpc(Channel.Unreliable)]`, `NetworkTransform` y queries físicas `Physics.RaycastNonAlloc`.
* **Entregable:** Piernas camina, salta y patea obstáculos con física fluida replicada en clientes.

### 🔹 Fase 5: Jugador 2 (Torso: Manipulación y Precisión)
* **Objetivo:** Apuntado libre en 360°, agarre, lanzamiento y rayo magnético.
* **Integración:** `Physics.OverlapSphereNonAlloc` para detección, validación autoritativa en servidor y efectos procedurales con DOTween.
* **Entregable:** Torso agarra cajas, lanza objetos en parábola y atrae llaves magnéticas sin atravesar paredes.

### 🔹 Fase 6: Sistema de Comandos (Command Pattern)
* **Objetivo:** Desacoplar la recepción de entradas del transporte de red y la física.
* **Integración:** `IPlayerCommand`, `CommandInvoker` y structs serializables por valor para Cero Asignaciones en el heap.
* **Entregable:** Nuevas habilidades se integran implementando `IPlayerCommand` sin tocar el código de red.

### 🔹 Fase 7: Fusión y Separación del Robot `[COMPLETADA]`
* **Objetivo:** Alternar entre dos cuerpos separados y un robot unificado.
* **Integración:** Emparentamiento en servidor a socket receptor, desactivación de `Rigidbody` secundario y animación snap con DOTween.
* **Entregable:** Ambos clientes ven la fusión en el mismo fotograma sin tirones físicos.

### 🔹 Fase 8: Cámara Cinemachine 3.x Adaptativa `[COMPLETADA]`
* **Objetivo:** Encuadre dinámico que responde a la Fusión/Separación y sacudida de pantalla por impactos.
* **Integración:** `CinemachineTargetGroup` con pesos adaptativos (1 objetivo en Fusión, 2 en Separación) y `CinemachineImpulseSource` en patadas y caídas pesadas.
* **Entregable:** Zoom y encuadre fluido en tiempo real al alejarse o juntarse los jugadores.

### 🔹 Fase 9: Bus de Eventos de Red (Observer Pattern) `[COMPLETADA]`
* **Objetivo:** Comunicación reactiva sin acoplamiento directo entre componentes.
* **Integración:** `GameEventBus` inyectado por VContainer y `NetworkEventRelay` con RPCs y Broadcasts de PurrNet.
* **Entregable:** Eventos globales (`RoomCompletedEvent`, `PlayerDiedEvent`) coordinan audio, UI y lógica sin dependencias directas.

### 🔹 Fase 10: Composite Pattern y Mecanismos de Puzzle `[COMPLETADA]`
* **Objetivo:** Árbol de condiciones lógicas para resolver salas y objetos interactivos.
* **Integración:** `RoomCompletion` con nodos AND/OR y feedback procedural con DOTween (compuertas, pulsadores, palancas).
* **Entregable:** Al cumplirse las condiciones del árbol, se dispara la apertura animada de la compuerta final.

### 🔹 Fase 11: Muerte, Reaparición y Checkpoints
* **Objetivo:** Flujo de reaparición sin fricción ante caídas al vacío.
* **Integración:** Temporizadores de respawn con `UniTask.Delay` y cancelación automática al cambiar de sala.
* **Entregable:** Jugador reaparece en menos de 1.5s en el checkpoint sin resetear el puzzle de la sala.

### 🔹 Fase 12: Diseño de Niveles (10 Salas de Prueba)
* **Objetivo:** Contenido jugable encadenado de inicio a fin con dificultad gradual.
* **Contenido:** 4 salas simples, 4 salas intermedias y 2 salas avanzadas.
* **Entregable:** Campaña completa jugable de principio a fin sin errores de transición de escena.

### 🔹 Fase 13: Comedia Física, Ragdolls y Pulido
* **Objetivo:** Fortalecer la premisa de comedia física donde los fallos son divertidos.
* **Integración:** Materiales físicos de fricción cómica, ragdolls de choque y audio de impacto replicado en red.
* **Entregable:** Choques y fallos físicos idénticos en ambos clientes con respuesta audiovisual satisfactoria.

### 🔹 Fase 14: Optimización Zero-GC y Build Final
* **Objetivo:** Certificación técnica bajo estrés de red y perfiles de memoria.
* **Pruebas:** Simulación de 100-150 ms de ping y 2% packet loss; cero asignaciones en el hot path con Unity Profiler.
* **Entregable:** Build ejecutable validada en dos PCs remotas funcionando con estabilidad total.

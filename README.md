# 🤖 Two Pilots, One Robot (TPOB)

**Two Pilots, One Robot (TPOB)** es un videojuego multijugador cooperativo asimétrico en 3D desarrollado en **Unity (URP)**. Dos jugadores pilotean de forma coordinada distintas mitades físicas de un mismo robot (**Piernas** y **Torso**), alternando estratégicamente entre operar como dos módulos independientes o unificarse en una única máquina pesada para resolver puzzles mecánicos, sortear peligros ambientales y superar un arco de 10 salas de prueba.

---

## 🎮 Pilares de Gameplay

1. **Cooperación Asimétrica Real:** Ningún jugador es secundario. Piernas gobierna la locomoción, el salto y la demolición física; Torso controla la torreta 360°, el agarre, los lanzamientos balísticos y el electromagnetismo.
2. **Fusión & Separación Táctica:** Conmutación fluida entre dos cuerpos ágiles separados y un robot combinado con doble poder motriz y torreta superior.
3. **Comedia Física Emergente:** Descoordinaciones entre compañeros provocan caídas cómicas, volteretas procedurales (`TumbleController`) y deslices de baja fricción sin sacrificar el control responsivo.
4. **Cero Garbage Collection (Zero-GC):** Toda la simulación física y bucles calientes (`Update`, `FixedUpdate`, `onTick`) tienen cero asignaciones en el heap para garantizar 60+ FPS sin caídas de frame por GC.

---

## 🕹️ Esquema de Controles

### 🦵 Jugador 1: PIERNAS (Locomoción y Demolición)
| Acción | Teclado | Gamepad |
| :--- | :--- | :--- |
| **Moverse / Pivotar** | `W, A, S, D` | `Stick Izquierdo` |
| **Saltar / Doble Salto** | `Espacio` | `Botón Sur (A)` |
| **Sprint / Carrera Rápida** | `Shift Izquierdo` | `Gatillo Izquierdo (LT)` |
| **Patada Física de Impacto** | `F` | `Botón Oeste (X)` |
| **Acoplarse / Desacoplarse** | `R` | `Botón Norte (Y)` |

### 🤖 Jugador 2: TORSO (Manipulación y Precisión)
| Acción | Teclado / Ratón | Gamepad |
| :--- | :--- | :--- |
| **Apuntado Torreta 360°** | `Ratón (Cursor)` | `Stick Derecho` |
| **Locomoción Oruga (Separado)**| `W, A, S, D` | `Stick Izquierdo` |
| **Agarrar / Soltar / Palancas** | `Click Izquierdo` / `E` | `Gatillo Derecho (RT)` |
| **Lanzamiento Balístico** | `Click Derecho` / `Q` | `Botón Superior Derecho (RB)` |
| **Rayo Magnético Tractor** | `Shift` / `C` | `Gatillo Izquierdo (LT)` |
| **Acoplarse / Desacoplarse** | `R` | `Botón Norte (Y)` |

---

## 🛠️ Stack Tecnológico y Arquitectura

* **Red y Multijugador:** [PurrNet](https://purrnet.dev/) — Física servidor-autoritativa, `NetworkTransform`, `NetworkRigidbody`, `[ServerRpc(requireOwnership: false)]` y `[ObserversRpc(bufferLast: true)]`.
* **Inyección de Dependencias:** [VContainer](https://vcontainer.hadashikick.jp/) — Jerarquía desacoplada de `GameLifetimeScope` (servicios persistentes globales) y `RoomLifetimeScope` (ciclo de vida por sala).
* **Asincronismo Zero-GC:** [UniTask](https://github.com/Cysharp/UniTask) — Cargas asíncronas de escenas (`SceneModule`), delays de respawn y secuencias de sala sin corrutinas `IEnumerator`.
* **Cámara Cinematográfica:** [Cinemachine 3.x](https://unity.com/features/cinemachine) — `CinemachineTargetGroup` dinámico (pesos adaptativos para Fusión/Separación) y `CinemachineImpulseSource` para vibración por impactos físicos.
* **Jugo y Micro-animaciones:** [DOTween](http://dotween.demigiant.com/) — Squash & Stretch cómico procedural, pulsadores elásticos y apertura de compuertas.
* **Ergonomía de Editor:** [Tri-Inspector](https://github.com/KyryloKuzyk/Tri-Inspector) — Atributos avanzados de inspector y botones de depuración en tiempo de ejecución.
* **Audio en Red:** `AudioService` (ObjectPool nativo de Unity Zero-GC) con `ProceduralAudioSynthesizer` y replicación mediante `NetworkAudioRelay`.

---

## 🏛️ Campaña y Salas de Prueba

El juego cuenta con un arco de 10 salas encadenadas con progresión guardada en [LevelManager](file:///Assets/_Project/Network/Services/LevelManager.cs):
* **Salas Simples (1 a 4):**
  * `Room_01`: Movimiento y coordinación básica.
  * `Room_02`: Fusión y plataformas elevadas.
  * `Room_03`: Patada física y demolición de obstáculos.
  * `Room_04`: Magnetismo y llaves balísticas.
* **Salas Intermedias (5 a 8):**
  * `Room_05`: El Abismo (acoplamiento sobre plataformas móviles).
  * `Room_06`: Doble Interruptor sincronizado y básculas de peso.
  * `Room_07`: La Torre (elevación cooperativa vertical).
  * `Room_08`: El Calzo (uso de cubos de soporte para compuertas).
* **Salas Avanzadas (9 y 10):**
  * `Room_09`: Balancines Dinámicos (`SeeSawPlatform` con física PhysX).
  * `Room_10`: El Reactor (puzzle final multidimensional de alta exigencia cooperativa).

---

## 🚀 Flujo de Partida y UI Multijugador

1. **Escena Inicial (`Boot.unity`):**
   * **Menú Principal:** Iniciar como Host en puerto 5000 o Conectar como Cliente ingresando IP/Puerto. Panel modal de Opciones (Audio Master/SFX, Video, Sensibilidad).
   * **Lobby de Pre-Ingreso:** Selección de roles exclusiva (**Piernas** vs **Torso**) sincronizada por red con prevención de duplicados, visualización de lore y confirmación obligatoria ("Listo") antes de arrancar.
2. **Durante la Partida (`Room_01` a `Room_10`):**
   * **Menú de Pausa In-Game (`Escape` / `Start`):** Suspensión no destructiva de lecturas de entrada local (sin alterar `Time.timeScale` para preservar la sincronización de PhysX y PurrNet).
   * **Monitor de Rendimiento (`F3`):** Overlay en vivo con FPS, ping de red, porcentaje de packet loss y tracking de asignaciones de memoria.

---

## 🧰 Herramientas de Editor (Menú `TPOB` en Unity)

Dentro del Editor de Unity, el menú superior **TPOB** ofrece automatización completa:
* **`TPOB -> 1. Generar Prefabs y Playground`:** Genera o repara todos los prefabs interactuables y el entorno de pruebas local.
* **`TPOB -> 2. Configurar Networking en Escena Actual`:** Cablea el NetworkManager y entidades de red en la escena abierta.
* **`TPOB -> 3. Generar las 10 Salas de Campaña`:** Construye proceduralmente `Room_01` a `Room_10` y las inscribe en `EditorBuildSettings`.
* **`TPOB -> 4. Auditar Código para Zero-GC`:** Escaneo estático en busca de asignaciones en caliente (`new`, LINQ, `.tag ==`).
* **`TPOB -> 5. Compilar Standalone Windows (x64)`:** Genera el ejecutable de producción en `Builds/Windows/`.
* **`TPOB -> 6. Lanzar 2 Instancias Locales (Host + Cliente)`:** Ejecuta automáticamente dos instancias simultáneas para testeo multijugador local.
* **`TPOB -> UI -> 8. Configurar y Actualizar Escena Boot Completa`:** Reconstruye y asocia todos los Canvas y controladores de UI en `Boot.unity`.

---

## 📚 Documentación Técnica Detallada

En la carpeta [`Docs/`](file:///Docs/) se encuentra la especificación completa:
* [[Docs/00_Index]]: Índice general y mapa de contenidos.
* [[Docs/01_GDD]]: Game Design Document en profundidad.
* [[Docs/02_Arquitectura]]: Arquitectura técnica, diagramas y contratos de red.
* [[Docs/03_Diagramas]]: Diagramas de clases y flujogramas de gameplay.
* [[Docs/04_Plan_de_Implementacion]]: Roadmap de 14 fases de producción completadas.
* [[Docs/05_Plantilla_Para_Notion]]: Plantilla exportable con checklist de producción.
* [[Docs/06_UI_Lobby_Pausa_Plan_de_Implementacion]]: Especificación técnica de la UI, Lobby y Pausa.
* [AGENTS.md](file:///AGENTS.md): Reglas críticas, lecciones aprendidas y anti-patrones del proyecto.

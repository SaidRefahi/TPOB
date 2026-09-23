# AGENTS.md - Reglas, Lecciones Aprendidas y Anti-Patrones de TPOB

Este archivo sirve como base de conocimiento viva para cualquier agente de IA o desarrollador que trabaje en el proyecto **TPOB**. Recopila los errores cometidos, causas raíz identificadas y patrones de diseño obligatorios para evitar tropezar dos veces con la misma piedra.

---

## 🚨 1. Input System y Lectura de Acciones

### ❌ Error cometido:
- En `TorsoInputReader`, la tecla `E` estaba vinculada a `Interact` y solo encendía el flag de agarre (`_grabTriggered = true`). El flag `_interactTriggered` jamás se seteaba a `true`, dejando la interacción con palancas y botones completamente muerta sin errores en consola.

### 🛡️ Reglas de Oro:
1. **Acciones Multipropósito:** Cuando un mismo botón (`Interact`) sirve tanto para tomar/soltar objetos como para accionar mecanismos:
   - El `InputReader` debe emitir los flags correspondientes a todas las intenciones posibles (`_grabTriggered = true; _interactTriggered = true;`).
   - El controlador consumidor (`TorsoController`) es el responsable de priorizar y consumir los flags:
     - Si agarra o suelta un objeto con éxito, debe limpiar inmediatamente `_pendingInput.InteractTriggered = false` para no accionar una palanca accidentalmente en el mismo fotograma.
     - Si no hay objeto agarrable al alcance, entonces procede a evaluar mecanismos interactuables.
2. **Auditoría de Flags:** Cada vez que se defina un `ConsumeXTrigger()`, verificar que exista al menos una asignación a `true` en el manejador del evento de Input.
3. **Prohibición Absoluta de `UnityEngine.Input` (Legacy):**
   - El proyecto tiene configurado `Active Input Handling = Input System Package (New)`.
   - Cualquier invocación a `UnityEngine.Input.GetKeyDown(...)` arroja `InvalidOperationException` en tiempo de ejecución.
   - Para atajos de teclado, overlays o herramientas de diagnóstico (ej. conmutar `PerformanceMonitor` con `F3`), usar siempre la API del nuevo Input System:
   ```csharp
   var keyboard = UnityEngine.InputSystem.Keyboard.current;
   if (keyboard != null && keyboard.f3Key.wasPressedThisFrame)
   {
       _showOverlay = !_showOverlay;
   }
   ```

---

## 🌐 2. Networking y Arquitectura PurrNet

### ❌ Errores cometidos:
- **Añadir `NetworkIdentity` redundante:** Confundir PurrNet con Mirror/NGO y agregar un componente `NetworkIdentity` a prefabs cuyos scripts ya heredaban de `NetworkBehaviour`, duplicando componentes de red y corrompiendo la tabla de serialización y los índices de componentes (`_componentIndex`).
- **Mecanismos mudos en clientes:** Métodos públicos como `Toggle()` que hacían `if (!isServer) return;`. Si un cliente presionaba la tecla de interacción, la llamada se descartaba en silencio y el servidor nunca se enteraba.
- **ServerRpc rechazados por Ownership:** Intentar enviar un `[ServerRpc]` desde el cliente hacia un objeto neutral de la escena (palanca, botón, compuerta) sin especificar `requireOwnership: false`. PurrNet descartaba el paquete porque el cliente no es el "dueño" del mecanismo de la sala.
- **Desincronización visual para observadores tardíos:** `ObserversRpc` sin `bufferLast: true`, provocando que jugadores que cargan la escena milisegundos después o se reconectan vean las palancas y puertas en su estado inicial (cerradas/apagadas) aunque en el servidor estén abiertas.
- **Simulación física descontrolada en clientes:** Dejar `isKinematic = false` en clientes para entidades autoritativas en el servidor, provocando desincronización y "jitter" constante contra las correcciones de `NetworkTransform`.

### 🛡️ Reglas de Oro Arquitectónicas:

#### 1. Herencia Directa: `NetworkBehaviour` YA ES un `NetworkIdentity`
- En el código fuente de PurrNet:
  ```csharp
  public abstract class NetworkBehaviour : NetworkIdentity { }
  ```
- **Consecuencia Crítica:** Cualquier script que herede de `NetworkBehaviour` (`TorsoController`, `LeverMechanism`, `WeightPlatform`, `SlidingDoor`) ya contiene internamente toda la infraestructura de `NetworkIdentity`.
- **PROHIBIDO:** Jamás añadir un componente `NetworkIdentity` por separado a un GameObject o Prefab que ya tenga un script `NetworkBehaviour`. Hacerlo provoca que existan dos identidades de red compitiendo en el mismo GameObject, rompiendo los hashes de RPC y la resolución de observadores.

#### 2. Interacciones Autorizadas por Clientes (`requireOwnership: false`)
- Los mecanismos de la sala (palancas, botones, sockets) pertenecen al servidor, no al jugador. Si un cliente interactúa con ellos, el método público debe redirigir a un `[ServerRpc(requireOwnership: false)]`:
  ```csharp
  public void Toggle(GameObject user)
  {
      if (isSpawned && !isServer)
      {
          ToggleServerRpc();
          return;
      }
      // Ejecución autoritativa en el Servidor / Host...
      _isActivated = !_isActivated;
      UpdateVisualsObserversRpc(_isActivated);
  }

  [ServerRpc(requireOwnership: false)]
  private void ToggleServerRpc() => Toggle(null);
  ```

#### 3. Persistencia de Estado Visual en Observadores (`bufferLast: true`)
- Todo RPC que modifique estados visuales, transformaciones procedurales o flags lógicos debe incluir `bufferLast: true`:
  ```csharp
  [ObserversRpc(runLocally: true, bufferLast: true)]
  private void UpdateVisualsObserversRpc(bool state)
  {
      UpdateVisuals(state);
  }
  ```
- Al spawnearse el objeto o entrar un nuevo cliente (`OnSpawned()`), PurrNet re-ejecutará automáticamente el último estado guardado en el buffer, garantizando que nadie vea puertas cerradas que ya fueron abiertas.

#### 4. Autoridad Física en `OnSpawned()`
- Para objetos con físicas gobernadas por el servidor:
  ```csharp
  protected override void OnSpawned()
  {
      base.OnSpawned();
      if (_rigidbody != null)
      {
          _rigidbody.isKinematic = !isServer;
      }
  }
  ```
- El servidor simula PhysX y propaga las posiciones mediante `NetworkTransform`. Los clientes actúan como receptores pasivos e interpolan, evitando tirones y colisiones fantasma.

#### 5. Separación de Ciclo de Vida: `Awake()` vs `OnSpawned()`
- En `Awake()`: Únicamente cachear componentes locales (`GetComponent`, `Shader.PropertyToID`, inicialización de estructuras).
- En `OnSpawned()`: Comprobar `isServer`, `isOwner`, registrar callbacks de red y sincronizar estados iniciales. En `Awake()` las propiedades de red aún no están inicializadas.

---

## 📦 3. Físicas, Detección y Colliders

### ❌ Errores cometidos:
- Prefabs interactuables cuya raíz no tenía collider (solo un hijo con un cubo de 0.2m de altura), haciendo que las búsquedas físicas por radio fallaran si el jugador apuntaba ligeramente arriba.
- Botones de pared que solo reaccionaban a patadas directas y rebotaban múltiples veces por segundo al impactar una caja balística arrojada por Torso, cambiando de estado 10 veces por frame.
- Báscula de peso con un volumen de detección demasiado bajo (1m), que no registraba a Torso cuando saltaba o estaba montado/fusionado encima de Piernas.

### 🛡️ Reglas de Oro:
1. **Colliders Generosos en la Raíz:**
   - Todo prefab interactuable (`LeverSwitch`, `TargetButton`, `PuzzleSocket`) debe tener un `Collider` en la raíz (generalmente `isTrigger = true`) con dimensiones cómodas (ej. `1.4m × 1.2m × 1.4m`) para facilitar la detección por `OverlapSphereNonAlloc`.
2. **Búsqueda Jerárquica en Controllers:**
   - Al buscar interactuables en el buffer de colliders, chequear siempre en 3 niveles:
   ```csharp
   if (col.TryGetComponent<IInteractableMechanism>(out var mech) ||
       (col.attachedRigidbody != null && col.attachedRigidbody.TryGetComponent<IInteractableMechanism>(out mech)) ||
       (col.GetComponentInParent<IInteractableMechanism>() is { } parentMech && (mech = parentMech) != null))
   {
       mech.Toggle(gameObject);
   }
   ```
3. **Debounce / Enfriamiento Obligatorio:**
   - En mecanismos accionables por colisión física (`TargetButton.OnCollisionEnter`), implementar siempre un temporizador de cooldown (`_cooldown = 0.4f`):
   ```csharp
   if (Time.time < _lastToggleTime + _cooldown) return;
   _lastToggleTime = Time.time;
   ```
4. **Volúmenes de Detección Cooperativa:**
   - Las plataformas de peso (`WeightPlatform`) deben tener un área de detección lo suficientemente alta (mínimo 3 metros) para abarcar a Torso trepado sobre Piernas, robots fusionados o cajas colocadas encima, usando `QueryTriggerInteraction.Collide`.

---

## 🎥 4. Cinemachine 3.x y Encuadre de Cámara

### ❌ Error cometido:
- Configurar `FramingSize = 0.8` con radios de miembros de `1.0m - 1.2m`. Cinemachine interpretaba que los jugadores debían ocupar el 80% del viewport y realizaba un zoom-in asfixiante hasta colocarse a 2.6m de distancia, dejando el 90% de la arena fuera de visión.

### 🛡️ Reglas de Oro:
1. **Framing Size Proporcional:**
   - Para juegos cooperativos en vista isométrica/top-down, `FramingSize` en `CinemachineGroupFraming` debe situarse entre **`0.45` y `0.55`** (nunca > `0.65`).
2. **Radios Mínimos en TargetGroup:**
   - En `TargetGroupCoordinator`, dar a cada miembro un radio de al menos **`3.0m - 3.5m`**. Esto garantiza que cuando ambos robots estén pegados, la cámara mantenga una distancia prudencial de 12-15 metros respecto al suelo.
3. **Field of View (FOV):**
   - Usar un FOV de **`55° - 60°`** para vistas isométricas amplias. Ángulos de `40°` o menores provocan un efecto telefoto claustrofóbico.

---

## 🎨 5. Gráficos, Materiales y URP

### ❌ Error cometido:
- Intentar cambiar el color de un material en URP asignando únicamente la propiedad `_Color` en `MaterialPropertyBlock`. En URP Lit, la propiedad principal de color es `_BaseColor`, por lo que el objeto permanecía visualmente inmutable.

### 🛡️ Reglas de Oro:
1. **Compatibilidad Dual URP/Built-in:**
   - Al usar `MaterialPropertyBlock` (Zero GC), asignar siempre ambas propiedades para garantizar compatibilidad con cualquier shader:
   ```csharp
   _propBlock.SetColor(Shader.PropertyToID("_BaseColor"), color);
   _propBlock.SetColor(Shader.PropertyToID("_Color"), color);
   _propBlock.SetColor(Shader.PropertyToID("_EmissionColor"), color * 1.5f);
   ```
2. **Feedback Inmediato en Mecanismos:**
   - Todo mecanismo físico (palanca, botón, plataforma) debe acompañar su animación DOTween con feedback cromático/emisivo visible (Rojo = Desactivado, Verde brillante = Activado).

---

## ⚡ 6. Rendimiento y Zero GC (Manifesto)

### 🛡️ Reglas Obligatorias:
- **Zero Allocations en Bucles:** Cero `new` en `Update`, `FixedUpdate`, `LateUpdate` o callbacks de colisión/trigger.
- **Buffers Pre-asignados:** Los arrays temporales para `Physics.OverlapSphereNonAlloc` deben tener tamaño fijo (`new Collider[16]`) y residir en campos privados de la clase.
- **Comparaciones de Tags:** Usar siempre `CompareTag("...")`, jamás `tag == "..."`.
- **Caché de Componentes y Hashes:** Cachear transformadas, renderers y `Shader.PropertyToID` en `Awake()`.
- **Campos Encapsulados:** Todo dato serializable debe ser `[SerializeField] private`, exponiendo únicamente getters de solo lectura.

---

## 🛠️ 7. Resolución de Nombres y Colisiones en Builders

### ❌ Error cometido:
- Declarar campos homónimos en structs internas de configuración (ej. `public GameObject KillVolume;` en `PrefabSet` o `public Material KillVolume;` en `MaterialSet`) dentro de `PlaygroundBuilder` y luego invocar `root.AddComponent<KillVolume>();`.
- El compilador de C# prioriza la búsqueda de identificadores en el ámbito de la clase contenedora y sus miembros internos, resolviendo `KillVolume` como un campo y fallando con el error `CS0246: The type or namespace name 'KillVolume' could not be found`.

### 🛡️ Reglas de Oro:
1. **Cualificación Explícita de Tipos en Builders:**
   - Al agregar componentes en scripts de Editor o constructores procedurales que manejen structs o colecciones de prefabs homónimos, usar siempre el namespace completo del componente:
   ```csharp
   root.AddComponent<Game.Gameplay.Interactables.KillVolume>();
   ```
2. **Evitar Ambigüedad en Identificadores:**
   - Si un struct agrupa referencias a prefabs o materiales, preferir prefijos o sufijos diferenciadores cuando sea posible, o asegurar cualificación absoluta del tipo de componente en llamadas a `AddComponent<T>()` o `GetComponent<T>()`.
3. **Desacoplamiento en Tiempo de Compilación para Nuevos Scripts de Runtime:**
   - Cuando se cree un nuevo script de Runtime (`Game.Gameplay`) y al mismo tiempo se actualice un Builder de Editor (`Game.Editor`), si Unity no ha importado aún el script de runtime a su base de datos de assets (`AssetDatabase`), una referencia estática directa (`root.AddComponent<NewType>()`) genera un bloqueo circular `CS0234` porque el ensamblado de Editor falla la compilación e impide que Unity ejecute el ciclo de refresco y domain reload.
   - En estos casos, resolver el tipo dinámicamente mediante el ensamblado en tiempo de ejecución:
   ```csharp
   var compType = typeof(SomeExistingType).Assembly.GetType("Namespace.NewType");
   if (compType != null) root.AddComponent(compType);
   ```

---

## 🚪 8. Producción de Contenido y Transición Multi-Escena (Campaña)

### ❌ Errores cometidos:
- Asumir que `ScenesModule.LoadSceneAsync(sceneName)` de PurrNet puede cargar escenas que no están registradas en `EditorBuildSettings.scenes`. PurrNet delega internamente a la API de Unity `SceneManager`, la cual ignora o arroja excepción si la escena no figura en la lista de build.
- Tratar de resolver tipos no MonoBehaviour (como `GameManager`) mediante `Object.FindFirstObjectByType<GameManager>()` en scopes locales (`CS0311`).

### 🛡️ Reglas de Oro:
1. **Registro Obligatorio en `EditorBuildSettings`:**
   - Toda escena de sala generada proceduralmente (`Room_01` a `Room_10`) debe añadirse inmediatamente al array `EditorBuildSettings.scenes` mediante `EditorBuildSettingsScene`, conservando a `Boot.unity` en el índice 0.
2. **Autoridad en Transiciones (`RoomExitTrigger`):**
   - La llamada para avanzar de escena (`AdvanceToNextRoomAsync`) debe originarse estrictamente en el Servidor/Host:
   ```csharp
   bool canExecute = !isSpawned || isServer;
   if (!canExecute) return;
   _isTransitioning = true;
   _levelManager.AdvanceToNextRoomAsync(ct).Forget();
   ```
   - Prevenir llamadas duplicadas con flags atómicos de debounce (`_isTransitioning = true`) en el trigger de cruce.
3. **Persistencia de Servicios Globales vs Scopes Locales:**
   - `GameLifetimeScope` persiste entre escenas gracias a `DontDestroyOnLoad`. Administra `ILevelManager`, `INetworkService`, `IGameEventBus` y `IPlayerRegistry`.
   - Cada escena posee su propio `RoomLifetimeScope` local que se desmantela y reconstruye al cambiar de sala, registrando el `RoomController`, `SpawnPointManager`, `CameraController` y `CheckpointSystem` propios de ese nivel.

---

## 👥 9. Transición de Escenas Multijugador, Spawning Asíncrono y Cinemachine

### ❌ Errores cometidos:
- **Spawning prematuro en el Servidor sin esperar al Cliente:** En `TPOBPlayerSpawner.OnSpawned()`, el servidor spawneaba a Piernas y Torso inmediatamente e invocaba `identity.GiveOwnership(playerId)`. El cliente todavía estaba cargando la escena asíncronamente por red, provocando que no recibiera el avatar o que se perdiera el ownership (`isOwner = false`), dejando los controles de Torso (`TorsoController.Update()`) completamente muertos.
- **Vaciado de targets de Cinemachine por fotograma (`Targets.Clear()` en `Update`):** Al detectar que Torso aún no existía en escena o que un target fue destruido, `TargetGroupCoordinator` ejecutaba `_targetGroup.Targets.Clear()` cada frame a 60 FPS. `CinemachineGroupFraming` con modo `DollyThenZoom` interpretaba un bounding box colapsado o en `(0,0,0)` y alejaba la cámara disparada hacia el cielo ("bug").
- **NetworkManager efímero (`_dontDestroyOnLoad = false`):** Crear el `NetworkManager` en builders sin activar `_dontDestroyOnLoad = true`, provocando que al llamar `SceneManager.LoadSceneAsync` con `LoadSceneMode.Single`, Unity destruyera el `NetworkManager` activo, rompiendo la sesión de red en clientes y dejando salas sucesivas con gestores desconectados o duplicados.

### 🛡️ Reglas de Oro:
1. **Spawning Sincronizado con `ScenePlayersModule.onPlayerLoadedScene`:**
   - En PurrNet, el servidor NUNCA debe asumir que un cliente remoto ha cargado la escena sólo porque el servidor ya está en ella.
   - Suscribirse siempre a `scenePlayersModule.onPlayerLoadedScene`.
   - Spawnear inmediatamente a los jugadores locales/host presentes en `scenePlayersModule.TryGetPlayersInScene(sceneId, out var players)`.
   - Para clientes remotos, esperar a que `onPlayerLoadedScene` confirme que el cliente completó la carga antes de llamar a `UnityProxy.Instantiate(prefab, pos, rot, gameObject.scene)`, `nm.Spawn()` y `identity.GiveOwnership(player)`.
2. **Estabilidad de Targets en Cinemachine:**
   - NUNCA invocar `_targetGroup.Targets.Clear()` en cada frame dentro de `Update()`.
   - Mantener variables cacheadas (`_cachedLegsTarget`, `_cachedTorsoTarget`) y sólo reconstruir targets cuando haya un cambio real de referencias válidas.
   - Si sólo un jugador está presente (ej. Host ya cargó y Cliente está conectando), la cámara debe encuadrar limpiamente al jugador presente. Al aparecer el segundo jugador, debe incorporarse suavemente al grupo sin saltos ni alejamiento de cámara.
3. **Persistencia Estricta de Red y Singleton en Scopes Globales:**
   - Todo `NetworkManager` debe tener `_dontDestroyOnLoad = true`.
   - En `GameLifetimeScope.Awake()`, verificar `Instance != null && Instance != this`. Si ya existe una instancia persistente de una sala previa, destruir inmediatamente el GameObject duplicado o la jerarquía `--- NETWORKING ---` creada para pruebas offline locales.

---

## 🚫 10. DontDestroyOnLoad en Objetos Hijos, Duplicados de Menú y Re-entrancia de Spawning

### ❌ Errores cometidos:
- **Parenting de NetworkManager bajo `--- NETWORKING ---`:** Unity arroja `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects` si se intenta marcar DDOL un GameObject que tiene padre en la jerarquía.
- **Colisión de MenuItems:** Declarar métodos con el mismo atributo `[MenuItem("TPOB/3. Generar las 10 Salas de Campaña")]` en dos clases de Editor (`PlaygroundBuilder` y `RoomContentBuilder`).
- **Triple Spawning de Torso por Re-entrancia y Fallback prematuro:**
  1. `OnSpawned()` ejecutaba `#if UNITY_EDITOR` cuando `ConnectedPlayers.Count == 0`, instanciando un Torso prematuro para un ID dummy.
  2. Al conectarse el cliente/host (`onPlayerLoadedScene`), `DetermineRoleForPlayer` llamaba a `_playerRegistry.TryAssignRole()`.
  3. `TryAssignRole` disparaba sincrónicamente el evento `OnRoleAssigned`.
  4. El handler `HandleRoleAssigned` re-invocaba `HandlePlayerLoadedScene` antes de que la llamada original añadiera el ID a `_spawnedPlayers`.
  5. Ambos stacks de ejecución llamaban a `SpawnPlayerForPlayer()`, sumando 3 Torsos y 0 Piernas en escena.

### 🛡️ Reglas de Oro:
1. **NetworkManager Siempre en la Raíz de la Escena:**
   - Todo GameObject que llame a `DontDestroyOnLoad(gameObject)` (como PurrNet `NetworkManager` cuando `_dontDestroyOnLoad = true`) DEBE ser un root GameObject (`transform.parent == null`).
   - En builders de escenas y en `GameLifetimeScope.Awake()`, asegurar `if (nm.transform.parent != null) nm.transform.SetParent(null);` antes de que se invoque el ciclo de vida DDOL.
2. **MenuItems Únicos:**
   - Cada `[MenuItem]` en el proyecto debe tener una ruta de menú única y distinguible. No duplicar métodos envoltorios con la misma ruta.
3. **Control de Re-entrancia y Single-Instance en Spawners:**
   - Marcar `_spawnedPlayers.Add(playerId)` **ANTES** de invocar asignaciones de roles o disparar eventos sincrónicos.
   - En `SpawnPlayerForPlayer`, implementar siempre guardas de unicidad: si ya existe `FindFirstObjectByType<TorsoController>()` o `FindFirstObjectByType<LegsController>()`, NO instanciar un nuevo avatar; en su lugar, asignar ownership a la entidad existente.
   - Eliminar spawns prematuros de mocks o dummies en `OnSpawned()`; dejar que `ScenePlayersModule` o `EnsureEditorBuddySpawned()` gobiernen la instanciación de forma autoritativa y limpia.

---

## 🛑 11. Bloqueo de Carga de Escenas (IsLoading) y Velocidades en Cuerpos Cinemáticos

### ❌ Errores cometidos:
- **Pasar CancellationToken Efímero a un Servicio Persistente (`LevelManager`):** `RoomExitTrigger` pasaba su propio `_cts.Token` a `_levelManager.AdvanceToNextRoomAsync(ct)`. Al comenzar a descargarse la Sala 1, `RoomExitTrigger.OnDestroy()` invocaba `_cts.Cancel()`. La tarea de carga de escena lanzaba `OperationCanceledException` antes de resetear `IsLoading = false`. Al llegar a la Sala 2, `IsLoading` continuaba en `true` para siempre, rechazando cualquier intento futuro de cargar la Sala 3 con el mensaje `[LevelManager] Room load already in progress`.
- **Modificar `linearVelocity` o `angularVelocity` en un `Rigidbody` cinemático:** Al agarrar objetos (`GrabbableObject.OnGrabbed`, `MagneticKey.OnGrabbed`), encajar llaves (`PuzzleSocket`), o en `PlayerDeathHandler.Respawn()`, se fijaba `isKinematic = true;` ANTES de resetear `linearVelocity = Vector3.zero;`, provocando la excepción/error de Unity `Setting linear/angular velocity of a kinematic body is not supported`.

### 🛡️ Reglas de Oro:
1. **Gestión de Ciclo de Vida en Servicios Persistentes (`try-finally` e `IsLoading`):**
   - En managers persistentes (`LevelManager`), envolver siempre la carga asíncrona en un bloque `try ... catch ... finally { IsLoading = false; }`.
   - En `HandleSceneLoaded(Scene scene, LoadSceneMode mode)`, resetear incondicionalmente `IsLoading = false;` para garantizar que la carga se desbloquee al confirmarse la escena.
   - **PROHIBIDO** pasar tokens de cancelación de MonoBehaviours de escena que serán destruidos a métodos de transición global. `LevelManager` debe gobernar sus cargas con `this.GetCancellationTokenOnDestroy()`.
2. **Asignación Segura de Físicas en Rigidbody:**
   - En cualquier transición hacia cinemático (`OnGrabbed`, `KillVolume`, `PuzzleSocket`, `Fusion`, `Death`):
     1. Comprobar `if (!rb.isKinematic)` y resetear velocidades (`linearVelocity = Vector3.zero; angularVelocity = Vector3.zero;`) **ANTES** de cambiar `isKinematic`.
     2. Fijar `rb.isKinematic = true;`.
   - En cualquier transición desde cinemático hacia dinámico (`OnReleased`, `Separation`, `Respawn`):
     1. Fijar `rb.isKinematic = false;`.
     2. Asignar las velocidades de impulso solo si `!rb.isKinematic`.

---

## 🤖 12. Fusión Cooperativa, Desacoplamiento de Rotación (Torreta) e Inmunidad a Torques de PhysX

### ❌ Errores cometidos:
- **Efecto calesita por torque de contacto PhysX:** Al fusionar, el `BoxCollider` de Torso intersectaba el `CapsuleCollider` de Piernas a la altura del socket (`y = 1.8`). No se ignoraban colisiones entre ellos ni se congelaba la rotación Y en el Rigidbody de Torso. PhysX generaba torque continuo en el contacto normal, haciendo que el Torso girara suavemente como una calesita de forma indefinida.
- **Acoplamiento rígido con la rotación de Piernas:** Al emparentar Torso en `attachPoint`, los giros de Piernas arrastraban la rotación del Torso. Además, `ApplyAiming()` hacía `return;` temprano cuando el stick de apuntado estaba en reposo, dejando al Torso sin orientación fija y a merced de la rotación de las Piernas.
- **Omisión de controles WASD / Stick Izquierdo al estar fusionado:** Dado que `ApplyCrawlLocomotion` se apaga al fusionar, los inputs de movimiento (`MoveDirection`) eran descartados, impidiendo que jugadores en teclado o con stick izquierdo pudieran apuntar la torreta.
- **Conflicto de `NetworkTransform` con jerarquías emparentadas:** `NetworkTransform` en Torso seguía activo sincronizando posiciones y rotaciones de mundo mientras era hijo de Piernas, compitiendo contra la interpolación del padre en clientes y generando tirones.

### 🛡️ Reglas de Oro:
1. **Aislamiento de Colisiones y Restricciones Físicas en Fusión:**
   - Al fusionar (`FusionStrategy.OnEnter`), ignorar colisiones mutuas entre todos los colliders de Torso y Piernas mediante `Physics.IgnoreCollision(colA, colB, true)`.
   - Fijar `_rigidbody.constraints = RigidbodyConstraints.FreezeAll;` y resetear `angularVelocity = Vector3.zero;` para neutralizar cualquier momento angular.
   - Al separar (`SeparationStrategy.OnEnter`), reactivar colisiones (`Physics.IgnoreCollision(..., false)`) y restaurar `RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ`.
2. **Torreta Independiente en Espacio de Mundo:**
   - Gobernar la orientación del Torso con un campo dedicado `_fusedWorldRotation`.
   - Si no hay input de apuntado (`AimDirection`), hacer fallback a `MoveDirection` para que WASD y el stick izquierdo permitan apuntar al estar fusionados.
   - Si el input está por debajo del umbral de zona muerta, **mantener estrictamente** `_fusedWorldRotation` (cero derivas, cero efecto calesita).
   - En `LateUpdate()`, asegurar `if (_isFused) transform.rotation = _fusedWorldRotation;` en cada frame renderizado para garantizar que los giros de las Piernas nunca afecten la visualización del Torso.
3. **Desactivación de `NetworkTransform` en Entidades Emparentadas:**
   - Al fusionar, deshabilitar el `NetworkTransform` de la entidad hija (`_networkTransform.enabled = false;`), sincronizando la rotación de torreta mediante un `SyncVar<Quaternion> _syncedFusedRotation`.
   - Al separar, volver a habilitar `_networkTransform.enabled = true;` para retomar el seguimiento de mundo normal.

---

## 🔊 13. Audio Procedural, Cero GC y Sincronización en Red con PurrNet

### ❌ Errores cometidos:
- **Dependencia de Assets Externos Inexistentes:** Diseñar sistemas de audio esperando archivos `.wav`/`.mp3` que no existen en el repositorio genera excepciones de referencia nula y silencio absoluto.
- **Límites de Ensamblados (Asmdef) y Dependencias Circulares:** Colocar la implementación de `AudioService` en `Game.Gameplay` mientras `GameLifetimeScope` (en `Game.Network`) intenta registrarlo genera un ciclo de ensamblado (`Game.Gameplay` -> `Game.Network` -> `Game.Gameplay`).
- **RPCs de Audio con Buffer Indebido (`bufferLast: true`):** Marcar RPCs de efectos de sonido con `bufferLast: true` provoca que un cliente que cargue la sala más tarde reproduzca en ráfaga todos los sonidos de saltos, patadas y colisiones pasados.

### 🛡️ Reglas de Oro:
1. **Síntesis Procedural como Fallback Autónomo:**
   - Implementar generadores sinusoidales y de ruido blanco procedural (`ProceduralAudioSynthesizer`) en `Awake()`. Si no hay clips en el inspector, el juego genera y cachea proceduralmente sus propios `AudioClip`s en memoria con zero-GC posterior.
2. **Ubicación de Servicios de Infraestructura en `Game.Core`:**
   - Servicios de audio (`IAudioService`, `AudioService`) deben residir en `Game.Core.Audio` para que tanto `Game.Network` (`GameLifetimeScope`) como `Game.Gameplay` puedan consumirlos e inyectarlos sin fricción de ensamblado.
3. **RPCs de Audio Efímeros (`bufferLast: false`):**
   - Los eventos de sonido son estrictamente efímeros y no representan estado persistente:
   ```csharp
   [ObserversRpc(runLocally: true, bufferLast: false)]
   private void PlayAudioObserversRpc(AudioCue cue, Vector3 position, float volume, float pitch)
   ```
4. **Pool de AudioSources sin Garbage Collection:**
   - Utilizar `UnityEngine.Pool.ObjectPool<AudioSource>` pre-asignado para reproducir sonidos 3D espaciales. La liberación al pool se gestiona en `Update()` mediante un tracking struct (`ActiveSourceTracker`), garantizando CERO allocations durante el juego activo.

---

## 🌐 14. Simulación de Red (PurrNet), Desconexiones Robustas y Auditoría Zero-GC

### ❌ Errores cometidos:
- **Simulación de red externa en vez del stack nativo:** Intentar introducir delays artificiales con sleeps de sistema o emuladores de paquetes de terceros cuando PurrNet `UDPTransport` ya provee internamente `networkSimulation` con soporte para min/max latency y packet loss.
- **Desconexión con bloqueo en Fusión:** Si un jugador se desconecta abruptamente mientras el robot está fusionado, dejar al Torso atrapado como hijo del transform de Piernas provoca que el cliente restante no pueda controlar el robot completo o que el Rigidbody permanezca en `isKinematic = true`.
- **Destrucción de avatares en `onPlayerLeft`:** Destruir los GameObjects de Piernas o Torso cuando un cliente se desconecta rompe las referencias de Cinemachine, RoomController y Spawners para futuros reintentos o reconexiones.

### 🛡️ Reglas de Oro:
1. **Simulación Nativa PurrNet (`UDPTransport.networkSimulation`):**
   - Configurar la simulación mediante perfiles (`Ideal = 0ms`, `Online = 50-80ms/1%`, `QA Stress = 100-150ms/2%`, `Extreme = 250-350ms/8%`).
   - Activar `includeInBuild = true` en `NetworkSimulation` para que las pruebas de estrés funcionen tanto en el editor como en las builds Standalone ejecutables.
   - Invocar `transport.SetStatisticsEnabled(true)` para registrar y medir la pérdida de paquetes.
2. **Preservación de Entidades y Liberación de Ownership:**
   - En `TPOBPlayerSpawner`, al dispararse `nm.onPlayerLeft`:
     ```csharp
     if (identity.owner.HasValue && identity.owner.Value == player)
     {
         identity.RemoveOwnership();
     }
     ```
   - No destruir el GameObject del avatar; remover el ownership permite que el jugador se reconecte y reclame su rol o que otro cliente tome el control inmediatamente.
3. **Separación de Emergencia ante Desconexión en Fusión:**
   - En `RobotCoordinator.UnregisterLegs` y `UnregisterTorso`:
     ```csharp
     if (_isFused.value && (isServer || !isSpawned))
     {
         TrySeparateOnServer();
     }
     ```
   - Esto garantiza que el Torso se des-emparenta, recupera sus colliders normales, y el Rigidbody vuelve al estado dinámico seguro.
4. **Auditoría Zero-GC y Monitoreo en Tiempo Real:**
   - Emplear `ZeroGCAuditor` (`TPOB/4. Auditar Código para Zero-GC`) para validar que los métodos de ciclo de vida en caliente no contengan `new` de clases, búsquedas de escenas (`FindObjectOfType`), concatenaciones de strings o llamadas a LINQ.
   - Mantener activo `PerformanceMonitor` (conmutable con `F3`) para auditar en tiempo real FPS, consumo de heap Mono, conteo de recolecciones por generación y deltas de memoria por segundo.
5. **Pipeline de Compilación Standalone Automatizado:**
   - Asegurar que `StandaloneBuildHelper` (`TPOB/5. Compilar Standalone Windows (x64)`) compile la lista completa de escenas (`Boot.unity` seguido de `Room_01` a `Room_10`).
   - Usar `TPOB/6. Lanzar 2 Instancias Locales (Host + Cliente)` para levantar automáticamente dos instancias en modo ventana (1280x720) y verificar la sincronización cooperativa en entornos reales de producción.

---

## 🧩 15. Prohibición de APIs de Unity en Constructores e Inicializadores de Campo de MonoBehaviour

### ❌ Error cometido:
- Declarar `private string _cachedRoomCode = "TPOB-" + UnityEngine.Random.Range(1000, 9999);` como campo en `MainMenuPresenter`.
- En C#, los inicializadores de campo se ejecutan dentro del constructor por defecto (`.ctor()`). Unity prohíbe invocar la mayoría de sus APIs nativas (incluyendo `Random.Range`, `GameObject`, `Transform`, etc.) desde constructores de MonoBehaviours durante la deserialización o instanciación, arrojando:
  `UnityException: RandomRangeInt is not allowed to be called from a MonoBehaviour constructor (or instance field initializer), call it in Awake or Start instead.`

### 🛡️ Reglas de Oro:
1. **Cero Invocaciones a Unity APIs en Inicializadores de Campo:**
   - Los campos de clases que hereden de `MonoBehaviour` o `NetworkBehaviour` deben inicializarse únicamente con valores constantes o literales primitivos (`null`, `0`, `string.Empty`).
   - Jamás invocar métodos como `Random.Range`, `Shader.PropertyToID`, `Color`, etc. en la declaración del campo.
2. **Inicialización Perezosa o en Métodos de Ciclo de Vida:**
   - Trasladar toda generación aleatoria o cálculo a `Awake()`, `Start()`, o propiedades / métodos seguros tipo *lazy initialization*:
   ```csharp
   private string _cachedRoomCode;

   private string GetRoomCode()
   {
       if (string.IsNullOrEmpty(_cachedRoomCode))
       {
           _cachedRoomCode = "TPOB-" + UnityEngine.Random.Range(1000, 9999);
       }
       return _cachedRoomCode;
   }
   ```

---

## 📐 16. Layout Groups, Botones con Altura 0 y Búsqueda de Inactivos en Builders

### ❌ Error cometido:
- **Colapso a Altura 0 en Botones:** En `MainMenuBuilder` y `LobbyBuilder`, se creó un `VerticalLayoutGroup` con `childControlHeight = true` y `childForceExpandHeight = false`. Los botones hijos creados proceduralmente no tenían componente `LayoutElement`. Como `Image` y `Button` no implementan altura preferida por defecto, Unity colapsó el `RectTransform.sizeDelta.y` de todos los botones a `0`.
- **Consecuencia:** Visualmente el texto de TextMeshProUGUI se dibujaba (por desbordamiento), pero la caja interactuable del botón (`Image`) medía 0 píxeles de alto. Al hacer clic, los raycasts pasaban de largo hacia el fondo vacío (`Text` tenía `raycastTarget = false`), haciendo que ningún botón del menú respondiera a clics en la build.
- **Acumulación de Canvases Inactivos en Escena:** `Object.FindFirstObjectByType<T>()` sin `FindObjectsInactive.Include` ignora objetos inactivos. Al re-ejecutar builders, Canvases inactivos como `Dialog_Settings` o `Canvas_Lobby` no eran detectados y se instanciaban copias duplicadas indefinidamente bajo `--- UI ---`.

### 🛡️ Reglas de Oro:
1. **`LayoutElement` Obligatorio bajo Layout Groups:**
   - Todo elemento UI procedural que viva dentro de un `VerticalLayoutGroup` o `HorizontalLayoutGroup` con `childControlHeight = true` o `childControlWidth = true` DEBE tener un componente `LayoutElement` con sus dimensiones mínimas y preferidas explícitas:
   ```csharp
   var le = go.AddComponent<LayoutElement>();
   le.minHeight = height;
   le.preferredHeight = height;
   le.flexibleWidth = 1f;
   ```
2. **Creación con `typeof(RectTransform)`:**
   - Crear GameObjects de UI siempre con `new GameObject(name, typeof(RectTransform))` para garantizar su naturaleza UI desde el primer fotograma.
3. **Búsqueda de Entidades Inactivas y Limpieza de UI previa:**
   - Al buscar componentes que puedan estar desactivados en escena, usar `Object.FindFirstObjectByType<T>(FindObjectsInactive.Include)`.
   - En builders que configuran escenas (`ConfigureCompleteBootScene`), limpiar siempre los hijos existentes del contenedor `--- UI ---` antes de instanciar prefabs nuevos para evitar duplicados residuales.

---

## 🔗 17. Fusión Multijugador: Ownership de Coordinador, Despacho de Input en Gameplay y Umbrales de Proximidad

### ❌ Errores cometidos:
- **Métodos Huérfanos sin Enlace de Input:** Declarar `RequestFusion()` y `RequestSeparation()` en los controladores y coordinadores pero nunca invocarlos desde el bucle de input (`Update`), haciendo imposible que los jugadores ejecuten la mecánica en una build jugable.
- **`[ServerRpc]` de Fusión sin `requireOwnership: false` en Objetos Neutrales:** `RobotCoordinator` pertenece a la escena (Host). Un cliente remoto que invocaba `RequestFusionServerRpc()` era rechazado en silencio por PurrNet por no ser dueño de la entidad.
- **Umbral de Distancia Estricto sin Tolerancia Vertical:** Calcular la distancia tridimensional entre el Torso en el suelo (`y = 0.5`) y el conector superior de las Piernas (`y = 1.8`) con un radio estricto de 3.5m reducía el margen de maniobra en el suelo a escasos 3.2m, fallando en silencio sin advertencias ni feedback al jugador.

### 🛡️ Reglas de Oro:
1. **Auditoría de Despacho de Comandos:**
   - Todo comando o mecánica cooperativa central (`FuseCommand`, `SeparateCommand`) debe tener su canal directo en el `InputReader` (`ConsumeFuseTrigger()`) y ser despachado en el bucle principal de control.
2. **`requireOwnership: false` en Coordinadores de Escena:**
   - En `NetworkBehaviour`s neutrales de sala (`RobotCoordinator`, `LevelManager`, `PuzzleMechanism`), marcar SIEMPRE `[ServerRpc(Channel.ReliableOrdered, requireOwnership: false)]` para que cualquiera de los clientes conectados pueda disparar la acción.
3. **Márgenes de Proximidad Generosos y Diagnóstico Visible:**
   - Usar un umbral de proximidad de al menos **`4.5m`** (`_maxFusionDistance = 4.5f`) para acomodar diferencias de altura entre socket y suelo.
   - En caso de rechazo autoritativo en servidor, emitir siempre un `Debug.LogWarning` con la distancia exacta medida y las referencias comprobadas para facilitar el diagnóstico en `Player.log`.

---

## 🖥️ 18. Ciclo de Vida de UI, GameObjects Inactivos en Escena y Transición al Lobby

### ❌ Error cometido:
- **GameObjects de Canvas Inactivos en Escena (`m_IsActive: 0`):** Al guardar la escena `Boot.unity` desde builders (`LobbyBuilder`, `PauseMenuBuilder`), se invocaba `lobbyInstance.SetActive(false);`. Esto grababa el prefab `Canvas_Lobby` en la escena con `m_IsActive: 0`.
- **Muerte de Ciclo de Vida de MonoBehaviours:** En Unity, cuando un GameObject arranca desactivado en la jerarquía, sus MonoBehaviours (`LobbyPresenter`, `LobbyView`) **NUNCA ejecutan `Awake()`, `Start()` ni `OnEnable()`**.
- **Consecuencia:** `LobbyPresenter` jamás se suscribió a `_gameManager.OnGameStateChanged`. Cuando el jugador pulsaba "Host" o "Connect", `GameManager` cambiaba el estado a `GameState.Lobby`. `MainMenuPresenter` recibía el evento y se apagaba a sí mismo (`_view.SetActive(false)`), pero `Canvas_Lobby` nunca despertaba ni recibía la señal, dejando la pantalla completamente negra y vacía.

### 🛡️ Reglas de Oro:
1. **Gestión de Visibilidad con `Canvas.enabled` en vez de `gameObject.SetActive`:**
   - En pantallas complejas o canvases gobernados por Presenters/Servicios (`Canvas_Lobby`, `Canvas_PauseMenu`, `Canvas_MainMenu`), **mantener siempre el GameObject raíz activo (`activeSelf = true`)**.
   - Para ocultar o mostrar la pantalla, conmutar únicamente `Canvas.enabled = active` y `GraphicRaycaster.enabled = active` (o `CanvasGroup.alpha` y `blocksRaycasts`):
   ```csharp
   public void SetActive(bool active)
   {
       if (_canvas != null) _canvas.enabled = active;
       if (_raycaster != null) _raycaster.enabled = active;
       if (!gameObject.activeSelf) gameObject.SetActive(true);
   }
   ```
   - Esto garantiza 0 draw calls y 0 intercepciones de clics mientras esté oculto, pero mantiene los scripts vivos en memoria escuchando eventos de red y cambios de estado.
2. **Fail-Safe en Presenters Emisores:**
   - Cuando un Presenter (`MainMenuPresenter`) oculte su vista por un cambio de estado hacia otra pantalla (`GameState.Lobby`), buscar defensivamente la vista destino mediante `Object.FindFirstObjectByType<LobbyView>(FindObjectsInactive.Include)` y asegurar explícitamente su activación y refresco (`lobbyView.SetActive(true); presenter.RefreshUI();`).
3. **Verificación de Conexión Síncrona en NetworkService:**
   - Al llamar `StartHost()` o `StartClient()`, si el `NetworkManager` ya pasa síncronamente al estado `Connected`, disparar de inmediato `OnConnected?.Invoke()` para garantizar que la transición de estado nunca dependa exclusivamente de un callback asíncrono tardío.
4. **Exhibición Visible del Código de Sala en el Lobby:**
   - El lobby debe mostrar de forma prominente el código de sala generado (`CÓDIGO DE SALA: TPOB-XXXX (Compártelo con tu compañero)`) para que el anfitrión pueda dictárselo a su compañero sin necesidad de salir del lobby ni recurrir a herramientas externas.

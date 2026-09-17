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

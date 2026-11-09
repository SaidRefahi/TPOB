using System.Collections.Generic;
using Game.Gameplay.Checkpoints;
using Game.Gameplay.Hazards;
using Game.Gameplay.Interactables;
using Game.Gameplay.Rooms;
using TriInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Gameplay.Level
{
    public enum WallSectionShape
    {
        RectanglePerimeter = 0,
        LShape = 1,
        UShape = 2,
        StraightLine = 3
    }

    public enum StraightWallDirection
    {
        NorthSouth_Z = 0,
        EastWest_X = 1
    }

    public enum MagneticTrackDirection
    {
        VerticalClimb = 0,
        HorizontalTraverse = 1,
        Ceiling = 2
    }

    [DisallowMultipleComponent]
    [DeclareBoxGroup("Acciones Rápidas")]
    [DeclareBoxGroup("Visualizador de Métricas en SceneView")]
    [DeclareBoxGroup("Enlazador Rápido de Mecanismos")]
    [DeclareBoxGroup("Generador de Secciones de Muros")]
    [DeclareBoxGroup("Generador de Pisos y Plataformas")]
    [DeclareBoxGroup("Generador de Pistas Magnéticas")]
    [DeclareBoxGroup("Generador de Plataformas y Puentes")]
    [DeclareBoxGroup("Calibrador de Balancín Catapulta")]
    [DeclareBoxGroup("Configuración de Snapping")]
    [DeclareBoxGroup("Paleta - Arquitectura")]
    [DeclareBoxGroup("Paleta - Mecanismos e Interactivos")]
    [DeclareBoxGroup("Paleta - Peligros y Dinámicas")]
    [DeclareBoxGroup("Paleta - Checkpoints")]
    [DeclareBoxGroup("Información del Nivel")]
    [DeclareBoxGroup("Alturas y Deadzone")]
    [DeclareBoxGroup("Spawns y Checkpoints")]
    public sealed class LevelDefinition : MonoBehaviour
    {
        private const string PrefabsPath = "Assets/_Project/Prefabs";

        [Group("Información del Nivel")]
        [SerializeField] private string _levelName = "Level_01";

        [Group("Información del Nivel")]
        [SerializeField] private int _levelIndex = 1;

        [Group("Alturas y Deadzone")]
        [SerializeField] private float _groundY = 0f;

        [Group("Alturas y Deadzone")]
        [SerializeField] private float _subterraneanDeadzoneY = -3f;

        [Group("Alturas y Deadzone")]
        [SerializeField] private float _deadzonePadding = 15f;

        [Group("Alturas y Deadzone")]
        [SerializeField] private KillVolume _subterraneanKillVolume;

        [Group("Spawns y Checkpoints")]
        [SerializeField] private Transform _legsSpawnPoint;

        [Group("Spawns y Checkpoints")]
        [SerializeField] private Transform _torsoSpawnPoint;

        [Group("Spawns y Checkpoints")]
        [SerializeField] private List<Checkpoint> _checkpoints = new();

        [Group("Spawns y Checkpoints")]
        [SerializeField] private RoomExitTrigger _exitTrigger;

#if UNITY_EDITOR
        // ---------------------------------------------------------------------------------
        // CONFIGURACIÓN DE HERRAMIENTAS EN EDITOR
        // ---------------------------------------------------------------------------------

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private bool _showMetricGizmos = true;

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private float _walkJumpDistance = 4.0f;

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private float _sprintJumpDistance = 7.0f;

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private float _jumpHeight = 2.5f;

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private float _throwForce = 14.0f;

        [Group("Visualizador de Métricas en SceneView")]
        [SerializeField] private float _magnetRange = 8.0f;

        [Group("Enlazador Rápido de Mecanismos")]
        [SerializeField] private GameObject _sourceMechanism;

        [Group("Enlazador Rápido de Mecanismos")]
        [SerializeField] private SlidingDoor _targetDoor;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private GameObject _customWallPrefab;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private WallSectionShape _wallSectionShape = WallSectionShape.RectanglePerimeter;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private StraightWallDirection _straightDirection = StraightWallDirection.NorthSouth_Z;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private int _widthModules = 6;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private int _lengthModules = 8;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private int _heightTiers = 1;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private float _moduleLength = 2.0f;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private float _moduleHeight = 3.0f;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private float _rotationOffset = 0f;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private bool _flipFacing = false;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private bool _centerOnSelection = true;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private bool _faceOutward = true;

        [Group("Generador de Secciones de Muros")]
        [SerializeField] private bool _generateFloorWithWalls = false;

        private const string DefaultFloorPrefabPath = "Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Ground.prefab";

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private GameObject _customFloorPrefab;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private int _floorWidthModules = 6;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private int _floorLengthModules = 8;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private float _floorModuleLengthX = 4.0f;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private float _floorModuleLengthZ = 4.0f;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private float _floorYOffset = 0.0f;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private bool _floorCenterOnSelection = true;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private bool _generateFloorBoxCollider = true;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private bool _createFloorVoid = false;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private int _voidHoleWidth = 2;

        [Group("Generador de Pisos y Plataformas")]
        [SerializeField] private int _voidHoleLength = 2;

        private GameObject _lastGeneratedFloor;

        [Group("Generador de Pistas Magnéticas")]
        private const string DefaultMagneticWallPath = "Assets/_Project/Prefabs/MagneticWall.prefab";

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private GameObject _customMagneticWallPrefab;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private MagneticTrackDirection _magneticTrackDirection = MagneticTrackDirection.VerticalClimb;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private StraightWallDirection _magneticTrackAxis = StraightWallDirection.EastWest_X;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private int _magneticPanelCount = 5;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private float _magneticPanelLength = 8.0f;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private float _magneticPanelHeight = 8.0f;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private float _magneticCeilingHeight = 6.0f;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private float _magneticRotationOffset = 0f;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private bool _magneticFlipFacing = false;

        [Group("Generador de Pistas Magnéticas")]
        [SerializeField] private bool _magneticCenterOnSelection = true;

        private GameObject _lastGeneratedMagneticTrack;

        [Group("Generador de Plataformas y Puentes")]
        [SerializeField] private GameObject _customPlatformPrefab;

        [Group("Generador de Plataformas y Puentes")]
        [SerializeField] private int _platformCount = 5;

        [Group("Generador de Plataformas y Puentes")]
        [SerializeField] private float _platformGap = 2.5f;

        [Group("Generador de Plataformas y Puentes")]
        [SerializeField] private float _stepHeightDelta = 0.0f;

        private const string DefaultSeeSawPath = "Assets/_Project/Prefabs/SeeSawPlatform.prefab";

        [Group("Calibrador de Balancín Catapulta")]
        [SerializeField] private GameObject _customSeeSawPrefab;

        [Group("Calibrador de Balancín Catapulta")]
        [SerializeField] private GameObject _customLedgePrefab;

        [Group("Calibrador de Balancín Catapulta")]
        [SerializeField] private float _catapultTargetLedgeHeight = 6.0f;

        private GameObject _lastGeneratedSeeSaw;

        [Group("Configuración de Snapping")]
        [SerializeField] private bool _enableGridSnapping = true;

        [Group("Configuración de Snapping")]
        [SerializeField] private float _gridSize = 1.0f;

        [Group("Configuración de Snapping")]
        [SerializeField] private bool _alignToSurface = true;

        [Group("Configuración de Snapping")]
        [SerializeField] private bool _spawnAtOriginZero = true;

        public bool SpawnAtOriginZero => _spawnAtOriginZero;
        public void SetSpawnAtOriginZero(bool value)
        {
            _spawnAtOriginZero = value;
            EditorUtility.SetDirty(this);
        }

        private GameObject _lastGeneratedSection;
#endif

        public string LevelName => _levelName;
        public int LevelIndex => _levelIndex;
        public float GroundY => _groundY;
        public float SubterraneanDeadzoneY => _subterraneanDeadzoneY;
        public float DeadzonePadding => _deadzonePadding;
        public KillVolume SubterraneanKillVolume => _subterraneanKillVolume;
        public Transform LegsSpawnPoint => _legsSpawnPoint;
        public Transform TorsoSpawnPoint => _torsoSpawnPoint;
        public IReadOnlyList<Checkpoint> Checkpoints => _checkpoints;
        public RoomExitTrigger ExitTrigger => _exitTrigger;

        public void SetSubterraneanKillVolume(KillVolume volume) => _subterraneanKillVolume = volume;
        public void SetSpawns(Transform legs, Transform torso) { _legsSpawnPoint = legs; _torsoSpawnPoint = torso; }
        public void SetCheckpoints(List<Checkpoint> checkpoints) { _checkpoints.Clear(); if (checkpoints != null) _checkpoints.AddRange(checkpoints); }
        public void SetExitTrigger(RoomExitTrigger exitTrigger) => _exitTrigger = exitTrigger;
        public void SetGroundY(float groundY) => _groundY = groundY;
        public void SetDeadzoneY(float deadzoneY) => _subterraneanDeadzoneY = deadzoneY;
        public void SetDeadzonePadding(float padding) => _deadzonePadding = padding;

#if UNITY_EDITOR
        public void ConfigureWallGenerator(
            GameObject prefab,
            WallSectionShape shape,
            StraightWallDirection direction,
            int width,
            int length,
            int heightTiers,
            float moduleLength,
            float moduleHeight,
            float rotationOffset = 0f,
            bool flipFacing = false,
            bool centerOnSelection = true,
            bool faceOutward = true)
        {
            _customWallPrefab = prefab;
            _wallSectionShape = shape;
            _straightDirection = direction;
            _widthModules = width;
            _lengthModules = length;
            _heightTiers = heightTiers;
            _moduleLength = moduleLength;
            _moduleHeight = moduleHeight;
            _rotationOffset = rotationOffset;
            _flipFacing = flipFacing;
            _centerOnSelection = centerOnSelection;
            _faceOutward = faceOutward;
            EditorUtility.SetDirty(this);
        }

        public void ConfigureWallFloor(bool generateFloor, bool createVoid = false, int voidW = 0, int voidL = 0, float tileSize = 1.0f)
        {
            _generateFloorWithWalls = generateFloor;
            if (generateFloor)
            {
                _createFloorVoid = createVoid;
                _voidHoleWidth = voidW;
                _voidHoleLength = voidL;
                if (tileSize > 0.05f)
                {
                    _floorModuleLengthX = tileSize;
                    _floorModuleLengthZ = tileSize;
                }
            }
            EditorUtility.SetDirty(this);
        }

        public void ConfigureFloorGenerator(
            GameObject prefab,
            int width,
            int length,
            float tileX,
            float tileZ,
            float yOffset,
            bool centerOnSelection,
            bool createVoid = false,
            int voidW = 0,
            int voidL = 0,
            bool generateBoxCollider = true)
        {
            _customFloorPrefab = prefab;
            _floorWidthModules = width;
            _floorLengthModules = length;
            _floorModuleLengthX = tileX;
            _floorModuleLengthZ = tileZ;
            _floorYOffset = yOffset;
            _floorCenterOnSelection = centerOnSelection;
            _createFloorVoid = createVoid;
            _voidHoleWidth = voidW;
            _voidHoleLength = voidL;
            _generateFloorBoxCollider = generateBoxCollider;
            EditorUtility.SetDirty(this);
        }

        public void ConfigurePuzzleLinker(GameObject source, SlidingDoor target)
        {
            _sourceMechanism = source;
            _targetDoor = target;
            EditorUtility.SetDirty(this);
        }

        public void ConfigureMagneticTrack(
            GameObject prefab,
            MagneticTrackDirection direction,
            StraightWallDirection axis,
            int count,
            float panelLength,
            float panelHeight,
            float ceilingHeight = 6.0f,
            float rotationOffset = 0f,
            bool flipFacing = false,
            bool centerOnSelection = true)
        {
            _customMagneticWallPrefab = prefab;
            _magneticTrackDirection = direction;
            _magneticTrackAxis = axis;
            _magneticPanelCount = count;
            _magneticPanelLength = panelLength;
            _magneticPanelHeight = panelHeight;
            _magneticCeilingHeight = ceilingHeight;
            _magneticRotationOffset = rotationOffset;
            _magneticFlipFacing = flipFacing;
            _magneticCenterOnSelection = centerOnSelection;
            EditorUtility.SetDirty(this);
        }

        public void ConfigurePlatformPath(GameObject prefab, int count, float gap, float heightDelta)
        {
            _customPlatformPrefab = prefab;
            _platformCount = count;
            _platformGap = gap;
            _stepHeightDelta = heightDelta;
            EditorUtility.SetDirty(this);
        }

        public void ConfigureSeeSaw(
            GameObject seeSawPrefab,
            GameObject ledgePrefab,
            float targetLedgeHeight)
        {
            _customSeeSawPrefab = seeSawPrefab;
            _customLedgePrefab = ledgePrefab;
            _catapultTargetLedgeHeight = targetLedgeHeight;
            EditorUtility.SetDirty(this);
        }

        public void SetMetricGizmosVisible(bool visible)
        {
            _showMetricGizmos = visible;
            EditorUtility.SetDirty(this);
        }

        // ---------------------------------------------------------------------------------
        // ACCIONES RÁPIDAS (TRI-INSPECTOR BUTTONS)
        // ---------------------------------------------------------------------------------

        [Group("Acciones Rápidas")]
        [Button(ButtonSizes.Large, "Generar / Actualizar Deadzone Subterranea")]
        public void GenerateSubterraneanDeadzone()
        {
            Bounds totalBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            var renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;

                if (!hasBounds)
                {
                    totalBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(r.bounds);
                }
            }

            if (!hasBounds)
            {
                totalBounds = new Bounds(Vector3.zero, new Vector3(40f, 2f, 40f));
            }

            float pad = _deadzonePadding > 0f ? _deadzonePadding : 15f;
            Vector3 size = new Vector3(totalBounds.size.x + pad * 2f, 4f, totalBounds.size.z + pad * 2f);
            Vector3 center = new Vector3(totalBounds.center.x, _subterraneanDeadzoneY - 2f, totalBounds.center.z);

            KillVolume targetVolume = _subterraneanKillVolume;
            if (targetVolume == null)
            {
                string kvPath = $"{PrefabsPath}/KillVolume.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kvPath);
                GameObject go;
                if (prefab != null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                }
                else
                {
                    go = new GameObject("KillVolume_Subterranean");
                    go.AddComponent<BoxCollider>().isTrigger = true;
                    go.AddComponent<KillVolume>();
                }

                go.name = "KillVolume_Subterranean";
                go.transform.SetParent(transform);
                targetVolume = go.GetComponent<KillVolume>();
                _subterraneanKillVolume = targetVolume;
                Undo.RegisterCreatedObjectUndo(go, "Generate Subterranean Deadzone");
            }

            Undo.RecordObject(targetVolume.transform, "Resize Subterranean Deadzone Transform");
            targetVolume.transform.position = center;

            var col = targetVolume.BoxCollider;
            if (col != null)
            {
                Undo.RecordObject(col, "Resize Subterranean Deadzone Collider");
                col.isTrigger = true;
                col.center = Vector3.zero;
                col.size = size;
            }

            EditorUtility.SetDirty(targetVolume);
            EditorUtility.SetDirty(this);
            Debug.Log($"[LevelDefinition] Deadzone Subterránea generada en {center} con dimensiones {size}.");
        }

        [Group("Acciones Rápidas")]
        [Button(ButtonSizes.Large, "Auto-Descubrir Spawns y Checkpoints")]
        public void AutoDiscoverElements()
        {
            Undo.RecordObject(this, "Auto-Discover Level Elements");

            var spawnMgr = FindFirstObjectByType<Spawning.SpawnPointManager>();
            if (spawnMgr != null)
            {
                var spLegs = spawnMgr.GetSpawnPoint(Game.Core.Enums.PlayerRole.Legs);
                var spTorso = spawnMgr.GetSpawnPoint(Game.Core.Enums.PlayerRole.Torso);
                _legsSpawnPoint = spLegs != null ? spLegs.transform : null;
                _torsoSpawnPoint = spTorso != null ? spTorso.transform : null;
            }

            var checkpoints = new List<Checkpoint>(FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
            checkpoints.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _checkpoints.Clear();
            _checkpoints.AddRange(checkpoints);

            var exit = FindFirstObjectByType<RoomExitTrigger>();
            if (exit != null)
            {
                _exitTrigger = exit;
            }

            var killVolumes = FindObjectsByType<KillVolume>(FindObjectsSortMode.None);
            for (int i = 0; i < killVolumes.Length; i++)
            {
                if (killVolumes[i].transform.position.y <= _groundY)
                {
                    _subterraneanKillVolume = killVolumes[i];
                    break;
                }
            }

            EditorUtility.SetDirty(this);
            Debug.Log($"[LevelDefinition] Descubrimiento finalizado: {_checkpoints.Count} checkpoints, Spawns {(spawnMgr != null ? "OK" : "NO ENCONTRADOS")}.");
        }

        [Group("Acciones Rápidas")]
        [Button(ButtonSizes.Gigantic, "Bake & Validate Level")]
        public void BakeAndValidate()
        {
            AutoDiscoverElements();

            var validatorType = System.Type.GetType("Game.Editor.LevelEditor.LevelValidator, Game.Editor");
            if (validatorType != null)
            {
                var method = validatorType.GetMethod("Validate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                method?.Invoke(null, new object[] { this });
            }
            else
            {
                Debug.LogWarning("[LevelDefinition] LevelValidator no encontrado en el ensamblado Game.Editor.");
            }
        }

        // ---------------------------------------------------------------------------------
        // 1-CLICK PUZZLE LINKER
        // ---------------------------------------------------------------------------------

        [Group("Enlazador Rápido de Mecanismos")]
        [Button(ButtonSizes.Large, "Enlazar Mecanismo a Puerta")]
        public void LinkMechanismToDoor()
        {
            if (_sourceMechanism == null)
            {
                if (Selection.activeGameObject != null && Selection.activeGameObject != gameObject)
                {
                    _sourceMechanism = Selection.activeGameObject;
                }
            }

            if (_sourceMechanism == null)
            {
                Debug.LogWarning("[LevelDefinition] Asigna o selecciona el mecanismo de origen (Botón Diana, Palanca o Plataforma de Peso).");
                return;
            }

            if (_targetDoor == null)
            {
                Debug.LogWarning("[LevelDefinition] Asigna la puerta corrediza de destino.");
                return;
            }

            var mechanismComponent = _sourceMechanism.GetComponent<Game.Core.Interfaces.IInteractableMechanism>();
            if (mechanismComponent == null)
            {
                Debug.LogError($"[LevelDefinition] '{_sourceMechanism.name}' no implementa IInteractableMechanism (TargetButton, LeverMechanism, WeightPlatform, etc.).");
                return;
            }

            var trigger = _targetDoor.GetComponent<DoorMechanismTrigger>();
            if (trigger == null)
            {
                trigger = _targetDoor.gameObject.AddComponent<DoorMechanismTrigger>();
                Undo.RegisterCreatedObjectUndo(trigger, "Add DoorMechanismTrigger");
            }

            Undo.RecordObject(trigger, "Configure DoorMechanismTrigger");
            trigger.Configure(_sourceMechanism.GetComponent<MonoBehaviour>(), _targetDoor, autoClose: true);
            EditorUtility.SetDirty(trigger);
            EditorUtility.SetDirty(_targetDoor);
            Debug.Log($"<color=#50fa7b><b>[ENLACE OK]</b></color> '{_sourceMechanism.name}' vinculado con éxito a puerta '{_targetDoor.name}'.");
        }

        // ---------------------------------------------------------------------------------
        // GENERADOR DE PISTAS MAGNÉTICAS
        // ---------------------------------------------------------------------------------

        [Group("Generador de Pistas Magnéticas")]
        [Button("Auto-Detectar Dimensiones de Prefab")]
        public void AutoDetectMagneticDimensions()
        {
            var targetPrefab = _customMagneticWallPrefab != null
                ? _customMagneticWallPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMagneticWallPath);

            if (targetPrefab == null)
            {
                Debug.LogWarning("[LevelDefinition] No hay prefab magnético asignado para detectar dimensiones.");
                return;
            }

            var col = targetPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _magneticPanelLength = Mathf.Max(col.size.x, col.size.z);
                _magneticPanelHeight = col.size.y;
                if (_magneticPanelLength < 0.1f) _magneticPanelLength = 8.0f;
                if (_magneticPanelHeight < 0.1f) _magneticPanelHeight = 8.0f;
                Debug.Log($"[LevelDefinition] Dimensiones detectadas de BoxCollider de '{targetPrefab.name}': Longitud={_magneticPanelLength:F2}m, Altura={_magneticPanelHeight:F2}m.");
                EditorUtility.SetDirty(this);
                return;
            }

            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _magneticPanelLength = Mathf.Max(size.x, size.z);
                _magneticPanelHeight = size.y;
                if (_magneticPanelLength < 0.1f) _magneticPanelLength = 8.0f;
                if (_magneticPanelHeight < 0.1f) _magneticPanelHeight = 8.0f;
                Debug.Log($"[LevelDefinition] Dimensiones detectadas de Mesh de '{targetPrefab.name}': Longitud={_magneticPanelLength:F2}m, Altura={_magneticPanelHeight:F2}m.");
                EditorUtility.SetDirty(this);
            }
        }

        [Group("Generador de Pistas Magnéticas")]
        [Button(ButtonSizes.Large, "Generar Pista Magnetica")]
        public void GenerateMagneticTrack()
        {
            var targetPrefab = _customMagneticWallPrefab != null
                ? _customMagneticWallPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMagneticWallPath);

            if (targetPrefab == null)
            {
                Debug.LogError("[LevelDefinition] No se pudo encontrar un prefab magnético válido para instanciar.");
                return;
            }

            Vector3 origin = GetPlacementOrigin();
            GameObject trackRoot = new GameObject($"Track_Magnetic_{_magneticTrackDirection}");
            trackRoot.transform.position = origin;
            trackRoot.transform.SetParent(transform);

            int count = Mathf.Max(1, _magneticPanelCount);
            float stepL = _magneticPanelLength > 0.1f ? _magneticPanelLength : 8.0f;
            float stepH = _magneticPanelHeight > 0.1f ? _magneticPanelHeight : 8.0f;
            float baseAngle = _magneticRotationOffset + (_magneticFlipFacing ? 180f : 0f);

            float baseY = origin.y;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = origin;
                Quaternion rot = Quaternion.identity;

                switch (_magneticTrackDirection)
                {
                    case MagneticTrackDirection.VerticalClimb:
                        pos = new Vector3(origin.x, baseY + (i + 0.5f) * stepH, origin.z);
                        rot = Quaternion.Euler(0f, baseAngle, 0f);
                        break;

                    case MagneticTrackDirection.HorizontalTraverse:
                        float halfH = stepH * 0.5f;
                        if (_magneticTrackAxis == StraightWallDirection.EastWest_X)
                        {
                            float startX = _magneticCenterOnSelection ? (origin.x - count * stepL * 0.5f) : origin.x;
                            pos = new Vector3(startX + (i + 0.5f) * stepL, baseY + halfH, origin.z);
                            rot = Quaternion.Euler(0f, 0f + baseAngle, 0f);
                        }
                        else
                        {
                            float startZ = _magneticCenterOnSelection ? (origin.z - count * stepL * 0.5f) : origin.z;
                            pos = new Vector3(origin.x, baseY + halfH, startZ + (i + 0.5f) * stepL);
                            rot = Quaternion.Euler(0f, 90f + baseAngle, 0f);
                        }
                        break;

                    case MagneticTrackDirection.Ceiling:
                        float ceilingY = baseY + (_magneticCeilingHeight > 0.5f ? _magneticCeilingHeight : 6.0f);
                        if (_magneticTrackAxis == StraightWallDirection.EastWest_X)
                        {
                            float startX = _magneticCenterOnSelection ? (origin.x - count * stepL * 0.5f) : origin.x;
                            pos = new Vector3(startX + (i + 0.5f) * stepL, ceilingY, origin.z);
                            rot = Quaternion.Euler(180f, 0f + baseAngle, 0f);
                        }
                        else
                        {
                            float startZ = _magneticCenterOnSelection ? (origin.z - count * stepL * 0.5f) : origin.z;
                            pos = new Vector3(origin.x, ceilingY, startZ + (i + 0.5f) * stepL);
                            rot = Quaternion.Euler(180f, 90f + baseAngle, 0f);
                        }
                        break;
                }

                SpawnWallPiece(targetPrefab, pos, rot, trackRoot.transform);
            }

            _lastGeneratedMagneticTrack = trackRoot;
            Undo.RegisterCreatedObjectUndo(trackRoot, "Generate Magnetic Track");
            Selection.activeGameObject = trackRoot;
            Debug.Log($"[LevelDefinition] Pista magnética '{_magneticTrackDirection}' creada con {count} paneles.");
        }

        private void SafeDestroyObject(GameObject target)
        {
            if (target == null) return;

            if (Selection.activeGameObject != null &&
                (Selection.activeGameObject == target || Selection.activeGameObject.transform.IsChildOf(target.transform)))
            {
                Selection.activeGameObject = gameObject;
            }

            var currentObjects = Selection.objects;
            if (currentObjects != null && currentObjects.Length > 0)
            {
                bool containsTarget = false;
                for (int i = 0; i < currentObjects.Length; i++)
                {
                    if (currentObjects[i] is GameObject go && (go == target || (go != null && go.transform.IsChildOf(target.transform))))
                    {
                        containsTarget = true;
                        break;
                    }
                }
                if (containsTarget)
                {
                    Selection.objects = new UnityEngine.Object[] { gameObject };
                    ActiveEditorTracker.sharedTracker?.ForceRebuild();
                }
            }

            Undo.DestroyObjectImmediate(target);
        }

        [Group("Generador de Pistas Magnéticas")]
        [Button("Eliminar Ultima Pista Generada")]
        public void DeleteLastGeneratedMagneticTrack()
        {
            if (_lastGeneratedMagneticTrack != null)
            {
                string name = _lastGeneratedMagneticTrack.name;
                SafeDestroyObject(_lastGeneratedMagneticTrack);
                _lastGeneratedMagneticTrack = null;
                Debug.Log($"[LevelDefinition] Se eliminó la pista '{name}'.");
            }
            else
            {
                Debug.LogWarning("[LevelDefinition] No hay pista registrada para eliminar.");
            }
        }

        public void ClearGeneratedMagneticTracks() => DeleteLastGeneratedMagneticTrack();

        // ---------------------------------------------------------------------------------
        // GENERADOR DE PLATAFORMAS FLOTANTES Y PUENTES
        // ---------------------------------------------------------------------------------

        [Group("Generador de Plataformas y Puentes")]
        [Button(ButtonSizes.Large, "Generar Camino de Plataformas")]
        public void GeneratePlatformPath()
        {
            var prefab = _customPlatformPrefab != null
                ? _customPlatformPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");

            if (prefab == null)
            {
                Debug.LogError("[LevelDefinition] No se encontró prefab para plataformas.");
                return;
            }

            Vector3 origin = GetPlacementOrigin();
            GameObject pathRoot = new GameObject("Path_Platforms");
            pathRoot.transform.position = origin;
            pathRoot.transform.SetParent(transform);

            int count = Mathf.Max(1, _platformCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = origin + new Vector3(i * _platformGap, i * _stepHeightDelta, 0f);
                SpawnWallPiece(prefab, pos, Quaternion.identity, pathRoot.transform);
            }

            Undo.RegisterCreatedObjectUndo(pathRoot, "Generate Platform Path");
            Selection.activeGameObject = pathRoot;
            Debug.Log($"[LevelDefinition] Camino de {count} plataformas generado con brecha de {_platformGap:F2}m.");
        }

        // ---------------------------------------------------------------------------------
        // CALIBRADOR DE BALANCÍN CATAPULTA
        // ---------------------------------------------------------------------------------

        [Group("Calibrador de Balancín Catapulta")]
        [Button(ButtonSizes.Large, "Generar Setup Balancin + Saliente Objetivo")]
        public void GenerateSeeSawSetup()
        {
            var seeSawPrefab = _customSeeSawPrefab != null
                ? _customSeeSawPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultSeeSawPath);

            var ledgePrefab = _customLedgePrefab != null
                ? _customLedgePrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");

            if (seeSawPrefab == null)
            {
                Debug.LogError("[LevelDefinition] Prefab SeeSawPlatform no encontrado.");
                return;
            }

            Vector3 origin = GetPlacementOrigin();
            GameObject setupRoot = new GameObject("Setup_SeeSaw_Catapult");
            setupRoot.transform.position = origin;
            setupRoot.transform.SetParent(transform);

            var seeSaw = (GameObject)PrefabUtility.InstantiatePrefab(seeSawPrefab);
            seeSaw.transform.position = origin + new Vector3(0f, 0.5f, 0f);
            seeSaw.transform.SetParent(setupRoot.transform);

            if (ledgePrefab != null)
            {
                var ledge = (GameObject)PrefabUtility.InstantiatePrefab(ledgePrefab);
                ledge.name = "TargetLedge_Catapult";
                ledge.transform.position = origin + new Vector3(4.5f, _catapultTargetLedgeHeight, 0f);
                ledge.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                ledge.transform.SetParent(setupRoot.transform);
            }

            _lastGeneratedSeeSaw = setupRoot;
            Undo.RegisterCreatedObjectUndo(setupRoot, "Generate SeeSaw Setup");
            Selection.activeGameObject = setupRoot;
            Debug.Log($"[LevelDefinition] Setup Balancín generado con saliente a {_catapultTargetLedgeHeight}m.");
        }

        [Group("Calibrador de Balancín Catapulta")]
        [Button("Eliminar Ultimo Setup de Balancin")]
        public void DeleteLastGeneratedSeeSaw()
        {
            if (_lastGeneratedSeeSaw != null)
            {
                string name = _lastGeneratedSeeSaw.name;
                SafeDestroyObject(_lastGeneratedSeeSaw);
                _lastGeneratedSeeSaw = null;
                Debug.Log($"[LevelDefinition] Se eliminó el setup '{name}'.");
            }
            else
            {
                Debug.LogWarning("[LevelDefinition] No hay setup de balancín registrado para eliminar.");
            }
        }

        public void ClearGeneratedSeeSaws() => DeleteLastGeneratedSeeSaw();

        // ---------------------------------------------------------------------------------
        // GENERADOR DE MUROS
        // ---------------------------------------------------------------------------------

        [Group("Generador de Secciones de Muros")]
        [Button("Auto-Detectar Dimensiones de Prefab")]
        public void AutoDetectWallSize()
        {
            var targetPrefab = _customWallPrefab != null ? _customWallPrefab : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            if (targetPrefab == null)
            {
                Debug.LogWarning("[LevelDefinition] No hay prefab asignado para detectar dimensiones.");
                return;
            }

            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _moduleLength = Mathf.Max(size.x, size.z);
                _moduleHeight = size.y;
                if (_moduleLength < 0.1f) _moduleLength = 2.0f;
                if (_moduleHeight < 0.1f) _moduleHeight = 3.0f;
                Debug.Log($"[LevelDefinition] Dimensiones detectadas desde Mesh de '{targetPrefab.name}': Longitud={_moduleLength:F2}m, Altura={_moduleHeight:F2}m (Eje natural: {(size.x >= size.z ? "X / Este-Oeste" : "Z / Norte-Sur")}).");
                return;
            }

            var col = targetPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _moduleLength = Mathf.Max(col.size.x, col.size.z);
                _moduleHeight = col.size.y;
                Debug.Log($"[LevelDefinition] Dimensiones detectadas desde BoxCollider de '{targetPrefab.name}': Longitud={_moduleLength:F2}m, Altura={_moduleHeight:F2}m.");
                return;
            }

            var renderer = targetPrefab.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 boundsSize = renderer.bounds.size;
                _moduleLength = Mathf.Max(boundsSize.x, boundsSize.z);
                _moduleHeight = boundsSize.y;
                if (_moduleLength < 0.1f) _moduleLength = 2.0f;
                if (_moduleHeight < 0.1f) _moduleHeight = 3.0f;
                Debug.Log($"[LevelDefinition] Dimensiones detectadas de '{targetPrefab.name}': Longitud={_moduleLength:F2}m, Altura={_moduleHeight:F2}m.");
            }
        }

        [Group("Generador de Secciones de Muros")]
        [Button(ButtonSizes.Large, "Generar Seccion de Muros")]
        public void GenerateWallSection()
        {
            var targetPrefab = _customWallPrefab != null ? _customWallPrefab : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            if (targetPrefab == null)
            {
                Debug.LogError("[LevelDefinition] No se pudo encontrar un prefab de muro válido para instanciar.");
                return;
            }

            // 1. Detectar si la longitud natural del modelo es X o Z
            bool modelAlongX = true;
            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                modelAlongX = mf.sharedMesh.bounds.size.x >= mf.sharedMesh.bounds.size.z;
            }
            else
            {
                var col = targetPrefab.GetComponentInChildren<BoxCollider>();
                if (col != null)
                {
                    modelAlongX = col.size.x >= col.size.z;
                }
                else
                {
                    var renderer = targetPrefab.GetComponentInChildren<Renderer>();
                    if (renderer != null)
                    {
                        modelAlongX = renderer.bounds.size.x >= renderer.bounds.size.z;
                    }
                }
            }

            // 2. Rotaciones calculadas para cada eje considerando si las caras miran hacia afuera o adentro
            float rotOffset = _rotationOffset + (_flipFacing ? 180f : 0f);
            float axisCorrection = modelAlongX ? 0f : 90f;
            float faceDirAngle = _faceOutward ? 0f : 180f;

            Quaternion rotSouth = Quaternion.Euler(0f, 0f + faceDirAngle + axisCorrection + rotOffset, 0f);
            Quaternion rotNorth = Quaternion.Euler(0f, 180f + faceDirAngle + axisCorrection + rotOffset, 0f);
            Quaternion rotWest = Quaternion.Euler(0f, 90f + faceDirAngle + axisCorrection + rotOffset, 0f);
            Quaternion rotEast = Quaternion.Euler(0f, 270f + faceDirAngle + axisCorrection + rotOffset, 0f);

            Vector3 originPos = GetPlacementOrigin();

            GameObject sectionRoot = new GameObject($"Section_Walls_{_wallSectionShape}");
            sectionRoot.transform.position = originPos;
            sectionRoot.transform.SetParent(transform);

            int L = Mathf.Max(1, _lengthModules);
            int W = Mathf.Max(1, _widthModules);
            float unitLen = _moduleLength > 0.05f ? _moduleLength : 2.0f;
            float unitH = _moduleHeight > 0.05f ? _moduleHeight : 3.0f;
            int tiers = Mathf.Max(1, _heightTiers);

            // Centrado en la selección / pivote
            Vector3 basePos = originPos;
            if (_centerOnSelection)
            {
                if (_wallSectionShape == WallSectionShape.StraightLine)
                {
                    if (_straightDirection == StraightWallDirection.NorthSouth_Z)
                    {
                        basePos -= new Vector3(0f, 0f, L * unitLen * 0.5f);
                    }
                    else
                    {
                        basePos -= new Vector3(L * unitLen * 0.5f, 0f, 0f);
                    }
                }
                else
                {
                    basePos -= new Vector3(W * unitLen * 0.5f, 0f, L * unitLen * 0.5f);
                }
            }

            for (int t = 0; t < tiers; t++)
            {
                float yOffset = t * unitH;

                switch (_wallSectionShape)
                {
                    case WallSectionShape.StraightLine:
                        if (_straightDirection == StraightWallDirection.NorthSouth_Z)
                        {
                            for (int i = 0; i < L; i++)
                            {
                                Vector3 pos = basePos + new Vector3(0f, yOffset, (i + 0.5f) * unitLen);
                                SpawnWallPiece(targetPrefab, pos, rotWest, sectionRoot.transform);
                            }
                        }
                        else
                        {
                            for (int i = 0; i < L; i++)
                            {
                                Vector3 pos = basePos + new Vector3((i + 0.5f) * unitLen, yOffset, 0f);
                                SpawnWallPiece(targetPrefab, pos, rotSouth, sectionRoot.transform);
                            }
                        }
                        break;

                    case WallSectionShape.LShape:
                        // Tramo 1: A lo largo de Z (Norte-Sur, en X = 0)
                        for (int i = 0; i < L; i++)
                        {
                            Vector3 pos = basePos + new Vector3(0f, yOffset, (i + 0.5f) * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotWest, sectionRoot.transform);
                        }
                        // Tramo 2: A lo largo de X (Este-Oeste, en Z = 0)
                        for (int j = 0; j < W; j++)
                        {
                            Vector3 pos = basePos + new Vector3((j + 0.5f) * unitLen, yOffset, 0f);
                            SpawnWallPiece(targetPrefab, pos, rotSouth, sectionRoot.transform);
                        }
                        break;

                    case WallSectionShape.UShape:
                        // Muro Oeste: a lo largo de Z (en X = 0)
                        for (int i = 0; i < L; i++)
                        {
                            Vector3 pos = basePos + new Vector3(0f, yOffset, (i + 0.5f) * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotWest, sectionRoot.transform);
                        }
                        // Muro Trasero / Norte: a lo largo de X (en Z = L * unitLen)
                        for (int j = 0; j < W; j++)
                        {
                            Vector3 pos = basePos + new Vector3((j + 0.5f) * unitLen, yOffset, L * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotNorth, sectionRoot.transform);
                        }
                        // Muro Este: a lo largo de Z (en X = W * unitLen)
                        for (int i = 0; i < L; i++)
                        {
                            Vector3 pos = basePos + new Vector3(W * unitLen, yOffset, (i + 0.5f) * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotEast, sectionRoot.transform);
                        }
                        break;

                    case WallSectionShape.RectanglePerimeter:
                        // Muro Sur: a lo largo de X (Z = 0)
                        for (int j = 0; j < W; j++)
                        {
                            Vector3 pos = basePos + new Vector3((j + 0.5f) * unitLen, yOffset, 0f);
                            SpawnWallPiece(targetPrefab, pos, rotSouth, sectionRoot.transform);
                        }

                        // Muro Norte: a lo largo de X (Z = L * unitLen)
                        for (int j = 0; j < W; j++)
                        {
                            Vector3 pos = basePos + new Vector3((j + 0.5f) * unitLen, yOffset, L * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotNorth, sectionRoot.transform);
                        }

                        // Muro Oeste: a lo largo de Z (X = 0)
                        for (int i = 0; i < L; i++)
                        {
                            Vector3 pos = basePos + new Vector3(0f, yOffset, (i + 0.5f) * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotWest, sectionRoot.transform);
                        }

                        // Muro Este: a lo largo de Z (X = W * unitLen)
                        for (int i = 0; i < L; i++)
                        {
                            Vector3 pos = basePos + new Vector3(W * unitLen, yOffset, (i + 0.5f) * unitLen);
                            SpawnWallPiece(targetPrefab, pos, rotEast, sectionRoot.transform);
                        }
                        break;
                }
            }

            if (_generateFloorWithWalls && _wallSectionShape != WallSectionShape.StraightLine)
            {
                GenerateFloorInsideSection(sectionRoot.transform, basePos, (int)W, (int)L, unitLen);
            }

            _lastGeneratedSection = sectionRoot;
            Undo.RegisterCreatedObjectUndo(sectionRoot, "Generate Wall Section");
            Selection.activeGameObject = sectionRoot;
            Debug.Log($"[LevelDefinition] Sección de muros '{_wallSectionShape}' generada con {sectionRoot.transform.childCount} piezas.");
        }

        [Group("Generador de Secciones de Muros")]
        [Button("Eliminar Ultima Seccion Generada")]
        public void DeleteLastGeneratedSection()
        {
            if (_lastGeneratedSection != null)
            {
                string name = _lastGeneratedSection.name;
                SafeDestroyObject(_lastGeneratedSection);
                _lastGeneratedSection = null;
                Debug.Log($"[LevelDefinition] Se eliminó la sección '{name}'.");
            }
            else
            {
                Debug.LogWarning("[LevelDefinition] No hay sección registrada para eliminar.");
            }
        }

        public void ClearGeneratedWalls() => DeleteLastGeneratedSection();

        // ---------------------------------------------------------------------------------
        // GENERADOR DE PISOS Y PLATAFORMAS
        // ---------------------------------------------------------------------------------

        [Group("Generador de Pisos y Plataformas")]
        [Button("Auto-Detectar Dimensiones de Baldosa")]
        public void AutoDetectFloorSize()
        {
            var targetPrefab = _customFloorPrefab != null 
                ? _customFloorPrefab 
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFloorPrefabPath);

            if (targetPrefab == null)
            {
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }

            if (targetPrefab == null)
            {
                Debug.LogWarning("[LevelDefinition] No hay prefab de piso para detectar dimensiones.");
                return;
            }

            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _floorModuleLengthX = size.x > 0.05f ? size.x : 4.0f;
                _floorModuleLengthZ = size.z > 0.05f ? size.z : 4.0f;
                Debug.Log($"[LevelDefinition] Dimensiones de baldosa detectadas desde Mesh de '{targetPrefab.name}': X={_floorModuleLengthX:F2}m, Z={_floorModuleLengthZ:F2}m.");
                EditorUtility.SetDirty(this);
                return;
            }

            var col = targetPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _floorModuleLengthX = col.size.x > 0.05f ? col.size.x : 4.0f;
                _floorModuleLengthZ = col.size.z > 0.05f ? col.size.z : 4.0f;
                Debug.Log($"[LevelDefinition] Dimensiones de baldosa detectadas desde BoxCollider de '{targetPrefab.name}': X={_floorModuleLengthX:F2}m, Z={_floorModuleLengthZ:F2}m.");
                EditorUtility.SetDirty(this);
                return;
            }

            var renderer = targetPrefab.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 boundsSize = renderer.bounds.size;
                _floorModuleLengthX = boundsSize.x > 0.05f ? boundsSize.x : 4.0f;
                _floorModuleLengthZ = boundsSize.z > 0.05f ? boundsSize.z : 4.0f;
                Debug.Log($"[LevelDefinition] Dimensiones de baldosa detectadas desde Renderer de '{targetPrefab.name}': X={_floorModuleLengthX:F2}m, Z={_floorModuleLengthZ:F2}m.");
                EditorUtility.SetDirty(this);
            }
        }

        [Group("Generador de Pisos y Plataformas")]
        [Button(ButtonSizes.Large, "Generar Superficie de Piso")]
        public void GenerateFloorSection()
        {
            var targetPrefab = _customFloorPrefab != null 
                ? _customFloorPrefab 
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFloorPrefabPath);

            if (targetPrefab == null)
            {
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }

            if (targetPrefab == null)
            {
                Debug.LogError("[LevelDefinition] No se pudo encontrar un prefab de piso válido para instanciar.");
                return;
            }

            Vector3 originPos = GetPlacementOrigin();
            GameObject floorRoot = new GameObject("Section_Floor_Grid");
            floorRoot.transform.position = originPos;
            floorRoot.transform.SetParent(transform);

            int W = Mathf.Max(1, _floorWidthModules);
            int L = Mathf.Max(1, _floorLengthModules);
            float tileX = _floorModuleLengthX > 0.05f ? _floorModuleLengthX : 4.0f;
            float tileZ = _floorModuleLengthZ > 0.05f ? _floorModuleLengthZ : 4.0f;

            float prefabSizeX = 4.0f;
            float prefabSizeZ = 4.0f;
            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                prefabSizeX = mf.sharedMesh.bounds.size.x;
                prefabSizeZ = mf.sharedMesh.bounds.size.z;
            }
            else
            {
                var col = targetPrefab.GetComponentInChildren<BoxCollider>();
                if (col != null)
                {
                    prefabSizeX = col.size.x;
                    prefabSizeZ = col.size.z;
                }
            }
            if (prefabSizeX < 0.05f) prefabSizeX = 4.0f;
            if (prefabSizeZ < 0.05f) prefabSizeZ = 4.0f;

            Vector3 basePos = originPos;
            if (_floorCenterOnSelection)
            {
                basePos -= new Vector3(W * tileX * 0.5f, 0f, L * tileZ * 0.5f);
            }

            int voidW = Mathf.Clamp(_voidHoleWidth, 1, Mathf.Max(1, W - 1));
            int voidL = Mathf.Clamp(_voidHoleLength, 1, Mathf.Max(1, L - 1));
            int startCol = (W - voidW) / 2;
            int endCol = startCol + voidW;
            int startRow = (L - voidL) / 2;
            int endRow = startRow + voidL;

            for (int i = 0; i < L; i++)
            {
                for (int j = 0; j < W; j++)
                {
                    if (_createFloorVoid && j >= startCol && j < endCol && i >= startRow && i < endRow)
                    {
                        continue;
                    }

                    Vector3 pos = basePos + new Vector3((j + 0.5f) * tileX, _floorYOffset, (i + 0.5f) * tileZ);
                    SpawnFloorPiece(targetPrefab, pos, Quaternion.identity, floorRoot.transform, tileX, tileZ, prefabSizeX, prefabSizeZ, !_generateFloorBoxCollider);
                }
            }

            if (_generateFloorBoxCollider)
            {
                ApplyUnifiedFloorBoxColliders(floorRoot, basePos, W, L, tileX, tileZ, _floorYOffset, _createFloorVoid, startCol, endCol, startRow, endRow);
            }

            _lastGeneratedFloor = floorRoot;
            Undo.RegisterCreatedObjectUndo(floorRoot, "Generate Floor Section");
            Selection.activeGameObject = floorRoot;
            Debug.Log($"[LevelDefinition] Superficie de piso generada con {floorRoot.transform.childCount} baldosas (BoxCollider unificado: {_generateFloorBoxCollider}).");
        }

        [Group("Generador de Pisos y Plataformas")]
        [Button("Ajustar BoxCollider a Pisos Existentes en Escena")]
        public void FitExistingFloorCollidersButton()
        {
            AutoFitFloorCollidersInScene();
        }

        public static int AutoFitFloorCollidersInScene()
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
            int updatedCount = 0;

            for (int i = 0; i < allTransforms.Length; i++)
            {
                Transform t = allTransforms[i];
                if (t == null) continue;

                if (t.name.StartsWith("Section_Floor_Grid", System.StringComparison.Ordinal) ||
                    t.name.StartsWith("Floor_Base", System.StringComparison.Ordinal))
                {
                    if (FitUnifiedBoxColliderToFloorContainer(t.gameObject))
                    {
                        updatedCount++;
                    }
                }
            }

            if (updatedCount > 0)
            {
                Debug.Log($"[LevelDefinition] BoxCollider unificado ajustado automáticamente en {updatedCount} contenedor(es) de piso.");
            }
            return updatedCount;
        }

        public static bool FitUnifiedBoxColliderToFloorContainer(GameObject floorRoot)
        {
            if (floorRoot == null) return false;

            var renderers = floorRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0) return false;

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    worldBounds.Encapsulate(renderers[i].bounds);
                }
            }

            // Disable per-tile colliders inside children to prevent internal PhysX edge snagging
            var childColliders = floorRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null && childColliders[i].gameObject != floorRoot)
                {
                    childColliders[i].enabled = false;
                }
            }

            var boxCol = floorRoot.GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = Undo.AddComponent<BoxCollider>(floorRoot);
            }
            else
            {
                Undo.RecordObject(boxCol, "Fit Floor BoxCollider");
            }

            Vector3 localCenter = floorRoot.transform.InverseTransformPoint(worldBounds.center);
            Vector3 lossy = floorRoot.transform.lossyScale;
            float sx = Mathf.Abs(lossy.x) > 0.0001f ? worldBounds.size.x / Mathf.Abs(lossy.x) : worldBounds.size.x;
            float sy = Mathf.Abs(lossy.y) > 0.0001f ? worldBounds.size.y / Mathf.Abs(lossy.y) : worldBounds.size.y;
            float sz = Mathf.Abs(lossy.z) > 0.0001f ? worldBounds.size.z / Mathf.Abs(lossy.z) : worldBounds.size.z;

            float topLocalY = localCenter.y + (sy * 0.5f);
            float thickness = Mathf.Max(0.25f, sy);

            boxCol.isTrigger = false;
            boxCol.size = new Vector3(Mathf.Max(0.1f, sx), thickness, Mathf.Max(0.1f, sz));
            boxCol.center = new Vector3(localCenter.x, topLocalY - (thickness * 0.5f), localCenter.z);

            EditorUtility.SetDirty(boxCol);
            EditorUtility.SetDirty(floorRoot);
            return true;
        }

        private static void ApplyUnifiedFloorBoxColliders(
            GameObject floorRoot,
            Vector3 basePos,
            int W,
            int L,
            float tileX,
            float tileZ,
            float floorYOffset,
            bool createVoid,
            int startCol,
            int endCol,
            int startRow,
            int endRow)
        {
            if (floorRoot == null) return;

            if (!createVoid)
            {
                if (FitUnifiedBoxColliderToFloorContainer(floorRoot))
                {
                    return;
                }
            }

            // Disable per-tile colliders inside children to eliminate internal seam snagging
            var childColliders = floorRoot.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null && childColliders[i].gameObject != floorRoot)
                {
                    childColliders[i].enabled = false;
                }
            }

            float topWorldY = floorRoot.transform.position.y + floorYOffset;
            var renderers = floorRoot.GetComponentsInChildren<Renderer>(includeInactive: false);
            if (renderers != null && renderers.Length > 0)
            {
                topWorldY = renderers[0].bounds.max.y;
                for (int i = 1; i < renderers.Length; i++)
                {
                    if (renderers[i] != null && renderers[i].bounds.max.y > topWorldY)
                    {
                        topWorldY = renderers[i].bounds.max.y;
                    }
                }
            }

            const float thickness = 0.25f;
            float localTopY = topWorldY - floorRoot.transform.position.y;
            float localCenterY = localTopY - (thickness * 0.5f);
            Vector3 localBase = basePos - floorRoot.transform.position;

            if (!createVoid)
            {
                var box = floorRoot.GetComponent<BoxCollider>();
                if (box == null) box = floorRoot.AddComponent<BoxCollider>();
                box.size = new Vector3(W * tileX, thickness, L * tileZ);
                box.center = new Vector3(localBase.x + (W * tileX * 0.5f), localCenterY, localBase.z + (L * tileZ * 0.5f));
                return;
            }

            // 4 perimeter slabs around the rectangular void hole
            if (startRow > 0)
            {
                var southBox = floorRoot.AddComponent<BoxCollider>();
                southBox.size = new Vector3(W * tileX, thickness, startRow * tileZ);
                southBox.center = new Vector3(localBase.x + (W * tileX * 0.5f), localCenterY, localBase.z + (startRow * tileZ * 0.5f));
            }
            if (endRow < L)
            {
                int northRows = L - endRow;
                var northBox = floorRoot.AddComponent<BoxCollider>();
                northBox.size = new Vector3(W * tileX, thickness, northRows * tileZ);
                northBox.center = new Vector3(localBase.x + (W * tileX * 0.5f), localCenterY, localBase.z + ((endRow + northRows * 0.5f) * tileZ));
            }
            int midRows = endRow - startRow;
            if (midRows > 0 && startCol > 0)
            {
                var westBox = floorRoot.AddComponent<BoxCollider>();
                westBox.size = new Vector3(startCol * tileX, thickness, midRows * tileZ);
                westBox.center = new Vector3(localBase.x + (startCol * tileX * 0.5f), localCenterY, localBase.z + ((startRow + midRows * 0.5f) * tileZ));
            }
            if (midRows > 0 && endCol < W)
            {
                int eastCols = W - endCol;
                var eastBox = floorRoot.AddComponent<BoxCollider>();
                eastBox.size = new Vector3(eastCols * tileX, thickness, midRows * tileZ);
                eastBox.center = new Vector3(localBase.x + ((endCol + eastCols * 0.5f) * tileX), localCenterY, localBase.z + ((startRow + midRows * 0.5f) * tileZ));
            }
        }

        [Group("Generador de Pisos y Plataformas")]
        [Button("Eliminar Ultimo Piso Generado")]
        public void DeleteLastGeneratedFloor()
        {
            if (_lastGeneratedFloor != null)
            {
                string name = _lastGeneratedFloor.name;
                SafeDestroyObject(_lastGeneratedFloor);
                _lastGeneratedFloor = null;
                Debug.Log($"[LevelDefinition] Se eliminó el piso '{name}'.");
            }
            else
            {
                Debug.LogWarning("[LevelDefinition] No hay piso registrado para eliminar.");
            }
        }

        public void ClearGeneratedFloors() => DeleteLastGeneratedFloor();

        [Group("Generador de Pisos y Plataformas")]
        [Button("Eliminar Baldosas Seleccionadas en Escena")]
        public void DeleteSelectedTiles()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("[LevelDefinition] No hay baldosas seleccionadas en la escena.");
                return;
            }

            // Deselect selected tiles BEFORE destroying them to prevent Inspector null target crash
            Selection.objects = new UnityEngine.Object[] { gameObject };

            int count = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                var go = selected[i];
                if (go != null && go.transform.IsChildOf(transform))
                {
                    SafeDestroyObject(go);
                    count++;
                }
            }
            Debug.Log($"[LevelDefinition] Se eliminaron {count} baldosas seleccionadas.");
        }

        private void GenerateFloorInsideSection(Transform parent, Vector3 basePos, int widthModules, int lengthModules, float wallUnitLen)
        {
            var floorPrefab = _customFloorPrefab != null 
                ? _customFloorPrefab 
                : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultFloorPrefabPath);

            if (floorPrefab == null)
            {
                floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }
            if (floorPrefab == null) return;

            GameObject floorContainer = new GameObject("Floor_Base");
            floorContainer.transform.position = basePos;
            floorContainer.transform.SetParent(parent);

            float tileX = _floorModuleLengthX > 0.05f ? _floorModuleLengthX : 1.0f;
            float tileZ = _floorModuleLengthZ > 0.05f ? _floorModuleLengthZ : 1.0f;

            float prefabSizeX = 4.0f;
            float prefabSizeZ = 4.0f;
            var mf = floorPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                prefabSizeX = mf.sharedMesh.bounds.size.x;
                prefabSizeZ = mf.sharedMesh.bounds.size.z;
            }
            else
            {
                var col = floorPrefab.GetComponentInChildren<BoxCollider>();
                if (col != null)
                {
                    prefabSizeX = col.size.x;
                    prefabSizeZ = col.size.z;
                }
            }
            if (prefabSizeX < 0.05f) prefabSizeX = 4.0f;
            if (prefabSizeZ < 0.05f) prefabSizeZ = 4.0f;

            int countX = Mathf.Max(1, Mathf.RoundToInt((widthModules * wallUnitLen) / tileX));
            int countZ = Mathf.Max(1, Mathf.RoundToInt((lengthModules * wallUnitLen) / tileZ));

            int voidW = Mathf.Clamp(_voidHoleWidth, 1, Mathf.Max(1, countX - 1));
            int voidL = Mathf.Clamp(_voidHoleLength, 1, Mathf.Max(1, countZ - 1));
            int startCol = (countX - voidW) / 2;
            int endCol = startCol + voidW;
            int startRow = (countZ - voidL) / 2;
            int endRow = startRow + voidL;

            for (int i = 0; i < countZ; i++)
            {
                for (int j = 0; j < countX; j++)
                {
                    if (_createFloorVoid && j >= startCol && j < endCol && i >= startRow && i < endRow)
                    {
                        continue;
                    }

                    Vector3 pos = basePos + new Vector3((j + 0.5f) * tileX, _floorYOffset, (i + 0.5f) * tileZ);
                    SpawnFloorPiece(floorPrefab, pos, Quaternion.identity, floorContainer.transform, tileX, tileZ, prefabSizeX, prefabSizeZ, !_generateFloorBoxCollider);
                }
            }

            if (_generateFloorBoxCollider)
            {
                ApplyUnifiedFloorBoxColliders(floorContainer, basePos, countX, countZ, tileX, tileZ, _floorYOffset, _createFloorVoid, startCol, endCol, startRow, endRow);
            }
        }

        [Group("Configuración de Snapping")]
        [Button("Alinear Objetos Seleccionados a la Grilla")]
        public void SnapSelectedObjects()
        {
            if (!_enableGridSnapping || _gridSize <= 0.01f) return;

            var transforms = Selection.transforms;
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                Undo.RecordObject(t, "Snap to Grid");
                Vector3 p = t.position;
                p.x = Mathf.Round(p.x / _gridSize) * _gridSize;
                p.y = Mathf.Round(p.y / _gridSize) * _gridSize;
                p.z = Mathf.Round(p.z / _gridSize) * _gridSize;
                t.position = p;
            }
        }

        // ---------------------------------------------------------------------------------
        // PALETA DIRECTA EN INSPECTOR
        // ---------------------------------------------------------------------------------

        [Group("Paleta - Arquitectura")]
        [Button("Muro")]
        private void SpawnWall() => SpawnPrefab($"{PrefabsPath}/Wall.prefab");

        [Group("Paleta - Arquitectura")]
        [Button("Piso / Baldosa")]
        private void SpawnFloorTile() => SpawnPrefab(DefaultFloorPrefabPath);

        [Group("Paleta - Arquitectura")]
        [Button("Muro Rompible")]
        private void SpawnBreakableWall() => SpawnPrefab($"{PrefabsPath}/BreakableWall.prefab");

        [Group("Paleta - Arquitectura")]
        [Button("Muro Magnético")]
        private void SpawnMagneticWall() => SpawnPrefab($"{PrefabsPath}/MagneticWall.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Interruptor Palanca")]
        private void SpawnLever() => SpawnPrefab($"{PrefabsPath}/LeverSwitch.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Botón Diana")]
        private void SpawnButton() => SpawnPrefab($"{PrefabsPath}/TargetButton.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Plataforma de Peso")]
        private void SpawnWeightPlatform() => SpawnPrefab($"{PrefabsPath}/WeightPlatform.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Puerta Corrediza")]
        private void SpawnDoor() => SpawnPrefab($"{PrefabsPath}/SlidingDoor.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Socket de Puzzle")]
        private void SpawnSocket() => SpawnPrefab($"{PrefabsPath}/PuzzleSocket.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Caja Agarrable")]
        private void SpawnGrabbableBox() => SpawnPrefab($"{PrefabsPath}/GrabbableBox.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Llave Magnética")]
        private void SpawnMagneticKey() => SpawnPrefab($"{PrefabsPath}/MagneticKey.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Caja Empujable")]
        private void SpawnPushableBox() => SpawnPrefab($"{PrefabsPath}/PushableBox.prefab");

        [Group("Paleta - Mecanismos e Interactivos")]
        [Button("Obstáculo Pateable")]
        private void SpawnKickableObstacle() => SpawnPrefab($"{PrefabsPath}/KickableObstacle.prefab");

        [Group("Paleta - Peligros y Dinámicas")]
        [Button("KillVolume")]
        private void SpawnKillVolume() => SpawnPrefab($"{PrefabsPath}/KillVolume.prefab");

        [Group("Paleta - Peligros y Dinámicas")]
        [Button("Plataforma Balancín (SeeSaw)")]
        private void SpawnSeeSaw() => SpawnPrefab($"{PrefabsPath}/SeeSawPlatform.prefab");

        [Group("Paleta - Checkpoints")]
        [Button(ButtonSizes.Large, "Spawn Checkpoint Asimétrico")]
        private void SpawnCheckpoint() => SpawnPrefab($"{PrefabsPath}/Checkpoint.prefab");

        private void SpawnPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[LevelDefinition] Prefab no encontrado en {path}");
                return;
            }

            Vector3 spawnPos = GetPlacementOrigin();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = spawnPos;
            instance.transform.SetParent(transform);

            Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
            Selection.activeGameObject = instance;
        }

        private static void SpawnWallPiece(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (piece == null) return;
            piece.transform.position = position;
            piece.transform.rotation = rotation;
        }

        private static void SpawnFloorPiece(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent,
            float tileX,
            float tileZ,
            float prefabSizeX,
            float prefabSizeZ,
            bool addTileColliderIfMissing = false)
        {
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            if (piece == null) return;
            piece.transform.position = position;
            piece.transform.rotation = rotation;

            float scaleX = prefabSizeX > 0.01f ? (tileX / prefabSizeX) : 1.0f;
            float scaleZ = prefabSizeZ > 0.01f ? (tileZ / prefabSizeZ) : 1.0f;
            piece.transform.localScale = new Vector3(scaleX, 1.0f, scaleZ);

            if (addTileColliderIfMissing && piece.GetComponentInChildren<Collider>() == null)
            {
                var box = piece.AddComponent<BoxCollider>();
                box.size = new Vector3(prefabSizeX, 0.2f, prefabSizeZ);
                box.center = new Vector3(0f, -0.1f, 0f);
            }
        }

        public Vector3 GetPlacementOrigin()
        {
            if (_spawnAtOriginZero)
            {
                return new Vector3(0f, _groundY, 0f);
            }

            Vector3 spawnPos = Vector3.zero;
            var sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null)
            {
                if (_alignToSurface)
                {
                    Ray ray = sceneView.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                    {
                        spawnPos = hit.point;
                    }
                    else
                    {
                        spawnPos = sceneView.pivot;
                    }
                }
                else
                {
                    spawnPos = sceneView.pivot;
                }
            }

            if (_enableGridSnapping && _gridSize > 0.01f)
            {
                spawnPos.x = Mathf.Round(spawnPos.x / _gridSize) * _gridSize;
                spawnPos.y = Mathf.Round(spawnPos.y / _gridSize) * _gridSize;
                spawnPos.z = Mathf.Round(spawnPos.z / _gridSize) * _gridSize;
            }

            return spawnPos;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_showMetricGizmos) return;

            Vector3 origin = transform.position;
            if (Selection.activeTransform != null && Selection.activeTransform != transform)
            {
                origin = Selection.activeTransform.position;
            }

            // 1. Walk Jump Parabola (Cyan)
            Gizmos.color = new Color(0.1f, 0.85f, 0.95f, 0.9f);
            DrawJumpArc(origin, _walkJumpDistance, _jumpHeight);

            // 2. Sprint Jump Parabola (Blue)
            Gizmos.color = new Color(0.2f, 0.4f, 1f, 0.9f);
            DrawJumpArc(origin, _sprintJumpDistance, _jumpHeight);

            // 3. Throw Parabola (Orange)
            Gizmos.color = new Color(0.95f, 0.55f, 0.1f, 0.9f);
            DrawThrowParabola(origin, _throwForce);

            // 4. Magnet Reach WireSphere (Yellow)
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.4f);
            Gizmos.DrawWireSphere(origin, _magnetRange);
        }

        private static void DrawJumpArc(Vector3 start, float distance, float maxHeight)
        {
            const int segments = 24;
            Vector3 prev = start;
            Vector3 forward = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward : Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                float x = t * distance;
                float y = 4f * maxHeight * t * (1f - t);
                Vector3 pt = start + forward * x + Vector3.up * y;
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }

        private static void DrawThrowParabola(Vector3 start, float force)
        {
            const int segments = 24;
            Vector3 prev = start + Vector3.up * 1.2f;
            Vector3 forward = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera.transform.forward : Vector3.forward;
            forward.y = 0.35f;
            forward.Normalize();

            float g = 9.81f;
            Vector3 vel = forward * force;
            float dt = 0.08f;
            Vector3 curr = prev;

            for (int i = 1; i <= segments; i++)
            {
                curr += vel * dt + 0.5f * Vector3.down * g * (dt * dt);
                vel += Vector3.down * g * dt;
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }
        }
#endif
    }
}

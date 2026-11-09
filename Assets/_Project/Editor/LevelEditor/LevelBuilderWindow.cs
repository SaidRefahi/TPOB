using System.Collections.Generic;
using System.IO;
using Game.Core.Enums;
using Game.Gameplay.Camera;
using Game.Gameplay.Checkpoints;
using Game.Gameplay.Hazards;
using Game.Gameplay.Interactables;
using Game.Gameplay.Level;
using Game.Gameplay.PhysicsJuice;
using Game.Gameplay.Player.Death;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Rooms;
using Game.Gameplay.Rooms.Conditions;
using Game.Gameplay.Spawning;
using Game.Network.Events;
using Game.Network.Scopes;
using Game.Network.Services;
using Game.Network.UI;
using PurrNet;
using PurrNet.Transports;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SpawnPoint = Game.Gameplay.Spawning.SpawnPoint;

namespace Game.Editor.LevelEditor
{
    public sealed class LevelBuilderWindow : EditorWindow
    {
        private const string PrefabsPath = "Assets/_Project/Prefabs";

        private Vector2 _scrollPos;
        private int _selectedTab;
        private readonly string[] _tabNames =
        {
            "Muros",
            "Pisos",
            "Paleta",
            "Puzzles",
            "Snapping y Metricas",
            "Auditoria"
        };

        private LevelDefinition _activeLevel;

        // Snapping
        private bool _enableGridSnapping = true;
        private float _gridSize = 1.0f;
        private bool _alignToSurface = true;
        private bool _spawnAtOriginZero = true;

        // Wall Generator settings
        private WallSectionShape _wallShape = WallSectionShape.RectanglePerimeter;
        private StraightWallDirection _straightDirection = StraightWallDirection.NorthSouth_Z;
        private GameObject _customWallPrefab;
        private int _wallWidthModules = 6;
        private int _wallLengthModules = 8;
        private int _wallHeightTiers = 1;
        private float _wallModuleLength = 2.0f;
        private float _wallModuleHeight = 3.0f;
        private float _rotationOffset = 0f;
        private bool _flipFacing = false;
        private bool _centerOnSelection = true;
        private bool _faceOutward = true;
        private bool _generateFloorWithWalls = false;
        private float _wallFloorTileSize = 1.0f;
        private bool _wallFloorVoid = false;
        private int _wallVoidWidth = 1;
        private int _wallVoidLength = 1;

        // Floor Generator settings
        private GameObject _customFloorPrefab;
        private int _floorWidthModules = 8;
        private int _floorLengthModules = 8;
        private float _floorModuleLengthX = 1.0f;
        private float _floorModuleLengthZ = 1.0f;
        private float _floorYOffset = 0.0f;
        private bool _floorCenterOnSelection = true;
        private bool _generateFloorBoxCollider = true;
        private bool _createFloorVoid = false;
        private int _voidHoleWidth = 1;
        private int _voidHoleLength = 1;

        // Puzzle Linker settings
        private GameObject _sourceMechanism;
        private SlidingDoor _targetDoor;

        // Magnetic Track settings
        private GameObject _customMagneticPrefab;
        private MagneticTrackDirection _magneticDirection = MagneticTrackDirection.VerticalClimb;
        private StraightWallDirection _magneticAxis = StraightWallDirection.EastWest_X;
        private int _magneticCount = 5;
        private float _magneticLength = 8.0f;
        private float _magneticHeight = 8.0f;
        private float _magneticCeilingHeight = 6.0f;
        private float _magneticRotationOffset = 0f;
        private bool _magneticFlipFacing = false;
        private bool _magneticCenterOnSelection = true;

        // Platform Generator settings
        private GameObject _customPlatformPrefab;
        private int _platformCount = 5;
        private float _platformGap = 2.5f;
        private float _platformStepHeight = 0.0f;

        // SeeSaw settings
        private GameObject _customSeeSawPrefab;
        private GameObject _customLedgePrefab;
        private float _catapultLedgeHeight = 6.0f;

        // Gizmos
        private bool _showGizmos = true;
        private string _lastLinkFeedback = "";

        [MenuItem("Window/TPOB/Level Builder", false, 1)]
        [MenuItem("TPOB/Level Builder Window", false, 1)]
        public static void ShowWindow()
        {
            var window = GetWindow<LevelBuilderWindow>("Level Builder");
            window.minSize = new Vector2(400, 560);
            window.Show();
        }

        [MenuItem("Window/TPOB/Reset Selection & Clean Inspector", false, 100)]
        [MenuItem("TPOB/Limpiar Seleccion e Inspector", false, 100)]
        public static void CleanStuckInspector()
        {
            Game.Editor.Core.Lifecycle.SelectionMementoService.Instance.ClearSelectionSafely();
            Game.Editor.Core.Lifecycle.InspectorTrackerSanitizer.ClearConsole();
            GUIUtility.hotControl = 0;
            GUIUtility.keyboardControl = 0;
            EditorUtility.UnloadUnusedAssetsImmediate();
            SceneView.RepaintAll();
            Debug.Log("<color=#50fa7b>[TPOB]</color> Inspector y Selección reseteados limpiamente.");
        }

        private void OnEnable()
        {
            FindActiveLevel();
            LoadDefaultPrefabs();
        }

        private void OnFocus()
        {
            if (_activeLevel == null)
            {
                FindActiveLevel();
            }
        }

        private void LoadDefaultPrefabs()
        {
            if (_customWallPrefab == null)
            {
                _customWallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }

            if (_customFloorPrefab == null)
            {
                _customFloorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Ground.prefab");
            }

            if (_customMagneticPrefab == null)
            {
                _customMagneticPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/MagneticWall.prefab");
            }

            if (_customPlatformPrefab == null)
            {
                _customPlatformPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }

            if (_customSeeSawPrefab == null)
            {
                _customSeeSawPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/SeeSawPlatform.prefab");
            }

            if (_customLedgePrefab == null)
            {
                _customLedgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            DrawHeader();

            EditorGUILayout.Space(4);
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames, GUILayout.Height(28));
            EditorGUILayout.Space(6);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_selectedTab)
            {
                case 0:
                    DrawWallsTab();
                    break;
                case 1:
                    DrawFloorsTab();
                    break;
                case 2:
                    DrawPaletteTab();
                    break;
                case 3:
                    DrawPuzzlesTab();
                    break;
                case 4:
                    DrawSnappingTab();
                    break;
                case 5:
                    DrawAuditTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("<b>TwoPilotsOneBody - Level Builder</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true, fontSize = 13 });
                    if (_activeLevel != null && GUILayout.Button("Ping Root", GUILayout.Width(75)))
                    {
                        Selection.activeGameObject = _activeLevel.gameObject;
                        EditorGUIUtility.PingObject(_activeLevel.gameObject);
                    }
                    if (GUILayout.Button("Limpiar Selección", GUILayout.Width(110)))
                    {
                        CleanStuckInspector();
                    }
                }

                EditorGUILayout.LabelField("Consola de autoría de nivel, muros, mecanismos, paleta y validación.", EditorStyles.miniLabel);

                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    _activeLevel = (LevelDefinition)EditorGUILayout.ObjectField("Nivel Activo:", _activeLevel, typeof(LevelDefinition), true);

                    if (GUILayout.Button("Buscar", GUILayout.Width(60)))
                    {
                        FindActiveLevel();
                    }

                    if (_activeLevel == null && GUILayout.Button("Crear Root", GUILayout.Width(90)))
                    {
                        CreateLevelRoot();
                    }

                    if (GUILayout.Button("Configurar Spawn/Play", GUILayout.Width(145)))
                    {
                        ConfigureActiveSceneForSpawning();
                    }
                }

                if (_activeLevel == null)
                {
                    EditorGUILayout.HelpBox("No hay ningún 'LevelDefinition' en la escena activa. Haz clic en 'Crear Root' para inicializarlo.", MessageType.Warning);
                }
            }
        }

        // =================================================================================
        // TAB 0: MUROS Y ESTRUCTURAS
        // =================================================================================

        private void DrawWallsTab()
        {
            EditorGUILayout.LabelField("<b>Generador Procedural de Secciones de Muros</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.HelpBox("Genera muros continuos en cuadrado, rectángulo, 'L', 'U' o línea recta utilizando cualquier prefab asignado.", MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _wallShape = (WallSectionShape)EditorGUILayout.EnumPopup("Forma de Sección:", _wallShape);

                if (_wallShape == WallSectionShape.StraightLine)
                {
                    _straightDirection = (StraightWallDirection)EditorGUILayout.EnumPopup("Dirección de la Línea:", _straightDirection);
                }

                _customWallPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab de Pared:", _customWallPrefab, typeof(GameObject), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Auto-Detectar Dimensiones de Prefab"))
                    {
                        AutoDetectWallDimensions();
                    }
                }

                EditorGUILayout.Space(4);
                if (_wallShape != WallSectionShape.StraightLine)
                {
                    _wallWidthModules = EditorGUILayout.IntSlider("Ancho (Módulos X):", _wallWidthModules, 1, 40);
                }
                _wallLengthModules = EditorGUILayout.IntSlider(_wallShape == WallSectionShape.StraightLine ? "Cantidad de Muros:" : "Largo (Módulos Z):", _wallLengthModules, 1, 40);
                _wallHeightTiers = EditorGUILayout.IntSlider("Capas de Altura:", _wallHeightTiers, 1, 10);
                _wallModuleLength = EditorGUILayout.FloatField("Longitud Módulo (m):", _wallModuleLength);
                _wallModuleHeight = EditorGUILayout.FloatField("Altura Módulo (m):", _wallModuleHeight);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("<b>Alineación y Orientación</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Giro Base:", GUILayout.Width(70));
                    if (GUILayout.Button("0°", _rotationOffset == 0f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _rotationOffset = 0f;
                    if (GUILayout.Button("90°", _rotationOffset == 90f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _rotationOffset = 90f;
                    if (GUILayout.Button("180°", _rotationOffset == 180f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _rotationOffset = 180f;
                    if (GUILayout.Button("270°", _rotationOffset == 270f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _rotationOffset = 270f;
                }

                _flipFacing = EditorGUILayout.Toggle("Invertir Caras (Flip 180°):", _flipFacing);
                _faceOutward = EditorGUILayout.Toggle("Caras Hacia Afuera:", _faceOutward);
                _centerOnSelection = EditorGUILayout.Toggle("Centrar en Selección/Pivote:", _centerOnSelection);
                _spawnAtOriginZero = EditorGUILayout.Toggle("Generar en (0, 0, 0):", _spawnAtOriginZero);

                if (_wallShape != WallSectionShape.StraightLine)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("<b>Piso Interior Integrado</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                    _generateFloorWithWalls = EditorGUILayout.Toggle("Generar Piso con Muros:", _generateFloorWithWalls);
                    if (_generateFloorWithWalls)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField("Resolución Baldosa:", GUILayout.Width(130));
                                if (GUILayout.Button("1m (Fino)", _wallFloorTileSize == 1f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _wallFloorTileSize = 1f;
                                if (GUILayout.Button("2m", _wallFloorTileSize == 2f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _wallFloorTileSize = 2f;
                                if (GUILayout.Button("4m", _wallFloorTileSize == 4f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _wallFloorTileSize = 4f;
                            }

                            _wallFloorVoid = EditorGUILayout.Toggle("Crear Foso Central:", _wallFloorVoid);
                            if (_wallFloorVoid)
                            {
                                int maxHoleX = Mathf.Max(1, Mathf.RoundToInt((_wallWidthModules * _wallModuleLength) / _wallFloorTileSize) - 1);
                                int maxHoleZ = Mathf.Max(1, Mathf.RoundToInt((_wallLengthModules * _wallModuleLength) / _wallFloorTileSize) - 1);
                                _wallVoidWidth = EditorGUILayout.IntSlider($"Ancho Foso ({_wallVoidWidth * _wallFloorTileSize:0.#}m):", _wallVoidWidth, 1, maxHoleX);
                                _wallVoidLength = EditorGUILayout.IntSlider($"Largo Foso ({_wallVoidLength * _wallFloorTileSize:0.#}m):", _wallVoidLength, 1, maxHoleZ);
                            }
                        }
                    }
                }

                EditorGUILayout.Space(8);
                var origColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
                if (GUILayout.Button("Generar Seccion de Muros", GUILayout.Height(32)))
                {
                    GenerateWalls();
                }
                GUI.backgroundColor = origColor;

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Eliminar Ultima Seccion Generada", GUILayout.Height(24)))
                {
                    ClearWalls();
                }
            }
        }

        private void AutoDetectWallDimensions()
        {
            if (_customWallPrefab == null)
            {
                Debug.LogWarning("[LevelBuilder] Asigna un prefab de pared para detectar sus dimensiones.");
                return;
            }

            var mf = _customWallPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _wallModuleLength = Mathf.Max(size.x, size.z);
                _wallModuleHeight = size.y;
                if (_wallModuleLength < 0.1f) _wallModuleLength = 2.0f;
                if (_wallModuleHeight < 0.1f) _wallModuleHeight = 3.0f;
                Debug.Log($"[LevelBuilder] Dimensiones detectadas desde Mesh de '{_customWallPrefab.name}': Longitud={_wallModuleLength:F2}m, Altura={_wallModuleHeight:F2}m (Eje natural: {(size.x >= size.z ? "X / Este-Oeste" : "Z / Norte-Sur")}).");
                return;
            }

            var col = _customWallPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _wallModuleLength = Mathf.Max(col.size.x, col.size.z);
                _wallModuleHeight = col.size.y;
                Debug.Log($"[LevelBuilder] Dimensiones detectadas desde BoxCollider de '{_customWallPrefab.name}': Longitud={_wallModuleLength:F2}m, Altura={_wallModuleHeight:F2}m.");
                return;
            }

            var renderer = _customWallPrefab.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Vector3 boundsSize = renderer.bounds.size;
                _wallModuleLength = Mathf.Max(boundsSize.x, boundsSize.z);
                _wallModuleHeight = boundsSize.y;
                if (_wallModuleLength < 0.1f) _wallModuleLength = 2.0f;
                if (_wallModuleHeight < 0.1f) _wallModuleHeight = 3.0f;
                Debug.Log($"[LevelBuilder] Dimensiones detectadas de '{_customWallPrefab.name}': Longitud={_wallModuleLength:F2}m, Altura={_wallModuleHeight:F2}m.");
            }
        }

        private void GenerateWalls()
        {
            EnsureActiveLevel();
            _activeLevel.ConfigureWallGenerator(
                _customWallPrefab,
                _wallShape,
                _straightDirection,
                _wallWidthModules,
                _wallLengthModules,
                _wallHeightTiers,
                _wallModuleLength,
                _wallModuleHeight,
                _rotationOffset,
                _flipFacing,
                _centerOnSelection,
                _faceOutward);
            _activeLevel.ConfigureWallFloor(
                _generateFloorWithWalls,
                _wallFloorVoid,
                _wallVoidWidth,
                _wallVoidLength,
                _wallFloorTileSize);
            _activeLevel.SetSpawnAtOriginZero(_spawnAtOriginZero);
            _activeLevel.GenerateWallSection();
        }

        private void ClearWalls()
        {
            if (_activeLevel != null)
            {
                _activeLevel.ClearGeneratedWalls();
            }
        }

        // =================================================================================
        // TAB 1: PISOS Y PLATAFORMAS
        // =================================================================================

        private void DrawFloorsTab()
        {
            EditorGUILayout.LabelField("<b>Generador Procedural de Pisos y Plataformas</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.HelpBox("Genera superficies y plataformas de piso en grilla (W x L) con soporte de fosos/vacíos centrales o eliminación libre.", MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _customFloorPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab de Baldosa/Piso:", _customFloorPrefab, typeof(GameObject), false);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Auto-Detectar Dimensiones de Baldosa"))
                    {
                        AutoDetectFloorDimensions();
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Presets Rápidos:", GUILayout.Width(110));
                    if (GUILayout.Button("1m x 1m (Fino)", _floorModuleLengthX == 1f ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        _floorModuleLengthX = 1f; _floorModuleLengthZ = 1f;
                    }
                    if (GUILayout.Button("2m x 2m (Medio)", _floorModuleLengthX == 2f ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        _floorModuleLengthX = 2f; _floorModuleLengthZ = 2f;
                    }
                    if (GUILayout.Button("4m x 4m (Grande)", _floorModuleLengthX == 4f ? EditorStyles.miniButtonMid : EditorStyles.miniButton))
                    {
                        _floorModuleLengthX = 4f; _floorModuleLengthZ = 4f;
                    }
                }

                EditorGUILayout.Space(4);
                _floorWidthModules = EditorGUILayout.IntSlider("Ancho (Baldosas X):", _floorWidthModules, 1, 40);
                _floorLengthModules = EditorGUILayout.IntSlider("Largo (Baldosas Z):", _floorLengthModules, 1, 40);
                _floorModuleLengthX = EditorGUILayout.FloatField("Tamaño Baldosa X (m):", _floorModuleLengthX);
                _floorModuleLengthZ = EditorGUILayout.FloatField("Tamaño Baldosa Z (m):", _floorModuleLengthZ);
                _floorYOffset = EditorGUILayout.FloatField("Desfase Altura Y (m):", _floorYOffset);
                _floorCenterOnSelection = EditorGUILayout.Toggle("Centrar en Selección/Pivote:", _floorCenterOnSelection);
                _spawnAtOriginZero = EditorGUILayout.Toggle("Generar en (0, 0, 0):", _spawnAtOriginZero);
                _generateFloorBoxCollider = EditorGUILayout.Toggle("Auto-Generar BoxCollider Unificado:", _generateFloorBoxCollider);

                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("<b>Fosos y Vacíos (Pits)</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                _createFloorVoid = EditorGUILayout.Toggle("Crear Foso / Hueco Central:", _createFloorVoid);
                if (_createFloorVoid)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        _voidHoleWidth = EditorGUILayout.IntSlider($"Ancho Foso ({_voidHoleWidth * _floorModuleLengthX:0.#}m):", _voidHoleWidth, 1, Mathf.Max(1, _floorWidthModules - 1));
                        _voidHoleLength = EditorGUILayout.IntSlider($"Largo Foso ({_voidHoleLength * _floorModuleLengthZ:0.#}m):", _voidHoleLength, 1, Mathf.Max(1, _floorLengthModules - 1));
                    }
                }

                EditorGUILayout.Space(8);
                var origColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
                if (GUILayout.Button("Generar Superficie de Piso", GUILayout.Height(32)))
                {
                    GenerateFloors();
                }
                GUI.backgroundColor = origColor;

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Ajustar BoxCollider a Pisos Existentes en Escena", GUILayout.Height(24)))
                {
                    LevelDefinition.AutoFitFloorCollidersInScene();
                }

                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Eliminar Ultimo Piso", GUILayout.Height(24)))
                    {
                        ClearFloors();
                    }
                    if (GUILayout.Button("Eliminar Baldosas Seleccionadas", GUILayout.Height(24)))
                    {
                        DeleteSelectedTiles();
                    }
                }
            }
        }

        private void AutoDetectFloorDimensions()
        {
            EnsureActiveLevel();
            var targetPrefab = _customFloorPrefab != null 
                ? _customFloorPrefab 
                : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Ground.prefab");

            if (targetPrefab == null)
            {
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Wall.prefab");
            }

            if (targetPrefab == null)
            {
                Debug.LogWarning("[LevelBuilder] No hay prefab de piso seleccionado para detectar dimensiones.");
                return;
            }

            var mf = targetPrefab.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _floorModuleLengthX = size.x > 0.05f ? size.x : 4.0f;
                _floorModuleLengthZ = size.z > 0.05f ? size.z : 4.0f;
                Debug.Log($"[LevelBuilder] Dimensiones de baldosa detectadas: X={_floorModuleLengthX:F2}m, Z={_floorModuleLengthZ:F2}m.");
                return;
            }

            var col = targetPrefab.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _floorModuleLengthX = col.size.x > 0.05f ? col.size.x : 4.0f;
                _floorModuleLengthZ = col.size.z > 0.05f ? col.size.z : 4.0f;
                Debug.Log($"[LevelBuilder] Dimensiones de baldosa detectadas desde BoxCollider: X={_floorModuleLengthX:F2}m, Z={_floorModuleLengthZ:F2}m.");
                return;
            }
        }

        private void GenerateFloors()
        {
            EnsureActiveLevel();
            _activeLevel.ConfigureFloorGenerator(
                _customFloorPrefab,
                _floorWidthModules,
                _floorLengthModules,
                _floorModuleLengthX,
                _floorModuleLengthZ,
                _floorYOffset,
                _floorCenterOnSelection,
                _createFloorVoid,
                _voidHoleWidth,
                _voidHoleLength,
                _generateFloorBoxCollider);
            _activeLevel.SetSpawnAtOriginZero(_spawnAtOriginZero);
            _activeLevel.GenerateFloorSection();
        }

        private void ClearFloors()
        {
            if (_activeLevel != null)
            {
                _activeLevel.ClearGeneratedFloors();
            }
        }

        private void DeleteSelectedTiles()
        {
            if (_activeLevel != null)
            {
                _activeLevel.DeleteSelectedTiles();
            }
        }

        // =================================================================================
        // TAB 2: PALETA DE PREFABS
        // =================================================================================

        private void DrawPaletteTab()
        {
            EditorGUILayout.LabelField("<b>Paleta Modular de Elementos</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.HelpBox("Haz clic en cualquier pieza para colocarla en la escena. Si hay snapping activo, se alineará a la grilla o superficie.", MessageType.Info);

            EditorGUILayout.Space(4);
            DrawCategory("Arquitectura y Estructuras", new[]
            {
                ("Muro Estándar", $"{PrefabsPath}/Wall.prefab"),
                ("Muro Rompible", $"{PrefabsPath}/BreakableWall.prefab"),
                ("Muro Magnético", $"{PrefabsPath}/MagneticWall.prefab"),
                ("Piso / Baldosa Modular", "Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs/Props/Ground.prefab")
            });

            EditorGUILayout.Space(6);
            DrawCategory("Mecanismos e Interactivos", new[]
            {
                ("Interruptor Palanca", $"{PrefabsPath}/LeverSwitch.prefab"),
                ("Botón Diana (Lanzamiento)", $"{PrefabsPath}/TargetButton.prefab"),
                ("Plataforma de Peso", $"{PrefabsPath}/WeightPlatform.prefab"),
                ("Puerta Corrediza", $"{PrefabsPath}/SlidingDoor.prefab"),
                ("Socket de Puzzle", $"{PrefabsPath}/PuzzleSocket.prefab"),
                ("Caja Agarrable", $"{PrefabsPath}/GrabbableBox.prefab"),
                ("Llave Magnética", $"{PrefabsPath}/MagneticKey.prefab"),
                ("Caja Empujable", $"{PrefabsPath}/PushableBox.prefab"),
                ("Obstáculo Pateable", $"{PrefabsPath}/KickableObstacle.prefab")
            });

            EditorGUILayout.Space(6);
            DrawCategory("Peligros y Plataformas Dinámicas", new[]
            {
                ("KillVolume (Zona Mortal)", $"{PrefabsPath}/KillVolume.prefab"),
                ("Plataforma Balancín (SeeSaw)", $"{PrefabsPath}/SeeSawPlatform.prefab")
            });

            EditorGUILayout.Space(6);
            DrawCategory("Checkpoints y Spawns", new[]
            {
                ("Checkpoint Asimétrico", $"{PrefabsPath}/Checkpoint.prefab")
            });
        }

        private void DrawCategory(string title, (string label, string path)[] items)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"<b>{title}</b>", new GUIStyle(EditorStyles.label) { richText = true });
                EditorGUILayout.Space(2);

                for (int i = 0; i < items.Length; i++)
                {
                    var (label, path) = items[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(label, GUILayout.Width(180));

                        bool exists = File.Exists(path);
                        using (new EditorGUI.DisabledScope(!exists))
                        {
                            if (GUILayout.Button(exists ? "Instanciar" : "No encontrado", GUILayout.Height(22)))
                            {
                                InstantiatePrefab(path);
                            }
                        }
                    }
                }
            }
        }

        // =================================================================================
        // TAB 3: PUZZLES Y MECANISMOS
        // =================================================================================

        private void DrawPuzzlesTab()
        {
            EditorGUILayout.LabelField("<b>Enlazador Rápido de Mecanismos (1-Click Puzzle Linker)</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.HelpBox("Vincula cualquier Botón, Palanca o Plataforma de Peso a una Puerta Corrediza inyectando automáticamente el trigger.", MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _sourceMechanism = (GameObject)EditorGUILayout.ObjectField("Mecanismo Origen:", _sourceMechanism, typeof(GameObject), true);
                    if (GUILayout.Button("Usar Selección", GUILayout.Width(100)))
                    {
                        if (Selection.activeGameObject != null)
                        {
                            _sourceMechanism = Selection.activeGameObject;
                        }
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _targetDoor = (SlidingDoor)EditorGUILayout.ObjectField("Puerta Destino:", _targetDoor, typeof(SlidingDoor), true);
                    if (GUILayout.Button("Usar Selección", GUILayout.Width(100)))
                    {
                        if (Selection.activeGameObject != null)
                        {
                            var door = Selection.activeGameObject.GetComponentInChildren<SlidingDoor>(true);
                            if (door != null) _targetDoor = door;
                        }
                    }
                }

                EditorGUILayout.Space(4);
                var origColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
                if (GUILayout.Button("Vincular Mecanismo a Puerta", GUILayout.Height(30)))
                {
                    LinkPuzzleMechanism();
                }

                if (GUILayout.Button("Vincular Seleccion Directa (Mecanismo + Puerta)", GUILayout.Height(26)))
                {
                    LinkDirectSelection();
                }
                GUI.backgroundColor = origColor;

                if (!string.IsNullOrEmpty(_lastLinkFeedback))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(_lastLinkFeedback, MessageType.Info);
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Generador de Pistas de Pared Magnetica</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _customMagneticPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Muro Magnético:", _customMagneticPrefab, typeof(GameObject), false);
                EditorGUILayout.HelpBox("Si no se asigna, usará el prefab por defecto: Assets/_Project/Prefabs/MagneticWall.prefab (8x8m).", MessageType.None);

                if (GUILayout.Button("Auto-Detectar Dimensiones de Prefab", GUILayout.Height(22)))
                {
                    AutoDetectMagneticDimensions();
                }

                EditorGUILayout.Space(4);
                _magneticDirection = (MagneticTrackDirection)EditorGUILayout.EnumPopup("Dirección de Pista:", _magneticDirection);

                if (_magneticDirection == MagneticTrackDirection.HorizontalTraverse || _magneticDirection == MagneticTrackDirection.Ceiling)
                {
                    _magneticAxis = (StraightWallDirection)EditorGUILayout.EnumPopup("Eje de Pista:", _magneticAxis);
                }

                if (_magneticDirection == MagneticTrackDirection.Ceiling)
                {
                    _magneticCeilingHeight = EditorGUILayout.FloatField("Altura Techo (m):", _magneticCeilingHeight);
                }

                _magneticCount = EditorGUILayout.IntSlider("Cantidad de Paneles:", _magneticCount, 1, 30);
                _magneticLength = EditorGUILayout.FloatField("Longitud Panel (m):", _magneticLength);
                _magneticHeight = EditorGUILayout.FloatField("Altura Panel (m):", _magneticHeight);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("<b>Alineación y Orientación</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Giro Base:", GUILayout.Width(70));
                    if (GUILayout.Button("0°", _magneticRotationOffset == 0f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _magneticRotationOffset = 0f;
                    if (GUILayout.Button("90°", _magneticRotationOffset == 90f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _magneticRotationOffset = 90f;
                    if (GUILayout.Button("180°", _magneticRotationOffset == 180f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _magneticRotationOffset = 180f;
                    if (GUILayout.Button("270°", _magneticRotationOffset == 270f ? EditorStyles.miniButtonMid : EditorStyles.miniButton)) _magneticRotationOffset = 270f;
                }

                _magneticFlipFacing = EditorGUILayout.Toggle("Invertir Caras (Flip 180°):", _magneticFlipFacing);
                _magneticCenterOnSelection = EditorGUILayout.Toggle("Centrar en Selección/Pivote:", _magneticCenterOnSelection);
                _spawnAtOriginZero = EditorGUILayout.Toggle("Generar en (0, 0, 0):", _spawnAtOriginZero);

                EditorGUILayout.Space(6);
                var origColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.75f, 1f);
                if (GUILayout.Button("Generar Pista Magnetica", GUILayout.Height(30)))
                {
                    GenerateMagneticTrack();
                }
                GUI.backgroundColor = origColor;

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Eliminar Ultima Pista Generada", GUILayout.Height(24)))
                {
                    ClearMagneticTrack();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Generador de Plataformas y Puentes (Stepping Stones)</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _customPlatformPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Plataforma:", _customPlatformPrefab, typeof(GameObject), false);
                _platformCount = EditorGUILayout.IntSlider("Cantidad de Peldaños:", _platformCount, 2, 20);
                _platformGap = EditorGUILayout.FloatField("Separación (m):", _platformGap);
                _platformStepHeight = EditorGUILayout.FloatField("Variación Altura/Paso (m):", _platformStepHeight);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Generar Camino de Plataformas", GUILayout.Height(28)))
                {
                    GeneratePlatformPath();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Calibrador de Balancin Catapulta</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _customSeeSawPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Balancín:", _customSeeSawPrefab, typeof(GameObject), false);
                _customLedgePrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Saliente:", _customLedgePrefab, typeof(GameObject), false);
                EditorGUILayout.HelpBox("Si no se asignan, usarán: SeeSawPlatform.prefab y Wall.prefab por defecto.", MessageType.None);

                _catapultLedgeHeight = EditorGUILayout.FloatField("Altura Saliente Objetivo (m):", _catapultLedgeHeight);
                _spawnAtOriginZero = EditorGUILayout.Toggle("Generar en (0, 0, 0):", _spawnAtOriginZero);

                EditorGUILayout.Space(4);
                var origColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.95f, 0.65f, 0.25f);
                if (GUILayout.Button("Instanciar Setup Balancin + Saliente", GUILayout.Height(30)))
                {
                    GenerateSeeSawSetup();
                }
                GUI.backgroundColor = origColor;

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Eliminar Ultimo Setup de Balancin", GUILayout.Height(24)))
                {
                    ClearSeeSawSetup();
                }
            }
        }

        private void LinkPuzzleMechanism()
        {
            if (_sourceMechanism == null)
            {
                _lastLinkFeedback = "[Aviso] Asigna o selecciona el mecanismo de origen (ej. Botón Diana, Palanca, Placa de Peso).";
                Debug.LogWarning("[LevelBuilder] " + _lastLinkFeedback);
                return;
            }

            if (_targetDoor == null)
            {
                _lastLinkFeedback = "[Aviso] Asigna o selecciona la puerta corredera de destino.";
                Debug.LogWarning("[LevelBuilder] " + _lastLinkFeedback);
                return;
            }

            EnsureActiveLevel();
            _activeLevel.ConfigurePuzzleLinker(_sourceMechanism, _targetDoor);
            _activeLevel.LinkMechanismToDoor();
            _lastLinkFeedback = $"[Exito] Vinculado correctamente: '{_sourceMechanism.name}' -> '{_targetDoor.name}'.";
        }

        private void LinkDirectSelection()
        {
            var gos = Selection.gameObjects;
            GameObject mechGo = null;
            SlidingDoor door = null;

            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (mechGo == null && g.GetComponentInChildren<Game.Core.Interfaces.IInteractableMechanism>() != null)
                {
                    mechGo = g;
                }
                if (door == null && g.GetComponentInChildren<SlidingDoor>() != null)
                {
                    door = g.GetComponentInChildren<SlidingDoor>();
                }
            }

            if (mechGo != null && door != null)
            {
                _sourceMechanism = mechGo;
                _targetDoor = door;
                LinkPuzzleMechanism();
            }
            else
            {
                _lastLinkFeedback = "[Aviso] Selecciona en la Jerarquia tanto el Mecanismo como la Puerta Corrediza (con Ctrl/Shift + Clic).";
            }
        }

        private void AutoDetectMagneticDimensions()
        {
            var target = _customMagneticPrefab != null
                ? _customMagneticPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/MagneticWall.prefab");

            if (target == null)
            {
                Debug.LogWarning("[LevelBuilder] No hay prefab magnético para detectar dimensiones.");
                return;
            }

            var col = target.GetComponentInChildren<BoxCollider>();
            if (col != null)
            {
                _magneticLength = Mathf.Max(col.size.x, col.size.z);
                _magneticHeight = col.size.y;
                if (_magneticLength < 0.1f) _magneticLength = 8.0f;
                if (_magneticHeight < 0.1f) _magneticHeight = 8.0f;
                Debug.Log($"[LevelBuilder] Dimensiones detectadas de BoxCollider de '{target.name}': Longitud={_magneticLength:F2}m, Altura={_magneticHeight:F2}m.");
                return;
            }

            var mf = target.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 size = mf.sharedMesh.bounds.size;
                _magneticLength = Mathf.Max(size.x, size.z);
                _magneticHeight = size.y;
                if (_magneticLength < 0.1f) _magneticLength = 8.0f;
                if (_magneticHeight < 0.1f) _magneticHeight = 8.0f;
                Debug.Log($"[LevelBuilder] Dimensiones detectadas de Mesh de '{target.name}': Longitud={_magneticLength:F2}m, Altura={_magneticHeight:F2}m.");
            }
        }

        private void GenerateMagneticTrack()
        {
            EnsureActiveLevel();
            _activeLevel.SetSpawnAtOriginZero(_spawnAtOriginZero);
            _activeLevel.ConfigureMagneticTrack(
                _customMagneticPrefab,
                _magneticDirection,
                _magneticAxis,
                _magneticCount,
                _magneticLength,
                _magneticHeight,
                _magneticCeilingHeight,
                _magneticRotationOffset,
                _magneticFlipFacing,
                _magneticCenterOnSelection);
            _activeLevel.GenerateMagneticTrack();
        }

        private void ClearMagneticTrack()
        {
            EnsureActiveLevel();
            _activeLevel.ClearGeneratedMagneticTracks();
        }

        private void GeneratePlatformPath()
        {
            EnsureActiveLevel();
            _activeLevel.ConfigurePlatformPath(_customPlatformPrefab, _platformCount, _platformGap, _platformStepHeight);
            _activeLevel.GeneratePlatformPath();
        }

        private void GenerateSeeSawSetup()
        {
            EnsureActiveLevel();
            _activeLevel.SetSpawnAtOriginZero(_spawnAtOriginZero);
            _activeLevel.ConfigureSeeSaw(_customSeeSawPrefab, _customLedgePrefab, _catapultLedgeHeight);
            _activeLevel.GenerateSeeSawSetup();
        }

        private void ClearSeeSawSetup()
        {
            EnsureActiveLevel();
            _activeLevel.ClearGeneratedSeeSaws();
        }

        // =================================================================================
        // TAB 4: SNAPPING Y MÉTRICAS
        // =================================================================================

        private void DrawSnappingTab()
        {
            EditorGUILayout.LabelField("<b>Configuración de Snapping y Grilla</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _enableGridSnapping = EditorGUILayout.Toggle("Activar Grid Snapping", _enableGridSnapping);
                using (new EditorGUI.DisabledScope(!_enableGridSnapping))
                {
                    _gridSize = EditorGUILayout.Slider("Tamaño de Grilla (m)", _gridSize, 0.25f, 4.0f);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("0.5m")) _gridSize = 0.5f;
                        if (GUILayout.Button("1.0m")) _gridSize = 1.0f;
                        if (GUILayout.Button("2.0m")) _gridSize = 2.0f;
                        if (GUILayout.Button("4.0m")) _gridSize = 4.0f;
                    }
                }

                EditorGUILayout.Space(4);
                _alignToSurface = EditorGUILayout.Toggle("Alinear a Superficie (Raycast)", _alignToSurface);

                EditorGUILayout.Space(6);
                if (GUILayout.Button("Alinear Objetos Seleccionados a la Grilla", GUILayout.Height(26)))
                {
                    SnapSelectedObjects();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Visualizador de Métricas en SceneView</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });
            EditorGUILayout.HelpBox("Dibuja arcos de salto y parábolas de lanzamiento calculadas para verificar distancias sin salir del editor.", MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                bool prevGizmos = _showGizmos;
                _showGizmos = EditorGUILayout.Toggle("Mostrar Arcos y Rangos en SceneView", _showGizmos);
                if (_showGizmos != prevGizmos && _activeLevel != null)
                {
                    _activeLevel.SetMetricGizmosVisible(_showGizmos);
                    SceneView.RepaintAll();
                }

                EditorGUILayout.HelpBox("Los arcos se renderizan automáticamente sobre el objeto seleccionado en la Scene View mientras este modo esté activo.", MessageType.None);
            }
        }

        // =================================================================================
        // TAB 5: AUDITORÍA Y ESCENAS
        // =================================================================================

        private void DrawAuditTab()
        {
            EditorGUILayout.LabelField("<b>Deadzone Subterránea y Auditoría de Nivel</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

            if (_activeLevel == null)
            {
                EditorGUILayout.HelpBox("Asigna o crea un LevelDefinition para configurar la deadzone y validar el nivel.", MessageType.Warning);
                if (GUILayout.Button("Crear Level Root", GUILayout.Height(30)))
                {
                    CreateLevelRoot();
                }
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"Nivel Activo: <b>{_activeLevel.LevelName}</b>", new GUIStyle(EditorStyles.label) { richText = true });

                float groundY = EditorGUILayout.FloatField("Nivel del Suelo (Ground Y):", _activeLevel.GroundY);
                if (Mathf.Abs(groundY - _activeLevel.GroundY) > 0.001f)
                {
                    Undo.RecordObject(_activeLevel, "Change Ground Y");
                    _activeLevel.SetGroundY(groundY);
                }

                float deadzoneY = EditorGUILayout.FloatField("Altura Deadzone Subterránea:", _activeLevel.SubterraneanDeadzoneY);
                if (Mathf.Abs(deadzoneY - _activeLevel.SubterraneanDeadzoneY) > 0.001f)
                {
                    Undo.RecordObject(_activeLevel, "Change Deadzone Y");
                    _activeLevel.SetDeadzoneY(deadzoneY);
                }

                float padding = EditorGUILayout.FloatField("Margen Deadzone (Padding):", _activeLevel.DeadzonePadding);
                if (Mathf.Abs(padding - _activeLevel.DeadzonePadding) > 0.001f)
                {
                    Undo.RecordObject(_activeLevel, "Change Deadzone Padding");
                    _activeLevel.SetDeadzonePadding(padding);
                }

                EditorGUILayout.Space(6);
                var origBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.6f, 0.3f);
                if (GUILayout.Button("Generar / Actualizar Deadzone Subterranea", GUILayout.Height(30)))
                {
                    _activeLevel.GenerateSubterraneanDeadzone();
                }
                GUI.backgroundColor = origBg;

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Auto-Descubrir Checkpoints y Spawns", GUILayout.Height(26)))
                {
                    _activeLevel.AutoDiscoverElements();
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Bake & Auditoría Estática</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var origBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.35f, 0.8f, 1f);
                if (GUILayout.Button("Reparar / Configurar Jerarquia de Spawn y Red (No Destructivo)", GUILayout.Height(32)))
                {
                    ConfigureActiveSceneForSpawning();
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.35f, 0.9f, 0.45f);
                if (GUILayout.Button("Bake & Validate Level", GUILayout.Height(36)))
                {
                    _activeLevel.AutoDiscoverElements();
                    LevelValidator.Validate(_activeLevel);
                }
                GUI.backgroundColor = origBg;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("<b>Generador de Escenas</b>", new GUIStyle(EditorStyles.boldLabel) { richText = true });

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (GUILayout.Button("Crear Nueva Escena de Nivel (Plantilla Limpia)", GUILayout.Height(30)))
                {
                    LevelSceneTemplateGenerator.CreateNewLevelScene();
                    FindActiveLevel();
                }
            }
        }

        // =================================================================================
        // HELPERS
        // =================================================================================

        private void FindActiveLevel()
        {
            _activeLevel = Object.FindFirstObjectByType<LevelDefinition>();
        }

        private void EnsureActiveLevel()
        {
            if (_activeLevel == null)
            {
                FindActiveLevel();
            }

            if (_activeLevel == null)
            {
                CreateLevelRoot();
            }
        }

        private void CreateLevelRoot()
        {
            var go = new GameObject("Level_Root");
            _activeLevel = go.AddComponent<LevelDefinition>();
            Undo.RegisterCreatedObjectUndo(go, "Create Level Root");
            Selection.activeGameObject = go;
            _activeLevel.AutoDiscoverElements();
            Debug.Log("<color=#50fa7b><b>[LevelBuilder]</b></color> 'Level_Root' creado e inicializado con LevelDefinition en la escena.");
        }

        private void InstantiatePrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[LevelBuilder] Prefab no encontrado en {path}");
                return;
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

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = spawnPos;

            EnsureActiveLevel();
            if (_activeLevel != null)
            {
                instance.transform.SetParent(_activeLevel.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Place {prefab.name}");
            Selection.activeGameObject = instance;
        }

        private void SnapSelectedObjects()
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

        #region CLI & Headless Level Construction

        private sealed class ModularPalette
        {
            public GameObject Floor;
            public GameObject Wall;
            public GameObject WallDoorL;
            public GameObject WallDoorR;
            public GameObject Barrier;
            public GameObject Cone;
            public GameObject Pallet;
            public GameObject Conveyor;
            public GameObject Machine;
            public GameObject CeilingPlate4x4;
            public GameObject CeilingPlate2x4;
            public GameObject TrimWall3x4;
            public GameObject BoxGrabbable;
            public GameObject BoxHeavy;
            public GameObject Lever;
            public GameObject SlidingDoor;
            public GameObject WeightPlatform;
            public GameObject TargetButton;
            public GameObject BreakableWall;
            public GameObject SeeSaw;
            public GameObject MagneticWall;
            public GameObject MagneticKey;
            public GameObject PuzzleSocket;
            public GameObject HeavyDoor;
            public GameObject Catapult;
            public GameObject Checkpoint;
        }

        [InitializeOnLoadMethod]
        private static void RegisterPlayableSceneAutoConfigurator()
        {
            EditorApplication.delayCall += AutoEnsureActiveScenePlayable;
            EditorSceneManager.sceneOpened -= OnSceneOpenedEnsurePlayable;
            EditorSceneManager.sceneOpened += OnSceneOpenedEnsurePlayable;
        }

        private static void OnSceneOpenedEnsurePlayable(Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            AutoEnsureActiveScenePlayable();
        }

        private static void AutoEnsureActiveScenePlayable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying) return;
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded) return;

            bool isLevelScene = Object.FindFirstObjectByType<LevelDefinition>() != null ||
                                Object.FindFirstObjectByType<SpawnPointManager>() != null;
            if (!isLevelScene) return;

            LevelDefinition.AutoFitFloorCollidersInScene();
            EnsureScenePlayableHierarchy(scene, new Vector3(0f, 0.1f, 1.5f), new Vector3(0f, 0.1f, -1.5f));
            AlignSpawnsAboveSceneFloor();

            var levelDef = Object.FindFirstObjectByType<LevelDefinition>();
            if (levelDef != null)
            {
                levelDef.AutoDiscoverElements();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem("TPOB/Tools/Configure Active Scene Hierarchy for Spawn & Play", priority = 11)]
        public static void ConfigureActiveSceneForSpawning()
        {
            var scene = SceneManager.GetActiveScene();
            EnsureScenePlayableHierarchy(scene, new Vector3(-2f, 0.1f, -4f), new Vector3(2f, 0.1f, -4f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#50fa7b>[LevelBuilderWindow] Jerarquía de Spawn, Cámara, VContainer y Red configurada (no destructiva) en '{scene.path}'.</color>");
        }

        public static void ConfigureSceneAForSpawning()
        {
            const string targetPath = "Assets/_Project/Scenes/A.unity";
            Scene scene;
            var active = SceneManager.GetActiveScene();
            if (active.path == targetPath)
            {
                scene = active;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(scene);

            LevelDefinition.AutoFitFloorCollidersInScene();
            EnsureScenePlayableHierarchy(scene, new Vector3(0f, 0.1f, 1.5f), new Vector3(0f, 0.1f, -1.5f));
            AlignSpawnsAboveSceneFloor();

            var levelDef = Object.FindFirstObjectByType<LevelDefinition>();
            if (levelDef != null)
            {
                levelDef.AutoDiscoverElements();
                LevelValidator.Validate(levelDef);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=#50fa7b>[LevelBuilderWindow] Escena '{targetPath}' configurada para Spawn y Play exitosamente sin tocar la geometría del nivel.</color>");
        }

        private static void AlignSpawnsAboveSceneFloor()
        {
            var floorGrid = GameObject.Find("Section_Floor_Grid");
            if (floorGrid == null) return;

            var floorCol = floorGrid.GetComponent<BoxCollider>();
            if (floorCol == null) return;

            float floorTopY = floorCol.bounds.max.y;

            var spLegs = GameObject.Find("SpawnPoint_Legs");
            if (spLegs != null)
            {
                Vector3 pos = spLegs.transform.position;
                pos.y = floorTopY + 0.15f;
                spLegs.transform.position = pos;
                EditorUtility.SetDirty(spLegs.transform);
            }

            var spTorso = GameObject.Find("SpawnPoint_Torso");
            if (spTorso != null)
            {
                Vector3 pos = spTorso.transform.position;
                if (Mathf.Approximately(pos.x, 2f) && Mathf.Approximately(pos.z, -4f))
                {
                    pos.x = 0f;
                    pos.z = -1.5f;
                }
                pos.y = floorTopY + 0.15f;
                spTorso.transform.position = pos;
                EditorUtility.SetDirty(spTorso.transform);
            }
        }

        public static void EnsureScenePlayableHierarchy(Scene scene, Vector3 defaultLegsSpawnPos, Vector3 defaultTorsoSpawnPos)
        {
            // 1. Find or create root groups (non-destructive)
            GameObject mgmt = GameObject.Find("--- MANAGEMENT ---");
            if (mgmt == null)
            {
                mgmt = new GameObject("--- MANAGEMENT ---");
                SceneManager.MoveGameObjectToScene(mgmt, scene);
            }

            GameObject net = GameObject.Find("--- NETWORKING ---");
            if (net == null)
            {
                net = new GameObject("--- NETWORKING ---");
                SceneManager.MoveGameObjectToScene(net, scene);
            }

            GameObject lighting = GameObject.Find("--- LIGHTING & CAMERA ---");
            if (lighting == null)
            {
                lighting = new GameObject("--- LIGHTING & CAMERA ---");
                SceneManager.MoveGameObjectToScene(lighting, scene);
            }

            var levelDef = Object.FindFirstObjectByType<LevelDefinition>();
            if (levelDef == null)
            {
                var levelRoot = GameObject.Find("Level_Root");
                if (levelRoot == null)
                {
                    levelRoot = new GameObject("Level_Root");
                    SceneManager.MoveGameObjectToScene(levelRoot, scene);
                }
                levelDef = levelRoot.AddComponent<LevelDefinition>();
            }

            // 2. SpawnPointManager & SpawnPoints (preserving existing user positions!)
            var spawnMgr = Object.FindFirstObjectByType<SpawnPointManager>();
            GameObject spawnMgrGo;
            if (spawnMgr == null)
            {
                spawnMgrGo = new GameObject("[SPAWN_POINT_MANAGER]");
                spawnMgrGo.transform.SetParent(mgmt.transform);
                spawnMgr = spawnMgrGo.AddComponent<SpawnPointManager>();
            }
            else
            {
                spawnMgrGo = spawnMgr.gameObject;
                if (spawnMgrGo.transform.parent == null)
                {
                    spawnMgrGo.transform.SetParent(mgmt.transform);
                }
            }

            SpawnPoint spLegs = null;
            SpawnPoint spTorso = null;
            var allSpawns = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < allSpawns.Length; i++)
            {
                var sp = allSpawns[i];
                if (sp.TargetRole == PlayerRole.Legs || sp.name.Contains("Legs"))
                {
                    spLegs = sp;
                }
                else if (sp.TargetRole == PlayerRole.Torso || sp.name.Contains("Torso"))
                {
                    spTorso = sp;
                }
            }

            if (spLegs == null)
            {
                var spLegsGo = new GameObject("SpawnPoint_Legs");
                spLegsGo.transform.SetParent(spawnMgrGo.transform);
                spLegsGo.transform.position = defaultLegsSpawnPos;
                spLegs = spLegsGo.AddComponent<SpawnPoint>();
            }
            var spLegsSo = new SerializedObject(spLegs);
            spLegsSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Legs;
            spLegsSo.ApplyModifiedPropertiesWithoutUndo();

            if (spTorso == null)
            {
                var spTorsoGo = new GameObject("SpawnPoint_Torso");
                spTorsoGo.transform.SetParent(spawnMgrGo.transform);
                spTorsoGo.transform.position = defaultTorsoSpawnPos;
                spTorso = spTorsoGo.AddComponent<SpawnPoint>();
            }
            var spTorsoSo = new SerializedObject(spTorso);
            spTorsoSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Torso;
            spTorsoSo.ApplyModifiedPropertiesWithoutUndo();

            var spawnMgrSo = new SerializedObject(spawnMgr);
            var spList = spawnMgrSo.FindProperty("_spawnPoints");
            spList.arraySize = 2;
            spList.GetArrayElementAtIndex(0).objectReferenceValue = spLegs;
            spList.GetArrayElementAtIndex(1).objectReferenceValue = spTorso;
            spawnMgrSo.ApplyModifiedPropertiesWithoutUndo();

            if (levelDef.LegsSpawnPoint == null || levelDef.TorsoSpawnPoint == null)
            {
                levelDef.SetSpawns(
                    levelDef.LegsSpawnPoint != null ? levelDef.LegsSpawnPoint : spLegs.transform,
                    levelDef.TorsoSpawnPoint != null ? levelDef.TorsoSpawnPoint : spTorso.transform);
            }

            // 3. [PLAYER_SPAWNER] (TPOBPlayerSpawner with LegsPlayer & TorsoPlayer prefabs)
            var playerSpawner = Object.FindFirstObjectByType<TPOBPlayerSpawner>();
            if (playerSpawner == null)
            {
                var spawnerGo = new GameObject("[PLAYER_SPAWNER]");
                spawnerGo.transform.SetParent(mgmt.transform);
                playerSpawner = spawnerGo.AddComponent<TPOBPlayerSpawner>();
            }
            else if (playerSpawner.transform.parent == null)
            {
                playerSpawner.transform.SetParent(mgmt.transform);
            }

            var legsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/LegsPlayer.prefab");
            var torsoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/TorsoPlayer.prefab");
            var spawnerSo = new SerializedObject(playerSpawner);
            if (legsPrefab != null) spawnerSo.FindProperty("_legsPrefab").objectReferenceValue = legsPrefab;
            if (torsoPrefab != null) spawnerSo.FindProperty("_torsoPrefab").objectReferenceValue = torsoPrefab;
            spawnerSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnMgr;
            spawnerSo.FindProperty("_autoSpawnOnStart").boolValue = true;
            spawnerSo.FindProperty("_spawnLegsInEditor").boolValue = true;
            spawnerSo.FindProperty("_spawnTorsoInEditor").boolValue = true;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. [ROBOT_COORDINATOR] (RobotCoordinator for Fusion / Separation)
            var robotCoordinator = Object.FindFirstObjectByType<RobotCoordinator>();
            if (robotCoordinator == null)
            {
                var coordGo = new GameObject("[ROBOT_COORDINATOR]");
                coordGo.transform.SetParent(mgmt.transform);
                robotCoordinator = coordGo.AddComponent<RobotCoordinator>();
            }
            else if (robotCoordinator.transform.parent == null)
            {
                robotCoordinator.transform.SetParent(mgmt.transform);
            }

            // 5. [CHECKPOINT_SYSTEM] & [RESPAWN_COORDINATOR]
            var cpSys = Object.FindFirstObjectByType<CheckpointSystem>();
            if (cpSys == null)
            {
                var cpSysGo = new GameObject("[CHECKPOINT_SYSTEM]");
                cpSysGo.transform.SetParent(mgmt.transform);
                cpSys = cpSysGo.AddComponent<CheckpointSystem>();
            }
            var cpSysSo = new SerializedObject(cpSys);
            cpSysSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnMgr;
            cpSysSo.ApplyModifiedPropertiesWithoutUndo();

            var respawnCoord = Object.FindFirstObjectByType<RespawnCoordinator>();
            if (respawnCoord == null)
            {
                var respawnCoordGo = new GameObject("[RESPAWN_COORDINATOR]");
                respawnCoordGo.transform.SetParent(mgmt.transform);
                respawnCoord = respawnCoordGo.AddComponent<RespawnCoordinator>();
            }
            var respawnCoordSo = new SerializedObject(respawnCoord);
            respawnCoordSo.FindProperty("_checkpointSystem").objectReferenceValue = cpSys;
            respawnCoordSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. Networking Hierarchy
            var netManager = Object.FindFirstObjectByType<NetworkManager>();
            GameObject netMgrGo;
            if (netManager == null)
            {
                netMgrGo = new GameObject("[NETWORK_MANAGER]");
                SceneManager.MoveGameObjectToScene(netMgrGo, scene);
                netManager = netMgrGo.AddComponent<NetworkManager>();
            }
            else
            {
                netMgrGo = netManager.gameObject;
                if (netMgrGo.transform.parent != null)
                {
                    netMgrGo.transform.SetParent(null);
                }
            }

            if (!netMgrGo.TryGetComponent<UDPTransport>(out _))
            {
                netMgrGo.AddComponent<UDPTransport>();
            }

            var defaultRules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/PurrNet/Defaults/NetworkRules/ServerStrict.asset");
            var defaultVisibility = AssetDatabase.LoadAssetAtPath<NetworkVisibilityRuleSet>("Assets/PurrNet/Defaults/VisibilitySets/AlwaysVisible.asset");
            var defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/_Project/Network/NetworkPrefabs.asset");

            var netSo = new SerializedObject(netManager);
            netSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
            if (defaultRules != null) netSo.FindProperty("_networkRules").objectReferenceValue = defaultRules;
            if (defaultVisibility != null) netSo.FindProperty("_visibilityRules").objectReferenceValue = defaultVisibility;
            if (defaultPrefabs != null) netSo.FindProperty("_networkPrefabs").objectReferenceValue = defaultPrefabs;
            netSo.ApplyModifiedPropertiesWithoutUndo();

            var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
            if (relay == null)
            {
                var relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
                relayGo.transform.SetParent(net.transform);
                relay = relayGo.AddComponent<NetworkEventRelay>();
            }

            var audioRelay = Object.FindFirstObjectByType<Game.Network.Audio.NetworkAudioRelay>();
            if (audioRelay == null)
            {
                var audioRelayGo = new GameObject("[NETWORK_AUDIO_RELAY]");
                audioRelayGo.transform.SetParent(net.transform);
                audioRelay = audioRelayGo.AddComponent<Game.Network.Audio.NetworkAudioRelay>();
            }

            var gameScope = Object.FindFirstObjectByType<GameLifetimeScope>();
            GameObject gameScopeGo;
            if (gameScope == null)
            {
                gameScopeGo = new GameObject("[GAME_LIFETIME_SCOPE]");
                gameScopeGo.transform.SetParent(net.transform);
                gameScope = gameScopeGo.AddComponent<GameLifetimeScope>();
            }
            else
            {
                gameScopeGo = gameScope.gameObject;
            }

            if (!gameScopeGo.TryGetComponent<LevelManager>(out var levelManager))
            {
                levelManager = gameScopeGo.AddComponent<LevelManager>();
            }

            if (!gameScopeGo.TryGetComponent<Game.Core.Audio.AudioService>(out var audioService))
            {
                audioService = gameScopeGo.AddComponent<Game.Core.Audio.AudioService>();
            }

            var audioSo = new SerializedObject(audioService);
            var menuClipProp = audioSo.FindProperty("_menuMusicClip");
            if (menuClipProp != null && menuClipProp.objectReferenceValue == null)
            {
                menuClipProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial (Take 2).mp3");
            }
            var levelClipProp = audioSo.FindProperty("_levelMusicClip");
            if (levelClipProp != null && levelClipProp.objectReferenceValue == null)
            {
                levelClipProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial.mp3");
            }
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/LobbyNetworkController.prefab");
            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            var lobbyPrefabProp = gameScopeSo.FindProperty("_lobbyNetworkControllerPrefab");
            if (lobbyPrefabProp != null) lobbyPrefabProp.objectReferenceValue = lobbyPrefab;
            gameScopeSo.ApplyModifiedPropertiesWithoutUndo();

            var boot = Object.FindFirstObjectByType<Bootstrapper>();
            if (boot == null)
            {
                var bootGo = new GameObject("[BOOTSTRAPPER]");
                bootGo.transform.SetParent(net.transform);
                bootGo.AddComponent<Bootstrapper>();
            }

            var hud = Object.FindFirstObjectByType<ConnectionHUD>();
            if (hud == null)
            {
                var hudGo = new GameObject("[CONNECTION_HUD]");
                hudGo.transform.SetParent(net.transform);
                hudGo.AddComponent<ConnectionHUD>();
            }

            // 7. Lighting & Camera Rig
            var dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(lighting.transform);
                lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.color = new Color(1f, 0.96f, 0.90f);
                dirLight.intensity = 1.3f;
                dirLight.shadows = LightShadows.Soft;
            }

            var mainCam = UnityEngine.Camera.main;
            if (mainCam == null)
            {
                mainCam = Object.FindFirstObjectByType<UnityEngine.Camera>();
            }
            GameObject camGo;
            if (mainCam == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(lighting.transform);
                camGo.transform.position = new Vector3(0f, 15f, -10f);
                camGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
                mainCam = camGo.AddComponent<UnityEngine.Camera>();
                mainCam.fieldOfView = 60f;
            }
            else
            {
                camGo = mainCam.gameObject;
                camGo.tag = "MainCamera";
            }

            if (!camGo.TryGetComponent<AudioListener>(out _))
            {
                camGo.AddComponent<AudioListener>();
            }
            if (!camGo.TryGetComponent<CinemachineBrain>(out _))
            {
                camGo.AddComponent<CinemachineBrain>();
            }

            var cameraRigGo = GameObject.Find("[CAMERA_RIG]");
            if (cameraRigGo == null)
            {
                cameraRigGo = new GameObject("[CAMERA_RIG]");
                cameraRigGo.transform.SetParent(mgmt.transform);
            }

            var targetGroup = Object.FindFirstObjectByType<CinemachineTargetGroup>();
            GameObject targetGroupGo;
            if (targetGroup == null)
            {
                targetGroupGo = new GameObject("CM_TargetGroup");
                targetGroupGo.transform.SetParent(cameraRigGo.transform);
                targetGroup = targetGroupGo.AddComponent<CinemachineTargetGroup>();
            }
            else
            {
                targetGroupGo = targetGroup.gameObject;
            }

            if (!targetGroupGo.TryGetComponent<TargetGroupCoordinator>(out var targetGroupCoord))
            {
                targetGroupCoord = targetGroupGo.AddComponent<TargetGroupCoordinator>();
            }

            var targetCoordSo = new SerializedObject(targetGroupCoord);
            targetCoordSo.FindProperty("_targetGroup").objectReferenceValue = targetGroup;
            targetCoordSo.ApplyModifiedPropertiesWithoutUndo();

            var cmCam = Object.FindFirstObjectByType<CinemachineCamera>();
            GameObject cmCamGo;
            if (cmCam == null)
            {
                cmCamGo = new GameObject("CM_AdaptiveCamera");
                cmCamGo.transform.SetParent(cameraRigGo.transform);
                cmCamGo.transform.position = new Vector3(0f, 15f, -10f);
                cmCamGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
                cmCam = cmCamGo.AddComponent<CinemachineCamera>();
                cmCam.Lens.FieldOfView = 55f;
            }
            else
            {
                cmCamGo = cmCam.gameObject;
            }

            cmCam.Target.TrackingTarget = targetGroupGo.transform;
            cmCam.Target.LookAtTarget = targetGroupGo.transform;

            if (!cmCamGo.TryGetComponent<CinemachineGroupFraming>(out var groupFraming))
            {
                groupFraming = cmCamGo.AddComponent<CinemachineGroupFraming>();
                groupFraming.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
                groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyThenZoom;
                groupFraming.FramingSize = 0.52f;
                groupFraming.Damping = 2f;
            }

            if (!cmCamGo.TryGetComponent<CinemachineImpulseListener>(out _))
            {
                cmCamGo.AddComponent<CinemachineImpulseListener>();
            }

            if (!cmCamGo.TryGetComponent<CinemachineImpulseSource>(out var impulseSource))
            {
                impulseSource = cmCamGo.AddComponent<CinemachineImpulseSource>();
            }

            if (!cameraRigGo.TryGetComponent<CameraController>(out var cameraController))
            {
                cameraController = cameraRigGo.AddComponent<CameraController>();
            }

            if (!cameraRigGo.TryGetComponent<WallTransparencyCoordinator>(out _))
            {
                cameraRigGo.AddComponent<WallTransparencyCoordinator>();
            }

            var camCtrlSo = new SerializedObject(cameraController);
            camCtrlSo.FindProperty("_cinemachineCamera").objectReferenceValue = cmCam;
            camCtrlSo.FindProperty("_groupFraming").objectReferenceValue = groupFraming;
            camCtrlSo.FindProperty("_targetCoordinator").objectReferenceValue = targetGroupCoord;
            camCtrlSo.FindProperty("_impulseSource").objectReferenceValue = impulseSource;
            camCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            // 8. Room System & RoomLifetimeScope
            var roomScope = Object.FindFirstObjectByType<RoomLifetimeScope>();
            GameObject roomSysGo;
            if (roomScope == null)
            {
                roomSysGo = new GameObject("[ROOM_SYSTEM]");
                roomSysGo.transform.SetParent(mgmt.transform);
                roomScope = roomSysGo.AddComponent<RoomLifetimeScope>();
            }
            else
            {
                roomSysGo = roomScope.gameObject;
                if (roomSysGo.transform.parent != mgmt.transform)
                {
                    roomSysGo.transform.SetParent(mgmt.transform);
                }
            }

            if (!roomSysGo.TryGetComponent<RoomController>(out var roomCtrl))
            {
                roomCtrl = roomSysGo.AddComponent<RoomController>();
            }

            if (!roomSysGo.TryGetComponent<RoomCompletion>(out _))
            {
                roomSysGo.AddComponent<RoomCompletion>();
            }

            var roomCtrlSo = new SerializedObject(roomCtrl);
            roomCtrlSo.FindProperty("_roomIndex").intValue = levelDef.LevelIndex;
            roomCtrlSo.FindProperty("_roomName").stringValue = string.IsNullOrEmpty(levelDef.LevelName) ? scene.name : levelDef.LevelName;
            roomCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            var roomScopeSo = new SerializedObject(roomScope);
            roomScopeSo.FindProperty("_roomController").objectReferenceValue = roomCtrl;
            roomScopeSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnMgr;
            roomScopeSo.FindProperty("_playerSpawner").objectReferenceValue = playerSpawner;
            roomScopeSo.FindProperty("_robotCoordinator").objectReferenceValue = robotCoordinator;
            roomScopeSo.FindProperty("_cameraController").objectReferenceValue = cameraController;
            roomScopeSo.FindProperty("_networkEventRelay").objectReferenceValue = relay;
            roomScopeSo.FindProperty("_networkAudioRelay").objectReferenceValue = audioRelay;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("TPOB/Tools/Build Escena 1 from Level Editor Design", priority = 10)]
        public static void BuildEscena1Direct()
        {
            const string targetPath = "Assets/_Project/Scenes/escena1.unity";
            Debug.Log($"<color=#50fa7b>[LevelBuilderWindow] Iniciando construcción cooperativa de 6 Zonas en '{targetPath}'...</color>");

            Scene scene;
            var active = SceneManager.GetActiveScene();
            if (active.path == targetPath)
            {
                scene = active;
            }
            else
            {
                scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            }
            SceneManager.SetActiveScene(scene);

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var r = roots[i];
                if (r.name == "Level_Root" || r.name.StartsWith("--- ") || r.name == "Directional Light" || r.name == "Main Camera" || r.name == "[NETWORK_MANAGER]")
                {
                    DestroyImmediate(r);
                }
            }

            var levelRoot = new GameObject("Level_Root");
            SceneManager.MoveGameObjectToScene(levelRoot, scene);
            var levelDef = levelRoot.AddComponent<LevelDefinition>();

            var geo = new GameObject("--- GEOMETRY ---");
            geo.transform.SetParent(levelRoot.transform);
            var interact = new GameObject("--- INTERACTABLES ---");
            interact.transform.SetParent(levelRoot.transform);
            var condRoot = new GameObject("--- CONDITIONS ---");
            condRoot.transform.SetParent(levelRoot.transform);

            EnsureScenePlayableHierarchy(scene, new Vector3(-4f, 0.1f, 4f), new Vector3(4f, 0.1f, 4f));

            var p = LoadModularPalette();

            BuildZone1_AsymmetricAccreditation(geo.transform, interact.transform, condRoot.transform, p);
            BuildZone2_KickPartnerAndHeavyDoor(geo.transform, interact.transform, condRoot.transform, p);
            BuildZone3_MagneticBootsAndCeiling(geo.transform, interact.transform, condRoot.transform, p);
            BuildZone4_SeeSawCounterweight(geo.transform, interact.transform, condRoot.transform, p);
            BuildZone5_ElasticCatapultChasm(geo.transform, interact.transform, condRoot.transform, p);
            BuildZone6_ReactorCoreFinale(geo.transform, interact.transform, condRoot.transform, p);

            levelDef.SetGroundY(0f);
            levelDef.SetDeadzoneY(-6f);
            levelDef.SetDeadzonePadding(40f);
            levelDef.GenerateSubterraneanDeadzone();
            levelDef.AutoDiscoverElements();

            LevelValidator.Validate(levelDef);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#50fa7b>[LevelBuilderWindow] ¡escena1.unity (6 Zonas Cooperativas, 0 Cubos Primitivos) construida y guardada en disco!</color>");
        }

        private static ModularPalette LoadModularPalette()
        {
            const string ekPath = "Assets/Assets importado/EKstudio/LowPoly Factory Machine Pack Demo/Prefabs";
            const string scifiPath = "Assets/Assets importado/Dzeruza/MinimalScifiPack/3DModels";

            return new ModularPalette
            {
                Floor = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Props/Ground.prefab"),
                Wall = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Walls/Wall.prefab"),
                WallDoorL = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Walls/WallDoorL.prefab"),
                WallDoorR = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Walls/WallDoorR.prefab"),
                Barrier = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Props/Barrier.prefab"),
                Cone = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Props/Cone.prefab"),
                Pallet = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Props/PaletteWooden.prefab"),
                Conveyor = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Conveyor/Conveyor_Long.prefab"),
                Machine = AssetDatabase.LoadAssetAtPath<GameObject>($"{ekPath}/Machine/Machn_2.prefab"),
                CeilingPlate4x4 = AssetDatabase.LoadAssetAtPath<GameObject>($"{scifiPath}/Ceilings/Prefabs/SM_Ceiling_Plate_4x4M.prefab"),
                CeilingPlate2x4 = AssetDatabase.LoadAssetAtPath<GameObject>($"{scifiPath}/Ceilings/Prefabs/SM_Ceiling_Plate_2x4M.prefab"),
                TrimWall3x4 = AssetDatabase.LoadAssetAtPath<GameObject>($"{scifiPath}/Walls/Prefabs/SM_Trim_Wall_3x4M_B1.prefab"),
                BoxGrabbable = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets importado/Pixel Boxes - lowpoly/Prefabs/Pixel_box_01.prefab"),
                BoxHeavy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets importado/Pixel Boxes - lowpoly/Prefabs/Pixel_box_02.prefab"),
                Lever = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/LeverSwitch.prefab"),
                SlidingDoor = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/SlidingDoor.prefab"),
                WeightPlatform = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/WeightPlatform.prefab"),
                TargetButton = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/TargetButton.prefab"),
                BreakableWall = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/BreakableWall.prefab"),
                SeeSaw = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/SeeSawPlatform.prefab"),
                MagneticWall = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/MagneticWall.prefab"),
                MagneticKey = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/MagneticKey.prefab"),
                PuzzleSocket = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/PuzzleSocket.prefab"),
                HeavyDoor = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/HeavyMagneticDoor.prefab"),
                Catapult = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/CatapultMechanism.prefab"),
                Checkpoint = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabsPath}/Checkpoint.prefab")
            };
        }

        private static void SetupSceneNetworking(GameObject net)
        {
            var netMgrGo = new GameObject("[NETWORK_MANAGER]");
            netMgrGo.transform.SetParent(net.transform);
            var netManager = netMgrGo.AddComponent<NetworkManager>();
            netMgrGo.AddComponent<UDPTransport>();

            var defaultRules = AssetDatabase.LoadAssetAtPath<NetworkRules>("Assets/PurrNet/Defaults/NetworkRules/ServerStrict.asset");
            var defaultVisibility = AssetDatabase.LoadAssetAtPath<NetworkVisibilityRuleSet>("Assets/PurrNet/Defaults/VisibilitySets/AlwaysVisible.asset");
            var defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabs>("Assets/_Project/Network/NetworkPrefabs.asset");

            var netSo = new SerializedObject(netManager);
            netSo.FindProperty("_dontDestroyOnLoad").boolValue = true;
            if (defaultRules != null) netSo.FindProperty("_networkRules").objectReferenceValue = defaultRules;
            if (defaultVisibility != null) netSo.FindProperty("_visibilityRules").objectReferenceValue = defaultVisibility;
            if (defaultPrefabs != null) netSo.FindProperty("_networkPrefabs").objectReferenceValue = defaultPrefabs;
            netSo.ApplyModifiedPropertiesWithoutUndo();

            var relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
            relayGo.transform.SetParent(net.transform);
            relayGo.AddComponent<NetworkEventRelay>();

            var audioRelayGo = new GameObject("[NETWORK_AUDIO_RELAY]");
            audioRelayGo.transform.SetParent(net.transform);
            audioRelayGo.AddComponent<Game.Network.Audio.NetworkAudioRelay>();

            var gameScopeGo = new GameObject("[GAME_LIFETIME_SCOPE]");
            gameScopeGo.transform.SetParent(net.transform);
            var gameScope = gameScopeGo.AddComponent<GameLifetimeScope>();
            var levelManager = gameScopeGo.AddComponent<LevelManager>();

            var roomScopeGo = new GameObject("[ROOM_LIFETIME_SCOPE]");
            roomScopeGo.transform.SetParent(net.transform);
            roomScopeGo.AddComponent<RoomLifetimeScope>();

            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/LobbyNetworkController.prefab");

            var bootGo = new GameObject("[BOOTSTRAPPER]");
            bootGo.transform.SetParent(net.transform);
            bootGo.AddComponent<Bootstrapper>();

            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            var lobbyPrefabProp = gameScopeSo.FindProperty("_lobbyNetworkControllerPrefab");
            if (lobbyPrefabProp != null) lobbyPrefabProp.objectReferenceValue = lobbyPrefab;
            gameScopeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupSceneLightingAndCamera(GameObject lighting, GameObject mgmt)
        {
            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(lighting.transform);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(lighting.transform);
            camGo.transform.position = new Vector3(0f, 15f, -10f);
            camGo.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            var cameraRigGo = new GameObject("[CAMERA_RIG]");
            cameraRigGo.transform.SetParent(mgmt.transform);

            var targetGroupGo = new GameObject("CM_TargetGroup");
            targetGroupGo.transform.SetParent(cameraRigGo.transform);
            var targetGroup = targetGroupGo.AddComponent<CinemachineTargetGroup>();

            var vcamGo = new GameObject("CM_VirtualCamera");
            vcamGo.transform.SetParent(cameraRigGo.transform);
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Target.LookAtTarget = targetGroupGo.transform;
            vcam.Target.TrackingTarget = targetGroupGo.transform;

            var coordGo = new GameObject("TargetGroupCoordinator");
            coordGo.transform.SetParent(cameraRigGo.transform);
            var coordinator = coordGo.AddComponent<TargetGroupCoordinator>();
            var coordSo = new SerializedObject(coordinator);
            coordSo.FindProperty("_targetGroup").objectReferenceValue = targetGroup;
            coordSo.ApplyModifiedPropertiesWithoutUndo();

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Assets importado/Clunky Electro-Funk Industrial.mp3");
            if (clip != null)
            {
                var audioGo = new GameObject("[AMBIENT_MUSIC]");
                audioGo.transform.SetParent(lighting.transform);
                var src = audioGo.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = true;
                src.volume = 0.28f;
                src.spatialBlend = 0f;
            }
        }

        // =================================================================================================
        // ZONA 1 (Z: 0m a 40m, Y: 0m): ACREDITACIÓN ASIMÉTRICA (Conducto Bajo + BreakableWall + Fusión)
        // =================================================================================================
        private static void BuildZone1_AsymmetricAccreditation(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone1_AsymmetricAccreditation");
            zone.transform.SetParent(geo);

            CreateModularFloorSection(zone.transform, "Floor_Z1", -10f, 10f, 0f, 40f, 0f, p.Floor, p.Wall, false, false);
            CreateModularWallSpanX(zone.transform, "Wall_Z1_Back", -10f, 10f, 0f, 0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z1_Left", -10f, 0f, 40f, 0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z1_Right", 10f, 0f, 40f, 0f, p.Wall);

            // Tabique central que separa a Piernas (Izquierda) y Torso (Derecha) desde Z=0 hasta Z=20
            CreateModularSideWallZ(zone.transform, "DividingWall_Z1_Center", 0f, 0f, 20f, 0f, p.Wall);

            // Carril Izquierdo (Piernas): Muro destructible a patadas en Z=14
            CreateModularWallSpanX(zone.transform, "Wall_Z1_LegsWingL", -10f, -6f, 14f, 0f, p.Wall);
            var breakWallLegs = (GameObject)PrefabUtility.InstantiatePrefab(p.BreakableWall, interact);
            breakWallLegs.name = "BreakableWall_Z1_LegsKick";
            breakWallLegs.transform.position = new Vector3(-4f, 0f, 14f);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(-5.5f, 0f, 12f), Quaternion.identity);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(-2.5f, 0f, 12f), Quaternion.identity);

            // Compuerta interna en Z=20 para que Torso libere el carril de Piernas tras cruzar el conducto bajo
            CreateModularWallSpanX(zone.transform, "Wall_Z1_InternalWing", -10f, -6f, 20f, 0f, p.Wall);
            var internalDoorGo = (GameObject)PrefabUtility.InstantiatePrefab(p.SlidingDoor, interact);
            internalDoorGo.name = "SlidingDoor_Z1_InternalReunion";
            internalDoorGo.transform.position = new Vector3(-3f, 0f, 20f);
            var internalDoor = internalDoorGo.GetComponent<SlidingDoor>();

            // Carril Derecho (Torso): Barrera industrial baja (1.15m de vano) en Z=14 para obligar a Crawl
            CreateLowCrawlBarrier(zone.transform, "LowCrawlBarrier_Z1_Torso", 0f, 10f, 14f, 0f, p.TrimWall3x4, p.Barrier);

            var leverReunionGo = (GameObject)PrefabUtility.InstantiatePrefab(p.Lever, interact);
            leverReunionGo.name = "LeverSwitch_Z1_OpenInternal";
            leverReunionGo.transform.position = new Vector3(5f, 0f, 18f);
            var leverReunion = leverReunionGo.GetComponent<LeverMechanism>();
            CreateLightPoint(leverReunionGo.transform, new Vector3(0f, 1.8f, 0f), new Color(1f, 0.75f, 0.3f), 1.6f, 6f);

            var cableInternal = CreateEnergyCable(interact, "Conduit_Z1_Internal", new Vector3(5f, 0.05f, 18f), new Vector3(-3f, 0.05f, 20f));
            var triggerInternal = internalDoorGo.AddComponent<DoorMechanismTrigger>();
            triggerInternal.Configure(leverReunion, internalDoor, autoClose: false, cable: cableInternal);

            // Parte 1B (Z=20 a Z=36): Fusión obligatoria (Peso 100kg + Estante alto con caja para Diana)
            CreateModularCatwalk(zone.transform, "HighShelf_Z1_Crate", -8f, -4f, 24f, 28f, 1.6f, p.CeilingPlate4x4);
            CreatePalletSteps(zone.transform, "PalletSteps_Z1", -6f, 21f, 0f, 2, p.Pallet);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z1_Shelf", new Vector3(-6f, 2.1f, 26f), 25f, true);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z1_Spare", new Vector3(6f, 0.6f, 26f), 25f, true);

            var wpFusionGo = (GameObject)PrefabUtility.InstantiatePrefab(p.WeightPlatform, interact);
            wpFusionGo.name = "WeightPlatform_Z1_Fusion100kg";
            wpFusionGo.transform.position = new Vector3(0f, 0.05f, 28f);
            var wpFusion = wpFusionGo.GetComponent<WeightPlatform>();
            SetRequiredMass(wpFusion, 95f);

            // Soporte modular y Diana alta en Z=34
            CreateModularWallSpanX(zone.transform, "TargetMount_Z1", 4f, 8f, 34f, 0f, p.Wall);
            var targetZ1Go = (GameObject)PrefabUtility.InstantiatePrefab(p.TargetButton, interact);
            targetZ1Go.name = "TargetButton_Z1_High";
            targetZ1Go.transform.position = new Vector3(6f, 3.2f, 33.5f);
            targetZ1Go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var targetZ1 = targetZ1Go.GetComponent<TargetButton>();
            CreateLightPoint(targetZ1Go.transform, new Vector3(0f, 0f, -0.6f), new Color(1f, 0.25f, 0.2f), 1.8f, 6f);

            var doorZ1 = CreateModularGateWall(zone.transform, interact, "SlidingDoor_Zone1_Exit", 36f, 0f, p);

            var cableZ1Wp = CreateEnergyCable(interact, "Conduit_Z1_Scale", new Vector3(0f, 0.05f, 28f), new Vector3(-0.5f, 0.05f, 36f));
            AttachCableIndicator(cableZ1Wp.gameObject, wpFusion, cableZ1Wp);

            var cableZ1Btn = CreateEnergyCable(interact, "Conduit_Z1_Target", new Vector3(6f, 0.05f, 33.5f), new Vector3(0.5f, 0.05f, 36f));
            AttachCableIndicator(cableZ1Btn.gameObject, targetZ1, cableZ1Btn);

            WireCompositeDoor(condRoot, "Zone1_Completion", doorZ1, new ConditionBase[]
            {
                CreateMechanismCondition(condRoot, "Cond_Z1_FusionScale", wpFusion),
                CreateMechanismCondition(condRoot, "Cond_Z1_TargetHit", targetZ1)
            });

            var cp1 = CreateLocalCheckpoint(interact, p.Checkpoint, new Vector3(0f, 0.05f, 38f), 10, "Checkpoint_Zone1");
            CreateLightSpot(cp1.transform, new Vector3(0f, 5f, 0f), Vector3.down, new Color(0f, 0.9f, 1f), 2.2f, 9f, 55f);
        }

        // =================================================================================================
        // ZONA 2 (Z: 40m a 88m, Y: 0m): ¡PATEA AL COMPAÑERO! (Despeje de Torso + Imán + HeavyMagneticDoor)
        // =================================================================================================
        private static void BuildZone2_KickPartnerAndHeavyDoor(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone2_KickPartnerAndHeavyDoor");
            zone.transform.SetParent(geo);

            CreateModularSideWallZ(zone.transform, "Wall_Z2_Left", -10f, 40f, 88f, 0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z2_Right", 10f, 40f, 88f, 0f, p.Wall);

            // Plataforma de Despegue (Z=40 a Z=48)
            CreateModularFloorSection(zone.transform, "Floor_Z2_Takeoff", -10f, 10f, 40f, 48f, 0f, p.Floor, p.Wall, false, true);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(-3f, 0f, 47.6f), Quaternion.identity);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(3f, 0f, 47.6f), Quaternion.identity);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(-1.2f, 0f, 47.2f), Quaternion.identity);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(1.2f, 0f, 47.2f), Quaternion.identity);

            // Foso de 12 metros (Z=48 a Z=60): Pasarela lateral izquierda cerrada por compuerta hasta que Torso sea pateado al otro lado
            CreateLightPoint(zone.transform, new Vector3(0f, -3.5f, 54f), new Color(0.95f, 0.15f, 0.15f), 2.5f, 16f);
            CreateModularCatwalk(zone.transform, "Bridge_Z2_LeftCatwalk", -9f, -5f, 48f, 60f, 0f, p.CeilingPlate4x4);

            var bridgeGateGo = (GameObject)PrefabUtility.InstantiatePrefab(p.SlidingDoor, interact);
            bridgeGateGo.name = "SlidingDoor_Z2_LeftBridge";
            bridgeGateGo.transform.position = new Vector3(-7f, 0f, 48.5f);
            var bridgeGate = bridgeGateGo.GetComponent<SlidingDoor>();

            // Isla de Aterrizaje y Extracción Magnética (Z=60 a Z=88)
            CreateModularFloorSection(zone.transform, "Floor_Z2_LandingAndMagnet", -10f, 10f, 60f, 88f, 0f, p.Floor, p.Wall, true, false);

            var leverBridgeGo = (GameObject)PrefabUtility.InstantiatePrefab(p.Lever, interact);
            leverBridgeGo.name = "LeverSwitch_Z2_UnlockBridge";
            leverBridgeGo.transform.position = new Vector3(4f, 0f, 62f);
            var leverBridge = leverBridgeGo.GetComponent<LeverMechanism>();
            CreateLightPoint(leverBridgeGo.transform, new Vector3(0f, 1.8f, 0f), new Color(1f, 0.75f, 0.3f), 1.6f, 6f);

            var cableBridge = CreateEnergyCable(interact, "Conduit_Z2_Bridge", new Vector3(4f, 0.05f, 62f), new Vector3(-7f, 0.05f, 48.5f));
            var triggerBridge = bridgeGateGo.AddComponent<DoorMechanismTrigger>();
            triggerBridge.Configure(leverBridge, bridgeGate, autoClose: false, cable: cableBridge);

            // Jaula lateral con MagneticKey (Torso la atrae con su Electroimán ApplyMagnet a través de las barreras)
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(-6.5f, 0f, 68f), Quaternion.Euler(0f, 90f, 0f));
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(-6.5f, 0f, 72f), Quaternion.Euler(0f, 90f, 0f));
            var keyZ2Go = (GameObject)PrefabUtility.InstantiatePrefab(p.MagneticKey, interact);
            keyZ2Go.name = "MagneticKey_Z2_Caged";
            keyZ2Go.transform.position = new Vector3(-8.2f, 0.6f, 70f);
            CreateLightPoint(keyZ2Go.transform, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.65f, 0.1f), 1.6f, 5f);

            var socketZ2Go = (GameObject)PrefabUtility.InstantiatePrefab(p.PuzzleSocket, interact);
            socketZ2Go.name = "PuzzleSocket_Z2_Unlock";
            socketZ2Go.transform.position = new Vector3(-4f, 0f, 74f);
            var socketZ2 = socketZ2Go.GetComponent<PuzzleSocket>();

            var preDoorZ2 = CreateModularGateWall(zone.transform, interact, "SlidingDoor_Z2_SocketGate", 78f, 0f, p);
            var cableSocketZ2 = CreateEnergyCable(interact, "Conduit_Z2_Socket", new Vector3(-4f, 0.05f, 74f), new Vector3(0f, 0.05f, 78f));
            var triggerSocketZ2 = preDoorZ2.gameObject.AddComponent<DoorMechanismTrigger>();
            triggerSocketZ2.Configure(socketZ2, preDoorZ2, autoClose: false, cable: cableSocketZ2);

            // Compuerta Magnética Pesada en Z=83 (Fusión: Torso engancha Imán + Piernas arrastra hacia atrás)
            CreateModularWallSpanX(zone.transform, "HeavyFrame_Z2_L", -10f, -2f, 83f, 0f, p.Wall);
            CreateModularWallSpanX(zone.transform, "HeavyFrame_Z2_R", 2f, 10f, 83f, 0f, p.Wall);
            var heavyDoorGo = (GameObject)PrefabUtility.InstantiatePrefab(p.HeavyDoor, interact);
            heavyDoorGo.name = "HeavyMagneticDoor_Z2";
            heavyDoorGo.transform.position = new Vector3(0f, 0f, 83f);
            CreateLightPoint(heavyDoorGo.transform, new Vector3(0f, 3.2f, -1f), new Color(0.2f, 0.85f, 1f), 1.8f, 7f);

            var cp2 = CreateLocalCheckpoint(interact, p.Checkpoint, new Vector3(0f, 0.05f, 86f), 20, "Checkpoint_Zone2");
            CreateLightSpot(cp2.transform, new Vector3(0f, 5f, 0f), Vector3.down, new Color(0f, 0.9f, 1f), 2.2f, 9f, 55f);
        }

        // =================================================================================================
        // ZONA 3 (Z: 88m a 136m, Y: 0m): GRAVEDAD AZUL (Botas Magnéticas de Piernas + Techo Energizado)
        // =================================================================================================
        private static void BuildZone3_MagneticBootsAndCeiling(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone3_MagneticBootsAndCeiling");
            zone.transform.SetParent(geo);

            CreateModularSideWallZ(zone.transform, "Wall_Z3_Left", -10f, 88f, 136f, 0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z3_Right", 10f, 88f, 136f, 0f, p.Wall);

            // Plataforma de Entrada (Z=88 a Z=96)
            CreateModularFloorSection(zone.transform, "Floor_Z3_Start", -10f, 10f, 88f, 96f, 0f, p.Floor, p.Wall, false, true);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(-2f, 0f, 95.6f), Quaternion.identity);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(2f, 0f, 95.6f), Quaternion.identity);

            // Palanca de Torso que energiza el techo magnético sobre el abismo (Z=96 a Z=112)
            var leverMagPowerGo = (GameObject)PrefabUtility.InstantiatePrefab(p.Lever, interact);
            leverMagPowerGo.name = "LeverSwitch_Z3_CeilingPower";
            leverMagPowerGo.transform.position = new Vector3(5f, 0f, 93f);
            var leverMagPower = leverMagPowerGo.GetComponent<LeverMechanism>();
            CreateLightPoint(leverMagPowerGo.transform, new Vector3(0f, 1.8f, 0f), new Color(0.1f, 0.8f, 1f), 1.8f, 6f);

            // Pista vertical de MagneticWall para las Botas Magnéticas de Piernas
            var magWallVertGo = (GameObject)PrefabUtility.InstantiatePrefab(p.MagneticWall, interact);
            magWallVertGo.name = "MagneticWall_Z3_VerticalClimb";
            magWallVertGo.transform.position = new Vector3(-9.4f, 2.5f, 93f);
            magWallVertGo.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            magWallVertGo.transform.localScale = new Vector3(5f, 5f, 0.5f);
            ConfigureMagneticWall(magWallVertGo.GetComponent<MagneticWall>(), true, null);

            // Techo Magnético Invertido sobre el foso (Z=96 a Z=112), controlado por la palanca de Torso
            var magCeilingGo = (GameObject)PrefabUtility.InstantiatePrefab(p.MagneticWall, interact);
            magCeilingGo.name = "MagneticCeiling_Z3_Bridge";
            magCeilingGo.transform.position = new Vector3(-6.5f, 5.2f, 104f);
            magCeilingGo.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            magCeilingGo.transform.localScale = new Vector3(4.5f, 0.5f, 18f);
            ConfigureMagneticWall(magCeilingGo.GetComponent<MagneticWall>(), false, leverMagPower);
            CreateLightPoint(magCeilingGo.transform, new Vector3(0f, -1.2f, 0f), new Color(0.1f, 0.85f, 1f), 2.2f, 12f);

            CreateEnergyCable(interact, "Conduit_Z3_MagCeiling", new Vector3(5f, 0.05f, 93f), new Vector3(-6.5f, 5.1f, 96f));

            // Pasarela derecha para Torso (bloqueada al final hasta que Piernas cruce por el techo y abra desde arriba)
            CreateModularCatwalk(zone.transform, "Catwalk_Z3_RightTorso", 5f, 9f, 96f, 112f, 0f, p.CeilingPlate4x4);
            var torsoGateGo = (GameObject)PrefabUtility.InstantiatePrefab(p.SlidingDoor, interact);
            torsoGateGo.name = "SlidingDoor_Z3_TorsoPass";
            torsoGateGo.transform.position = new Vector3(7f, 0f, 111.5f);
            var torsoGate = torsoGateGo.GetComponent<SlidingDoor>();

            // Plataforma de Llegada (Z=112 a Z=136) + Balcón Superior Izquierdo (Y=3.0m) donde aterriza Piernas
            CreateModularFloorSection(zone.transform, "Floor_Z3_End", -10f, 10f, 112f, 136f, 0f, p.Floor, p.Wall, true, false);
            CreateModularCatwalk(zone.transform, "HighBalcony_Z3_LegsLanding", -9.5f, -3.5f, 112f, 120f, 3.0f, p.CeilingPlate4x4);

            var leverReleaseTorsoGo = (GameObject)PrefabUtility.InstantiatePrefab(p.Lever, interact);
            leverReleaseTorsoGo.name = "LeverSwitch_Z3_ReleaseTorso";
            leverReleaseTorsoGo.transform.position = new Vector3(-7.5f, 3.0f, 115f);
            var leverReleaseTorso = leverReleaseTorsoGo.GetComponent<LeverMechanism>();

            var cableReleaseTorso = CreateEnergyCable(interact, "Conduit_Z3_ReleaseTorso", new Vector3(-7.5f, 3.05f, 115f), new Vector3(7f, 0.05f, 111.5f));
            var triggerReleaseTorso = torsoGateGo.AddComponent<DoorMechanismTrigger>();
            triggerReleaseTorso.Configure(leverReleaseTorso, torsoGate, autoClose: false, cable: cableReleaseTorso);

            // Caja pesada de 80kg en el borde del balcón para que Piernas la patee hacia abajo
            SpawnPixelCrate(p.BoxHeavy, interact, "PushableBox_Z3_Drop80kg", new Vector3(-4.5f, 3.65f, 118f), 80f, false);

            var wpZ3Go = (GameObject)PrefabUtility.InstantiatePrefab(p.WeightPlatform, interact);
            wpZ3Go.name = "WeightPlatform_Z3_HeavyScale";
            wpZ3Go.transform.position = new Vector3(0f, 0.05f, 126f);
            var wpZ3 = wpZ3Go.GetComponent<WeightPlatform>();
            SetRequiredMass(wpZ3, 75f);

            var doorZ3 = CreateModularGateWall(zone.transform, interact, "SlidingDoor_Zone3_Exit", 132f, 0f, p);
            var cableZ3Exit = CreateEnergyCable(interact, "Conduit_Z3_Exit", new Vector3(0f, 0.05f, 126f), new Vector3(0f, 0.05f, 132f));
            var triggerZ3Exit = doorZ3.gameObject.AddComponent<DoorMechanismTrigger>();
            triggerZ3Exit.Configure(wpZ3, doorZ3, autoClose: true, cable: cableZ3Exit);

            var cp3 = CreateLocalCheckpoint(interact, p.Checkpoint, new Vector3(0f, 0.05f, 134f), 30, "Checkpoint_Zone3");
            CreateLightSpot(cp3.transform, new Vector3(0f, 5f, 0f), Vector3.down, new Color(0f, 0.9f, 1f), 2.2f, 9f, 55f);
        }

        // =================================================================================================
        // ZONA 4 (Z: 136m a 180m, Y: 0m -> 2.0m): EQUILIBRIO DINÁMICO (Balancín 14m + Contrapeso 80kg)
        // =================================================================================================
        private static void BuildZone4_SeeSawCounterweight(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone4_SeeSawCounterweight");
            zone.transform.SetParent(geo);

            CreateModularSideWallZ(zone.transform, "Wall_Z4_Left", -10f, 136f, 180f, 0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z4_Right", 10f, 136f, 180f, 0f, p.Wall);

            // Plataforma de Aproximación (Z=136 a Z=144, Y=0m) con cajas de contrapeso
            CreateModularFloorSection(zone.transform, "Floor_Z4_Approach", -10f, 10f, 136f, 144f, 0f, p.Floor, p.Wall, false, true);
            SpawnPixelCrate(p.BoxHeavy, interact, "PushableBox_Z4_Counterweight", new Vector3(-3f, 0.7f, 140f), 80f, false);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z4_ThrowA", new Vector3(4f, 0.6f, 140f), 25f, true);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z4_ThrowB", new Vector3(6f, 0.6f, 140f), 25f, true);

            // Foso de 16m (Z=144 a Z=160) con SeeSawPlatform en el centro (Z=152)
            CreateLightPoint(zone.transform, new Vector3(0f, -3.5f, 152f), new Color(0.95f, 0.2f, 0.15f), 2.5f, 16f);
            var seeSawGo = (GameObject)PrefabUtility.InstantiatePrefab(p.SeeSaw, interact);
            seeSawGo.name = "SeeSawPlatform_Z4_Main";
            seeSawGo.transform.position = new Vector3(0f, 0.8f, 152f);

            // Terraza Elevada de Llegada (Z=160 a Z=180, Cota Y=+2.0m) con muro de contención frontal
            CreateModularFloorSection(zone.transform, "Floor_Z4_HighTerrace", -10f, 10f, 160f, 180f, 2.0f, p.Floor, p.Wall, true, false);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z4_Terrace", new Vector3(5f, 2.6f, 164f), 25f, true);

            var wpZ4Go = (GameObject)PrefabUtility.InstantiatePrefab(p.WeightPlatform, interact);
            wpZ4Go.name = "WeightPlatform_Z4_Terrace";
            wpZ4Go.transform.position = new Vector3(-4f, 2.05f, 168f);
            var wpZ4 = wpZ4Go.GetComponent<WeightPlatform>();
            SetRequiredMass(wpZ4, 45f);

            CreateModularWallSpanX(zone.transform, "TargetMount_Z4", 4f, 8f, 172f, 2.0f, p.Wall);
            var targetZ4Go = (GameObject)PrefabUtility.InstantiatePrefab(p.TargetButton, interact);
            targetZ4Go.name = "TargetButton_Z4_High";
            targetZ4Go.transform.position = new Vector3(6f, 5.0f, 171.5f);
            targetZ4Go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var targetZ4 = targetZ4Go.GetComponent<TargetButton>();
            CreateLightPoint(targetZ4Go.transform, new Vector3(0f, 0f, -0.6f), new Color(1f, 0.25f, 0.2f), 1.8f, 6f);

            var doorZ4 = CreateModularGateWall(zone.transform, interact, "SlidingDoor_Zone4_Exit", 176f, 2.0f, p);

            var cableZ4Wp = CreateEnergyCable(interact, "Conduit_Z4_Scale", new Vector3(-4f, 2.05f, 168f), new Vector3(-0.5f, 2.05f, 176f));
            AttachCableIndicator(cableZ4Wp.gameObject, wpZ4, cableZ4Wp);

            var cableZ4Btn = CreateEnergyCable(interact, "Conduit_Z4_Target", new Vector3(6f, 2.05f, 171.5f), new Vector3(0.5f, 2.05f, 176f));
            AttachCableIndicator(cableZ4Btn.gameObject, targetZ4, cableZ4Btn);

            WireCompositeDoor(condRoot, "Zone4_Completion", doorZ4, new ConditionBase[]
            {
                CreateMechanismCondition(condRoot, "Cond_Z4_Scale", wpZ4),
                CreateMechanismCondition(condRoot, "Cond_Z4_Target", targetZ4)
            });

            var cp4 = CreateLocalCheckpoint(interact, p.Checkpoint, new Vector3(0f, 2.05f, 178f), 40, "Checkpoint_Zone4");
            CreateLightSpot(cp4.transform, new Vector3(0f, 5f, 0f), Vector3.down, new Color(0f, 0.9f, 1f), 2.2f, 9f, 55f);
        }

        // =================================================================================================
        // ZONA 5 (Z: 180m a 228m, Y: 2.0m -> 4.0m): ¡EL CAÑÓN ELÁSTICO! (Set-Piece de Catapulta sobre 20m)
        // =================================================================================================
        private static void BuildZone5_ElasticCatapultChasm(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone5_ElasticCatapultChasm");
            zone.transform.SetParent(geo);

            CreateModularSideWallZ(zone.transform, "Wall_Z5_Left", -10f, 180f, 228f, 2.0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z5_Right", 10f, 180f, 228f, 2.0f, p.Wall);

            // Pista de Tensado de la Catapulta (Z=180 a Z=192, Cota Y=2.0m)
            CreateModularFloorSection(zone.transform, "Floor_Z5_CatapultRunway", -10f, 10f, 180f, 192f, 2.0f, p.Floor, p.Wall, false, true);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(-5.5f, 2.0f, 191.5f), Quaternion.identity);
            SpawnAssetDirect(p.Barrier, zone.transform, new Vector3(5.5f, 2.0f, 191.5f), Quaternion.identity);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(-3.2f, 2.0f, 191.2f), Quaternion.identity);
            SpawnAssetDirect(p.Cone, zone.transform, new Vector3(3.2f, 2.0f, 191.2f), Quaternion.identity);

            // LA CATAPULTA COOPERATIVA en el borde del abismo (Z=190.5m, Y=2.0m)
            var catapultGo = (GameObject)PrefabUtility.InstantiatePrefab(p.Catapult, interact);
            catapultGo.name = "CatapultMechanism_Z5_Main";
            catapultGo.transform.position = new Vector3(0f, 2.0f, 190.5f);
            catapultGo.transform.rotation = Quaternion.identity;
            ConfigureCatapult(catapultGo.GetComponent<CatapultMechanism>(), 6.5f, 35f, 19f, 26f, 28f);
            CreateLightPoint(catapultGo.transform, new Vector3(0f, 2.2f, 0f), new Color(0.2f, 1f, 0.45f), 2.2f, 8f);

            // Gran Abismo de 20 Metros (Z=192 a Z=212)
            CreateLightPoint(zone.transform, new Vector3(0f, -3.0f, 202f), new Color(1f, 0.15f, 0.15f), 3.0f, 22f);

            // Cubierta Elevada de Aterrizaje (Z=212 a Z=228, Cota Y=+4.0m) con muro de contención frontal
            CreateModularFloorSection(zone.transform, "Floor_Z5_HighLandingDeck", -10f, 10f, 212f, 228f, 4.0f, p.Floor, p.Wall, true, false);

            // Muro destructible que custodia la entrada al Reactor Central
            CreateModularWallSpanX(zone.transform, "Wall_Z5_GateL", -10f, -2f, 222f, 4.0f, p.Wall);
            CreateModularWallSpanX(zone.transform, "Wall_Z5_GateR", 2f, 10f, 222f, 4.0f, p.Wall);
            var breakWallZ5 = (GameObject)PrefabUtility.InstantiatePrefab(p.BreakableWall, interact);
            breakWallZ5.name = "BreakableWall_Z5_ReactorEntry";
            breakWallZ5.transform.position = new Vector3(0f, 4.0f, 222f);

            var cp5 = CreateLocalCheckpoint(interact, p.Checkpoint, new Vector3(0f, 4.05f, 225f), 50, "Checkpoint_Zone5");
            CreateLightSpot(cp5.transform, new Vector3(0f, 5f, 0f), Vector3.down, new Color(0f, 0.9f, 1f), 2.2f, 9f, 55f);
        }

        // =================================================================================================
        // ZONA 6 (Z: 228m a 268m, Y: 4.0m): EL REACTOR CENTRAL (Clímax Triple + Cámara de Extracción)
        // =================================================================================================
        private static void BuildZone6_ReactorCoreFinale(Transform geo, Transform interact, Transform condRoot, ModularPalette p)
        {
            var zone = new GameObject("Zone6_ReactorCoreFinale");
            zone.transform.SetParent(geo);

            CreateModularFloorSection(zone.transform, "Floor_Z6_ReactorArena", -10f, 10f, 228f, 268f, 4.0f, p.Floor, p.Wall, false, true);
            CreateModularSideWallZ(zone.transform, "Wall_Z6_Left", -10f, 228f, 268f, 4.0f, p.Wall);
            CreateModularSideWallZ(zone.transform, "Wall_Z6_Right", 10f, 228f, 268f, 4.0f, p.Wall);
            CreateModularWallSpanX(zone.transform, "Wall_Z6_EndWall", -10f, 10f, 268f, 4.0f, p.Wall);

            // Maquinaria industrial decorativa en los flancos del Reactor
            SpawnAssetDirect(p.Machine, zone.transform, new Vector3(-7.5f, 4.0f, 232f), Quaternion.Euler(0f, 90f, 0f));
            SpawnAssetDirect(p.Machine, zone.transform, new Vector3(7.5f, 4.0f, 232f), Quaternion.Euler(0f, -90f, 0f));
            SpawnAssetDirect(p.Conveyor, zone.transform, new Vector3(-8f, 4.0f, 250f), Quaternion.identity);

            // Flanco Izquierdo (Piernas): Subida por MagneticWall hasta pasarela alta (Y=7.0m) para liberar la MagneticKey del Reactor
            var magWallZ6Go = (GameObject)PrefabUtility.InstantiatePrefab(p.MagneticWall, interact);
            magWallZ6Go.name = "MagneticWall_Z6_LeftClimb";
            magWallZ6Go.transform.position = new Vector3(-9.4f, 6.5f, 238f);
            magWallZ6Go.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            magWallZ6Go.transform.localScale = new Vector3(5f, 5f, 0.5f);
            ConfigureMagneticWall(magWallZ6Go.GetComponent<MagneticWall>(), true, null);

            CreateModularCatwalk(zone.transform, "Catwalk_Z6_KeyVault", -9.5f, -4.5f, 236f, 246f, 7.0f, p.CeilingPlate4x4);
            var vaultWallGo = (GameObject)PrefabUtility.InstantiatePrefab(p.BreakableWall, interact);
            vaultWallGo.name = "BreakableWall_Z6_KeyVault";
            vaultWallGo.transform.position = new Vector3(-7f, 7.0f, 241f);

            var keyZ6Go = (GameObject)PrefabUtility.InstantiatePrefab(p.MagneticKey, interact);
            keyZ6Go.name = "MagneticKey_Z6_ReactorCore";
            keyZ6Go.transform.position = new Vector3(-7f, 7.6f, 244f);
            CreateLightPoint(keyZ6Go.transform, new Vector3(0f, 0.8f, 0f), new Color(1f, 0.65f, 0.1f), 1.8f, 6f);

            // Flanco Derecho (Torso): Conducto bajo (1.15m) hacia depósito de cajas para la Diana del Reactor
            CreateModularSideWallZ(zone.transform, "DuctWall_Z6_Inner", 4f, 236f, 244f, 4.0f, p.Wall);
            CreateLowCrawlBarrier(zone.transform, "LowCrawlBarrier_Z6_Duct", 4f, 10f, 236f, 4.0f, p.TrimWall3x4, p.Barrier);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z6_CoreA", new Vector3(7f, 4.6f, 240f), 25f, true);
            SpawnPixelCrate(p.BoxGrabbable, interact, "GrabbableBox_Z6_CoreB", new Vector3(7f, 4.6f, 242f), 25f, true);

            CreateModularWallSpanX(zone.transform, "TargetMount_Z6", 4f, 8f, 252f, 4.0f, p.Wall);
            var targetZ6Go = (GameObject)PrefabUtility.InstantiatePrefab(p.TargetButton, interact);
            targetZ6Go.name = "TargetButton_Z6_Core";
            targetZ6Go.transform.position = new Vector3(6f, 7.2f, 251.5f);
            targetZ6Go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var targetZ6 = targetZ6Go.GetComponent<TargetButton>();
            CreateLightPoint(targetZ6Go.transform, new Vector3(0f, 0f, -0.6f), new Color(1f, 0.2f, 0.2f), 1.8f, 6f);

            // Centro del Reactor: Socket Magnético + Báscula de Fusión (100kg)
            var socketZ6Go = (GameObject)PrefabUtility.InstantiatePrefab(p.PuzzleSocket, interact);
            socketZ6Go.name = "PuzzleSocket_Z6_Core";
            socketZ6Go.transform.position = new Vector3(-4f, 4.0f, 250f);
            var socketZ6 = socketZ6Go.GetComponent<PuzzleSocket>();

            var wpZ6Go = (GameObject)PrefabUtility.InstantiatePrefab(p.WeightPlatform, interact);
            wpZ6Go.name = "WeightPlatform_Z6_CoreFusion";
            wpZ6Go.transform.position = new Vector3(0f, 4.05f, 250f);
            var wpZ6 = wpZ6Go.GetComponent<WeightPlatform>();
            SetRequiredMass(wpZ6, 95f);

            // Compuerta Final de la Bóveda (Z=256) con 3 Cables Convergentes
            var exitDoor = CreateModularGateWall(zone.transform, interact, "SlidingDoor_FinalExit", 256f, 4.0f, p);

            var cableCoreSocket = CreateEnergyCable(interact, "Conduit_Z6_Socket", new Vector3(-4f, 4.05f, 250f), new Vector3(-0.8f, 4.05f, 256f));
            AttachCableIndicator(cableCoreSocket.gameObject, socketZ6, cableCoreSocket);

            var cableCoreScale = CreateEnergyCable(interact, "Conduit_Z6_Scale", new Vector3(0f, 4.05f, 250f), new Vector3(0f, 4.05f, 256f));
            AttachCableIndicator(cableCoreScale.gameObject, wpZ6, cableCoreScale);

            var cableCoreTarget = CreateEnergyCable(interact, "Conduit_Z6_Target", new Vector3(6f, 4.05f, 251.5f), new Vector3(0.8f, 4.05f, 256f));
            AttachCableIndicator(cableCoreTarget.gameObject, targetZ6, cableCoreTarget);

            WireCompositeDoor(condRoot, "Zone6_FinalReactorCompletion", exitDoor, new ConditionBase[]
            {
                CreateSocketCondition(condRoot, "Cond_Z6_CoreSocket", socketZ6),
                CreateMechanismCondition(condRoot, "Cond_Z6_CoreScale", wpZ6),
                CreateMechanismCondition(condRoot, "Cond_Z6_CoreTarget", targetZ6)
            });

            // Cámara de Extracción (Z=256 a Z=268)
            var exitTriggerGo = new GameObject("RoomExitTrigger");
            exitTriggerGo.transform.SetParent(interact);
            exitTriggerGo.transform.position = new Vector3(0f, 5.5f, 263f);

            var exitCol = exitTriggerGo.AddComponent<BoxCollider>();
            exitCol.isTrigger = true;
            exitCol.size = new Vector3(8f, 4f, 6f);

            var exitComp = exitTriggerGo.AddComponent<RoomExitTrigger>();
            var exitSo = new SerializedObject(exitComp);
            exitSo.FindProperty("_exitDoor").objectReferenceValue = exitDoor;
            exitSo.FindProperty("_requireDoorOpened").boolValue = true;
            exitSo.ApplyModifiedPropertiesWithoutUndo();

            CreateLightSpot(exitTriggerGo.transform, new Vector3(0f, 6f, 0f), Vector3.down, new Color(0.2f, 1f, 0.4f), 3.0f, 12f, 65f);
        }

        // =================================================================================================
        // GENERADORES MODULARES MATEMÁTICOS (0 CUBOS PRIMITIVOS, GRID ESTRICTO DE 4.0 METROS)
        // =================================================================================================
        private static void CreateModularFloorSection(
            Transform parent,
            string name,
            float xMin,
            float xMax,
            float zMin,
            float zMax,
            float yElevation,
            GameObject groundPrefab,
            GameObject wallPrefab,
            bool frontRetainingWall,
            bool backRetainingWall)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            const float tileSize = 4f;
            int widthCount = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / tileSize));
            int lengthCount = Mathf.Max(1, Mathf.RoundToInt((zMax - zMin) / tileSize));

            if (groundPrefab != null)
            {
                for (int ix = 0; ix < widthCount; ix++)
                {
                    float x = xMin + 2f + ix * tileSize;
                    for (int iz = 0; iz < lengthCount; iz++)
                    {
                        float z = zMin + 2f + iz * tileSize;
                        var tile = (GameObject)PrefabUtility.InstantiatePrefab(groundPrefab, root.transform);
                        tile.transform.localPosition = new Vector3(x, yElevation, z);
                    }
                }
            }

            // Muros de contención inferiores para que ningún borde de plataforma o cambio de cota flote en el aire
            if (wallPrefab != null && frontRetainingWall)
            {
                for (int ix = 0; ix < widthCount; ix++)
                {
                    float x = xMin + 2f + ix * tileSize;
                    var rw = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, root.transform);
                    rw.transform.localPosition = new Vector3(x, yElevation - 4f, zMin);
                    rw.transform.localRotation = Quaternion.identity;
                }
            }

            if (wallPrefab != null && backRetainingWall)
            {
                for (int ix = 0; ix < widthCount; ix++)
                {
                    float x = xMin + 2f + ix * tileSize;
                    var rw = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, root.transform);
                    rw.transform.localPosition = new Vector3(x, yElevation - 4f, zMax);
                    rw.transform.localRotation = Quaternion.identity;
                }
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3((xMin + xMax) * 0.5f, yElevation - 0.5f, (zMin + zMax) * 0.5f);
            col.size = new Vector3(xMax - xMin, 1f, zMax - zMin);
            col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
        }

        private static void CreateModularSideWallZ(Transform parent, string name, float xPos, float zMin, float zMax, float yElevation, GameObject wallPrefab)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            const float step = 4f;
            int count = Mathf.Max(1, Mathf.RoundToInt((zMax - zMin) / step));

            if (wallPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    float z = zMin + 2f + i * step;
                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, root.transform);
                    piece.transform.localPosition = new Vector3(xPos, yElevation, z);
                    piece.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(xPos, yElevation + 2.25f, (zMin + zMax) * 0.5f);
            col.size = new Vector3(0.8f, 4.5f, zMax - zMin);
            col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
        }

        private static void CreateModularWallSpanX(Transform parent, string name, float xMin, float xMax, float zPos, float yElevation, GameObject wallPrefab)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            const float step = 4f;
            int count = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / step));

            if (wallPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = xMin + 2f + i * step;
                    var piece = (GameObject)PrefabUtility.InstantiatePrefab(wallPrefab, root.transform);
                    piece.transform.localPosition = new Vector3(x, yElevation, zPos);
                    piece.transform.localRotation = Quaternion.identity;
                }
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3((xMin + xMax) * 0.5f, yElevation + 2.25f, zPos);
            col.size = new Vector3(xMax - xMin, 4.5f, 0.8f);
            col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
        }

        private static SlidingDoor CreateModularGateWall(Transform parent, Transform interact, string doorName, float zPos, float yElevation, ModularPalette p)
        {
            var gateRoot = new GameObject($"GateWall_{doorName}");
            gateRoot.transform.SetParent(parent);
            gateRoot.transform.position = Vector3.zero;

            if (p.Wall != null)
            {
                var wLeftOuter = (GameObject)PrefabUtility.InstantiatePrefab(p.Wall, gateRoot.transform);
                wLeftOuter.transform.localPosition = new Vector3(-8f, yElevation, zPos);

                var wRightOuter = (GameObject)PrefabUtility.InstantiatePrefab(p.Wall, gateRoot.transform);
                wRightOuter.transform.localPosition = new Vector3(8f, yElevation, zPos);
            }

            var doorFrameL = p.WallDoorL != null ? p.WallDoorL : p.Wall;
            if (doorFrameL != null)
            {
                var wDoorL = (GameObject)PrefabUtility.InstantiatePrefab(doorFrameL, gateRoot.transform);
                wDoorL.transform.localPosition = new Vector3(-4f, yElevation, zPos);
            }

            var doorFrameR = p.WallDoorR != null ? p.WallDoorR : p.Wall;
            if (doorFrameR != null)
            {
                var wDoorR = (GameObject)PrefabUtility.InstantiatePrefab(doorFrameR, gateRoot.transform);
                wDoorR.transform.localPosition = new Vector3(4f, yElevation, zPos);
            }

            var colLeft = gateRoot.AddComponent<BoxCollider>();
            colLeft.center = new Vector3(-6f, yElevation + 2.25f, zPos);
            colLeft.size = new Vector3(8f, 4.5f, 0.8f);

            var colRight = gateRoot.AddComponent<BoxCollider>();
            colRight.center = new Vector3(6f, yElevation + 2.25f, zPos);
            colRight.size = new Vector3(8f, 4.5f, 0.8f);

            var doorGo = (GameObject)PrefabUtility.InstantiatePrefab(p.SlidingDoor, interact);
            doorGo.name = doorName;
            doorGo.transform.position = new Vector3(0f, yElevation, zPos);
            CreateLightPoint(doorGo.transform, new Vector3(0f, 3.8f, -0.6f), new Color(0.2f, 0.85f, 1f), 1.8f, 7f);

            return doorGo.GetComponent<SlidingDoor>();
        }

        private static void CreateLowCrawlBarrier(Transform parent, string name, float xMin, float xMax, float zPos, float yElevation, GameObject trimWallPrefab, GameObject barrierPrefab)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            float width = xMax - xMin;
            int count = Mathf.Max(1, Mathf.RoundToInt(width / 4f));

            if (trimWallPrefab != null)
            {
                for (int i = 0; i < count; i++)
                {
                    float x = xMin + 2f + i * 4f;
                    var beam = (GameObject)PrefabUtility.InstantiatePrefab(trimWallPrefab, root.transform);
                    beam.transform.localPosition = new Vector3(x, yElevation + 1.15f, zPos);
                    beam.transform.localRotation = Quaternion.identity;
                }
            }

            if (barrierPrefab != null)
            {
                var bLeft = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab, root.transform);
                bLeft.transform.localPosition = new Vector3(xMin + 1.2f, yElevation, zPos - 0.5f);
                var bRight = (GameObject)PrefabUtility.InstantiatePrefab(barrierPrefab, root.transform);
                bRight.transform.localPosition = new Vector3(xMax - 1.2f, yElevation, zPos - 0.5f);
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3((xMin + xMax) * 0.5f, yElevation + 2.65f, zPos);
            col.size = new Vector3(width, 3.0f, 0.8f);
            col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
        }

        private static void CreateModularCatwalk(Transform parent, string name, float xMin, float xMax, float zMin, float zMax, float yElevation, GameObject platePrefab)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            const float tileSize = 4f;
            int widthCount = Mathf.Max(1, Mathf.RoundToInt((xMax - xMin) / tileSize));
            int lengthCount = Mathf.Max(1, Mathf.RoundToInt((zMax - zMin) / tileSize));

            if (platePrefab != null)
            {
                for (int ix = 0; ix < widthCount; ix++)
                {
                    float x = xMin + 2f + ix * tileSize;
                    for (int iz = 0; iz < lengthCount; iz++)
                    {
                        float z = zMin + 2f + iz * tileSize;
                        var plate = (GameObject)PrefabUtility.InstantiatePrefab(platePrefab, root.transform);
                        plate.transform.localPosition = new Vector3(x, yElevation, z);
                    }
                }
            }

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3((xMin + xMax) * 0.5f, yElevation - 0.2f, (zMin + zMax) * 0.5f);
            col.size = new Vector3(xMax - xMin, 0.4f, zMax - zMin);
            col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
        }

        private static void CreatePalletSteps(Transform parent, string name, float xCenter, float zStart, float yBase, int stepCount, GameObject palletPrefab)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = Vector3.zero;

            for (int i = 0; i < stepCount; i++)
            {
                float stepHeight = (i + 1) * 0.5f;
                float z = zStart + i * 1.6f;

                if (palletPrefab != null)
                {
                    for (int h = 0; h <= i; h++)
                    {
                        var pal = (GameObject)PrefabUtility.InstantiatePrefab(palletPrefab, root.transform);
                        pal.transform.localPosition = new Vector3(xCenter, yBase + h * 0.45f, z);
                    }
                }

                var col = root.AddComponent<BoxCollider>();
                col.center = new Vector3(xCenter, yBase + stepHeight * 0.5f, z);
                col.size = new Vector3(2.4f, stepHeight, 1.5f);
                col.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
            }
        }

        private static void ConfigureMagneticWall(MagneticWall wall, bool initialActive, LeverMechanism linkedLever)
        {
            if (wall == null) return;
            var so = new SerializedObject(wall);
            var initProp = so.FindProperty("_initialActive");
            if (initProp != null) initProp.boolValue = initialActive;
            var leverProp = so.FindProperty("_linkedLever");
            if (leverProp != null) leverProp.objectReferenceValue = linkedLever;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureCatapult(CatapultMechanism catapult, float maxPull, float springRes, float minSpeed, float maxSpeed, float angle)
        {
            if (catapult == null) return;
            var so = new SerializedObject(catapult);
            var pPull = so.FindProperty("_maxPullDistance");
            if (pPull != null) pPull.floatValue = maxPull;
            var pSpring = so.FindProperty("_springResistance");
            if (pSpring != null) pSpring.floatValue = springRes;
            var pMin = so.FindProperty("_minLaunchSpeed");
            if (pMin != null) pMin.floatValue = minSpeed;
            var pMax = so.FindProperty("_maxLaunchSpeed");
            if (pMax != null) pMax.floatValue = maxSpeed;
            var pAng = so.FindProperty("_launchElevationAngle");
            if (pAng != null) pAng.floatValue = angle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRequiredMass(WeightPlatform wp, float mass)
        {
            if (wp == null) return;
            var so = new SerializedObject(wp);
            var prop = so.FindProperty("_requiredMass");
            if (prop != null) prop.floatValue = mass;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AttachCableIndicator(GameObject host, MonoBehaviour mechanism, LineRenderer cable)
        {
            var trigger = host.AddComponent<DoorMechanismTrigger>();
            trigger.Configure(mechanism, null, autoClose: true, cable: cable);
        }

        private static MechanismCondition CreateMechanismCondition(Transform parent, string name, MonoBehaviour mechanism)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var cond = go.AddComponent<MechanismCondition>();
            cond.Configure(mechanism, true);
            return cond;
        }

        private static SocketCondition CreateSocketCondition(Transform parent, string name, PuzzleSocket socket)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            var cond = go.AddComponent<SocketCondition>();
            cond.Configure(socket);
            return cond;
        }

        private static void WireCompositeDoor(Transform condRoot, string groupName, SlidingDoor door, ConditionBase[] subConditions)
        {
            var groupGo = new GameObject(groupName);
            groupGo.transform.SetParent(condRoot);
            var andCond = groupGo.AddComponent<AndCondition>();
            andCond.SetConditions(subConditions);

            var completion = groupGo.AddComponent<RoomCompletion>();
            var so = new SerializedObject(completion);
            var autoProp = so.FindProperty("_autoCompleteRoom");
            if (autoProp != null) autoProp.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            completion.Configure(andCond, door, null);
        }

        private static void SpawnPixelCrate(GameObject prefab, Transform parent, string name, Vector3 pos, float mass, bool grabbable)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;

            if (go.TryGetComponent<Collider>(out var c))
            {
                c.sharedMaterial = PhysicsMaterialFactory.ComedicNormal;
            }

            if (grabbable && go.GetComponent<GrabbableObject>() == null)
            {
                go.AddComponent<GrabbableObject>();
            }
            else if (!grabbable && go.GetComponent<PushableBox>() == null)
            {
                go.AddComponent<PushableBox>();
            }
        }

        private static void SpawnAssetDirect(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = rot;
        }

        private static LineRenderer CreateEnergyCable(Transform parent, string name, Vector3 start, Vector3 end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 4;
            line.startWidth = 0.09f;
            line.endWidth = 0.09f;

            line.SetPosition(0, start);
            line.SetPosition(1, new Vector3(start.x, start.y, (start.z + end.z) * 0.5f));
            line.SetPosition(2, new Vector3(end.x, end.y, (start.z + end.z) * 0.5f));
            line.SetPosition(3, end);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader) { color = Color.white };
            line.material = mat;
            line.startColor = new Color(0.9f, 0.25f, 0.2f);
            line.endColor = new Color(0.9f, 0.25f, 0.2f);

            return line;
        }

        private static void CreateLightPoint(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light_Point");
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
        }

        private static void CreateLightSpot(Transform parent, Vector3 localPos, Vector3 forwardDir, Color color, float intensity, float range, float angle)
        {
            var go = new GameObject("Light_Spot");
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.forward = forwardDir;
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = angle;
        }

        private static GameObject CreateLocalCheckpoint(Transform parent, GameObject prefab, Vector3 pos, int priority, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;

            var cp = go.GetComponent<Checkpoint>();
            var so = new SerializedObject(cp);
            so.FindProperty("_priority").intValue = priority;

            var spLegs = new GameObject("Spawn_Legs");
            spLegs.transform.SetParent(go.transform);
            spLegs.transform.localPosition = new Vector3(-1.2f, 0.1f, 0f);

            var spTorso = new GameObject("Spawn_Torso");
            spTorso.transform.SetParent(go.transform);
            spTorso.transform.localPosition = new Vector3(1.2f, 0.1f, 0f);

            so.FindProperty("_legsSpawnPoint").objectReferenceValue = spLegs.transform;
            so.FindProperty("_torsoSpawnPoint").objectReferenceValue = spTorso.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        #endregion
    }
}

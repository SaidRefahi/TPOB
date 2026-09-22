using System.IO;
using Game.Core.Enums;
using Game.Gameplay.Camera;
using Game.Gameplay.Checkpoints;
using Game.Gameplay.Hazards;
using Game.Gameplay.Interactables;
using Game.Gameplay.Player.Death;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Rooms;
using Game.Gameplay.Rooms.Conditions;
using Game.Gameplay.Spawning;
using Game.Network.Events;
using Game.Network.Scopes;
using Game.Network.Services;
using PurrNet;
using PurrNet.Transports;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    public static class RoomContentBuilder
    {
        private const string RoomsDirectory = "Assets/_Project/Scenes/Rooms";

        [MenuItem("TPOB/3. Generar las 10 Salas de Campaña", false, 3)]
        public static void BuildAllCampaignRooms()
        {
            PlaygroundBuilder.EnsureDirectories();
            if (!Directory.Exists(RoomsDirectory)) Directory.CreateDirectory(RoomsDirectory);

            var mats = PlaygroundBuilder.CreateMaterials();
            var prefabs = PlaygroundBuilder.CreatePrefabs(mats);

            BuildRoom01(prefabs, mats);
            BuildRoom02(prefabs, mats);
            BuildRoom03(prefabs, mats);
            BuildRoom04(prefabs, mats);
            BuildRoom05(prefabs, mats);
            BuildRoom06(prefabs, mats);
            BuildRoom07(prefabs, mats);
            BuildRoom08(prefabs, mats);
            BuildRoom09(prefabs, mats);
            BuildRoom10(prefabs, mats);

            UpdateEditorBuildSettings();

            // Re-open Room 01 for the designer
            EditorSceneManager.OpenScene($"{RoomsDirectory}/Room_01.unity", OpenSceneMode.Single);
            Debug.Log("<color=green>[TPOB] ¡Las 10 salas de la campaña han sido generadas y configuradas con éxito en EditorBuildSettings!</color>");
        }

        #region Room Builders

        // 🟢 SALA 1: EL DESPERTAR (Fundamentos: movimiento, agarre de Torso, palanca y puerta)
        public static void BuildRoom01(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_01.unity";
            var ctx = SetupBaseRoom(path, 0, "Room_01", new Vector3(20f, 1f, 22f), new Vector3(0f, 0f, 11f), prefabs, mats);

            // Grabbable Boxes for practicing Torso grab
            var box1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, ctx.InteractGroup.transform);
            box1.transform.position = new Vector3(-3f, 0.5f, -2f);

            var box2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, ctx.InteractGroup.transform);
            box2.transform.position = new Vector3(3f, 0.5f, -2f);

            // Accessible lever
            var lever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            lever.transform.position = new Vector3(5f, 0f, 3f);

            // Condition: Lever switched on
            var condGo = new GameObject("Cond_Lever");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<MechanismCondition>();
            cond.Configure(lever.GetComponent<LeverMechanism>(), true);

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 2: PRIMERA FUSIÓN (Báscula de peso 120kg, requiere la masa combinada de Piernas + Torso)
        public static void BuildRoom02(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_02.unity";
            var ctx = SetupBaseRoom(path, 1, "Room_02", new Vector3(22f, 1f, 24f), new Vector3(0f, 0f, 12f), prefabs, mats);

            // WeightPlatform calibrated for fused robot (120kg)
            var weightPlatform = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            weightPlatform.transform.position = new Vector3(0f, 0f, 1f);

            var wpComp = weightPlatform.GetComponent<WeightPlatform>();
            var wpSo = new SerializedObject(wpComp);
            wpSo.FindProperty("_requiredMass").floatValue = 110f; // Piernas (80kg) + Torso (40kg) = 120kg
            wpSo.ApplyModifiedPropertiesWithoutUndo();

            var condGo = new GameObject("Cond_WeightPlatform");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<MechanismCondition>();
            cond.Configure(wpComp, true);

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 3: LA PATADA Y LA DIANA (Muro destructible por patada de Piernas, llave magnética a socket)
        public static void BuildRoom03(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_03.unity";
            var ctx = SetupBaseRoom(path, 2, "Room_03", new Vector3(24f, 1f, 26f), new Vector3(0f, 0f, 13f), prefabs, mats);

            // Kickable obstacle
            var kickable = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, ctx.InteractGroup.transform);
            kickable.transform.position = new Vector3(0f, 0.6f, -1f);

            // Breakable wall blocking access to side alcove
            var wall = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.BreakableWall, ctx.InteractGroup.transform);
            wall.transform.position = new Vector3(7f, 0f, 4f);

            // Magnetic key inside side alcove
            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(10f, 0.5f, 4f);

            // Puzzle socket near exit door
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(-4f, 0f, 6f);

            var condGo = new GameObject("Cond_Socket");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<SocketCondition>();
            cond.Configure(socket.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 4: MAGNETO-BÁSICO (Foso con KillVolume, llave en isla distante atraída con imán de Torso)
        public static void BuildRoom04(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_04.unity";
            var ctx = SetupBaseRoom(path, 3, "Room_04", new Vector3(22f, 1f, 26f), new Vector3(0f, 0f, 13f), prefabs, mats);

            // Distant pedestal island across a trench
            var island = GameObject.CreatePrimitive(PrimitiveType.Cube);
            island.name = "RemotePedestal";
            island.transform.SetParent(ctx.EnvGroup.transform);
            island.transform.position = new Vector3(0f, 1f, 6f);
            island.transform.localScale = new Vector3(3f, 2f, 3f);
            island.GetComponent<Renderer>().sharedMaterial = mats.Ramp;

            // Key on the pedestal
            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(0f, 2.3f, 6f);

            // Puzzle socket on the players' platform
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(0f, 0f, 0f);

            var condGo = new GameObject("Cond_Socket");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<SocketCondition>();
            cond.Configure(socket.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 5: EL ABISMO Y EL PUENTE (Foso ancho, lanzamiento de Torso, botón remoto que abre compuerta)
        public static void BuildRoom05(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_05.unity";
            var ctx = SetupBaseRoom(path, 4, "Room_05", new Vector3(22f, 1f, 30f), new Vector3(0f, 0f, 15f), prefabs, mats);

            // Far landing ledge across the chasm
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "FarLedge";
            ledge.transform.SetParent(ctx.EnvGroup.transform);
            ledge.transform.position = new Vector3(0f, 0.5f, 7f);
            ledge.transform.localScale = new Vector3(8f, 1f, 6f);
            ledge.GetComponent<Renderer>().sharedMaterial = mats.Floor;

            // Remote Target Button on the far wall
            var btn = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            btn.transform.position = new Vector3(0f, 1.5f, 9.8f);
            btn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var condGo = new GameObject("Cond_Button");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<MechanismCondition>();
            cond.Configure(btn.GetComponent<TargetButton>(), true);

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 6: DOBLE CONMUTADOR (Interruptores sincronizados en alas opuestas de la sala)
        public static void BuildRoom06(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_06.unity";
            var ctx = SetupBaseRoom(path, 5, "Room_06", new Vector3(32f, 1f, 24f), new Vector3(0f, 0f, 12f), prefabs, mats);

            // Left wing button
            var btnLeft = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            btnLeft.transform.position = new Vector3(-12f, 1.5f, 2f);
            btnLeft.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // Right wing lever
            var leverRight = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            leverRight.transform.position = new Vector3(12f, 0f, 2f);

            // Conditions
            var condLeftGo = new GameObject("SubCond_LeftButton");
            condLeftGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condLeft = condLeftGo.AddComponent<MechanismCondition>();
            condLeft.Configure(btnLeft.GetComponent<TargetButton>(), true);

            var condRightGo = new GameObject("SubCond_RightLever");
            condRightGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condRight = condRightGo.AddComponent<MechanismCondition>();
            condRight.Configure(leverRight.GetComponent<LeverMechanism>(), true);

            var timedCondGo = new GameObject("Root_TimedCondition");
            timedCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var timedCond = timedCondGo.AddComponent<TimedMechanismCondition>();
            timedCond.Configure(new ConditionBase[] { condLeft, condRight }, 4f);

            ctx.RoomComp.Configure(timedCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 7: LA TORRE VERTICAL (Torso empuja caja pesada desde plataforma alta; Piernas la recibe en báscula)
        public static void BuildRoom07(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_07.unity";
            var ctx = SetupBaseRoom(path, 6, "Room_07", new Vector3(22f, 1f, 26f), new Vector3(0f, 0f, 13f), prefabs, mats);

            // High tower platform
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "TowerPlatform";
            tower.transform.SetParent(ctx.EnvGroup.transform);
            tower.transform.position = new Vector3(6f, 1.75f, 2f);
            tower.transform.localScale = new Vector3(6f, 3.5f, 6f);
            tower.GetComponent<Renderer>().sharedMaterial = mats.Floor;

            // Ramp leading to tower
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "TowerRamp";
            ramp.transform.SetParent(ctx.EnvGroup.transform);
            ramp.transform.position = new Vector3(6f, 1.75f, -4f);
            ramp.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            ramp.transform.localScale = new Vector3(3.5f, 0.4f, 8f);
            ramp.GetComponent<Renderer>().sharedMaterial = mats.Ramp;

            // Pushable Box on top of tower
            var pushBox = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            pushBox.transform.position = new Vector3(6f, 4.2f, 2f);

            // WeightPlatform on ground floor
            var weightPlatform = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            weightPlatform.transform.position = new Vector3(-4f, 0f, 2f);

            var condGo = new GameObject("Cond_WeightPlatform");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<MechanismCondition>();
            cond.Configure(weightPlatform.GetComponent<WeightPlatform>(), true);

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 8: EL CALZO MÓVIL (Transportar carga rampa arriba con patadas de Piernas y retén magnético de Torso)
        public static void BuildRoom08(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_08.unity";
            var ctx = SetupBaseRoom(path, 7, "Room_08", new Vector3(20f, 1f, 28f), new Vector3(0f, 3f, 14f), prefabs, mats);

            // Incline ramp
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "LongRamp";
            ramp.transform.SetParent(ctx.EnvGroup.transform);
            ramp.transform.position = new Vector3(0f, 1.5f, 4f);
            ramp.transform.rotation = Quaternion.Euler(16f, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.5f, 12f);
            ramp.GetComponent<Renderer>().sharedMaterial = mats.Ramp;

            // Top plateau
            var topPlateau = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topPlateau.name = "TopPlateau";
            topPlateau.transform.SetParent(ctx.EnvGroup.transform);
            topPlateau.transform.position = new Vector3(0f, 3f, 12f);
            topPlateau.transform.localScale = new Vector3(8f, 1f, 6f);
            topPlateau.GetComponent<Renderer>().sharedMaterial = mats.Floor;

            // Magnetic key at ramp base
            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(0f, 0.5f, -3f);

            // Puzzle socket at top plateau
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(0f, 3.5f, 12f);

            var condGo = new GameObject("Cond_Socket");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<SocketCondition>();
            cond.Configure(socket.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🔴 SALA 9: CIRCUITO DE BALANCINES (Plataforma SeeSaw basculante sobre abismo con checkpoints)
        public static void BuildRoom09(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_09.unity";
            var ctx = SetupBaseRoom(path, 8, "Room_09", new Vector3(22f, 1f, 32f), new Vector3(0f, 0f, 16f), prefabs, mats);

            // Center see-saw platform over pit
            var seeSaw = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SeeSawPlatform, ctx.InteractGroup.transform);
            seeSaw.transform.position = new Vector3(0f, 0f, 3f);

            // Left stabilizer lever
            var leverLeft = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            leverLeft.transform.position = new Vector3(-6f, 0f, 10f);

            // Right stabilizer lever
            var leverRight = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            leverRight.transform.position = new Vector3(6f, 0f, 10f);

            // Conditions: AND of both levers
            var condLeftGo = new GameObject("SubCond_LeftLever");
            condLeftGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condLeft = condLeftGo.AddComponent<MechanismCondition>();
            condLeft.Configure(leverLeft.GetComponent<LeverMechanism>(), true);

            var condRightGo = new GameObject("SubCond_RightLever");
            condRightGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condRight = condRightGo.AddComponent<MechanismCondition>();
            condRight.Configure(leverRight.GetComponent<LeverMechanism>(), true);

            var andCondGo = new GameObject("Root_AndCondition");
            andCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var andCond = andCondGo.AddComponent<AndCondition>();
            andCond.SetConditions(new ConditionBase[] { condLeft, condRight });

            ctx.RoomComp.Configure(andCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🔴 SALA 10: EL REACTOR (Clímax final: combo de volea, núcleo del reactor con celda magnética y compuerta final)
        public static void BuildRoom10(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_10.unity";
            var ctx = SetupBaseRoom(path, 9, "Room_10", new Vector3(26f, 1f, 32f), new Vector3(0f, 0f, 16f), prefabs, mats);

            // High Target Button over reactor chamber
            var highBtn = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            highBtn.transform.position = new Vector3(0f, 4.5f, 8f);
            highBtn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Reactor Core PuzzleSocket in center
            var reactorCore = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            reactorCore.name = "ReactorCoreSocket";
            reactorCore.transform.position = new Vector3(0f, 0f, 2f);

            // Magnetic energy cell
            var energyCell = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            energyCell.transform.position = new Vector3(0f, 0.5f, -2f);

            // Pushable/Kickable crate for volley combo
            var box = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            box.transform.position = new Vector3(-3f, 0.6f, -3f);

            var condGo = new GameObject("Cond_ReactorSocket");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<SocketCondition>();
            cond.Configure(reactorCore.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        #endregion

        #region Base Infrastructure Generator

        private struct RoomContext
        {
            public Scene Scene;
            public GameObject EnvGroup;
            public GameObject InteractGroup;
            public GameObject MgmtGroup;
            public GameObject ConditionsRoot;
            public RoomController RoomCtrl;
            public RoomCompletion RoomComp;
            public SlidingDoor ExitDoor;
            public RoomExitTrigger ExitTrigger;
        }

        private static RoomContext SetupBaseRoom(string scenePath, int roomIndex, string roomName, Vector3 floorScale, Vector3 exitDoorPos, PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            Scene scene;
            if (File.Exists(scenePath))
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var rootObjects = scene.GetRootGameObjects();
            for (int i = 0; i < rootObjects.Length; i++)
            {
                Object.DestroyImmediate(rootObjects[i]);
            }

            // 1. ENVIRONMENT
            var envGroup = new GameObject("--- ENVIRONMENT ---");

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ArenaFloor";
            floor.transform.SetParent(envGroup.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = floorScale;
            floor.GetComponent<Renderer>().sharedMaterial = mats.Floor;

            float halfX = floorScale.x * 0.5f;
            float halfZ = floorScale.z * 0.5f;
            float wallH = 4f;
            float wallT = 1f;

            // Walls (leaving gateway at exitDoorPos)
            PlaygroundBuilder.CreateWall("Wall_South", new Vector3(0f, wallH * 0.5f, -halfZ), new Vector3(floorScale.x, wallH, wallT), envGroup.transform, mats.Wall);
            PlaygroundBuilder.CreateWall("Wall_West", new Vector3(-halfX, wallH * 0.5f, 0f), new Vector3(wallT, wallH, floorScale.z), envGroup.transform, mats.Wall);
            PlaygroundBuilder.CreateWall("Wall_East", new Vector3(halfX, wallH * 0.5f, 0f), new Vector3(wallT, wallH, floorScale.z), envGroup.transform, mats.Wall);

            // North Wall with gap for door
            float doorGapHalf = 2.5f;
            float northWallSegmentWidth = (floorScale.x - (doorGapHalf * 2f)) * 0.5f;
            float leftCenterX = -halfX + (northWallSegmentWidth * 0.5f);
            float rightCenterX = halfX - (northWallSegmentWidth * 0.5f);

            PlaygroundBuilder.CreateWall("Wall_North_Left", new Vector3(leftCenterX, wallH * 0.5f, halfZ), new Vector3(northWallSegmentWidth, wallH, wallT), envGroup.transform, mats.Wall);
            PlaygroundBuilder.CreateWall("Wall_North_Right", new Vector3(rightCenterX, wallH * 0.5f, halfZ), new Vector3(northWallSegmentWidth, wallH, wallT), envGroup.transform, mats.Wall);

            // Kill Volume underneath floor
            var killVol = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KillVolume, envGroup.transform);
            killVol.transform.position = new Vector3(0f, -4f, 0f);
            killVol.transform.localScale = new Vector3(floorScale.x * 1.5f, 1f, floorScale.z * 1.5f);

            // 2. INTERACTABLES
            var interactGroup = new GameObject("--- INTERACTABLES ---");

            // Mid-point checkpoint
            var checkpoint = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Checkpoint, interactGroup.transform);
            checkpoint.transform.position = new Vector3(0f, 0f, -halfZ * 0.2f);

            // Sliding door at exit gateway
            var exitDoorGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SlidingDoor, interactGroup.transform);
            exitDoorGo.transform.position = exitDoorPos;
            var exitDoor = exitDoorGo.GetComponent<SlidingDoor>();

            // RoomExitTrigger behind door
            var exitTriggerGo = new GameObject("RoomExitGateway");
            exitTriggerGo.transform.SetParent(interactGroup.transform);
            exitTriggerGo.transform.position = exitDoorPos + Vector3.forward * 2f;

            var exitCol = exitTriggerGo.AddComponent<BoxCollider>();
            exitCol.size = new Vector3(4f, 4f, 4f);
            exitCol.center = new Vector3(0f, 1.5f, 0f);
            exitCol.isTrigger = true;

            var gatewayPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gatewayPad.name = "GatewayIndicatorPad";
            gatewayPad.transform.SetParent(exitTriggerGo.transform);
            gatewayPad.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            gatewayPad.transform.localScale = new Vector3(3.8f, 0.1f, 3.8f);
            var padRenderer = gatewayPad.GetComponent<Renderer>();
            padRenderer.sharedMaterial = mats.CheckpointIndicator;
            Object.DestroyImmediate(gatewayPad.GetComponent<Collider>());

            var exitTrigger = exitTriggerGo.AddComponent<RoomExitTrigger>();

            // 3. MANAGEMENT
            var mgmtGroup = new GameObject("--- MANAGEMENT ---");

            var roomSystem = new GameObject("[ROOM_SYSTEM]");
            roomSystem.transform.SetParent(mgmtGroup.transform);
            var roomCtrl = roomSystem.AddComponent<RoomController>();
            var roomScope = roomSystem.AddComponent<RoomLifetimeScope>();
            var roomComp = roomSystem.AddComponent<RoomCompletion>();

            var roomCtrlSo = new SerializedObject(roomCtrl);
            roomCtrlSo.FindProperty("_roomIndex").intValue = roomIndex;
            roomCtrlSo.FindProperty("_roomName").stringValue = roomName;
            roomCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            var conditionsRoot = new GameObject("[PUZZLE_CONDITIONS]");
            conditionsRoot.transform.SetParent(roomSystem.transform);

            // Spawn points
            var spawnGroup = new GameObject("[SPAWN_POINTS]");
            spawnGroup.transform.SetParent(mgmtGroup.transform);
            var spawnManager = spawnGroup.AddComponent<SpawnPointManager>();

            var spLegs = new GameObject("SpawnPoint_Legs");
            spLegs.transform.SetParent(spawnGroup.transform);
            spLegs.transform.position = new Vector3(-2.5f, 0f, -halfZ + 3f);
            var spLegsComp = spLegs.AddComponent<Game.Gameplay.Spawning.SpawnPoint>();
            var spLegsSo = new SerializedObject(spLegsComp);
            spLegsSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Legs;
            spLegsSo.ApplyModifiedPropertiesWithoutUndo();

            var spTorso = new GameObject("SpawnPoint_Torso");
            spTorso.transform.SetParent(spawnGroup.transform);
            spTorso.transform.position = new Vector3(2.5f, 0f, -halfZ + 3f);
            var spTorsoComp = spTorso.AddComponent<Game.Gameplay.Spawning.SpawnPoint>();
            var spTorsoSo = new SerializedObject(spTorsoComp);
            spTorsoSo.FindProperty("_targetRole").enumValueIndex = (int)PlayerRole.Torso;
            spTorsoSo.ApplyModifiedPropertiesWithoutUndo();

            // Player Spawner
            var spawnerGo = new GameObject("[PLAYER_SPAWNER]");
            spawnerGo.transform.SetParent(mgmtGroup.transform);
            var playerSpawner = spawnerGo.AddComponent<TPOBPlayerSpawner>();

            var spawnerSo = new SerializedObject(playerSpawner);
            spawnerSo.FindProperty("_legsPrefab").objectReferenceValue = prefabs.Legs;
            spawnerSo.FindProperty("_torsoPrefab").objectReferenceValue = prefabs.Torso;
            spawnerSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            spawnerSo.ApplyModifiedPropertiesWithoutUndo();

            // Robot Coordinator
            var coordGo = new GameObject("[ROBOT_COORDINATOR]");
            coordGo.transform.SetParent(mgmtGroup.transform);
            var coordinator = coordGo.AddComponent<RobotCoordinator>();

            // Checkpoint System & Respawn Coordinator
            var cpSysGo = new GameObject("[CHECKPOINT_SYSTEM]");
            cpSysGo.transform.SetParent(mgmtGroup.transform);
            var cpSys = cpSysGo.AddComponent<CheckpointSystem>();
            var cpSysSo = new SerializedObject(cpSys);
            cpSysSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            cpSysSo.ApplyModifiedPropertiesWithoutUndo();

            var respawnGo = new GameObject("[RESPAWN_COORDINATOR]");
            respawnGo.transform.SetParent(mgmtGroup.transform);
            var respawnCoord = respawnGo.AddComponent<RespawnCoordinator>();
            var respawnSo = new SerializedObject(respawnCoord);
            respawnSo.FindProperty("_checkpointSystem").objectReferenceValue = cpSys;
            respawnSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. NETWORKING (NetworkManager stays at root for DontDestroyOnLoad compatibility)
            var netGroup = new GameObject("--- NETWORKING ---");

            var netMgrGo = new GameObject("[NETWORK_MANAGER]");
            var netManager = netMgrGo.AddComponent<NetworkManager>();
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

            var relayGo = new GameObject("[NETWORK_EVENT_RELAY]");
            relayGo.transform.SetParent(netGroup.transform);
            var relay = relayGo.AddComponent<NetworkEventRelay>();

            var audioRelayGo = new GameObject("[NETWORK_AUDIO_RELAY]");
            audioRelayGo.transform.SetParent(netGroup.transform);
            var audioRelay = audioRelayGo.AddComponent<Game.Network.Audio.NetworkAudioRelay>();

            var gameScopeGo = new GameObject("[GAME_LIFETIME_SCOPE]");
            gameScopeGo.transform.SetParent(netGroup.transform);
            var gameScope = gameScopeGo.AddComponent<GameLifetimeScope>();
            var levelManager = gameScopeGo.AddComponent<LevelManager>();

            var lobbyCtrlGo = new GameObject("[LOBBY_NETWORK_CONTROLLER]");
            lobbyCtrlGo.transform.SetParent(netGroup.transform);
            var lobbyCtrl = lobbyCtrlGo.AddComponent<LobbyNetworkController>();

            var bootGo = new GameObject("[BOOTSTRAPPER]");
            bootGo.transform.SetParent(netGroup.transform);
            bootGo.AddComponent<Bootstrapper>();

            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            gameScopeSo.FindProperty("_lobbyNetworkController").objectReferenceValue = lobbyCtrl;
            gameScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. LIGHTING & CAMERA
            var lightingGroup = new GameObject("--- LIGHTING & CAMERA ---");

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.SetParent(lightingGroup.transform);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.2f;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(lightingGroup.transform);
            camGo.transform.position = new Vector3(0f, 18f, -halfZ - 5f);
            camGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();

            var cameraRigGo = new GameObject("[CAMERA_RIG]");
            cameraRigGo.transform.SetParent(mgmtGroup.transform);

            var targetGroupGo = new GameObject("CM_TargetGroup");
            targetGroupGo.transform.SetParent(cameraRigGo.transform);
            var targetGroup = targetGroupGo.AddComponent<CinemachineTargetGroup>();
            var targetGroupCoord = targetGroupGo.AddComponent<TargetGroupCoordinator>();

            var cmCamGo = new GameObject("CM_AdaptiveCamera");
            cmCamGo.transform.SetParent(cameraRigGo.transform);
            cmCamGo.transform.position = new Vector3(0f, 18f, -halfZ - 5f);
            cmCamGo.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            var cmCam = cmCamGo.AddComponent<CinemachineCamera>();
            cmCam.Target.TrackingTarget = targetGroupGo.transform;
            cmCam.Target.LookAtTarget = targetGroupGo.transform;
            cmCam.Lens.FieldOfView = 55f;

            var groupFraming = cmCamGo.AddComponent<CinemachineGroupFraming>();
            groupFraming.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
            groupFraming.SizeAdjustment = CinemachineGroupFraming.SizeAdjustmentModes.DollyThenZoom;
            groupFraming.FramingSize = 0.52f;
            groupFraming.Damping = 2f;

            cmCamGo.AddComponent<CinemachineImpulseListener>();
            var impulseSource = cmCamGo.AddComponent<CinemachineImpulseSource>();

            var cameraController = cameraRigGo.AddComponent<CameraController>();
            var camCtrlSo = new SerializedObject(cameraController);
            camCtrlSo.FindProperty("_cinemachineCamera").objectReferenceValue = cmCam;
            camCtrlSo.FindProperty("_groupFraming").objectReferenceValue = groupFraming;
            camCtrlSo.FindProperty("_targetCoordinator").objectReferenceValue = targetGroupCoord;
            camCtrlSo.FindProperty("_impulseSource").objectReferenceValue = impulseSource;
            camCtrlSo.ApplyModifiedPropertiesWithoutUndo();

            var targetCoordSo = new SerializedObject(targetGroupCoord);
            targetCoordSo.FindProperty("_targetGroup").objectReferenceValue = targetGroup;
            targetCoordSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire RoomLifetimeScope
            var roomScopeSo = new SerializedObject(roomScope);
            roomScopeSo.FindProperty("_roomController").objectReferenceValue = roomCtrl;
            roomScopeSo.FindProperty("_spawnPointManager").objectReferenceValue = spawnManager;
            roomScopeSo.FindProperty("_playerSpawner").objectReferenceValue = playerSpawner;
            roomScopeSo.FindProperty("_robotCoordinator").objectReferenceValue = coordinator;
            roomScopeSo.FindProperty("_networkEventRelay").objectReferenceValue = relay;
            roomScopeSo.FindProperty("_networkAudioRelay").objectReferenceValue = audioRelay;
            roomScopeSo.FindProperty("_cameraController").objectReferenceValue = cameraController;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();

            // Configure ExitTrigger
            exitTrigger.Configure(roomCtrl, exitDoor, padRenderer);

            EditorSceneManager.SaveScene(scene, scenePath);

            return new RoomContext
            {
                Scene = scene,
                EnvGroup = envGroup,
                InteractGroup = interactGroup,
                MgmtGroup = mgmtGroup,
                ConditionsRoot = conditionsRoot,
                RoomCtrl = roomCtrl,
                RoomComp = roomComp,
                ExitDoor = exitDoor,
                ExitTrigger = exitTrigger
            };
        }

        private static void UpdateEditorBuildSettings()
        {
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Boot.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_01.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_02.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_03.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_04.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_05.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_06.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_07.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_08.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_09.unity", true),
                new EditorBuildSettingsScene($"{RoomsDirectory}/Room_10.unity", true),
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log("<color=cyan>[TPOB] EditorBuildSettings actualizado con las 10 salas y Boot.unity.</color>");
        }

        #endregion
    }
}

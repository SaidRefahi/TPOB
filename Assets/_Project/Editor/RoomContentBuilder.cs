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

            PauseMenuBuilder.GenerateAllUIPrefabs();

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

        // 🟢 SALA 1: EL DESPERTAR (Fundamentos: agarre de Torso en placa ligera de 20kg + palanca en pedestal)
        public static void BuildRoom01(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_01.unity";
            var ctx = SetupBaseRoom(path, 0, "Room_01", new Vector3(20f, 1f, 26f), new Vector3(0f, 0f, 13f), prefabs, mats);

            // Sector A: Práctica de agarre de Torso
            var box1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, ctx.InteractGroup.transform);
            box1.transform.position = new Vector3(-2.5f, 0.5f, -4f);

            var box2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, ctx.InteractGroup.transform);
            box2.transform.position = new Vector3(2.5f, 0.5f, -4f);

            // WeightPlatform ligera (20kg) para abrir compuerta intermedia
            var wp1 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            wp1.transform.position = new Vector3(0f, 0f, -1f);
            var wpComp1 = wp1.GetComponent<WeightPlatform>();
            var wpSo1 = new SerializedObject(wpComp1);
            wpSo1.FindProperty("_requiredMass").floatValue = 20f;
            wpSo1.ApplyModifiedPropertiesWithoutUndo();

            var condWp1Go = new GameObject("SubCond_WeightPlatform1");
            condWp1Go.transform.SetParent(ctx.ConditionsRoot.transform);
            var condWp1 = condWp1Go.AddComponent<MechanismCondition>();
            condWp1.Configure(wpComp1, true);

            // Tabique divisorio con compuerta intermedia en Z = 1
            var interDoor = CreateDividingWallWithDoor(ctx, 1f, 20f, prefabs.SlidingDoor, mats);
            var connectorGo = new GameObject("Connector_InterDoor");
            connectorGo.transform.SetParent(interDoor.transform);
            var connector = connectorGo.AddComponent<DoorMechanismConnector>();
            connector.Configure(condWp1, interDoor);

            // Sector B: Pedestal con palanca de energía
            CreatePlatform(ctx, "Pedestal_B", new Vector3(0f, 0.5f, 6f), new Vector3(4f, 1f, 4f), mats.Floor);
            CreateRamp(ctx, "Ramp_Pedestal", new Vector3(0f, 0.5f, 3.5f), new Vector3(15f, 0f, 0f), new Vector3(2.5f, 0.3f, 3f), mats.Ramp);

            var lever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            lever.transform.position = new Vector3(0f, 1f, 6f);

            var condExitGo = new GameObject("RootCond_LeverExit");
            condExitGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condExit = condExitGo.AddComponent<MechanismCondition>();
            condExit.Configure(lever.GetComponent<LeverMechanism>(), true);

            ctx.RoomComp.Configure(condExit, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 2: PRIMERA FUSIÓN (Cámara de acoplamiento, tiro a diana elevada fusionados + placa de 110kg)
        public static void BuildRoom02(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_02.unity";
            var ctx = SetupBaseRoom(path, 1, "Room_02", new Vector3(22f, 1f, 28f), new Vector3(0f, 0f, 14f), prefabs, mats);

            // Sector A: Cubos y diana elevada
            var box = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.GrabbableBox, ctx.InteractGroup.transform);
            box.transform.position = new Vector3(-3f, 0.5f, -5f);

            var interDoor = CreateDividingWallWithDoor(ctx, 2f, 22f, prefabs.SlidingDoor, mats);

            var highBtn = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            highBtn.transform.position = new Vector3(0f, 3.6f, 1.5f);
            highBtn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var btnCondGo = new GameObject("SubCond_TargetButton");
            btnCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var btnCond = btnCondGo.AddComponent<MechanismCondition>();
            btnCond.Configure(highBtn.GetComponent<TargetButton>(), true);

            var connectorGo = new GameObject("Connector_InterDoor");
            connectorGo.transform.SetParent(interDoor.transform);
            var connector = connectorGo.AddComponent<DoorMechanismConnector>();
            connector.Configure(btnCond, interDoor);

            // Sector B: Báscula de compresión industrial (110kg -> Requiere Piernas 80kg + Torso 40kg = 120kg)
            var weightPlatform = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            weightPlatform.transform.position = new Vector3(0f, 0f, 8f);

            var wpComp = weightPlatform.GetComponent<WeightPlatform>();
            var wpSo = new SerializedObject(wpComp);
            wpSo.FindProperty("_requiredMass").floatValue = 110f;
            wpSo.ApplyModifiedPropertiesWithoutUndo();

            var condExitGo = new GameObject("RootCond_FusedWeight");
            condExitGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condExit = condExitGo.AddComponent<MechanismCondition>();
            condExit.Configure(wpComp, true);

            ctx.RoomComp.Configure(condExit, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 3: FUERZA DE IMPACTO (Patada a muro fisurado + extracción magnética de llave en pasaje estrecho)
        public static void BuildRoom03(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_03.unity";
            var ctx = SetupBaseRoom(path, 2, "Room_03", new Vector3(24f, 1f, 28f), new Vector3(0f, 0f, 14f), prefabs, mats);

            // Sector A: Muro destructible por patada de Piernas
            var breakableWall = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.BreakableWall, ctx.InteractGroup.transform);
            breakableWall.transform.position = new Vector3(0f, 0f, -2f);

            // Obstáculo pateable para calibrar impacto
            var kickable = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.KickableObstacle, ctx.InteractGroup.transform);
            kickable.transform.position = new Vector3(-4f, 0.6f, -6f);

            // Sector B: Alcoba estrecha con llave magnética
            PlaygroundBuilder.CreateWall("Alcove_Wall_L", new Vector3(6f, 2f, 4f), new Vector3(1f, 4f, 6f), ctx.EnvGroup.transform, mats.Wall);
            PlaygroundBuilder.CreateWall("Alcove_Wall_R", new Vector3(10f, 2f, 4f), new Vector3(1f, 4f, 6f), ctx.EnvGroup.transform, mats.Wall);
            PlaygroundBuilder.CreateWall("Alcove_Wall_Back", new Vector3(8f, 2f, 7f), new Vector3(5f, 4f, 1f), ctx.EnvGroup.transform, mats.Wall);

            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(8f, 0.5f, 5f);

            // Sector C: Zócalo de puzzle frente a compuerta de salida
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(-4f, 0f, 10f);

            var condGo = new GameObject("RootCond_Socket");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<SocketCondition>();
            cond.Configure(socket.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟢 SALA 4: EL FOSO MAGNÉTICO (Balancín con bloque metálico + Compuerta pesada manual con retroceso)
        public static void BuildRoom04(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_04.unity";
            var ctx = SetupBaseRoom(path, 3, "Room_04", new Vector3(22f, 1f, 32f), new Vector3(0f, 0f, 16f), prefabs, mats);

            // Sector A: Foso con KillVolume y balancín central
            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, -2f), new Vector3(22f, 1f, 6f), prefabs.KillVolume);

            var seeSaw = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SeeSawPlatform, ctx.InteractGroup.transform);
            seeSaw.transform.position = new Vector3(0f, 0f, -2f);

            var pushBox = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            pushBox.transform.position = new Vector3(0f, 0.6f, 3f);

            // Checkpoint a mitad de sala
            var cp = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.Checkpoint, ctx.InteractGroup.transform);
            cp.transform.position = new Vector3(0f, 0f, 5f);

            // Sector B: Compuerta pesada de tracción continua
            var heavyDoor = CreateHeavyDoor(ctx, new Vector3(0f, 0f, 9f), Quaternion.identity, prefabs.HeavyMagneticDoor, true);

            // Palanca de enclave tras la compuerta pesada
            var latchLever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            latchLever.transform.position = new Vector3(5f, 0f, 12f);

            var condGo = new GameObject("RootCond_LatchLever");
            condGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var cond = condGo.AddComponent<MechanismCondition>();
            cond.Configure(latchLever.GetComponent<LeverMechanism>(), true);

            ctx.RoomComp.Configure(cond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 5: PUNTERÍA Y ALTURA (Impulso de caja para subir a cornisa + diana a distancia protegida)
        public static void BuildRoom05(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_05.unity";
            var ctx = SetupBaseRoom(path, 4, "Room_05", new Vector3(24f, 1f, 32f), new Vector3(0f, 0f, 16f), prefabs, mats);

            // Sector A: Cornisa alta a 3.5m y caja para impulsarse
            CreatePlatform(ctx, "MidLedge", new Vector3(0f, 1.75f, -1f), new Vector3(24f, 3.5f, 4f), mats.Floor);

            var pushBox = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            pushBox.transform.position = new Vector3(0f, 0.6f, -6f);

            var ledgeLever = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            ledgeLever.transform.position = new Vector3(-6f, 3.5f, -1f);

            // Rampa que desciende para Piernas cuando Torso acciona la palanca de arriba
            CreateRamp(ctx, "Ramp_Access", new Vector3(6f, 1.75f, -4f), new Vector3(20f, 0f, 0f), new Vector3(3.5f, 0.4f, 8f), mats.Ramp);

            // Sector B: Diana lejana sobre foso
            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, 6f), new Vector3(24f, 1f, 6f), prefabs.KillVolume);

            var farLedge = CreatePlatform(ctx, "FarLedge", new Vector3(0f, 0.5f, 12f), new Vector3(10f, 1f, 6f), mats.Floor);

            var btn = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            btn.transform.position = new Vector3(0f, 2.5f, 14.8f);
            btn.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var btnCondGo = new GameObject("SubCond_TargetButton");
            btnCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var btnCond = btnCondGo.AddComponent<MechanismCondition>();
            btnCond.Configure(btn.GetComponent<TargetButton>(), true);

            var wp = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            wp.transform.position = new Vector3(6f, 3.5f, -1f);
            var wpComp = wp.GetComponent<WeightPlatform>();

            var wpCondGo = new GameObject("SubCond_WeightPlatform");
            wpCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var wpCond = wpCondGo.AddComponent<MechanismCondition>();
            wpCond.Configure(wpComp, true);

            var andCondGo = new GameObject("RootCond_And_ButtonAndPlate");
            andCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var andCond = andCondGo.AddComponent<AndCondition>();
            andCond.SetConditions(new ConditionBase[] { btnCond, wpCond });

            ctx.RoomComp.Configure(andCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 6: EL MURO AZUL (Piernas por pared magnética vertical, caja hacia abajo + doble interruptor temporizado)
        public static void BuildRoom06(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_06.unity";
            var ctx = SetupBaseRoom(path, 5, "Room_06", new Vector3(30f, 1f, 30f), new Vector3(0f, 0f, 15f), prefabs, mats);

            // Sector A: Pared magnética en lateral Oeste
            var magWall = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticWall, ctx.InteractGroup.transform);
            magWall.transform.position = new Vector3(-14.5f, 4f, -4f);
            magWall.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            magWall.transform.localScale = new Vector3(8f, 8f, 0.6f);

            // Pasarela alta a 8m
            CreatePlatform(ctx, "HighCatwalk", new Vector3(-9f, 8f, -4f), new Vector3(8f, 0.6f, 8f), mats.Floor);

            var boxHigh = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            boxHigh.transform.position = new Vector3(-9f, 8.8f, -4f);

            // Sector B: Doble interruptor sincronizado (Temporizado a 5 segundos)
            var btnHigh = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            btnHigh.transform.position = new Vector3(-9f, 9.5f, 0f);

            var leverLow = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            leverLow.transform.position = new Vector3(10f, 0f, 6f);

            var condHighGo = new GameObject("SubCond_HighButton");
            condHighGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condHigh = condHighGo.AddComponent<MechanismCondition>();
            condHigh.Configure(btnHigh.GetComponent<TargetButton>(), true);

            var condLowGo = new GameObject("SubCond_LowLever");
            condLowGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condLow = condLowGo.AddComponent<MechanismCondition>();
            condLow.Configure(leverLow.GetComponent<LeverMechanism>(), true);

            var timedCondGo = new GameObject("RootCond_TimedSwitches");
            timedCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var timedCond = timedCondGo.AddComponent<TimedMechanismCondition>();
            timedCond.Configure(new ConditionBase[] { condHigh, condLow }, 5f);

            ctx.RoomComp.Configure(timedCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🟡 SALA 7: BALANCÍN DINÁMICO (Patada a caja en rampa para catapultar balancín + llave magnética a socket)
        public static void BuildRoom07(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_07.unity";
            var ctx = SetupBaseRoom(path, 6, "Room_07", new Vector3(24f, 1f, 34f), new Vector3(0f, 0f, 17f), prefabs, mats);

            // Sector A: Foso y balancín físico
            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, -3f), new Vector3(24f, 1f, 8f), prefabs.KillVolume);

            var seeSaw = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.SeeSawPlatform, ctx.InteractGroup.transform);
            seeSaw.transform.position = new Vector3(0f, 0f, -3f);

            // Rampa alta para cargar caja
            CreatePlatform(ctx, "RampTower", new Vector3(7f, 2f, -10f), new Vector3(4f, 4f, 4f), mats.Floor);
            CreateRamp(ctx, "SeesawRamp", new Vector3(7f, 2f, -6f), new Vector3(20f, 0f, 0f), new Vector3(3f, 0.4f, 6f), mats.Ramp);

            var pushBox = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PushableBox, ctx.InteractGroup.transform);
            pushBox.transform.position = new Vector3(7f, 4.6f, -10f);

            // Balcón superior al otro lado del balancín
            CreatePlatform(ctx, "HighBalcony", new Vector3(0f, 4.5f, 4f), new Vector3(8f, 0.6f, 6f), mats.Floor);

            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(0f, 5f, 4f);

            // Sector B: Socket en suelo y placa en balcón
            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(-5f, 0f, 12f);

            var balconyWp = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            balconyWp.transform.position = new Vector3(0f, 4.5f, 6f);
            var bWpComp = balconyWp.GetComponent<WeightPlatform>();

            var condSocketGo = new GameObject("SubCond_Socket");
            condSocketGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condSocket = condSocketGo.AddComponent<SocketCondition>();
            condSocket.Configure(socket.GetComponent<PuzzleSocket>());

            var condWpGo = new GameObject("SubCond_BalconyWeight");
            condWpGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condWp = condWpGo.AddComponent<MechanismCondition>();
            condWp.Configure(bWpComp, true);

            var andCondGo = new GameObject("RootCond_And_SocketAndBalcony");
            andCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var andCond = andCondGo.AddComponent<AndCondition>();
            andCond.SetConditions(new ConditionBase[] { condSocket, condWp });

            ctx.RoomComp.Configure(andCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🔴 SALA 8: EL CAÑÓN ELÁSTICO (Catapulta de tensión coordinada sobre abismo de 14m + torres gemelas)
        public static void BuildRoom08(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_08.unity";
            var ctx = SetupBaseRoom(path, 7, "Room_08", new Vector3(26f, 1f, 38f), new Vector3(0f, 0f, 19f), prefabs, mats);

            // Sector A: Plataforma de lanzamiento y Catapulta elástica
            var catapult = CreateCatapult(ctx, new Vector3(0f, 0f, -10f), Quaternion.identity, prefabs.Catapult);

            // Abismo masivo de 14m
            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, 0f), new Vector3(26f, 1f, 14f), prefabs.KillVolume);

            // Sector B: Plataforma de llegada y Torres gemelas
            CreatePlatform(ctx, "LandingDeck", new Vector3(0f, 0.5f, 9f), new Vector3(14f, 1f, 6f), mats.Floor);

            // Torre Izquierda (Torso -> Diana)
            CreatePlatform(ctx, "TowerLeft", new Vector3(-6f, 3f, 14f), new Vector3(4f, 6f, 4f), mats.Floor);
            var btnLeft = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            btnLeft.transform.position = new Vector3(-6f, 6.5f, 14f);

            // Torre Derecha (Piernas -> Palanca de salto)
            CreatePlatform(ctx, "TowerRight", new Vector3(6f, 3f, 14f), new Vector3(4f, 6f, 4f), mats.Floor);
            var leverRight = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.LeverSwitch, ctx.InteractGroup.transform);
            leverRight.transform.position = new Vector3(6f, 6f, 14f);

            var condLeftGo = new GameObject("SubCond_TowerLeft");
            condLeftGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condLeft = condLeftGo.AddComponent<MechanismCondition>();
            condLeft.Configure(btnLeft.GetComponent<TargetButton>(), true);

            var condRightGo = new GameObject("SubCond_TowerRight");
            condRightGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condRight = condRightGo.AddComponent<MechanismCondition>();
            condRight.Configure(leverRight.GetComponent<LeverMechanism>(), true);

            var andCondGo = new GameObject("RootCond_And_TwinTowers");
            andCondGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var andCond = andCondGo.AddComponent<AndCondition>();
            andCond.SetConditions(new ConditionBase[] { condLeft, condRight });

            ctx.RoomComp.Configure(andCond, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🔴 SALA 9: RUTA INVERTIDA (Techo magnético sobre foso de radiación + compuerta pesada y acarreo)
        public static void BuildRoom09(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_09.unity";
            var ctx = SetupBaseRoom(path, 8, "Room_09", new Vector3(26f, 1f, 38f), new Vector3(0f, 0f, 19f), prefabs, mats);

            // Sector A: Foso mortal continuo
            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, -2f), new Vector3(26f, 1f, 18f), prefabs.KillVolume);

            // Techo magnético invertido a 8m
            var magCeiling = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticWall, ctx.InteractGroup.transform);
            magCeiling.name = "Ceiling_InvertedTrack";
            magCeiling.transform.position = new Vector3(0f, 8f, -2f);
            magCeiling.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            magCeiling.transform.localScale = new Vector3(6f, 0.6f, 18f);

            // Pared magnética de subida para Piernas
            var magAscent = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticWall, ctx.InteractGroup.transform);
            magAscent.name = "Wall_Ascent";
            magAscent.transform.position = new Vector3(-12.5f, 4f, -12f);
            magAscent.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            magAscent.transform.localScale = new Vector3(6f, 8f, 0.6f);

            // Islas flotantes para Torso
            CreatePlatform(ctx, "Island1", new Vector3(0f, 0f, -6f), new Vector3(3f, 1f, 3f), mats.Floor);
            CreatePlatform(ctx, "Island2", new Vector3(0f, 0f, 2f), new Vector3(3f, 1f, 3f), mats.Floor);

            var key = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            key.transform.position = new Vector3(0f, 0.6f, -6f);

            // Sector B: Compuerta pesada de llegada en Z = 9
            var heavyDoor = CreateHeavyDoor(ctx, new Vector3(0f, 0f, 9f), Quaternion.identity, prefabs.HeavyMagneticDoor, true);

            var socket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            socket.transform.position = new Vector3(0f, 0f, 14f);

            var condSocketGo = new GameObject("RootCond_Socket");
            condSocketGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condSocket = condSocketGo.AddComponent<SocketCondition>();
            condSocket.Configure(socket.GetComponent<PuzzleSocket>());

            ctx.RoomComp.Configure(condSocket, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        // 🔴 SALA 10: EL GRAN REACTOR (Clímax Final: Demolición, Catapulta, Techo Magnético, Núcleo y Fusión 120kg)
        public static void BuildRoom10(PlaygroundBuilder.PrefabSet prefabs, PlaygroundBuilder.MaterialSet mats)
        {
            string path = $"{RoomsDirectory}/Room_10.unity";
            var ctx = SetupBaseRoom(path, 9, "Room_10", new Vector3(32f, 1f, 46f), new Vector3(0f, 0f, 23f), prefabs, mats);

            // --- ACTO 1: LA BRECHA Y EL LANZAMIENTO (Z = -20 a Z = -10) ---
            var reinforcedWall = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.BreakableWall, ctx.InteractGroup.transform);
            reinforcedWall.transform.position = new Vector3(0f, 0f, -16f);

            var catapult = CreateCatapult(ctx, new Vector3(0f, 0f, -13f), Quaternion.identity, prefabs.Catapult);

            CreatePitsAndHazards(ctx, new Vector3(0f, -4f, -7f), new Vector3(32f, 1f, 8f), prefabs.KillVolume);

            // --- ACTO 2: EL NÚCLEO DE PLASMA Y LA EXTRACCIÓN (Z = -2 a Z = 10) ---
            CreatePlatform(ctx, "ReactorCoreDeck", new Vector3(0f, 0f, 0f), new Vector3(16f, 1f, 12f), mats.Floor);

            // Pared magnética hacia el techo del reactor
            var magWallCore = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticWall, ctx.InteractGroup.transform);
            magWallCore.name = "Reactor_MagWall";
            magWallCore.transform.position = new Vector3(-7.5f, 4.5f, 0f);
            magWallCore.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            magWallCore.transform.localScale = new Vector3(8f, 9f, 0.6f);

            var magCeilingCore = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticWall, ctx.InteractGroup.transform);
            magCeilingCore.name = "Reactor_MagCeiling";
            magCeilingCore.transform.position = new Vector3(0f, 9f, 0f);
            magCeilingCore.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            magCeilingCore.transform.localScale = new Vector3(14f, 0.6f, 10f);

            // Interruptor disyuntor en el techo
            var ceilingBreaker = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.TargetButton, ctx.InteractGroup.transform);
            ceilingBreaker.transform.position = new Vector3(0f, 8.8f, 2f);
            ceilingBreaker.transform.rotation = Quaternion.Euler(180f, 0f, 0f);

            // Núcleo magnético suspendido
            var coreKey = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.MagneticKey, ctx.InteractGroup.transform);
            coreKey.name = "ReactorCore_EnergyCell";
            coreKey.transform.position = new Vector3(0f, 2.5f, 0f);

            // Compuerta pesada que comunica al Acto 3
            var heavyDoor = CreateHeavyDoor(ctx, new Vector3(0f, 0f, 9f), Quaternion.identity, prefabs.HeavyMagneticDoor, false);

            // --- ACTO 3: LA SOBRECARGA Y FUSIÓN FINAL (Z = 12 a Z = 22) ---
            CreatePlatform(ctx, "IgnitionChamber", new Vector3(0f, 0.5f, 16f), new Vector3(18f, 1f, 12f), mats.Floor);

            var reactorSocket = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.PuzzleSocket, ctx.InteractGroup.transform);
            reactorSocket.name = "ReactorCore_Socket";
            reactorSocket.transform.position = new Vector3(0f, 1f, 15f);

            var coreScale = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.WeightPlatform, ctx.InteractGroup.transform);
            coreScale.name = "Fusion_CompressionScale";
            coreScale.transform.position = new Vector3(0f, 0.5f, 19f);

            var wpComp = coreScale.GetComponent<WeightPlatform>();
            var wpSo = new SerializedObject(wpComp);
            wpSo.FindProperty("_requiredMass").floatValue = 110f; // Exige robot fusionado (120kg)
            wpSo.ApplyModifiedPropertiesWithoutUndo();

            // Condiciones combinadas del clímax final
            var condSocketGo = new GameObject("SubCond_ReactorSocket");
            condSocketGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condSocket = condSocketGo.AddComponent<SocketCondition>();
            condSocket.Configure(reactorSocket.GetComponent<PuzzleSocket>());

            var condScaleGo = new GameObject("SubCond_FusionScale");
            condScaleGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var condScale = condScaleGo.AddComponent<MechanismCondition>();
            condScale.Configure(wpComp, true);

            var rootAndGo = new GameObject("RootCond_ReactorIgnition");
            rootAndGo.transform.SetParent(ctx.ConditionsRoot.transform);
            var rootAnd = rootAndGo.AddComponent<AndCondition>();
            rootAnd.SetConditions(new ConditionBase[] { condSocket, condScale });

            ctx.RoomComp.Configure(rootAnd, ctx.ExitDoor, ctx.RoomCtrl);
            EditorSceneManager.SaveScene(ctx.Scene);
        }

        #endregion

        #region Base Infrastructure and Helper Methods

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

            // 4. NETWORKING
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

            var lobbyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/LobbyNetworkController.prefab");

            var bootGo = new GameObject("[BOOTSTRAPPER]");
            bootGo.transform.SetParent(netGroup.transform);
            bootGo.AddComponent<Bootstrapper>();

            var gameScopeSo = new SerializedObject(gameScope);
            gameScopeSo.FindProperty("_networkManager").objectReferenceValue = netManager;
            gameScopeSo.FindProperty("_levelManager").objectReferenceValue = levelManager;
            var lobbyPrefabProp = gameScopeSo.FindProperty("_lobbyNetworkControllerPrefab");
            if (lobbyPrefabProp != null) lobbyPrefabProp.objectReferenceValue = lobbyPrefab;
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

            var roomScopeSo = new SerializedObject(roomScope);
            roomScopeSo.FindProperty("_cameraController").objectReferenceValue = cameraController;
            roomScopeSo.FindProperty("_networkEventRelay").objectReferenceValue = relay;
            roomScopeSo.ApplyModifiedPropertiesWithoutUndo();

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

        private static SlidingDoor CreateDividingWallWithDoor(RoomContext ctx, float zPos, float floorWidth, GameObject doorPrefab, PlaygroundBuilder.MaterialSet mats)
        {
            float wallH = 4f;
            float wallT = 0.8f;
            float doorGap = 4f;
            float halfWidth = floorWidth * 0.5f;
            float sideWidth = (floorWidth - doorGap) * 0.5f;

            // Pared izquierda
            float leftCenterX = -halfWidth + (sideWidth * 0.5f);
            PlaygroundBuilder.CreateWall($"DividingWall_L_{zPos}", new Vector3(leftCenterX, wallH * 0.5f, zPos), new Vector3(sideWidth, wallH, wallT), ctx.EnvGroup.transform, mats.Wall);

            // Pared derecha
            float rightCenterX = halfWidth - (sideWidth * 0.5f);
            PlaygroundBuilder.CreateWall($"DividingWall_R_{zPos}", new Vector3(rightCenterX, wallH * 0.5f, zPos), new Vector3(sideWidth, wallH, wallT), ctx.EnvGroup.transform, mats.Wall);

            // Dintel superior
            PlaygroundBuilder.CreateWall($"DividingWall_Lintel_{zPos}", new Vector3(0f, wallH - 0.25f, zPos), new Vector3(doorGap, 0.5f, wallT), ctx.EnvGroup.transform, mats.Wall);

            // Compuerta deslizante en el centro
            var doorGo = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, ctx.InteractGroup.transform);
            doorGo.name = $"IntermediateSlidingDoor_{zPos}";
            doorGo.transform.position = new Vector3(0f, 0f, zPos);
            return doorGo.GetComponent<SlidingDoor>();
        }

        private static GameObject CreatePlatform(RoomContext ctx, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            platform.transform.SetParent(ctx.EnvGroup.transform);
            platform.transform.position = pos;
            platform.transform.localScale = scale;
            platform.GetComponent<Renderer>().sharedMaterial = mat;
            return platform;
        }

        private static GameObject CreateRamp(RoomContext ctx, string name, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
        {
            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name;
            ramp.transform.SetParent(ctx.EnvGroup.transform);
            ramp.transform.position = pos;
            ramp.transform.rotation = Quaternion.Euler(rot);
            ramp.transform.localScale = scale;
            ramp.GetComponent<Renderer>().sharedMaterial = mat;
            return ramp;
        }

        private static void CreatePitsAndHazards(RoomContext ctx, Vector3 pos, Vector3 scale, GameObject killVolPrefab)
        {
            var killVol = (GameObject)PrefabUtility.InstantiatePrefab(killVolPrefab, ctx.EnvGroup.transform);
            killVol.transform.position = pos;
            killVol.transform.localScale = scale;
        }

        private static CatapultMechanism CreateCatapult(RoomContext ctx, Vector3 pos, Quaternion rot, GameObject catapultPrefab)
        {
            var catGo = (GameObject)PrefabUtility.InstantiatePrefab(catapultPrefab, ctx.InteractGroup.transform);
            catGo.transform.position = pos;
            catGo.transform.rotation = rot;
            return catGo.GetComponent<CatapultMechanism>();
        }

        private static HeavyMagneticDoor CreateHeavyDoor(RoomContext ctx, Vector3 pos, Quaternion rot, GameObject heavyDoorPrefab, bool autoClose = false)
        {
            var doorGo = (GameObject)PrefabUtility.InstantiatePrefab(heavyDoorPrefab, ctx.InteractGroup.transform);
            doorGo.transform.position = pos;
            doorGo.transform.rotation = rot;
            var heavyDoor = doorGo.GetComponent<HeavyMagneticDoor>();
            if (autoClose)
            {
                var so = new SerializedObject(heavyDoor);
                so.FindProperty("_autoCloseOnRelease").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return heavyDoor;
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

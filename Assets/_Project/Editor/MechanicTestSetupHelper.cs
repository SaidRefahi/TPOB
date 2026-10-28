using Game.Gameplay.Interactables;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class MechanicTestSetupHelper
    {
        [MenuItem("TPOB/Pruebas/1. Crear Catapulta de Prueba en Escena", false, 50)]
        public static void CreateCatapultInActiveScene()
        {
            var root = new GameObject("Catapult_Mechanism");
            Undo.RegisterCreatedObjectUndo(root, "Create Catapult");

            Vector3 spawnPos = Vector3.zero;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                spawnPos = sceneView.pivot;
                spawnPos.y = 0f;
            }
            root.transform.position = spawnPos;

            // Anchor point
            var anchorGo = new GameObject("AnchorPoint");
            anchorGo.transform.SetParent(root.transform);
            anchorGo.transform.localPosition = Vector3.zero;

            // Material for straps
            var strapMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            if (strapMat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }
                if (shader != null)
                {
                    strapMat = new Material(shader) { color = new Color(0.9f, 0.45f, 0.15f) };
                }
            }

            // Post Left
            var postLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            postLeft.name = "Post_Left";
            postLeft.transform.SetParent(root.transform);
            postLeft.transform.localPosition = new Vector3(-2.5f, 1.5f, 0f);
            postLeft.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);

            // Post Right
            var postRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            postRight.name = "Post_Right";
            postRight.transform.SetParent(root.transform);
            postRight.transform.localPosition = new Vector3(2.5f, 1.5f, 0f);
            postRight.transform.localScale = new Vector3(0.4f, 1.5f, 0.4f);

            // Strap Handle
            var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleGo.name = "Strap_Handle";
            handleGo.transform.SetParent(root.transform);
            handleGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            handleGo.transform.localScale = new Vector3(0.8f, 0.5f, 0.5f);

            var handleCol = handleGo.GetComponent<BoxCollider>();
            if (handleCol != null)
            {
                handleCol.isTrigger = true;
                handleCol.size = new Vector3(2.5f, 2.5f, 2.5f);
            }

            // Line Renderers
            var lineLeftGo = new GameObject("Line_Left");
            lineLeftGo.transform.SetParent(root.transform);
            var lineLeft = lineLeftGo.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineLeft, strapMat);

            var lineRightGo = new GameObject("Line_Right");
            lineRightGo.transform.SetParent(root.transform);
            var lineRight = lineRightGo.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lineRight, strapMat);

            // CatapultMechanism & Impulse
            var catapult = root.AddComponent<CatapultMechanism>();
            var impulse = root.AddComponent<CinemachineImpulseSource>();

            var so = new SerializedObject(catapult);
            so.FindProperty("_anchorPoint").objectReferenceValue = anchorGo.transform;
            so.FindProperty("_strapHandle").objectReferenceValue = handleGo.transform;
            so.FindProperty("_postLeft").objectReferenceValue = postLeft.transform;
            so.FindProperty("_postRight").objectReferenceValue = postRight.transform;
            so.FindProperty("_strapLeft").objectReferenceValue = lineLeft;
            so.FindProperty("_strapRight").objectReferenceValue = lineRight;
            so.FindProperty("_impulseSource").objectReferenceValue = impulse;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("<color=green>[TPOB] Catapulta de prueba generada con éxito en la escena!</color>");
        }

        [MenuItem("TPOB/Pruebas/2. Crear Puerta Pesada de Prueba en Escena", false, 51)]
        public static void CreateHeavyDoorInActiveScene()
        {
            var root = new GameObject("Heavy_Magnetic_Door");
            Undo.RegisterCreatedObjectUndo(root, "Create Heavy Door");

            Vector3 spawnPos = Vector3.zero;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                spawnPos = sceneView.pivot;
                spawnPos.y = 0f;
            }
            root.transform.position = spawnPos;

            // Frame Left & Right
            var frameL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameL.name = "Frame_Left";
            frameL.transform.SetParent(root.transform);
            frameL.transform.localPosition = new Vector3(-2f, 2f, 0f);
            frameL.transform.localScale = new Vector3(0.5f, 4f, 0.5f);

            var frameR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frameR.name = "Frame_Right";
            frameR.transform.SetParent(root.transform);
            frameR.transform.localPosition = new Vector3(2f, 2f, 0f);
            frameR.transform.localScale = new Vector3(0.5f, 4f, 0.5f);

            // Top Rail (Guiding beam for sideways sliding)
            var topRail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topRail.name = "Top_Rail";
            topRail.transform.SetParent(root.transform);
            topRail.transform.localPosition = new Vector3(1.5f, 3.8f, 0f);
            topRail.transform.localScale = new Vector3(7.5f, 0.4f, 0.6f);

            // Door Leaf
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Door_Leaf";
            leaf.transform.SetParent(root.transform);
            leaf.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            leaf.transform.localScale = new Vector3(3.5f, 3.6f, 0.3f);

            // Handle Point (Metallic Plate)
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Magnetic_Handle";
            handle.transform.SetParent(leaf.transform);
            handle.transform.localPosition = new Vector3(0f, -0.2f, -0.65f);
            handle.transform.localScale = new Vector3(0.8f, 0.4f, 0.3f);

            var handleCol = handle.GetComponent<BoxCollider>();
            if (handleCol != null)
            {
                handleCol.isTrigger = true;
                handleCol.size = new Vector3(2f, 2f, 3f);
            }

            var door = root.AddComponent<HeavyMagneticDoor>();
            var impulse = root.AddComponent<CinemachineImpulseSource>();

            var so = new SerializedObject(door);
            so.FindProperty("_doorLeaf").objectReferenceValue = leaf.transform;
            so.FindProperty("_handlePoint").objectReferenceValue = handle.transform;
            so.FindProperty("_handleIndicatorRenderer").objectReferenceValue = handle.GetComponent<Renderer>();
            so.FindProperty("_openOffset").vector3Value = new Vector3(3.8f, 0f, 0f);
            so.FindProperty("_requiredPullDistance").floatValue = 3.5f;
            so.FindProperty("_dragSpeedMultiplier").floatValue = 0.35f;
            so.FindProperty("_impulseSource").objectReferenceValue = impulse;
            so.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("<color=green>[TPOB] Puerta pesada de prueba generada con éxito en la escena!</color>");
        }

        [MenuItem("TPOB/Pruebas/3. Crear Pared Magnetica en Escena", false, 52)]
        public static void CreateMagneticWallInActiveScene()
        {
            var root = new GameObject("MagneticWall_Mechanism");
            Undo.RegisterCreatedObjectUndo(root, "Create Magnetic Wall");

            Vector3 spawnPos = Vector3.zero;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                spawnPos = sceneView.pivot;
                spawnPos.y = 0f;
            }
            root.transform.position = spawnPos;

            // Wall Visual & Collider
            var wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallGo.name = "Wall_Magnetic";
            wallGo.transform.SetParent(root.transform);
            wallGo.transform.localPosition = new Vector3(0f, 4f, 5f);
            wallGo.transform.localScale = new Vector3(8f, 8f, 0.6f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            var wallMat = new Material(shader);
            wallMat.name = "Mat_MagneticWall_Test";
            wallMat.color = new Color(0.12f, 0.16f, 0.22f);
            if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0.9f);
            if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.85f);
            wallMat.EnableKeyword("_EMISSION");
            wallMat.SetColor("_EmissionColor", new Color(0.1f, 0.75f, 1f));

            var renderer = wallGo.GetComponent<Renderer>();
            renderer.sharedMaterial = wallMat;

            var magWall = wallGo.AddComponent<Game.Gameplay.Interactables.MagneticWall>();

            // Setup Lever next to wall
            var leverRoot = new GameObject("Lever_WallControl");
            leverRoot.transform.SetParent(root.transform);
            leverRoot.transform.localPosition = new Vector3(5f, 0.5f, 4.5f);

            var leverBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leverBase.name = "LeverBase";
            leverBase.transform.SetParent(leverRoot.transform);
            leverBase.transform.localPosition = Vector3.zero;
            leverBase.transform.localScale = new Vector3(0.6f, 0.3f, 0.6f);

            var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handleGo.name = "LeverHandle";
            handleGo.transform.SetParent(leverRoot.transform);
            handleGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            handleGo.transform.localScale = new Vector3(0.15f, 0.5f, 0.15f);

            var lever = leverRoot.AddComponent<LeverMechanism>();
            var leverCol = leverRoot.AddComponent<BoxCollider>();
            leverCol.isTrigger = true;
            leverCol.size = new Vector3(2f, 2f, 2f);

            var leverSo = new SerializedObject(lever);
            leverSo.FindProperty("_handleTransform").objectReferenceValue = handleGo.transform;
            leverSo.FindProperty("_indicatorRenderer").objectReferenceValue = handleGo.GetComponent<Renderer>();
            leverSo.FindProperty("_isActivated").boolValue = true;
            leverSo.ApplyModifiedPropertiesWithoutUndo();

            // Link Lever to MagneticWall
            var wallSo = new SerializedObject(magWall);
            wallSo.FindProperty("_linkedLever").objectReferenceValue = lever;
            wallSo.FindProperty("_wallRenderer").objectReferenceValue = renderer;
            wallSo.ApplyModifiedPropertiesWithoutUndo();

            // Setup Connected Magnetic Ceiling (Y = 8m)
            var ceilingGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceilingGo.name = "Ceiling_Magnetic";
            ceilingGo.transform.SetParent(root.transform);
            ceilingGo.transform.localPosition = new Vector3(0f, 8.3f, 1.3f);
            ceilingGo.transform.localScale = new Vector3(8f, 0.6f, 8f);

            var ceilingRenderer = ceilingGo.GetComponent<Renderer>();
            ceilingRenderer.sharedMaterial = wallMat;

            var magCeiling = ceilingGo.AddComponent<Game.Gameplay.Interactables.MagneticWall>();
            var ceilingSo = new SerializedObject(magCeiling);
            ceilingSo.FindProperty("_linkedLever").objectReferenceValue = lever;
            ceilingSo.FindProperty("_wallRenderer").objectReferenceValue = ceilingRenderer;
            ceilingSo.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log("<color=cyan>[TPOB] Pared y techo magnéticos con palanca generados con éxito en la escena!</color>");
        }

        private static void ConfigureLineRenderer(LineRenderer line, Material mat)
        {
            line.positionCount = 2;
            line.startWidth = 0.08f;
            line.endWidth = 0.08f;
            line.material = mat;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}

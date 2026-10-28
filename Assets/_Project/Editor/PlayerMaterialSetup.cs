using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class PlayerMaterialSetup
    {
        [MenuItem("TPOB/Configurar Materiales de Jugadores", false, 10)]
        public static void SetupPlayerMaterials()
        {
            // 1. Rutas de Texturas
            string legsBaseColorPath = "Assets/_Project/Art/Textures/Legs/T_LegsRobot_BaseColor.png";
            string legsNormalPath = "Assets/_Project/Art/Textures/Legs/T_LegsRobot_Normal.png";
            string legsMetallicPath = "Assets/_Project/Art/Textures/Legs/T_LegsRobot_MetallicSmoothness.png";

            string torsoBaseColorPath = "Assets/_Project/Art/Textures/Torso/T_TorsoRobot_BaseColor.png";
            string torsoNormalPath = "Assets/_Project/Art/Textures/Torso/T_TorsoRobot_Normal.png";
            string torsoMetallicPath = "Assets/_Project/Art/Textures/Torso/T_TorsoRobot_MetallicSmoothness.png";

            // Asegurar importación de Normal Maps
            EnsureTextureImporterType(legsNormalPath, TextureImporterType.NormalMap);
            EnsureTextureImporterType(torsoNormalPath, TextureImporterType.NormalMap);

            var legsBase = AssetDatabase.LoadAssetAtPath<Texture2D>(legsBaseColorPath);
            var legsNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(legsNormalPath);
            var legsMetallic = AssetDatabase.LoadAssetAtPath<Texture2D>(legsMetallicPath);

            var torsoBase = AssetDatabase.LoadAssetAtPath<Texture2D>(torsoBaseColorPath);
            var torsoNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(torsoNormalPath);
            var torsoMetallic = AssetDatabase.LoadAssetAtPath<Texture2D>(torsoMetallicPath);

            // 2. Configurar Mat_Legs.mat
            var matLegs = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Mat_Legs.mat");
            if (matLegs != null)
            {
                matLegs.SetColor("_BaseColor", Color.white);
                matLegs.SetColor("_Color", Color.white);
                if (legsBase != null)
                {
                    matLegs.SetTexture("_BaseMap", legsBase);
                    matLegs.SetTexture("_MainTex", legsBase);
                }
                if (legsNormal != null)
                {
                    matLegs.SetTexture("_BumpMap", legsNormal);
                    matLegs.EnableKeyword("_NORMALMAP");
                }
                if (legsMetallic != null)
                {
                    matLegs.SetTexture("_MetallicGlossMap", legsMetallic);
                    matLegs.EnableKeyword("_METALLICSPECGLOSSMAP");
                    matLegs.SetFloat("_Smoothness", 1.0f);
                }
                EditorUtility.SetDirty(matLegs);
                Debug.Log("<color=green>[TPOB] Mat_Legs.mat configurado con sus texturas!</color>");
            }

            // También configurar Leg.mat si existe
            var legMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Textures/Legs/Leg.mat");
            if (legMat != null)
            {
                legMat.SetColor("_BaseColor", Color.white);
                legMat.SetColor("_Color", Color.white);
                if (legsBase != null) legMat.SetTexture("_BaseMap", legsBase);
                if (legsNormal != null) { legMat.SetTexture("_BumpMap", legsNormal); legMat.EnableKeyword("_NORMALMAP"); }
                if (legsMetallic != null) { legMat.SetTexture("_MetallicGlossMap", legsMetallic); legMat.EnableKeyword("_METALLICSPECGLOSSMAP"); legMat.SetFloat("_Smoothness", 1.0f); }
                EditorUtility.SetDirty(legMat);
            }

            // 3. Configurar Mat_Torso.mat
            var matTorso = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Mat_Torso.mat");
            if (matTorso != null)
            {
                matTorso.SetColor("_BaseColor", Color.white);
                matTorso.SetColor("_Color", Color.white);
                if (torsoBase != null)
                {
                    matTorso.SetTexture("_BaseMap", torsoBase);
                    matTorso.SetTexture("_MainTex", torsoBase);
                }
                if (torsoNormal != null)
                {
                    matTorso.SetTexture("_BumpMap", torsoNormal);
                    matTorso.EnableKeyword("_NORMALMAP");
                }
                if (torsoMetallic != null)
                {
                    matTorso.SetTexture("_MetallicGlossMap", torsoMetallic);
                    matTorso.EnableKeyword("_METALLICSPECGLOSSMAP");
                    matTorso.SetFloat("_Smoothness", 1.0f);
                }
                EditorUtility.SetDirty(matTorso);
                Debug.Log("<color=green>[TPOB] Mat_Torso.mat configurado con sus texturas!</color>");
            }

            // 4. Asignar a Prefabs
            AssignMaterialToPrefab("Assets/_Project/Prefabs/LegsPlayer.prefab", matLegs != null ? matLegs : legMat);
            AssignMaterialToPrefab("Assets/_Project/Prefabs/TorsoPlayer.prefab", matTorso);

            // 5. Vincular modelo 3D de Torso
            CheckAndAssignTorsoModel();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green><b>[TPOB] Configuración y asignación de materiales de jugadores completada.</b></color>");
        }

        private static void EnsureTextureImporterType(string path, TextureImporterType type)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != type)
            {
                importer.textureType = type;
                importer.SaveAndReimport();
            }
        }

        private static void AssignMaterialToPrefab(string prefabPath, Material material)
        {
            if (material == null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    r.sharedMaterial = material;
                    EditorUtility.SetDirty(r);
                }
            }
            PrefabUtility.SavePrefabAsset(prefab);
            Debug.Log($"<color=cyan>[TPOB] Material {material.name} asignado a {prefab.name}</color>");
        }

        private static void CheckAndAssignTorsoModel()
        {
            string torsoFbxPath = "Assets/_Project/Art/3D Model/Torso_Unity.fbx";
            var torsoFbx = AssetDatabase.LoadAssetAtPath<GameObject>(torsoFbxPath);
            Mesh mesh = null;

            if (torsoFbx != null)
            {
                var torsoMeshFilter = torsoFbx.GetComponentInChildren<MeshFilter>(true);
                var torsoSkinned = torsoFbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
                mesh = torsoMeshFilter != null ? torsoMeshFilter.sharedMesh : (torsoSkinned != null ? torsoSkinned.sharedMesh : null);
            }

            if (mesh == null)
            {
                var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(torsoFbxPath);
                foreach (var sub in allSubAssets)
                {
                    if (sub is Mesh m && !m.name.StartsWith("__preview__"))
                    {
                        mesh = m;
                        break;
                    }
                }
            }

            if (mesh != null)
            {
                var torsoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/TorsoPlayer.prefab");
                if (torsoPrefab != null)
                {
                    MeshFilter mf = null;
                    var torsoMeshTransform = torsoPrefab.transform.Find("AimPivot/TorsoMesh");
                    if (torsoMeshTransform != null)
                    {
                        mf = torsoMeshTransform.GetComponent<MeshFilter>();
                    }
                    if (mf == null)
                    {
                        mf = torsoPrefab.GetComponentInChildren<MeshFilter>(true);
                    }
                    if (mf != null)
                    {
                        mf.sharedMesh = mesh;
                        EditorUtility.SetDirty(mf);
                        PrefabUtility.SavePrefabAsset(torsoPrefab);
                        Debug.Log($"<color=green>[TPOB] Malla 3D {mesh.name} de Torso_Unity.fbx asignada a TorsoPlayer.prefab!</color>");
                    }
                }
            }
        }
    }
}

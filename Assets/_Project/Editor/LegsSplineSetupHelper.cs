using System.Collections.Generic;
using Game.Gameplay.Player.Legs;
using PurrNet;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Game.Editor
{
    public static class LegsSplineSetupHelper
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/LegsPlayer.prefab";

        [MenuItem("TPOB/Rigging/Setup Legs Spline Hose Rig")]
        public static void SetupLegsSplineHoseRig()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[LegsSplineSetupHelper] Failed to load prefab at {PrefabPath}");
                return;
            }

            try
            {
                Debug.Log("[LegsSplineSetupHelper] Migrating LegsPlayer to Bezier Spline Hose Rig...");

                LegsController controller = prefabRoot.GetComponent<LegsController>();
                Rigidbody rb = prefabRoot.GetComponent<Rigidbody>();

                // 1. Locate Hip bone
                Transform hipBone = null;
                Transform[] allChildren = prefabRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allChildren.Length; i++)
                {
                    if (allChildren[i].name == "Hip")
                    {
                        hipBone = allChildren[i];
                        break;
                    }
                }

                if (hipBone == null)
                {
                    Debug.LogError("[LegsSplineSetupHelper] Could not locate 'Hip' bone in LegsPlayer hierarchy!");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                Transform armature = hipBone.parent;
                Transform visualContainer = armature != null && armature.parent != null ? armature.parent : armature;
                GameObject visualRoot = visualContainer != null ? visualContainer.gameObject : prefabRoot;

                // 2. Clean up old Animation Rigging components (RigBuilder, Rig, IK_Targets, old animator)
                RigBuilder oldRigBuilder = visualRoot.GetComponent<RigBuilder>();
                if (oldRigBuilder != null)
                {
                    Object.DestroyImmediate(oldRigBuilder);
                    Debug.Log("[LegsSplineSetupHelper] Removed old RigBuilder.");
                }

                Transform oldRig = visualRoot.transform.Find("Rig");
                if (oldRig != null)
                {
                    Object.DestroyImmediate(oldRig.gameObject);
                    Debug.Log("[LegsSplineSetupHelper] Removed old Rig GameObject.");
                }

                Transform oldTargets = visualRoot.transform.Find("IK_Targets");
                if (oldTargets != null)
                {
                    Object.DestroyImmediate(oldTargets.gameObject);
                    Debug.Log("[LegsSplineSetupHelper] Removed old IK_Targets GameObject.");
                }

                // Clean up any missing scripts left over from deleted components
                int removedRootMissing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabRoot);
                if (removedRootMissing > 0)
                {
                    Debug.Log($"[LegsSplineSetupHelper] Removed {removedRootMissing} missing MonoBehaviours from root.");
                }

                foreach (var child in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    int removedChildMissing = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
                    if (removedChildMissing > 0)
                    {
                        Debug.Log($"[LegsSplineSetupHelper] Removed {removedChildMissing} missing MonoBehaviours from {child.name}.");
                    }
                }

                // 3. Gather bones for all 4 legs
                Transform[] bonesFL = GatherLegBones(hipBone, ".FL");
                Transform[] bonesFR = GatherLegBones(hipBone, ".FR");
                Transform[] bonesBL = GatherLegBones(hipBone, ".BL");
                Transform[] bonesBR = GatherLegBones(hipBone, ".BR");

                if (bonesFL.Length < 13 || bonesFR.Length < 13 || bonesBL.Length < 13 || bonesBR.Length < 13)
                {
                    Debug.LogError($"[LegsSplineSetupHelper] Incomplete bone chains found! FL:{bonesFL.Length}, FR:{bonesFR.Length}, BL:{bonesBL.Length}, BR:{bonesBR.Length}");
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    return;
                }

                // Revert any accidental property modifications on individual bones in the FBX prefab instance
                for (int i = 0; i < bonesFL.Length; i++)
                {
                    PrefabUtility.RevertObjectOverride(bonesFL[i], InteractionMode.AutomatedAction);
                    PrefabUtility.RevertObjectOverride(bonesFR[i], InteractionMode.AutomatedAction);
                    PrefabUtility.RevertObjectOverride(bonesBL[i], InteractionMode.AutomatedAction);
                    PrefabUtility.RevertObjectOverride(bonesBR[i], InteractionMode.AutomatedAction);
                }

                PrefabUtility.RevertObjectOverride(hipBone, InteractionMode.AutomatedAction);
                if (armature != null)
                {
                    PrefabUtility.RevertObjectOverride(armature, InteractionMode.AutomatedAction);
                }

                // 4. Ensure Rest Points exist at true ground contact level
                Transform restPointsParent = prefabRoot.transform.Find("Rest_Points");
                if (restPointsParent == null)
                {
                    GameObject restGo = new GameObject("Rest_Points");
                    restGo.transform.SetParent(prefabRoot.transform, false);
                    restPointsParent = restGo.transform;
                }

                Vector3 localFL = prefabRoot.transform.InverseTransformPoint(bonesFL[12].position);
                Vector3 localFR = prefabRoot.transform.InverseTransformPoint(bonesFR[12].position);
                Vector3 localBL = prefabRoot.transform.InverseTransformPoint(bonesBL[12].position);
                Vector3 localBR = prefabRoot.transform.InverseTransformPoint(bonesBR[12].position);

                // Ground plane contact level
                localFL.y = 0.04f;
                localFR.y = 0.04f;
                localBL.y = 0.04f;
                localBR.y = 0.04f;

                Transform restFL = GetOrCreateChildLocal(restPointsParent, "Rest_FL", localFL);
                Transform restFR = GetOrCreateChildLocal(restPointsParent, "Rest_FR", localFR);
                Transform restBL = GetOrCreateChildLocal(restPointsParent, "Rest_BL", localBL);
                Transform restBR = GetOrCreateChildLocal(restPointsParent, "Rest_BR", localBR);

                // 5. Ensure PurrNet NetworkBones is configured with all 53 bones
                NetworkBones netBones = visualRoot.GetComponent<NetworkBones>();
                if (netBones == null)
                {
                    netBones = visualRoot.AddComponent<NetworkBones>();
                }

                List<Transform> allBones = new List<Transform>(64);
                allBones.Add(hipBone);
                allBones.AddRange(bonesFL);
                allBones.AddRange(bonesFR);
                allBones.AddRange(bonesBL);
                allBones.AddRange(bonesBR);

                netBones.extraBones = allBones.ToArray();

                SerializedObject netBonesSo = new SerializedObject(netBones);
                var ownerAuthProp = netBonesSo.FindProperty("_ownerAuth");
                if (ownerAuthProp != null)
                {
                    ownerAuthProp.boolValue = false; // Server authoritative
                }
                var sendRateProp = netBonesSo.FindProperty("_sendRatePerSecond");
                if (sendRateProp != null)
                {
                    sendRateProp.intValue = 15;
                }
                netBonesSo.ApplyModifiedPropertiesWithoutUndo();

                // 6. Setup LegsSplineHoseAnimator on prefabRoot
                LegsSplineHoseAnimator splineAnim = prefabRoot.GetComponent<LegsSplineHoseAnimator>();
                if (splineAnim == null)
                {
                    splineAnim = prefabRoot.AddComponent<LegsSplineHoseAnimator>();
                }

                SplineLeg[] legs = new SplineLeg[4];
                legs[0] = new SplineLeg(LegID.FrontLeft, restFL, bonesFL);
                legs[1] = new SplineLeg(LegID.BackRight, restBR, bonesBR);
                legs[2] = new SplineLeg(LegID.FrontRight, restFR, bonesFR);
                legs[3] = new SplineLeg(LegID.BackLeft, restBL, bonesBL);

                splineAnim.SetLegReferences(legs, hipBone);

                SerializedObject splineSo = new SerializedObject(splineAnim);
                var ctrlProp = splineSo.FindProperty("_controller");
                if (ctrlProp != null) ctrlProp.objectReferenceValue = controller;
                var rbProp = splineSo.FindProperty("_rigidbody");
                if (rbProp != null) rbProp.objectReferenceValue = rb;
                var bodyProp = splineSo.FindProperty("_bodyRoot");
                if (bodyProp != null) bodyProp.objectReferenceValue = hipBone;

                splineSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(splineAnim);
                EditorUtility.SetDirty(prefabRoot);

                // Save Prefab
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                Debug.Log($"[LegsSplineSetupHelper] Successfully configured Bezier Spline Hose Rig on {PrefabPath}!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static Transform[] GatherLegBones(Transform root, string suffix)
        {
            List<Transform> list = new List<Transform>(13);
            for (int i = 1; i <= 13; i++)
            {
                string bName = $"Bone.{i:D3}{suffix}";
                Transform b = FindChildByName(root, bName);
                if (b != null)
                {
                    list.Add(b);
                }
            }
            return list.ToArray();
        }

        private static Transform FindChildByName(Transform root, string targetName)
        {
            if (root.name == targetName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildByName(root.GetChild(i), targetName);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform GetOrCreateChildLocal(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = parent.Find(name);
            if (child == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                child = go.transform;
            }
            child.localPosition = localPosition;
            return child;
        }

        [MenuItem("TPOB/Rigging/Log Leg Bone Positions")]
        public static void LogBonePositions()
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform hip = FindChildByName(prefabRoot.transform, "Hip");
                Transform[] bones = GatherLegBones(hip, ".FL");
                float totalLen = 0f;
                for (int i = 0; i < bones.Length; i++)
                {
                    float segLen = (i < bones.Length - 1) ? Vector3.Distance(bones[i].position, bones[i + 1].position) : 0f;
                    totalLen += segLen;
                    Debug.Log($"[BoneLog] Bone[{i}] {bones[i].name} localPos={bones[i].localPosition} worldPos={bones[i].position} segLen={segLen:F4}");
                }
                Debug.Log($"[BoneLog] Total Leg FL Chain Length = {totalLen:F4}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}

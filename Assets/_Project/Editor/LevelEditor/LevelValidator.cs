using System.Collections.Generic;
using Game.Gameplay.Checkpoints;
using Game.Gameplay.Hazards;
using Game.Gameplay.Interactables;
using Game.Gameplay.Level;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Spawning;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.LevelEditor
{
    public static class LevelValidator
    {
        public sealed class ValidationReport
        {
            public readonly List<string> Errors = new();
            public readonly List<string> Warnings = new();
            public readonly List<string> Passes = new();

            public bool HasErrors => Errors.Count > 0;
            public bool HasWarnings => Warnings.Count > 0;
        }

        public static ValidationReport Validate(LevelDefinition level)
        {
            var report = new ValidationReport();

            if (level == null)
            {
                report.Errors.Add("LevelDefinition is null. Please select or assign a LevelDefinition component.");
                PrintReport(report);
                return report;
            }

            // 1. Validate Spawns
            ValidateSpawns(level, report);

            // 2. Validate Checkpoints
            ValidateCheckpoints(level, report);

            // 3. Validate Deadzone
            ValidateDeadzone(level, report);

            // 4. Validate Interactive Mechanics
            ValidateMechanisms(report);

            // 5. Validate Missing Scripts
            ValidateMissingScripts(report);

            PrintReport(report);
            return report;
        }

        private static void ValidateSpawns(LevelDefinition level, ValidationReport report)
        {
            if (level.LegsSpawnPoint == null)
            {
                report.Errors.Add("[Spawns] LegsSpawnPoint is not assigned in LevelDefinition.");
            }
            else
            {
                report.Passes.Add($"[Spawns] LegsSpawnPoint verified at {level.LegsSpawnPoint.position}.");
            }

            if (level.TorsoSpawnPoint == null)
            {
                report.Errors.Add("[Spawns] TorsoSpawnPoint is not assigned in LevelDefinition.");
            }
            else
            {
                report.Passes.Add($"[Spawns] TorsoSpawnPoint verified at {level.TorsoSpawnPoint.position}.");
            }

            var spawnMgr = Object.FindFirstObjectByType<SpawnPointManager>();
            if (spawnMgr == null)
            {
                report.Warnings.Add("[Spawns] SpawnPointManager was not found in the active scene.");
            }
            else
            {
                report.Passes.Add("[Spawns] SpawnPointManager verified in scene.");
            }

            var playerSpawner = Object.FindFirstObjectByType<TPOBPlayerSpawner>();
            if (playerSpawner == null)
            {
                report.Errors.Add("[Spawns] TPOBPlayerSpawner was not found in the active scene. Player models will not spawn on Play.");
            }
            else
            {
                report.Passes.Add("[Spawns] TPOBPlayerSpawner verified in scene.");
            }

            var robotCoord = Object.FindFirstObjectByType<RobotCoordinator>();
            if (robotCoord == null)
            {
                report.Warnings.Add("[Spawns] RobotCoordinator was not found in the active scene.");
            }
            else
            {
                report.Passes.Add("[Spawns] RobotCoordinator verified in scene.");
            }
        }

        private static void ValidateCheckpoints(LevelDefinition level, ValidationReport report)
        {
            var sceneCheckpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            if (sceneCheckpoints.Length == 0)
            {
                report.Warnings.Add("[Checkpoints] No Checkpoints found in scene. Level has no intermediate save points.");
                return;
            }

            report.Passes.Add($"[Checkpoints] Found {sceneCheckpoints.Length} checkpoints in scene.");

            for (int i = 0; i < sceneCheckpoints.Length; i++)
            {
                var cp = sceneCheckpoints[i];
                if (cp.LegsSpawnPoint == cp.transform)
                {
                    report.Warnings.Add($"[Checkpoints] Checkpoint '{cp.name}' does not have dedicated LegsSpawnPoint (falls back to root).");
                }
                if (cp.TorsoSpawnPoint == cp.transform)
                {
                    report.Warnings.Add($"[Checkpoints] Checkpoint '{cp.name}' does not have dedicated TorsoSpawnPoint (falls back to root).");
                }
            }

            var cpSystem = Object.FindFirstObjectByType<CheckpointSystem>();
            if (cpSystem == null)
            {
                report.Errors.Add("[Checkpoints] CheckpointSystem component missing in scene.");
            }
            else
            {
                report.Passes.Add("[Checkpoints] CheckpointSystem verified in scene.");
            }
        }

        private static void ValidateDeadzone(LevelDefinition level, ValidationReport report)
        {
            var deadzone = level.SubterraneanKillVolume;
            if (deadzone == null)
            {
                deadzone = Object.FindFirstObjectByType<KillVolume>();
                if (deadzone != null && deadzone.transform.position.y <= level.GroundY)
                {
                    report.Warnings.Add($"[Deadzone] LevelDefinition has no subterranean KillVolume linked, but '{deadzone.name}' was found.");
                }
                else
                {
                    report.Errors.Add("[Deadzone] Subterranean Deadzone (KillVolume) is missing below floor level. Players can fall infinitely!");
                    return;
                }
            }

            var col = deadzone.BoxCollider;
            if (col == null)
            {
                report.Errors.Add($"[Deadzone] KillVolume '{deadzone.name}' has no BoxCollider.");
                return;
            }

            if (!col.isTrigger)
            {
                report.Errors.Add($"[Deadzone] KillVolume '{deadzone.name}' BoxCollider must have isTrigger = true.");
            }
            else
            {
                report.Passes.Add("[Deadzone] KillVolume is properly configured as Trigger.");
            }

            if (deadzone.transform.position.y > level.GroundY)
            {
                report.Errors.Add($"[Deadzone] KillVolume Y ({deadzone.transform.position.y:F2}) is above GroundY ({level.GroundY:F2}). Players will die immediately!");
            }
            else
            {
                report.Passes.Add($"[Deadzone] Subterranean KillVolume verified at Y={deadzone.transform.position.y:F2} below GroundY={level.GroundY:F2}.");
            }
        }

        private static void ValidateMechanisms(ValidationReport report)
        {
            var doorTriggers = Object.FindObjectsByType<DoorMechanismTrigger>(FindObjectsSortMode.None);
            for (int i = 0; i < doorTriggers.Length; i++)
            {
                var dt = doorTriggers[i];
                if (dt.Mechanism == null)
                {
                    report.Warnings.Add($"[Mechanics] DoorMechanismTrigger en '{dt.name}' no tiene Mechanism asignado.");
                }
                if (dt.Door == null && dt.ConduitCable == null)
                {
                    report.Warnings.Add($"[Mechanics] DoorMechanismTrigger en '{dt.name}' no tiene Door ni ConduitCable asignado.");
                }
            }

            var catapults = Object.FindObjectsByType<CatapultMechanism>(FindObjectsSortMode.None);
            if (catapults.Length == 0)
            {
                report.Warnings.Add("[Mechanics] No CatapultMechanism found in scene.");
            }
            else
            {
                report.Passes.Add($"[Mechanics] Verified {catapults.Length} CatapultMechanism(s) active in scene.");
            }

            var conditions = Object.FindObjectsByType<Game.Gameplay.Rooms.Conditions.MechanismCondition>(FindObjectsSortMode.None);
            for (int i = 0; i < conditions.Length; i++)
            {
                var cond = conditions[i];
                if (!cond.IsSatisfied && cond.name.Contains("Missing"))
                {
                    report.Warnings.Add($"[Mechanics] MechanismCondition '{cond.name}' tiene configuración incompleta.");
                }
            }
        }

        private static void ValidateMissingScripts(ValidationReport report)
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            int totalMissing = 0;

            for (int i = 0; i < roots.Length; i++)
            {
                var gos = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < gos.Length; j++)
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gos[j].gameObject);
                    if (missing > 0)
                    {
                        totalMissing += missing;
                        report.Warnings.Add($"[Scene] GameObject '{gos[j].name}' has {missing} missing script component(s).");
                    }
                }
            }

            if (totalMissing == 0)
            {
                report.Passes.Add("[Scene] No missing scripts detected.");
            }
        }

        private static void PrintReport(ValidationReport report)
        {
            for (int i = 0; i < report.Passes.Count; i++)
            {
                Debug.Log($"<color=#50fa7b><b>[PASS]</b></color> {report.Passes[i]}");
            }

            for (int i = 0; i < report.Warnings.Count; i++)
            {
                Debug.LogWarning($"<color=#ffb86c><b>[WARN]</b></color> {report.Warnings[i]}");
            }

            for (int i = 0; i < report.Errors.Count; i++)
            {
                Debug.LogError($"<color=#ff5555><b>[FAIL]</b></color> {report.Errors[i]}");
            }

            Debug.Log($"<b>[LevelValidator]</b> Audit Complete: {report.Passes.Count} Passed, {report.Warnings.Count} Warnings, {report.Errors.Count} Errors.");
        }
    }
}

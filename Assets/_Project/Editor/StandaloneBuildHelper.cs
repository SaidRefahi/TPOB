using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    public static class StandaloneBuildHelper
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string RoomsDirectory = "Assets/_Project/Scenes/Rooms";
        private static readonly string BuildRelativePath = Path.Combine("Builds", "Windows", "TPOB.exe");

        public static string GetExecutablePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, BuildRelativePath);
        }

        public static string[] GetAllScenePaths()
        {
            return new string[]
            {
                BootScenePath,
                $"{RoomsDirectory}/Room_01.unity",
                $"{RoomsDirectory}/Room_02.unity",
                $"{RoomsDirectory}/Room_03.unity",
                $"{RoomsDirectory}/Room_04.unity",
                $"{RoomsDirectory}/Room_05.unity",
                $"{RoomsDirectory}/Room_06.unity",
                $"{RoomsDirectory}/Room_07.unity",
                $"{RoomsDirectory}/Room_08.unity",
                $"{RoomsDirectory}/Room_09.unity",
                $"{RoomsDirectory}/Room_10.unity"
            };
        }

        [MenuItem("TPOB/5. Compilar Standalone Windows (x64)", false, 5)]
        public static bool BuildStandaloneWindows()
        {
            string exePath = GetExecutablePath();
            string outputDirectory = Path.GetDirectoryName(exePath);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string[] scenes = GetAllScenePaths();
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!File.Exists(scenes[i]))
                {
                    Debug.LogError($"[StandaloneBuildHelper] Escena no encontrada: {scenes[i]}. Abortando compilación.");
                    EditorUtility.DisplayDialog("Error de Compilación", $"Falta la escena requerida:\n{scenes[i]}", "Aceptar");
                    return false;
                }
            }

            Debug.Log($"<color=cyan>[StandaloneBuildHelper] Iniciando compilación de {scenes.Length} escenas en: {exePath}...</color>");

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                string sizeMb = (summary.totalSize / (1024f * 1024f)).ToString("F2");
                Debug.Log($"<color=#55FF55><b>[StandaloneBuildHelper] ¡Compilación Exitosa!</b></color> Tamaño: {sizeMb} MB, Duración: {summary.totalTime.TotalSeconds:F1}s.");
                EditorUtility.DisplayDialog(
                    "Compilación Exitosa",
                    $"TPOB Standalone Windows (x64) generado con éxito en:\n\n{exePath}\n\nTamaño: {sizeMb} MB\nDuración: {summary.totalTime.TotalSeconds:F1} s",
                    "Aceptar"
                );
                return true;
            }
            else
            {
                Debug.LogError($"[StandaloneBuildHelper] La compilación falló con estado: {summary.result}. Errores: {summary.totalErrors}");
                EditorUtility.DisplayDialog(
                    "Fallo de Compilación",
                    $"La compilación de Standalone Windows falló.\nErrores detectados: {summary.totalErrors}\n\nRevisa la consola de Unity para detalles.",
                    "Aceptar"
                );
                return false;
            }
        }

        [MenuItem("TPOB/6. Lanzar 2 Instancias Locales (Host + Cliente)", false, 6)]
        public static void LaunchTwoInstances()
        {
            string exePath = GetExecutablePath();

            if (!File.Exists(exePath))
            {
                bool shouldBuild = EditorUtility.DisplayDialog(
                    "Ejecutable No Encontrado",
                    $"No se encontró el ejecutable en:\n{exePath}\n\n¿Deseas compilar el juego ahora?",
                    "Compilar y Lanzar",
                    "Cancelar"
                );

                if (shouldBuild)
                {
                    if (!BuildStandaloneWindows())
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            try
            {
                string arguments = "-screen-width 1280 -screen-height 720 -window-mode windowed";

                // Instance 1: Host
                var startInfo1 = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                Process.Start(startInfo1);
                Debug.Log("<color=cyan>[StandaloneBuildHelper] Instancia 1 (Host) iniciada.</color>");

                // Instance 2: Client
                var startInfo2 = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                };
                Process.Start(startInfo2);
                Debug.Log("<color=cyan>[StandaloneBuildHelper] Instancia 2 (Cliente) iniciada.</color>");

                Debug.Log("<color=#55FF55><b>[StandaloneBuildHelper] Ambas instancias se han lanzado en modo ventana (1280x720).</b></color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[StandaloneBuildHelper] Error al lanzar instancias: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class ZeroGCAuditor
    {
        private static readonly string[] HotPathMethodNames = {
            "Update", "FixedUpdate", "LateUpdate", "OnTick", "OnFixedTick"
        };

        // Allowed value types where 'new' does not allocate on the heap
        private static readonly HashSet<string> AllowedValueTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Vector2", "Vector3", "Vector4", "Quaternion", "Color", "Color32",
            "Ray", "Ray2D", "Plane", "Bounds", "Matrix4x4", "LayerMask",
            "NativeArray", "PlayerSlot", "RPCInfo"
        };

        [MenuItem("TPOB/4. Auditar Código para Zero-GC", false, 4)]
        public static void AuditCodebaseZeroGC()
        {
            string projectPath = Application.dataPath;
            string targetFolder = Path.Combine(projectPath, "_Project");

            if (!Directory.Exists(targetFolder))
            {
                Debug.LogError($"[ZeroGCAuditor] Target folder not found: {targetFolder}");
                return;
            }

            string[] csFiles = Directory.GetFiles(targetFolder, "*.cs", SearchOption.AllDirectories);
            int filesScanned = 0;
            int totalIssuesFound = 0;
            var issues = new List<string>();

            foreach (string file in csFiles)
            {
                // Skip Editor scripts and auto-generated scripts
                if (file.Contains(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar) ||
                    file.Contains(Path.AltDirectorySeparatorChar + "Editor" + Path.AltDirectorySeparatorChar))
                {
                    continue;
                }

                filesScanned++;
                string content = File.ReadAllText(file);
                string relativePath = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');

                AuditFile(relativePath, content, issues);
            }

            totalIssuesFound = issues.Count;

            if (totalIssuesFound == 0)
            {
                Debug.Log($"<color=#55FF55><b>[ZeroGCAuditor] ¡ÉXITO!</b></color> Se auditaron {filesScanned} archivos de código de runtime en _Project sin detectar violaciones de Zero-GC en hot paths.");
                EditorUtility.DisplayDialog(
                    "Auditoría Zero-GC Exitosa",
                    $"Se auditaron {filesScanned} archivos de runtime.\n\n¡CERO violaciones detectadas en métodos Update/FixedUpdate/LateUpdate!",
                    "Aceptar"
                );
            }
            else
            {
                Debug.LogWarning($"<color=#FFAA00><b>[ZeroGCAuditor]</b></color> Se encontraron {totalIssuesFound} posibles advertencias de Zero-GC en {filesScanned} archivos.");
                foreach (string issue in issues)
                {
                    Debug.LogWarning(issue);
                }

                EditorUtility.DisplayDialog(
                    "Auditoría Zero-GC",
                    $"Se auditaron {filesScanned} archivos.\nSe detectaron {totalIssuesFound} advertencias en hot paths.\n\nRevisa la consola para más detalles.",
                    "Aceptar"
                );
            }
        }

        private static void AuditFile(string filePath, string content, List<string> issues)
        {
            string[] lines = content.Split('\n');
            bool insideHotPath = false;
            string currentMethod = "";
            int braceDepth = 0;
            int hotPathStartDepth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int lineNumber = i + 1;

                if (!insideHotPath)
                {
                    foreach (string methodName in HotPathMethodNames)
                    {
                        // Match method declarations like: void Update(), private void FixedUpdate()
                        if (Regex.IsMatch(line, $@"\b{methodName}\s*\("))
                        {
                            insideHotPath = true;
                            currentMethod = methodName;
                            hotPathStartDepth = braceDepth;
                            break;
                        }
                    }
                }

                // Count braces
                int openBraces = CountOccurrences(line, '{');
                int closeBraces = CountOccurrences(line, '}');
                braceDepth += openBraces - closeBraces;

                if (insideHotPath)
                {
                    // Check if we exited the hot path method
                    if (braceDepth <= hotPathStartDepth && (openBraces > 0 || closeBraces > 0))
                    {
                        insideHotPath = false;
                        currentMethod = "";
                        continue;
                    }

                    // Check for forbidden patterns in hot paths
                    AuditHotPathLine(filePath, lineNumber, line, currentMethod, issues);
                }
            }
        }

        private static void AuditHotPathLine(string filePath, int lineNumber, string line, string method, List<string> issues)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//") || line.StartsWith("/*"))
            {
                return;
            }

            // 1. Check for 'tag =='
            if (line.Contains(".tag ==") || line.Contains("== .tag"))
            {
                issues.Add($"[{filePath}:{lineNumber}] ({method}) Uso de '.tag =='. Reemplazar con 'CompareTag(...)'.");
            }

            // 2. Check for GameObject.Find / FindObjectsByType in hot path
            if (line.Contains("FindFirstObjectByType") || line.Contains("FindAnyObjectByType") ||
                line.Contains("FindObjectsByType") || line.Contains("GameObject.Find("))
            {
                issues.Add($"[{filePath}:{lineNumber}] ({method}) Búsqueda de objetos en escena dentro del hot path.");
            }

            // 3. Check for LINQ invocations in hot path
            if (Regex.IsMatch(line, @"\.(Where|Select|ToList|ToArray|First|FirstOrDefault|Any)\s*\("))
            {
                issues.Add($"[{filePath}:{lineNumber}] ({method}) Posible uso de LINQ en hot path (genera recolección de basura).");
            }

            // 4. Check for 'new ' reference type allocations
            var match = Regex.Match(line, @"\bnew\s+([A-Za-z0-9_<>]+)\s*(\(|\[)");
            if (match.Success)
            {
                string typeName = match.Groups[1].Value;
                if (!AllowedValueTypes.Contains(typeName) && !line.Contains("fixedDeltaTime") && !line.Contains("Time.deltaTime"))
                {
                    issues.Add($"[{filePath}:{lineNumber}] ({method}) Asignación de memoria ('new {typeName}') dentro del hot path.");
                }
            }
        }

        private static int CountOccurrences(string source, char target)
        {
            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == target) count++;
            }
            return count;
        }
    }
}

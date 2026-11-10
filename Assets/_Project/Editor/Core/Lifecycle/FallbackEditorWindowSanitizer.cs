using UnityEditor;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Detecta y purga automáticamente ventanas de tipo FallbackEditorWindow ('Failed to load')
    /// que hayan quedado huérfanas en el DockArea tras una recompilación o error previo,
    /// impidiendo que WindowLayout.CheckWindowConsistency() arroje errores al maximizar ventanas.
    /// </summary>
    [InitializeOnLoad]
    public static class FallbackEditorWindowSanitizer
    {
        static FallbackEditorWindowSanitizer()
        {
            EditorApplication.delayCall += PurgeFallbackWindows;
        }

        [MenuItem("TPOB/Herramientas/Purgar Ventanas Corruptas (Failed to load)", false, 101)]
        [MenuItem("Window/TPOB/Purgar Ventanas Corruptas", false, 101)]
        public static void PurgeFallbackWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            int purged = 0;

            for (int i = 0; i < windows.Length; i++)
            {
                var win = windows[i];
                if (win == null) continue;

                string typeName = win.GetType().FullName ?? string.Empty;
                string title = win.titleContent != null ? win.titleContent.text : string.Empty;

                if (typeName.Contains("FallbackEditorWindow") || title.Contains("Failed to load"))
                {
                    try
                    {
                        win.Close();
                    }
                    catch
                    {
                        // FallbackEditorWindow can throw NRE inside UnityEditor.EditorWindow.Close()
                        // when internal host view / dock references are uninitialized or corrupted.
                    }

                    try
                    {
                        Object.DestroyImmediate(win, true);
                        purged++;
                    }
                    catch
                    {
                        // Guard against secondary destruction failures
                    }
                }
            }

            if (purged > 0)
            {
                Debug.Log($"<color=#50fa7b>[TPOB]</color> Se purgaron {purged} ventana(s) inválida(s) de tipo FallbackEditorWindow.");
            }
        }
    }
}

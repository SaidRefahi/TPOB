#if UNITY_EDITOR
using Game.Editor.Core.Lifecycle;

namespace Game.Editor.LevelEditor
{
    public static class InspectorSanitizer
    {
        public static void SanitizeSelectionAndInspectors()
        {
            InspectorTrackerSanitizer.SanitizeActiveEditors();
            InspectorTrackerSanitizer.UnlockInspectors(forceUnlock: false);
        }

        public static void ClearConsole()
        {
            InspectorTrackerSanitizer.ClearConsole();
        }

        public static void ManualSanitize()
        {
            SelectionMementoService.Instance.ClearSelectionSafely();
            InspectorTrackerSanitizer.ClearConsole();
        }
    }
}
#endif

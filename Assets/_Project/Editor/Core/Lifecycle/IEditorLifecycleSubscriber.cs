#if UNITY_EDITOR
namespace Game.Editor.Core.Lifecycle
{
    public interface IEditorLifecycleSubscriber
    {
        void OnBeforePlayModeEntered();
        void OnPlayModeEntered();
        void OnBeforePlayModeExited();
        void OnPlayModeExited();
        void OnBeforeDomainReload();
        void OnAfterDomainReload();
    }
}
#endif

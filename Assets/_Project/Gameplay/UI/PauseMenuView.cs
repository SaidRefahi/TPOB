using UnityEngine;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuView : MonoBehaviour
    {
        [Header("Contenedor Raíz de Pausa")]
        [SerializeField] private GameObject _pauseRoot;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Botones Principales")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _optionsButton;
        [SerializeField] private Button _exitToMenuButton;
        [SerializeField] private Button _exitToDesktopButton;

        [Header("Diálogo de Configuración")]
        [SerializeField] private GameObject _settingsDialog;

        public Button ResumeButton => _resumeButton;
        public Button OptionsButton => _optionsButton;
        public Button ExitToMenuButton => _exitToMenuButton;
        public Button ExitToDesktopButton => _exitToDesktopButton;
        public GameObject SettingsDialog => _settingsDialog;
        public GameObject PauseRoot => _pauseRoot;
        public CanvasGroup CanvasGroup => _canvasGroup;

        public bool IsPauseActive => _pauseRoot != null && _pauseRoot.activeSelf;
        public bool IsSettingsActive => _settingsDialog != null && _settingsDialog.activeSelf;

        public void SetPauseActive(bool active)
        {
            if (_pauseRoot != null)
            {
                _pauseRoot.SetActive(active);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = active ? 1f : 0f;
                _canvasGroup.interactable = active;
                _canvasGroup.blocksRaycasts = active;
            }
        }

        public void SetSettingsActive(bool active)
        {
            if (_settingsDialog != null)
            {
                _settingsDialog.SetActive(active);
            }
        }
    }
}

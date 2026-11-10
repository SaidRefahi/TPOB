using Cysharp.Threading.Tasks;
using PurrNet;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class VictoryPresenter : MonoBehaviour
    {
        [Header("Botones")]
        [SerializeField] private Button _mainMenuButton;
        [SerializeField] private Button _retryButton;

        private void OnEnable()
        {
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.AddListener(HandleMainMenuClicked);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(HandleRetryClicked);
            }
        }

        private void OnDisable()
        {
            if (_mainMenuButton != null)
            {
                _mainMenuButton.onClick.RemoveListener(HandleMainMenuClicked);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveListener(HandleRetryClicked);
            }
        }

        private void HandleMainMenuClicked()
        {
            var nm = NetworkManager.main;
            if (nm != null)
            {
                if (nm.isServer)
                {
                    nm.StopServer();
                }

                if (nm.isClient)
                {
                    nm.StopClient();
                }
            }

            if (UIInputModuleFixer.Instance != null)
            {
                Destroy(UIInputModuleFixer.Instance.gameObject);
            }

            SceneManager.LoadScene("Boot");
        }

        private void HandleRetryClicked()
        {
            var levelManager = Object.FindFirstObjectByType<Game.Network.Services.LevelManager>();
            if (levelManager != null)
            {
                levelManager.LoadRoomAsync(0).Forget();
            }
            else
            {
                if (UIInputModuleFixer.Instance != null)
                {
                    Destroy(UIInputModuleFixer.Instance.gameObject);
                }

                SceneManager.LoadScene("Boot");
            }
        }
    }
}

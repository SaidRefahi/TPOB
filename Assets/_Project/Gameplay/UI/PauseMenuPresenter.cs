using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        [SerializeField] private PauseMenuView _view;

        private static PauseMenuPresenter _instance;

        private INetworkService _networkService;
        private IGameManager _gameManager;
        private IGameEventBus _eventBus;
        private RobotCoordinator _cachedCoordinator;

        private bool _isPaused;

        [Inject]
        public void Construct(INetworkService networkService, IGameManager gameManager, IGameEventBus eventBus)
        {
            _networkService = networkService;
            _gameManager = gameManager;
            _eventBus = eventBus;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            ResolveDependencies();
            InitializeView();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            UnsubscribeEvents();
            UnbindCoordinator();
        }

        private void ResolveDependencies()
        {
            if (_networkService == null || _gameManager == null)
            {
                var container = Game.Network.Scopes.GameLifetimeScope.Instance != null 
                    ? Game.Network.Scopes.GameLifetimeScope.Instance.Container 
                    : null;

                if (container == null)
                {
                    var lifetimeScope = UnityEngine.Object.FindFirstObjectByType<VContainer.Unity.LifetimeScope>();
                    if (lifetimeScope != null)
                    {
                        container = lifetimeScope.Container;
                    }
                }

                if (container != null)
                {
                    try
                    {
                        _networkService ??= container.Resolve<INetworkService>();
                        _gameManager ??= container.Resolve<IGameManager>();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[PauseMenuPresenter] Container resolve notice: {ex.Message}");
                    }
                }
            }

            if (_view == null)
            {
                _view = GetComponent<PauseMenuView>();
            }

            BindCoordinator();
        }

        private void BindCoordinator()
        {
            if (_cachedCoordinator != null)
            {
                UnbindCoordinator();
            }

            _cachedCoordinator = Object.FindFirstObjectByType<RobotCoordinator>();
            if (_cachedCoordinator != null)
            {
                _cachedCoordinator.OnPauseStateChanged -= HandleNetworkPauseStateChanged;
                _cachedCoordinator.OnPauseStateChanged += HandleNetworkPauseStateChanged;
            }
        }

        private void UnbindCoordinator()
        {
            if (_cachedCoordinator != null)
            {
                _cachedCoordinator.OnPauseStateChanged -= HandleNetworkPauseStateChanged;
                _cachedCoordinator = null;
            }
        }

        private void InitializeView()
        {
            if (_view == null) return;

            _view.SetPauseActive(false);
            _view.SetSettingsActive(false);

            if (_view.SettingsView != null && _view.SettingsView.CloseButton != null)
            {
                _view.SettingsView.CloseButton.onClick.RemoveListener(HandleCloseSettingsClicked);
                _view.SettingsView.CloseButton.onClick.AddListener(HandleCloseSettingsClicked);
            }
        }

        private void SubscribeEvents()
        {
            if (_view != null)
            {
                if (_view.ResumeButton != null)
                    _view.ResumeButton.onClick.AddListener(HandleResumeClicked);

                if (_view.OptionsButton != null)
                    _view.OptionsButton.onClick.AddListener(HandleOptionsClicked);

                if (_view.ExitToMenuButton != null)
                    _view.ExitToMenuButton.onClick.AddListener(HandleExitToMenuClicked);

                if (_view.ExitToDesktopButton != null)
                    _view.ExitToDesktopButton.onClick.AddListener(HandleExitToDesktopClicked);
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
                _gameManager.OnGameStateChanged += HandleGameStateChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (_view != null)
            {
                if (_view.ResumeButton != null)
                    _view.ResumeButton.onClick.RemoveListener(HandleResumeClicked);

                if (_view.OptionsButton != null)
                    _view.OptionsButton.onClick.RemoveListener(HandleOptionsClicked);

                if (_view.ExitToMenuButton != null)
                    _view.ExitToMenuButton.onClick.RemoveListener(HandleExitToMenuClicked);

                if (_view.ExitToDesktopButton != null)
                    _view.ExitToDesktopButton.onClick.RemoveListener(HandleExitToDesktopClicked);

                if (_view.SettingsView != null && _view.SettingsView.CloseButton != null)
                {
                    _view.SettingsView.CloseButton.onClick.RemoveListener(HandleCloseSettingsClicked);
                }
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Update()
        {
            // Listen for Escape key or Gamepad Start
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            bool pausePressed = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                                (gamepad != null && gamepad.startButton.wasPressedThisFrame);

            if (!pausePressed)
            {
                return;
            }

            // Only permit pausing during InGame or Paused state
            if (_gameManager != null && 
                _gameManager.CurrentState != GameState.InGame && 
                _gameManager.CurrentState != GameState.Paused)
            {
                return;
            }

            // If settings modal is open inside pause menu, close settings first and return to pause menu
            if (_view != null && _view.IsSettingsActive)
            {
                _view.SetSettingsActive(false);
                return;
            }

            TogglePause();
        }

        public void TogglePause()
        {
            if (_cachedCoordinator == null)
            {
                BindCoordinator();
            }

            if (_cachedCoordinator != null && _cachedCoordinator.isSpawned)
            {
                _cachedCoordinator.RequestTogglePause();
            }
            else
            {
                ApplyPauseLocally(!_isPaused);
            }
        }

        private void HandleNetworkPauseStateChanged(bool paused)
        {
            ApplyPauseLocally(paused);
        }

        private void ApplyPauseLocally(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (_view != null)
            {
                _view.SetPauseActive(paused);
                if (!paused)
                {
                    _view.SetSettingsActive(false);
                }
                else
                {
                    UpdateNetworkStatusBadge();
                }
            }

            SetPlayerInputEnabled(!paused);

            if (paused)
            {
                UIInputModuleFixer.EnsureUIFocusAndInput();

                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null && _view != null && _view.ResumeButton != null)
                {
                    eventSystem.SetSelectedGameObject(_view.ResumeButton.gameObject);
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                var eventSystem = UnityEngine.EventSystems.EventSystem.current;
                if (eventSystem != null)
                {
                    eventSystem.SetSelectedGameObject(null);
                }
            }

            _gameManager?.ChangeState(paused ? GameState.Paused : GameState.InGame);
        }

        private void UpdateNetworkStatusBadge()
        {
            if (_networkService != null && _view != null)
            {
                string quality = _networkService.ConnectionQualityText;
                Color color = (quality.StartsWith("P2P") || quality.StartsWith("Red Local"))
                    ? new Color(0.2f, 1f, 0.4f)
                    : new Color(1f, 0.85f, 0.2f);
                _view.SetConnectionQuality($"<size=14><color=#88AACC>Enlace de Red: </color><b>{quality}</b></size>", color);
            }
        }

        private void SetPlayerInputEnabled(bool isEnabled)
        {
            var legsInput = UnityEngine.Object.FindFirstObjectByType<LegsInputReader>();
            if (legsInput != null)
            {
                legsInput.enabled = isEnabled;
            }

            var torsoInput = UnityEngine.Object.FindFirstObjectByType<TorsoInputReader>();
            if (torsoInput != null)
            {
                torsoInput.enabled = isEnabled;
            }
        }

        private void HandleResumeClicked()
        {
            TogglePause();
        }

        private void HandleOptionsClicked()
        {
            if (_view != null)
            {
                _view.SetSettingsActive(true);
            }
        }

        private void HandleCloseSettingsClicked()
        {
            if (_view != null)
            {
                _view.SetSettingsActive(false);
            }
        }

        private void HandleExitToMenuClicked()
        {
            if (_cachedCoordinator != null && _cachedCoordinator.isSpawned && _isPaused)
            {
                _cachedCoordinator.RequestTogglePause();
            }

            ApplyPauseLocally(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _networkService?.Disconnect();

            if (_gameManager != null)
            {
                _gameManager.IsLocalMode = false;
            }

            SceneManager.LoadScene("Boot");
        }

        private void HandleExitToDesktopClicked()
        {
            Time.timeScale = 1f;
            _networkService?.Disconnect();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch { }
#endif
        }

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState != GameState.InGame && newState != GameState.Paused)
            {
                if (_isPaused)
                {
                    ApplyPauseLocally(false);
                }
            }
        }
    }
}

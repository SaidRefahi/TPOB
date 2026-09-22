using System;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PauseMenuView))]
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        private PauseMenuView _view;
        private IGameManager _gameManager;
        private INetworkService _networkService;

        private bool _isPaused;

        [Inject]
        public void Construct(IGameManager gameManager, INetworkService networkService)
        {
            _gameManager = gameManager;
            _networkService = networkService;
        }

        private void Awake()
        {
            _view = GetComponent<PauseMenuView>();
        }

        private void Start()
        {
            ResolveDependencies();
            InitializeView();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void Update()
        {
            CheckPauseInput();
        }

        private void ResolveDependencies()
        {
            if (_networkService != null && _gameManager != null)
            {
                return;
            }

            var scopes = UnityEngine.Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] != null && scopes[i].Container != null)
                {
                    try
                    {
                        if (_networkService == null)
                        {
                            _networkService = scopes[i].Container.Resolve<INetworkService>();
                        }

                        if (_gameManager == null)
                        {
                            _gameManager = scopes[i].Container.Resolve<IGameManager>();
                        }

                        if (_networkService != null && _gameManager != null)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // Scope does not contain the requested services
                    }
                }
            }
        }

        private void InitializeView()
        {
            _isPaused = false;
            if (_view != null)
            {
                _view.SetPauseActive(false);
                _view.SetSettingsActive(false);
            }
        }

        private void SubscribeEvents()
        {
            if (_view != null)
            {
                if (_view.ResumeButton != null)
                {
                    _view.ResumeButton.onClick.AddListener(HandleResumeClicked);
                }

                if (_view.OptionsButton != null)
                {
                    _view.OptionsButton.onClick.AddListener(HandleOptionsClicked);
                }

                if (_view.ExitToMenuButton != null)
                {
                    _view.ExitToMenuButton.onClick.AddListener(HandleExitToMenuClicked);
                }

                if (_view.ExitToDesktopButton != null)
                {
                    _view.ExitToDesktopButton.onClick.AddListener(HandleExitToDesktopClicked);
                }
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
                {
                    _view.ResumeButton.onClick.RemoveListener(HandleResumeClicked);
                }

                if (_view.OptionsButton != null)
                {
                    _view.OptionsButton.onClick.RemoveListener(HandleOptionsClicked);
                }

                if (_view.ExitToMenuButton != null)
                {
                    _view.ExitToMenuButton.onClick.RemoveListener(HandleExitToMenuClicked);
                }

                if (_view.ExitToDesktopButton != null)
                {
                    _view.ExitToDesktopButton.onClick.RemoveListener(HandleExitToDesktopClicked);
                }
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void CheckPauseInput()
        {
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

            // If settings modal is open inside pause menu, close settings first
            if (_view != null && _view.IsSettingsActive)
            {
                _view.SetSettingsActive(false);
                return;
            }

            TogglePause();
        }

        public void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_view != null)
            {
                _view.SetPauseActive(_isPaused);
                if (!_isPaused)
                {
                    _view.SetSettingsActive(false);
                }
            }

            // 1. Soft-Pause: Suspend local player inputs without altering Time.timeScale
            SetPlayerInputEnabled(!_isPaused);

            // 2. Cursor management
            Cursor.lockState = _isPaused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _isPaused;

            // 3. Game state update
            _gameManager?.ChangeState(_isPaused ? GameState.Paused : GameState.InGame);
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
            if (_isPaused)
            {
                TogglePause();
            }
        }

        private void HandleOptionsClicked()
        {
            if (_view != null)
            {
                _view.SetSettingsActive(true);
            }
        }

        private void HandleExitToMenuClicked()
        {
            _isPaused = false;
            if (_view != null)
            {
                _view.SetPauseActive(false);
                _view.SetSettingsActive(false);
            }

            // Restore cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Disconnect network session cleanly
            _networkService?.Disconnect();

            // Load Boot scene
            SceneManager.LoadScene("Boot");
        }

        private void HandleExitToDesktopClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void HandleGameStateChanged(GameState oldState, GameState newState)
        {
            if (newState != GameState.InGame && newState != GameState.Paused)
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    if (_view != null)
                    {
                        _view.SetPauseActive(false);
                        _view.SetSettingsActive(false);
                    }
                    SetPlayerInputEnabled(true);
                }
            }
        }
    }
}

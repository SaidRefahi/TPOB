using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using PurrNet;
using PurrNet.Modules;
using TriInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.Network.Services
{
    [DeclareBoxGroup("Configuración de Salas")]
    [DeclareBoxGroup("Estado Actual")]
    public sealed class LevelManager : MonoBehaviour, ILevelManager
    {
        [Group("Configuración de Salas")]
        [SerializeField] private string[] _roomScenes = new string[]
        {
            "Room_01",
            "Room_02",
            "Room_03",
            "Room_04",
            "Room_05",
            "Room_06",
            "Room_07",
            "Room_08",
            "Room_09",
            "Room_10"
        };

        [Group("Estado Actual")]
        [ShowInInspector, ReadOnly]
        public int CurrentRoomIndex { get; private set; } = -1;

        [Group("Estado Actual")]
        [ShowInInspector, ReadOnly]
        public string CurrentRoomName => (CurrentRoomIndex >= 0 && CurrentRoomIndex < _roomScenes.Length) 
            ? _roomScenes[CurrentRoomIndex] 
            : string.Empty;

        [Group("Estado Actual")]
        [ShowInInspector, ReadOnly]
        public int TotalRooms => _roomScenes != null ? _roomScenes.Length : 0;

        [Group("Estado Actual")]
        [ShowInInspector, ReadOnly]
        public bool IsLastRoom => CurrentRoomIndex >= 0 && CurrentRoomIndex >= TotalRooms - 1;

        [Group("Estado Actual")]
        [ShowInInspector, ReadOnly]
        public bool IsLoading { get; private set; }

        public event Action<int, string> OnRoomLoaded;
        public event Action<int, string> OnRoomUnloaded;

        private INetworkService _networkService;
        private IGameManager _gameManager;

        [Inject]
        public void Construct(INetworkService networkService, IGameManager gameManager)
        {
            _networkService = networkService;
            _gameManager = gameManager;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        public async UniTask LoadRoomAsync(int roomIndex, CancellationToken ct = default)
        {
            if (roomIndex < 0 || roomIndex >= _roomScenes.Length)
            {
                Debug.LogWarning($"[LevelManager] Invalid room index: {roomIndex}");
                return;
            }

            if (IsLoading)
            {
                Debug.LogWarning("[LevelManager] Room load already in progress.");
                return;
            }

            IsLoading = true;
            string sceneName = _roomScenes[roomIndex];
            int previousIndex = CurrentRoomIndex;

            try
            {
                var cancelToken = this.GetCancellationTokenOnDestroy();

                if (_networkService != null && _networkService.IsServer)
                {
                    await LoadRoomServerAuthoritativeAsync(sceneName, cancelToken);
                }
                else
                {
                    // Standalone local or offline preview fallback
                    AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                    if (op != null)
                    {
                        await op.ToUniTask(cancellationToken: cancelToken);
                    }
                }

                CurrentRoomIndex = roomIndex;
            }
            catch (OperationCanceledException)
            {
                Debug.Log($"[LevelManager] LoadRoomAsync for '{sceneName}' was cancelled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LevelManager] Error loading room '{sceneName}': {ex}");
            }
            finally
            {
                IsLoading = false;
            }

            if (previousIndex >= 0 && previousIndex < _roomScenes.Length)
            {
                OnRoomUnloaded?.Invoke(previousIndex, _roomScenes[previousIndex]);
            }

            OnRoomLoaded?.Invoke(CurrentRoomIndex, sceneName);
            _gameManager?.ChangeState(GameState.InGame);
        }

        private async UniTask LoadRoomServerAuthoritativeAsync(string sceneName, CancellationToken ct)
        {
            if (NetworkManager.main != null && NetworkManager.main.TryGetModule<ScenesModule>(true, out var scenesModule))
            {
                var settings = new PurrSceneSettings
                {
                    mode = LoadSceneMode.Single,
                    physicsMode = LocalPhysicsMode.None,
                    isPublic = true
                };

                AsyncOperation op = scenesModule.LoadSceneAsync(sceneName, settings);
                if (op != null)
                {
                    await op.ToUniTask(cancellationToken: ct);
                }
            }
            else
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (op != null)
                {
                    await op.ToUniTask(cancellationToken: ct);
                }
            }
        }

        public async UniTask AdvanceToNextRoomAsync(CancellationToken ct = default)
        {
            int nextIndex = CurrentRoomIndex + 1;
            if (nextIndex < _roomScenes.Length)
            {
                await LoadRoomAsync(nextIndex, ct);
            }
            else
            {
                Debug.Log("[LevelManager] All rooms completed! Setting GameOver state.");
                _gameManager?.ChangeState(GameState.GameOver);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Verifies if the loaded scene is one of our registered rooms
            for (int i = 0; i < _roomScenes.Length; i++)
            {
                if (_roomScenes[i] == scene.name)
                {
                    CurrentRoomIndex = i;
                    IsLoading = false;
                    OnRoomLoaded?.Invoke(i, scene.name);
                    _gameManager?.ChangeState(GameState.InGame);
                    break;
                }
            }
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            for (int i = 0; i < _roomScenes.Length; i++)
            {
                if (_roomScenes[i] == scene.name)
                {
                    OnRoomUnloaded?.Invoke(i, scene.name);
                    break;
                }
            }
        }

        [Button("Cargar Sala 1 (Servidor)")]
        private void DebugLoadRoom0()
        {
            LoadRoomAsync(0, this.GetCancellationTokenOnDestroy()).Forget();
        }

        [Button("Cargar Sala 2 (Servidor)")]
        private void DebugLoadRoom1()
        {
            LoadRoomAsync(1, this.GetCancellationTokenOnDestroy()).Forget();
        }

        [Button("Avanzar a Siguiente Sala")]
        private void DebugAdvanceRoom()
        {
            AdvanceToNextRoomAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }
}

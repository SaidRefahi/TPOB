using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using UnityEngine;
using VContainer;

namespace Game.Network.UI
{
    [DisallowMultipleComponent]
    public sealed class ConnectionHUD : MonoBehaviour
    {
        [SerializeField] private bool _showGUI = true;
        [SerializeField] private Vector2 _guiPosition = new Vector2(20, 20);

        private INetworkService _networkService;
        private IPlayerRegistry _playerRegistry;
        private ILevelManager _levelManager;
        private IGameManager _gameManager;

        [Inject]
        public void Construct(
            INetworkService networkService, 
            IPlayerRegistry playerRegistry,
            ILevelManager levelManager,
            IGameManager gameManager)
        {
            _networkService = networkService;
            _playerRegistry = playerRegistry;
            _levelManager = levelManager;
            _gameManager = gameManager;
        }

        private void OnGUI()
        {
            if (!_showGUI) return;

            GUILayout.BeginArea(new Rect(_guiPosition.x, _guiPosition.y, 320, 420), GUI.skin.box);
            GUILayout.Label("<b>TPOB — Control Global</b>");

            string gameStateStr = _gameManager != null ? _gameManager.CurrentState.ToString() : "Unknown";
            GUILayout.Label($"Estado de Juego: <b>{gameStateStr}</b>");

            bool isConnected = _networkService != null && _networkService.IsConnected;

            if (!isConnected)
            {
                if (GUILayout.Button("Start Host (Server + Client)", GUILayout.Height(35)))
                {
                    _networkService?.StartHost();
                }

                if (GUILayout.Button("Start Client", GUILayout.Height(35)))
                {
                    _networkService?.StartClient();
                }
            }
            else
            {
                string mode = _networkService.IsServer ? "Host (Server)" : "Client";
                GUILayout.Label($"Conexión: <color=green>Conectado como {mode}</color>");

                PlayerRole role = _playerRegistry != null ? _playerRegistry.LocalRole : PlayerRole.None;
                string roleColor = role switch
                {
                    PlayerRole.Legs => "cyan",
                    PlayerRole.Torso => "orange",
                    _ => "white"
                };
                GUILayout.Label($"Rol Asignado: <color={roleColor}><b>{role}</b></color>");

                if (_levelManager != null)
                {
                    string roomStr = string.IsNullOrEmpty(_levelManager.CurrentRoomName) ? "Ninguna (Lobby)" : _levelManager.CurrentRoomName;
                    GUILayout.Label($"Sala Actual: <b>{roomStr}</b>");
                }

                GUILayout.Space(8);
                GUILayout.Label("Jugadores en Sesión:");
                if (_playerRegistry != null)
                {
                    var players = _playerRegistry.ConnectedPlayers;
                    for (int i = 0; i < players.Count; i++)
                    {
                        var p = players[i];
                        string localMarker = p.IsLocal ? " (Tú)" : "";
                        GUILayout.Label($" • Jugador {p.PlayerId}: {p.Role}{localMarker}");
                    }
                }

                GUILayout.Space(8);
                // Host controls for level sequencing
                if (_networkService != null && _networkService.IsServer && _levelManager != null)
                {
                    if (_levelManager.IsLoading)
                    {
                        GUILayout.Label("<color=yellow>Cargando sala en red...</color>");
                    }
                    else
                    {
                        if (_levelManager.CurrentRoomIndex < 0)
                        {
                            if (GUILayout.Button("Iniciar Partida (Cargar Sala 1)", GUILayout.Height(32)))
                            {
                                _levelManager.LoadRoomAsync(0, this.GetCancellationTokenOnDestroy()).Forget();
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Avanzar a Siguiente Sala", GUILayout.Height(32)))
                            {
                                _levelManager.AdvanceToNextRoomAsync(this.GetCancellationTokenOnDestroy()).Forget();
                            }
                        }
                    }
                }

                GUILayout.Space(8);
                if (GUILayout.Button("Desconectar", GUILayout.Height(28)))
                {
                    _networkService?.Disconnect();
                }
            }

            GUILayout.EndArea();
        }
    }
}

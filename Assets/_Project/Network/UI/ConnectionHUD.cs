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

        [Inject]
        public void Construct(INetworkService networkService, IPlayerRegistry playerRegistry)
        {
            _networkService = networkService;
            _playerRegistry = playerRegistry;
        }

        private void OnGUI()
        {
            if (!_showGUI) return;

            GUILayout.BeginArea(new Rect(_guiPosition.x, _guiPosition.y, 300, 320), GUI.skin.box);
            GUILayout.Label("<b>TPOB — Network Connection</b>");

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
                GUILayout.Label($"Status: <color=green>Connected as {mode}</color>");

                PlayerRole role = _playerRegistry != null ? _playerRegistry.LocalRole : PlayerRole.None;
                string roleColor = role switch
                {
                    PlayerRole.Legs => "cyan",
                    PlayerRole.Torso => "orange",
                    _ => "white"
                };
                GUILayout.Label($"Assigned Role: <color={roleColor}><b>{role}</b></color>");

                GUILayout.Space(10);
                GUILayout.Label("Connected Players:");
                if (_playerRegistry != null)
                {
                    var players = _playerRegistry.ConnectedPlayers;
                    for (int i = 0; i < players.Count; i++)
                    {
                        var p = players[i];
                        string localMarker = p.IsLocal ? " (You)" : "";
                        GUILayout.Label($" • Player {p.PlayerId}: {p.Role}{localMarker}");
                    }
                }

                GUILayout.Space(10);
                if (GUILayout.Button("Disconnect", GUILayout.Height(30)))
                {
                    _networkService?.Disconnect();
                }
            }

            GUILayout.EndArea();
        }
    }
}

using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Network.Events;
using Game.Network.Scopes;
using PurrNet;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Network.UI
{
    [DisallowMultipleComponent]
    public sealed class ConnectionHUD : MonoBehaviour
    {
        [SerializeField] private bool _showGUI = true;
        [SerializeField] private Vector2 _guiPosition = new Vector2(20, 20);
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private string _serverPort = "5000";

        private INetworkService _networkService;
        private IPlayerRegistry _playerRegistry;
        private ILevelManager _levelManager;
        private IGameManager _gameManager;
        private IGameEventBus _eventBus;

        private Vector2 _scrollPosition;
        private bool _showDebugFold;
        private bool _showHelpFold = true;

        [Inject]
        public void Construct(
            INetworkService networkService,
            IPlayerRegistry playerRegistry,
            ILevelManager levelManager,
            IGameManager gameManager,
            IGameEventBus eventBus = null)
        {
            _networkService = networkService;
            _playerRegistry = playerRegistry;
            _levelManager = levelManager;
            _gameManager = gameManager;
            _eventBus = eventBus;
        }

        private void Start()
        {
            if (_networkService == null || _playerRegistry == null)
            {
                var scopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
                for (int i = 0; i < scopes.Length; i++)
                {
                    if (scopes[i] != null && scopes[i].Container != null)
                    {
                        scopes[i].Container.Inject(this);
                        if (_networkService != null) break;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (!_showGUI) return;

            bool isConnected = _networkService != null && _networkService.IsConnected;
            float panelWidth = 370f;
            float panelHeight = isConnected ? 600f : 440f;

            GUILayout.BeginArea(new Rect(_guiPosition.x, _guiPosition.y, panelWidth, panelHeight), GUI.skin.box);
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            DrawHeader(isConnected);

            if (!isConnected)
            {
                DrawConnectionControls();
            }
            else
            {
                DrawRoleCard();
                DrawPlayersList();
                DrawActionControls();
                DrawLevelControls();
                DrawDebugControls();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHeader(bool isConnected)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b><size=15>TPOB — Cooperativo Multijugador</size></b>");

            string gameStateStr = _gameManager != null ? _gameManager.CurrentState.ToString() : "Lobby";
            GUILayout.Label($"Estado de Partida: <b>{gameStateStr}</b>");

            if (isConnected)
            {
                string mode = _networkService != null && _networkService.IsServer ? "Host (Servidor + Jugador 1)" : "Cliente (Jugador 2)";
                GUILayout.Label($"Conexión: <color=#55FF55><b>● En Línea</b> ({mode})</color>");
            }
            else
            {
                GUILayout.Label("Conexión: <color=#FFAA00>○ Desconectado</color>");
            }
            GUILayout.EndVertical();
        }

        private void DrawConnectionControls()
        {
            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>1. Crear Servidor (Host)</b>");
            GUILayout.Label("<size=11>Inicia el juego como Servidor y toma el rol de Piernas.</size>");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Puerto:", GUILayout.Width(60));
            _serverPort = GUILayout.TextField(_serverPort, GUILayout.Width(80));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("▶ Iniciar como Host", GUILayout.Height(35)))
            {
                if (ushort.TryParse(_serverPort, out ushort port))
                {
                    _networkService?.StartHost(port);
                }
                else
                {
                    _networkService?.StartHost();
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>2. Unirse a Partida (Join Client)</b>");
            GUILayout.Label("<size=11>Únete al Host existente y toma el rol de Torso.</size>");

            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:", GUILayout.Width(60));
            _serverAddress = GUILayout.TextField(_serverAddress, GUILayout.Width(130));
            GUILayout.Label("Puerto:", GUILayout.Width(50));
            _serverPort = GUILayout.TextField(_serverPort, GUILayout.Width(60));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("⚡ Unirse como Cliente", GUILayout.Height(35)))
            {
                if (ushort.TryParse(_serverPort, out ushort port))
                {
                    _networkService?.StartClient(_serverAddress, port);
                }
                else
                {
                    _networkService?.StartClient();
                }
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);
            _showHelpFold = GUILayout.Toggle(_showHelpFold, "<b>¿Cómo jugar con dos ventanas?</b>");
            if (_showHelpFold)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("<size=11>1. En Unity: abre <b>Multiplayer Play Mode</b>.\n2. Ventana 1: Clic en <b>Iniciar como Host</b>.\n3. Ventana 2: Clic en <b>Unirse como Cliente</b>.\n4. ¡Ambas partes se vincularán automáticamente!</size>");
                GUILayout.EndVertical();
            }
        }

        private void DrawRoleCard()
        {
            GUILayout.Space(6);

            // Determine role by actual NetworkIdentity ownership first, fallback to registry
            PlayerRole role = PlayerRole.None;
            var identities = Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
            for (int i = 0; i < identities.Length; i++)
            {
                var id = identities[i];
                if (id != null && id.isOwner)
                {
                    string name = id.gameObject.name;
                    if (name.IndexOf("Legs", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        role = PlayerRole.Legs;
                        break;
                    }
                    else if (name.IndexOf("Torso", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        role = PlayerRole.Torso;
                        break;
                    }
                }
            }

            if (role == PlayerRole.None && _playerRegistry != null && _playerRegistry.LocalRole != PlayerRole.None)
            {
                role = _playerRegistry.LocalRole;
            }

            if (role == PlayerRole.Legs)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("<color=#00FFFF><b><size=14>🦵 TU ROL: PIERNAS (Jugador 1)</size></b></color>");
                GUILayout.Label("<b>Misión:</b> Movilidad, saltos y potencia física.");
                GUILayout.Label("<b>Controles Asignados:</b>\n" +
                                " • <b>[WASD] / [Stick Izq]</b>: Moverse y posicionarse\n" +
                                " • <b>[Espacio] / [Botón A]</b>: Saltar / Doble salto\n" +
                                " • <b>[Shift] / [Gatillo Izq]</b>: Sprint / Acelerón\n" +
                                " • <b>[F] / [Botón X]</b>: Patada física a objetos/enemigos\n" +
                                " • <b>[R] / [Botón Y]</b>: Acoplarse / Desacoplarse del Torso");
                GUILayout.EndVertical();
            }
            else if (role == PlayerRole.Torso)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("<color=#FF9900><b><size=14>🤖 TU ROL: TORSO (Jugador 2)</size></b></color>");
                GUILayout.Label("<b>Misión:</b> Manipulación, puntería, imán y resolución.");
                GUILayout.Label("<b>Controles Asignados:</b>\n" +
                                " • <b>[Mouse] / [Stick Der]</b>: Apuntar con precisión\n" +
                                " • <b>[Click Izq / E]</b>: Agarrar / Soltar objeto\n" +
                                " • <b>[Click Der / Q]</b>: Lanzamiento con impulso\n" +
                                " • <b>[Shift / C]</b>: Activar Rayo Magnético tractor\n" +
                                " • <b>[WASD]</b>: Moverse rodando (si estás separado)\n" +
                                " • <b>[R] / [Botón Y]</b>: Acoplarse / Desacoplarse de Piernas");
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("<color=#CCCCCC><b><size=13>⏳ SINCRONIZANDO ROL...</size></b></color>");
                GUILayout.Label("Esperando que el servidor asigne Piernas o Torso.");
                GUILayout.EndVertical();
            }
        }

        private void DrawPlayersList()
        {
            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Control de Avatares en Red:</b>");

            var identities = Object.FindObjectsByType<NetworkIdentity>(FindObjectsSortMode.None);
            NetworkIdentity legsId = null;
            NetworkIdentity torsoId = null;

            for (int i = 0; i < identities.Length; i++)
            {
                var id = identities[i];
                if (id == null) continue;
                if (id.gameObject.name.IndexOf("Legs", System.StringComparison.OrdinalIgnoreCase) >= 0) legsId = id;
                else if (id.gameObject.name.IndexOf("Torso", System.StringComparison.OrdinalIgnoreCase) >= 0) torsoId = id;
            }

            string legsOwnerStr = legsId != null ? (legsId.hasOwner ? $"Jugador {legsId.owner}" : "Sin asignar (Espera)") : "No instanciado";
            string legsYou = (legsId != null && legsId.isOwner) ? " <color=#55FF55><b>(Tú)</b></color>" : "";
            GUILayout.Label($" • Piernas: <color=#00FFFF><b>{legsOwnerStr}</b></color>{legsYou}");

            string torsoOwnerStr = torsoId != null ? (torsoId.hasOwner ? $"Jugador {torsoId.owner}" : "Sin asignar (Espera)") : "No instanciado";
            string torsoYou = (torsoId != null && torsoId.isOwner) ? " <color=#55FF55><b>(Tú)</b></color>" : "";
            GUILayout.Label($" • Torso: <color=#FF9900><b>{torsoOwnerStr}</b></color>{torsoYou}");

            GUILayout.EndVertical();
        }

        private void DrawActionControls()
        {
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("⇄ Intercambiar Roles (Swap)", GUILayout.Height(32)))
            {
                var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
                if (relay != null)
                {
                    if (relay.isServer)
                    {
                        _playerRegistry?.SwapRoles();
                    }
                    else
                    {
                        relay.RequestSwapRolesServerRpc();
                    }
                }
                else
                {
                    _playerRegistry?.SwapRoles();
                }
            }

            if (GUILayout.Button("✕ Desconectar", GUILayout.Height(32)))
            {
                _networkService?.Disconnect();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawLevelControls()
        {
            if (_networkService == null || !_networkService.IsServer || _levelManager == null) return;

            GUILayout.Space(6);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Control de Niveles (Host):</b>");

            string currentRoom = string.IsNullOrEmpty(_levelManager.CurrentRoomName) ? "Lobby / Espera" : _levelManager.CurrentRoomName;
            GUILayout.Label($"Sala Actual: <b>{currentRoom}</b>");

            if (_levelManager.IsLoading)
            {
                GUILayout.Label("<color=yellow>Cargando nivel en red...</color>");
            }
            else
            {
                if (_levelManager.CurrentRoomIndex < 0)
                {
                    if (GUILayout.Button("Iniciar Partida (Cargar Sala 1)", GUILayout.Height(30)))
                    {
                        _levelManager.LoadRoomAsync(0, this.GetCancellationTokenOnDestroy()).Forget();
                    }
                }
                else
                {
                    if (GUILayout.Button("Avanzar a Siguiente Sala", GUILayout.Height(30)))
                    {
                        _levelManager.AdvanceToNextRoomAsync(this.GetCancellationTokenOnDestroy()).Forget();
                    }
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawDebugControls()
        {
            GUILayout.Space(6);
            _showDebugFold = GUILayout.Toggle(_showDebugFold, "<b>Pruebas de Simulación (Muerte / Respawn)</b>");
            if (!_showDebugFold) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("💀 Morir Piernas", GUILayout.Height(28)))
            {
                TriggerDeathSimulation(PlayerRole.Legs);
            }

            if (GUILayout.Button("💀 Morir Torso", GUILayout.Height(28)))
            {
                TriggerDeathSimulation(PlayerRole.Torso);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("💚 Revivir Piernas", GUILayout.Height(28)))
            {
                TriggerRespawnSimulation(PlayerRole.Legs);
            }

            if (GUILayout.Button("💚 Revivir Torso", GUILayout.Height(28)))
            {
                TriggerRespawnSimulation(PlayerRole.Torso);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void TriggerDeathSimulation(PlayerRole role)
        {
            Vector3 deathPos = transform.position;
            int id = role == PlayerRole.Legs ? 1 : 2;
            var evt = new PlayerDiedEvent(id, role, deathPos, DeathCause.Hazard);

            if (_eventBus != null)
            {
                _eventBus.Publish(evt);
            }
            else
            {
                var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
                if (relay != null)
                {
                    relay.BroadcastPlayerDied(evt);
                }
            }
        }

        private void TriggerRespawnSimulation(PlayerRole role)
        {
            Vector3 spawnPos = transform.position;
            int id = role == PlayerRole.Legs ? 1 : 2;
            var evt = new PlayerRespawnedEvent(id, role, spawnPos);

            if (_eventBus != null)
            {
                _eventBus.Publish(evt);
            }
            else
            {
                var relay = Object.FindFirstObjectByType<NetworkEventRelay>();
                if (relay != null)
                {
                    relay.BroadcastPlayerRespawned(evt);
                }
            }
        }
    }
}

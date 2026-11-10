using System;
using Game.Core.Enums;
using Game.Core.Interfaces;
using UnityEngine;

namespace Game.Network.Services
{
    public sealed class LobbyService : ILobbyService
    {
        private int _legsPlayerId = -1;
        private bool _legsReady;
        private int _torsoPlayerId = -1;
        private bool _torsoReady;
        private int _selectedRoomIndex;

        private LobbyNetworkController _activeController;

        public bool AreBothPlayersReady => _legsPlayerId != -1 && _torsoPlayerId != -1 && _legsReady && _torsoReady;
        public int LegsPlayerId => _legsPlayerId;
        public int TorsoPlayerId => _torsoPlayerId;
        public bool LegsReady => _legsReady;
        public bool TorsoReady => _torsoReady;
        public int SelectedRoomIndex => _selectedRoomIndex;
        public bool HasActiveSession => _activeController != null && _activeController.isSpawned;

        public event Action<int, PlayerRole, bool> OnPlayerLobbyStateChanged;
        public event Action<bool> OnBothPlayersReadyStatusChanged;
        public event Action<int> OnSelectedRoomChanged;

        public void BindNetworkController(LobbyNetworkController controller)
        {
            if (controller == null) return;

            _activeController = controller;

            _legsPlayerId = controller.LegsPlayerId;
            _legsReady = controller.LegsReady;
            _torsoPlayerId = controller.TorsoPlayerId;
            _torsoReady = controller.TorsoReady;
            _selectedRoomIndex = controller.SelectedRoomIndex;

            NotifyStateChanged();
            OnSelectedRoomChanged?.Invoke(_selectedRoomIndex);
        }

        public void UnbindNetworkController(LobbyNetworkController controller)
        {
            if (_activeController == controller)
            {
                _activeController = null;
                ResetState();
            }
        }

        public void UpdateLobbyState(int legsId, bool legsReady, int torsoId, bool torsoReady, int selectedRoomIndex = 0)
        {
            _legsPlayerId = legsId;
            _legsReady = legsReady;
            _torsoPlayerId = torsoId;
            _torsoReady = torsoReady;
            bool roomChanged = _selectedRoomIndex != selectedRoomIndex;
            _selectedRoomIndex = selectedRoomIndex;

            NotifyStateChanged();
            if (roomChanged)
            {
                OnSelectedRoomChanged?.Invoke(selectedRoomIndex);
            }
        }

        public void SetSelectedRoom(int roomIndex)
        {
            if (_activeController != null)
            {
                _activeController.RequestSetSelectedRoomServerRpc(roomIndex);
            }
        }

        public void ResetState()
        {
            _legsPlayerId = -1;
            _legsReady = false;
            _torsoPlayerId = -1;
            _torsoReady = false;
            _selectedRoomIndex = 0;

            NotifyStateChanged();
            OnSelectedRoomChanged?.Invoke(0);
        }

        private void NotifyStateChanged()
        {
            OnPlayerLobbyStateChanged?.Invoke(_legsPlayerId, PlayerRole.Legs, _legsReady);
            OnPlayerLobbyStateChanged?.Invoke(_torsoPlayerId, PlayerRole.Torso, _torsoReady);
            OnBothPlayersReadyStatusChanged?.Invoke(AreBothPlayersReady);
        }

        public void SelectRole(PlayerRole role)
        {
            if (HasActiveSession)
            {
                _activeController.SelectRole(role);
            }
            else
            {
                Debug.LogWarning("[LobbyService] Cannot SelectRole: No active spawned LobbyNetworkController.");
            }
        }

        public void ToggleReady()
        {
            if (HasActiveSession)
            {
                _activeController.ToggleReady();
            }
            else
            {
                Debug.LogWarning("[LobbyService] Cannot ToggleReady: No active spawned LobbyNetworkController.");
            }
        }

        public void StartGame()
        {
            if (HasActiveSession)
            {
                _activeController.StartGame();
            }
            else
            {
                Debug.LogWarning("[LobbyService] Cannot StartGame: No active spawned LobbyNetworkController.");
            }
        }

        public void RequestSync()
        {
            if (HasActiveSession)
            {
                _activeController.RequestSync();
            }
            else
            {
                NotifyStateChanged();
            }
        }
    }
}

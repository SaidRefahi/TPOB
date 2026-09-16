using System;
using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;

namespace Game.Network.Services
{
    public sealed class PlayerRegistry : IPlayerRegistry, IDisposable
    {
        private readonly INetworkService _networkService;
        private readonly IGameEventBus _eventBus;
        private readonly List<PlayerSlot> _players = new(4);

        public IReadOnlyList<PlayerSlot> ConnectedPlayers => _players;
        public PlayerRole LocalRole { get; private set; } = PlayerRole.None;

        public event Action<PlayerSlot> OnPlayerRegistered;
        public event Action<int> OnPlayerUnregistered;
        public event Action<PlayerSlot> OnRoleAssigned;

        public PlayerRegistry(INetworkService networkService, IGameEventBus eventBus)
        {
            _networkService = networkService;
            _eventBus = eventBus;

            if (_networkService != null)
            {
                _networkService.OnPlayerConnected += HandlePlayerConnected;
                _networkService.OnPlayerDisconnected += HandlePlayerDisconnected;
            }
        }

        private void HandlePlayerConnected(int playerId, bool isLocal)
        {
            PlayerRole assignedRole = DetermineNextAvailableRole();
            var slot = new PlayerSlot(playerId, assignedRole, isLocal);
            _players.Add(slot);

            if (isLocal)
            {
                LocalRole = assignedRole;
            }

            OnPlayerRegistered?.Invoke(slot);
            OnRoleAssigned?.Invoke(slot);
            _eventBus?.Publish(new PlayerRoleChangedEvent(playerId, assignedRole));
        }

        private void HandlePlayerDisconnected(int playerId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    if (_players[i].IsLocal)
                    {
                        LocalRole = PlayerRole.None;
                    }

                    _players.RemoveAt(i);
                    OnPlayerUnregistered?.Invoke(playerId);
                    break;
                }
            }
        }

        private PlayerRole DetermineNextAvailableRole()
        {
            bool hasLegs = false;
            bool hasTorso = false;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].Role == PlayerRole.Legs) hasLegs = true;
                if (_players[i].Role == PlayerRole.Torso) hasTorso = true;
            }

            if (!hasLegs) return PlayerRole.Legs;
            if (!hasTorso) return PlayerRole.Torso;
            return PlayerRole.Observer;
        }

        public PlayerRole GetRoleForPlayer(int playerId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    return _players[i].Role;
                }
            }
            return PlayerRole.None;
        }

        public bool TryAssignRole(int playerId, PlayerRole newRole)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].PlayerId == playerId)
                {
                    var updated = new PlayerSlot(playerId, newRole, _players[i].IsLocal);
                    _players[i] = updated;

                    if (updated.IsLocal)
                    {
                        LocalRole = newRole;
                    }

                    OnRoleAssigned?.Invoke(updated);
                    _eventBus?.Publish(new PlayerRoleChangedEvent(playerId, newRole));
                    return true;
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (_networkService != null)
            {
                _networkService.OnPlayerConnected -= HandlePlayerConnected;
                _networkService.OnPlayerDisconnected -= HandlePlayerDisconnected;
            }
            _players.Clear();
        }
    }
}

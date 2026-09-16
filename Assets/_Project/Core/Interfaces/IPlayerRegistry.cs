using System;
using System.Collections.Generic;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public readonly struct PlayerSlot
    {
        public readonly int PlayerId;
        public readonly PlayerRole Role;
        public readonly bool IsLocal;

        public PlayerSlot(int playerId, PlayerRole role, bool isLocal)
        {
            PlayerId = playerId;
            Role = role;
            IsLocal = isLocal;
        }
    }

    public interface IPlayerRegistry
    {
        IReadOnlyList<PlayerSlot> ConnectedPlayers { get; }
        PlayerRole LocalRole { get; }

        event Action<PlayerSlot> OnPlayerRegistered;
        event Action<int> OnPlayerUnregistered;
        event Action<PlayerSlot> OnRoleAssigned;

        PlayerRole GetRoleForPlayer(int playerId);
        bool TryAssignRole(int playerId, PlayerRole role);
    }
}

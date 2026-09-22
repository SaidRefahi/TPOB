using System;
using System.Collections.Generic;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public readonly struct PlayerSlot : IEquatable<PlayerSlot>
    {
        public readonly int PlayerId;
        public readonly PlayerRole Role;
        public readonly bool IsLocal;
        public readonly bool IsReady;

        public PlayerSlot(int playerId, PlayerRole role, bool isLocal, bool isReady = false)
        {
            PlayerId = playerId;
            Role = role;
            IsLocal = isLocal;
            IsReady = isReady;
        }

        public bool Equals(PlayerSlot other) =>
            PlayerId == other.PlayerId && Role == other.Role && IsLocal == other.IsLocal && IsReady == other.IsReady;

        public override bool Equals(object obj) => obj is PlayerSlot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(PlayerId, (int)Role, IsLocal, IsReady);
        public static bool operator ==(PlayerSlot left, PlayerSlot right) => left.Equals(right);
        public static bool operator !=(PlayerSlot left, PlayerSlot right) => !left.Equals(right);
    }

    public interface IPlayerRegistry
    {
        IReadOnlyList<PlayerSlot> ConnectedPlayers { get; }
        PlayerRole LocalRole { get; }

        event Action<PlayerSlot> OnPlayerRegistered;
        event Action<int> OnPlayerUnregistered;
        event Action<PlayerSlot> OnRoleAssigned;
        event Action<PlayerSlot> OnPlayerReadyChanged;

        PlayerRole GetRoleForPlayer(int playerId);
        bool TryAssignRole(int playerId, PlayerRole role);
        bool TrySetPlayerReady(int playerId, bool isReady);
        void SwapRoles();
    }
}

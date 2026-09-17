using Game.Core.Enums;
using PurrNet.Packing;
using UnityEngine;

namespace Game.Core.Events
{
    public readonly struct GameStateChangedEvent
    {
        public readonly GameState OldState;
        public readonly GameState NewState;

        public GameStateChangedEvent(GameState oldState, GameState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    public readonly struct PlayerConnectedEvent
    {
        public readonly int PlayerId;
        public readonly bool IsLocal;

        public PlayerConnectedEvent(int playerId, bool isLocal)
        {
            PlayerId = playerId;
            IsLocal = isLocal;
        }
    }

    public readonly struct PlayerDisconnectedEvent
    {
        public readonly int PlayerId;

        public PlayerDisconnectedEvent(int playerId)
        {
            PlayerId = playerId;
        }
    }

    public readonly struct PlayerRoleChangedEvent
    {
        public readonly int PlayerId;
        public readonly PlayerRole NewRole;

        public PlayerRoleChangedEvent(int playerId, PlayerRole newRole)
        {
            PlayerId = playerId;
            NewRole = newRole;
        }
    }

    public struct PlayerDiedEvent : IPackedAuto
    {
        public int PlayerId;
        public PlayerRole Role;
        public Vector3 Position;
        public DeathCause Cause;

        public PlayerDiedEvent(int playerId, PlayerRole role, Vector3 position, DeathCause cause = DeathCause.Hazard)
        {
            PlayerId = playerId;
            Role = role;
            Position = position;
            Cause = cause;
        }
    }

    public struct PlayerRespawnedEvent : IPackedAuto
    {
        public int PlayerId;
        public PlayerRole Role;
        public Vector3 Position;

        public PlayerRespawnedEvent(int playerId, PlayerRole role, Vector3 position)
        {
            PlayerId = playerId;
            Role = role;
            Position = position;
        }
    }

    public struct RoomCompletedEvent : IPackedAuto
    {
        public int RoomIndex;
        public float Duration;

        public RoomCompletedEvent(int roomIndex, float duration)
        {
            RoomIndex = roomIndex;
            Duration = duration;
        }
    }

    public struct RobotFusedEvent : IPackedAuto
    {
        public Vector3 FusionPosition;

        public RobotFusedEvent(Vector3 fusionPosition)
        {
            FusionPosition = fusionPosition;
        }
    }

    public struct RobotSeparatedEvent : IPackedAuto
    {
        public Vector3 SeparationPosition;

        public RobotSeparatedEvent(Vector3 separationPosition)
        {
            SeparationPosition = separationPosition;
        }
    }
}

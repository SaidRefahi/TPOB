using Game.Core.Enums;

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
}

using Game.Core.Events;

namespace Game.Core.Interfaces
{
    public interface INetworkEventRelay
    {
        void BroadcastPlayerDied(PlayerDiedEvent evt);
        void BroadcastPlayerRespawned(PlayerRespawnedEvent evt);
        void BroadcastRoomCompleted(RoomCompletedEvent evt);
        void BroadcastRobotFused(RobotFusedEvent evt);
        void BroadcastRobotSeparated(RobotSeparatedEvent evt);
    }
}

using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IRespawnCoordinator
    {
        void RequestRespawn(IDamageable player, PlayerRole role, float delaySeconds = 1.0f, bool wasFused = false);
    }

    public sealed class NullRespawnCoordinator : IRespawnCoordinator
    {
        public void RequestRespawn(IDamageable player, PlayerRole role, float delaySeconds = 1.0f, bool wasFused = false)
        {
        }
    }
}

using System;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        PlayerRole Role { get; }
        void Kill(DeathCause cause = DeathCause.Hazard);
        void Respawn(UnityEngine.Vector3 position, UnityEngine.Quaternion rotation);
        event Action<DeathCause> OnDied;
        event Action OnRespawned;
    }
}

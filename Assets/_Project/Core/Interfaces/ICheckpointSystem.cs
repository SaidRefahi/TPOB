using Game.Core.Enums;
using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface ICheckpointSystem
    {
        Vector3 GetRespawnPosition(PlayerRole role);
        Quaternion GetRespawnRotation(PlayerRole role);
        bool HasActiveCheckpoint(PlayerRole role);
    }

    public sealed class NullCheckpointSystem : ICheckpointSystem
    {
        public Vector3 GetRespawnPosition(PlayerRole role) => Vector3.zero;
        public Quaternion GetRespawnRotation(PlayerRole role) => Quaternion.identity;
        public bool HasActiveCheckpoint(PlayerRole role) => false;
    }
}

using Game.Core.Enums;
using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface ICheckpointSystem
    {
        Vector3 GetRespawnPosition(PlayerRole role);
        Quaternion GetRespawnRotation(PlayerRole role);
    }
}

using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IKickable
    {
        void OnKicked(Vector3 hitPoint, Vector3 direction, float kickForce);
    }
}

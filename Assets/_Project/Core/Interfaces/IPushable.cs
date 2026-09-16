using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IPushable
    {
        void OnPushed(Vector3 direction, float force);
    }
}

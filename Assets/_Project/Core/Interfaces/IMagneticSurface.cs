using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface IMagneticSurface
    {
        bool IsActive { get; }
        float AdhesionForce { get; }
        Vector3 GetSurfaceNormal(Vector3 worldPoint);
        void SetActive(bool active);
    }
}

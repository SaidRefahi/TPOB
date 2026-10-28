using UnityEngine;

namespace Game.Core.Interfaces
{
    public interface ICatapultable
    {
        bool CanBeCatapulted { get; }
        Vector3 LaunchOriginPosition { get; }
        void ApplyCatapultResistance(Vector3 springForce);
        void ExecuteCatapultLaunch(Vector3 launchImpulse);
    }
}

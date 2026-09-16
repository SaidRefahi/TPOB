using Game.Core.Enums;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Spawning
{
    [SelectionBase]
    public sealed class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private PlayerRole _targetRole = PlayerRole.None;
        [SerializeField] private int _priority = 0;

        public PlayerRole TargetRole => _targetRole;
        public int Priority => _priority;
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;

        private void OnDrawGizmos()
        {
            Color color = _targetRole switch
            {
                PlayerRole.Legs => Color.cyan,
                PlayerRole.Torso => new Color(1f, 0.5f, 0f), // Orange
                _ => Color.yellow
            };

            Gizmos.color = color;
            Vector3 pos = transform.position;

            // Base circle
            Gizmos.DrawWireSphere(pos + Vector3.up * 0.1f, 0.5f);

            // Orientation pointer
            Vector3 forward = transform.forward * 0.8f;
            Gizmos.DrawRay(pos + Vector3.up * 0.1f, forward);

            // Upright marker
            Gizmos.DrawLine(pos, pos + Vector3.up * 1.5f);
        }
    }
}

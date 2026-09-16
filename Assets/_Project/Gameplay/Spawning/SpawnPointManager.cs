using System.Collections.Generic;
using Game.Core.Enums;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Spawning
{
    [DisallowMultipleComponent]
    public sealed class SpawnPointManager : MonoBehaviour
    {
        [SerializeField] private List<SpawnPoint> _spawnPoints = new(4);

        public IReadOnlyList<SpawnPoint> AllSpawnPoints => _spawnPoints;

        private void Awake()
        {
            if (_spawnPoints.Count == 0)
            {
                GetComponentsInChildren(true, _spawnPoints);
            }
        }

        public SpawnPoint GetSpawnPoint(PlayerRole role)
        {
            SpawnPoint bestMatch = null;
            SpawnPoint fallback = null;

            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                var sp = _spawnPoints[i];
                if (sp == null) continue;

                if (sp.TargetRole == role)
                {
                    if (bestMatch == null || sp.Priority > bestMatch.Priority)
                    {
                        bestMatch = sp;
                    }
                }
                else if (sp.TargetRole == PlayerRole.None)
                {
                    if (fallback == null || sp.Priority > fallback.Priority)
                    {
                        fallback = sp;
                    }
                }
            }

            return bestMatch != null ? bestMatch : (fallback != null ? fallback : (_spawnPoints.Count > 0 ? _spawnPoints[0] : null));
        }

        [Button("Detectar SpawnPoints en Escena")]
        private void DiscoverSpawnPoints()
        {
            _spawnPoints.Clear();
            GetComponentsInChildren(true, _spawnPoints);
            Debug.Log($"[SpawnPointManager] Detected {_spawnPoints.Count} spawn points in room.");
        }
    }
}

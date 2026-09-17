using System.Collections.Generic;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Spawning;
using PurrNet;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Checkpoints
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Checkpoints")]
    [DeclareBoxGroup("Referencias")]
    public sealed class CheckpointSystem : NetworkBehaviour, ICheckpointSystem
    {
        [Group("Checkpoints")]
        [SerializeField] private List<Checkpoint> _checkpoints = new(4);

        [Group("Checkpoints")]
        [SerializeField] private Checkpoint _activeCheckpoint;

        [Group("Referencias")]
        [SerializeField] private SpawnPointManager _spawnPointManager;

        public Checkpoint ActiveCheckpoint => _activeCheckpoint;

        private void Awake()
        {
            if (_spawnPointManager == null)
            {
                _spawnPointManager = FindFirstObjectByType<SpawnPointManager>();
            }

            if (_checkpoints.Count == 0)
            {
                GetComponentsInChildren(true, _checkpoints);
                if (_checkpoints.Count == 0)
                {
                    _checkpoints.AddRange(FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
                }
            }
        }

        public void SetActiveCheckpoint(Checkpoint checkpoint)
        {
            if (checkpoint == null || checkpoint == _activeCheckpoint)
            {
                return;
            }

            if (isSpawned && !isServer)
            {
                return;
            }

            if (_activeCheckpoint != null)
            {
                _activeCheckpoint.SetActive(false);
            }

            _activeCheckpoint = checkpoint;
            _activeCheckpoint.SetActive(true);
        }

        public Vector3 GetRespawnPosition(PlayerRole role)
        {
            if (_activeCheckpoint != null)
            {
                Transform targetPoint = role == PlayerRole.Legs
                    ? _activeCheckpoint.LegsSpawnPoint
                    : _activeCheckpoint.TorsoSpawnPoint;

                if (targetPoint != null)
                {
                    return targetPoint.position;
                }

                Vector3 offset = role == PlayerRole.Legs ? new Vector3(-1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
                return _activeCheckpoint.transform.position + offset;
            }

            if (_spawnPointManager != null)
            {
                var sp = _spawnPointManager.GetSpawnPoint(role);
                if (sp != null)
                {
                    return sp.transform.position;
                }
            }

            return role == PlayerRole.Legs ? new Vector3(-3f, 0f, -7f) : new Vector3(3f, 0f, -7f);
        }

        public Quaternion GetRespawnRotation(PlayerRole role)
        {
            if (_activeCheckpoint != null)
            {
                Transform targetPoint = role == PlayerRole.Legs
                    ? _activeCheckpoint.LegsSpawnPoint
                    : _activeCheckpoint.TorsoSpawnPoint;

                if (targetPoint != null)
                {
                    return targetPoint.rotation;
                }

                return _activeCheckpoint.transform.rotation;
            }

            if (_spawnPointManager != null)
            {
                var sp = _spawnPointManager.GetSpawnPoint(role);
                if (sp != null)
                {
                    return sp.transform.rotation;
                }
            }

            return Quaternion.identity;
        }

        [Group("Checkpoints")]
        [Button("Buscar Checkpoints en Escena")]
        private void DiscoverCheckpoints()
        {
            _checkpoints.Clear();
            _checkpoints.AddRange(FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
            Debug.Log($"[CheckpointSystem] Se detectaron {_checkpoints.Count} checkpoints en escena.");
        }
    }
}

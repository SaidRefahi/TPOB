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
        [SerializeField] private Checkpoint _activeLegsCheckpoint;

        [Group("Checkpoints")]
        [SerializeField] private Checkpoint _activeTorsoCheckpoint;

        [Group("Referencias")]
        [SerializeField] private SpawnPointManager _spawnPointManager;

        public Checkpoint ActiveLegsCheckpoint => _activeLegsCheckpoint;
        public Checkpoint ActiveTorsoCheckpoint => _activeTorsoCheckpoint;
        public Checkpoint ActiveCheckpoint => _activeLegsCheckpoint != null ? _activeLegsCheckpoint : _activeTorsoCheckpoint;

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

        public bool HasActiveCheckpoint(PlayerRole role)
        {
            if (role == PlayerRole.Legs)
            {
                return _activeLegsCheckpoint != null;
            }

            if (role == PlayerRole.Torso)
            {
                return _activeTorsoCheckpoint != null;
            }

            return _activeLegsCheckpoint != null || _activeTorsoCheckpoint != null;
        }

        public void RegisterCheckpointReached(Checkpoint checkpoint, PlayerRole role, bool isFused)
        {
            if (checkpoint == null)
            {
                return;
            }

            if (isSpawned && !isServer)
            {
                return;
            }

            if (isFused)
            {
                UpdateRoleCheckpoint(ref _activeLegsCheckpoint, checkpoint, PlayerRole.Legs);
                UpdateRoleCheckpoint(ref _activeTorsoCheckpoint, checkpoint, PlayerRole.Torso);
            }
            else
            {
                if (role == PlayerRole.Legs)
                {
                    UpdateRoleCheckpoint(ref _activeLegsCheckpoint, checkpoint, PlayerRole.Legs);
                }
                else if (role == PlayerRole.Torso)
                {
                    UpdateRoleCheckpoint(ref _activeTorsoCheckpoint, checkpoint, PlayerRole.Torso);
                }
            }
        }

        public void SetActiveCheckpoint(Checkpoint checkpoint)
        {
            RegisterCheckpointReached(checkpoint, PlayerRole.None, isFused: true);
        }

        private void UpdateRoleCheckpoint(ref Checkpoint current, Checkpoint next, PlayerRole role)
        {
            if (next == current)
            {
                return;
            }

            if (current != null && next.Priority < current.Priority)
            {
                return;
            }

            if (current != null)
            {
                current.SetRoleReached(role, false);
            }

            current = next;
            current.SetRoleReached(role, true);
        }

        public Vector3 GetRespawnPosition(PlayerRole role)
        {
            Checkpoint targetCheckpoint = role == PlayerRole.Legs ? _activeLegsCheckpoint : _activeTorsoCheckpoint;

            if (targetCheckpoint != null)
            {
                Transform targetPoint = role == PlayerRole.Legs
                    ? targetCheckpoint.LegsSpawnPoint
                    : targetCheckpoint.TorsoSpawnPoint;

                if (targetPoint != null)
                {
                    return targetPoint.position;
                }

                Vector3 offset = role == PlayerRole.Legs ? new Vector3(-1f, 0f, 0f) : new Vector3(1f, 0f, 0f);
                return targetCheckpoint.transform.position + offset;
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
            Checkpoint targetCheckpoint = role == PlayerRole.Legs ? _activeLegsCheckpoint : _activeTorsoCheckpoint;

            if (targetCheckpoint != null)
            {
                Transform targetPoint = role == PlayerRole.Legs
                    ? targetCheckpoint.LegsSpawnPoint
                    : targetCheckpoint.TorsoSpawnPoint;

                if (targetPoint != null)
                {
                    return targetPoint.rotation;
                }

                return targetCheckpoint.transform.rotation;
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

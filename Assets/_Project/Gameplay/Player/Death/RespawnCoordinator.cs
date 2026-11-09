using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Enums;
using Game.Core.Interfaces;
using Game.Gameplay.Checkpoints;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Player.Death
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Referencias")]
    public sealed class RespawnCoordinator : NetworkBehaviour, IRespawnCoordinator
    {
        [Group("Configuración")]
        [SerializeField] private float _defaultRespawnDelay = 1.0f;

        [Group("Referencias")]
        [SerializeField] private CheckpointSystem _checkpointSystem;

        private ICheckpointSystem _injectedCheckpointSystem;
        private IRobotCoordinator _robotCoordinator;

        private bool _legsDiedFused;
        private bool _torsoDiedFused;
        private float _legsDeathTime;
        private float _torsoDeathTime;

        [Inject]
        public void Construct(ICheckpointSystem checkpointSystem = null, IRobotCoordinator robotCoordinator = null)
        {
            _injectedCheckpointSystem = checkpointSystem;
            _robotCoordinator = robotCoordinator;
        }

        private void Awake()
        {
            if (_checkpointSystem == null)
            {
                _checkpointSystem = FindFirstObjectByType<CheckpointSystem>();
            }

            if (_robotCoordinator == null)
            {
                _robotCoordinator = FindFirstObjectByType<Robot.RobotCoordinator>();
            }
        }

        public void RequestRespawn(IDamageable player, PlayerRole role, float delaySeconds = 1.0f, bool wasFused = false)
        {
            if (player == null)
            {
                return;
            }

            if (isSpawned && !isServer)
            {
                return;
            }

            if (role == PlayerRole.Legs)
            {
                _legsDiedFused = wasFused;
                _legsDeathTime = Time.time;
            }
            else if (role == PlayerRole.Torso)
            {
                _torsoDiedFused = wasFused;
                _torsoDeathTime = Time.time;
            }

            float delay = delaySeconds > 0f ? delaySeconds : _defaultRespawnDelay;
            ExecuteRespawnAsync(player, role, delay, destroyCancellationToken).Forget();
        }

        private async UniTaskVoid ExecuteRespawnAsync(IDamageable player, PlayerRole role, float delay, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (player == null || ct.IsCancellationRequested)
            {
                return;
            }

            Vector3 spawnPos;
            Quaternion spawnRot;

            var checkpointSys = _injectedCheckpointSystem != null ? _injectedCheckpointSystem : _checkpointSystem;
            if (checkpointSys != null)
            {
                spawnPos = checkpointSys.GetRespawnPosition(role);
                spawnRot = checkpointSys.GetRespawnRotation(role);
            }
            else
            {
                spawnPos = role == PlayerRole.Legs ? new Vector3(-3f, 0f, -7f) : new Vector3(3f, 0f, -7f);
                spawnRot = Quaternion.identity;
            }

            player.Respawn(spawnPos, spawnRot);

            // If both players died while fused, coordinate re-fusion upon respawning
            if (_legsDiedFused && _torsoDiedFused && Mathf.Abs(_legsDeathTime - _torsoDeathTime) < 2.5f)
            {
                _legsDiedFused = false;
                _torsoDiedFused = false;

                try
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_robotCoordinator != null && !_robotCoordinator.IsFused)
                {
                    _robotCoordinator.RequestFusion();
                }
            }
        }
    }
}

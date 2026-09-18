using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Network.Audio
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Audio Relay")]
    public sealed class NetworkAudioRelay : NetworkBehaviour
    {
        private IAudioService _audioService;
        private IGameEventBus _eventBus;

        [Inject]
        public void Construct(IAudioService audioService = null, IGameEventBus eventBus = null)
        {
            _audioService = audioService;
            _eventBus = eventBus;
        }

        private void Start()
        {
            if (_audioService == null)
            {
                _audioService = FindFirstObjectByType<Game.Core.Audio.AudioService>();
            }
        }

        private void OnEnable()
        {
            if (_eventBus != null)
            {
                _eventBus.Subscribe<AudioCueEvent>(HandleAudioCueEvent);
                _eventBus.Subscribe<RobotFusedEvent>(HandleRobotFused);
                _eventBus.Subscribe<RobotSeparatedEvent>(HandleRobotSeparated);
                _eventBus.Subscribe<RoomCompletedEvent>(HandleRoomCompleted);
                _eventBus.Subscribe<PlayerDiedEvent>(HandlePlayerDied);
                _eventBus.Subscribe<PlayerRespawnedEvent>(HandlePlayerRespawned);
            }
        }

        private void OnDisable()
        {
            if (_eventBus != null)
            {
                _eventBus.Unsubscribe<AudioCueEvent>(HandleAudioCueEvent);
                _eventBus.Unsubscribe<RobotFusedEvent>(HandleRobotFused);
                _eventBus.Unsubscribe<RobotSeparatedEvent>(HandleRobotSeparated);
                _eventBus.Unsubscribe<RoomCompletedEvent>(HandleRoomCompleted);
                _eventBus.Unsubscribe<PlayerDiedEvent>(HandlePlayerDied);
                _eventBus.Unsubscribe<PlayerRespawnedEvent>(HandlePlayerRespawned);
            }
        }

        public void PlayNetworkAudio(AudioCue cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (!isSpawned)
            {
                _audioService?.PlaySfx(cue, position, volume, pitch);
                return;
            }

            if (!isServer)
            {
                PlayAudioServerRpc(cue, position, volume, pitch);
            }
            else
            {
                PlayAudioObserversRpc(cue, position, volume, pitch);
            }
        }

        [ServerRpc(requireOwnership: false)]
        private void PlayAudioServerRpc(AudioCue cue, Vector3 position, float volume, float pitch)
        {
            PlayAudioObserversRpc(cue, position, volume, pitch);
        }

        [ObserversRpc(runLocally: true, bufferLast: false)]
        private void PlayAudioObserversRpc(AudioCue cue, Vector3 position, float volume, float pitch)
        {
            if (_audioService == null)
            {
                _audioService = FindFirstObjectByType<Game.Core.Audio.AudioService>();
            }

            _audioService?.PlaySfx(cue, position, volume, pitch);
        }

        private void HandleAudioCueEvent(AudioCueEvent evt)
        {
            PlayNetworkAudio(evt.Cue, evt.Position, evt.Volume, evt.Pitch);
        }

        private void HandleRobotFused(RobotFusedEvent evt)
        {
            PlayNetworkAudio(AudioCue.Dock, evt.FusionPosition, 1f, 1f);
        }

        private void HandleRobotSeparated(RobotSeparatedEvent evt)
        {
            PlayNetworkAudio(AudioCue.Undock, evt.SeparationPosition, 1f, 1f);
        }

        private void HandleRoomCompleted(RoomCompletedEvent evt)
        {
            PlayNetworkAudio(AudioCue.RoomClear, transform.position, 1f, 1f);
        }

        private void HandlePlayerDied(PlayerDiedEvent evt)
        {
            PlayNetworkAudio(AudioCue.PlayerDeath, evt.Position, 1f, 1f);
        }

        private void HandlePlayerRespawned(PlayerRespawnedEvent evt)
        {
            PlayNetworkAudio(AudioCue.PlayerRespawn, evt.Position, 1f, 1f);
        }
    }
}

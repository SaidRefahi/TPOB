using System;
using Game.Core.Enums;
using Game.Core.Interfaces;
using VContainer.Unity;

namespace Game.Network.Audio
{
    public sealed class MusicDirector : IInitializable, IDisposable
    {
        private readonly IAudioService _audioService;
        private readonly IGameManager _gameManager;
        private bool _isDisposed;

        public MusicDirector(IAudioService audioService, IGameManager gameManager)
        {
            _audioService = audioService;
            _gameManager = gameManager;
        }

        public void Initialize()
        {
            if (_isDisposed || !UnityEngine.Application.isPlaying) return;
            if (_audioService == null || (_audioService is UnityEngine.Object uObj && uObj == null)) return;

            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged += HandleGameStateChanged;

                if (_gameManager.CurrentState == GameState.InGame)
                {
                    _audioService.PlayMusic(MusicTrack.Level, 1.2f);
                }
                else
                {
                    _audioService.PlayMusic(MusicTrack.Menu, 1.2f);
                }
            }
            else
            {
                _audioService.PlayMusic(MusicTrack.Menu, 1.2f);
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
            if (_gameManager != null)
            {
                _gameManager.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void HandleGameStateChanged(GameState previousState, GameState newState)
        {
            if (_isDisposed || !UnityEngine.Application.isPlaying) return;
            if (_audioService == null || (_audioService is UnityEngine.Object uObj && uObj == null)) return;

            switch (newState)
            {
                case GameState.Booting:
                case GameState.Lobby:
                    _audioService.SetMusicDucked(false, 0.3f);
                    _audioService.PlayMusic(MusicTrack.Menu, 1.2f);
                    break;

                case GameState.InGame:
                    _audioService.SetMusicDucked(false, 0.3f);
                    _audioService.PlayMusic(MusicTrack.Level, 1.2f);
                    break;

                case GameState.Paused:
                    _audioService.SetMusicDucked(true, 0.3f);
                    break;

                case GameState.GameOver:
                    _audioService.SetMusicDucked(true, 0.5f);
                    break;
            }
        }
    }
}

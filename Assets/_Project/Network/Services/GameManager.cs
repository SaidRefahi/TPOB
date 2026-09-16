using System;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;

namespace Game.Network.Services
{
    public sealed class GameManager : IGameManager, IDisposable
    {
        private readonly INetworkService _networkService;
        private readonly IGameEventBus _eventBus;

        public GameState CurrentState { get; private set; } = GameState.Booting;

        public event Action<GameState, GameState> OnGameStateChanged;

        public GameManager(INetworkService networkService, IGameEventBus eventBus)
        {
            _networkService = networkService;
            _eventBus = eventBus;

            if (_networkService != null)
            {
                _networkService.OnConnected += HandleNetworkConnected;
                _networkService.OnDisconnected += HandleNetworkDisconnected;
            }
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            GameState oldState = CurrentState;
            CurrentState = newState;

            OnGameStateChanged?.Invoke(oldState, newState);
            _eventBus?.Publish(new GameStateChangedEvent(oldState, newState));
        }

        private void HandleNetworkConnected()
        {
            if (CurrentState == GameState.Booting)
            {
                ChangeState(GameState.Lobby);
            }
        }

        private void HandleNetworkDisconnected()
        {
            ChangeState(GameState.Booting);
        }

        public void Dispose()
        {
            if (_networkService != null)
            {
                _networkService.OnConnected -= HandleNetworkConnected;
                _networkService.OnDisconnected -= HandleNetworkDisconnected;
            }
        }
    }
}

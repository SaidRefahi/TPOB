using System;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IGameManager
    {
        GameState CurrentState { get; }

        event Action<GameState, GameState> OnGameStateChanged;

        void ChangeState(GameState newState);
    }
}

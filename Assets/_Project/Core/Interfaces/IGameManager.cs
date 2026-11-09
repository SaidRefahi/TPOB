using System;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IGameManager
    {
        GameState CurrentState { get; }
        bool IsLocalMode { get; set; }

        event Action<GameState, GameState> OnGameStateChanged;

        void ChangeState(GameState newState);
    }
}

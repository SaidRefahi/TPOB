using System;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface ILobbyService
    {
        bool AreBothPlayersReady { get; }
        int LegsPlayerId { get; }
        int TorsoPlayerId { get; }
        bool LegsReady { get; }
        bool TorsoReady { get; }

        event Action<int, PlayerRole, bool> OnPlayerLobbyStateChanged;
        event Action<bool> OnBothPlayersReadyStatusChanged;

        void SelectRole(PlayerRole role);
        void ToggleReady();
        void StartGame();
    }
}

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

        int SelectedRoomIndex { get; }

        event Action<int, PlayerRole, bool> OnPlayerLobbyStateChanged;
        event Action<bool> OnBothPlayersReadyStatusChanged;
        event Action<int> OnSelectedRoomChanged;

        void SelectRole(PlayerRole role);
        void ToggleReady();
        void StartGame();
        void RequestSync();
        void SetSelectedRoom(int roomIndex);
    }
}

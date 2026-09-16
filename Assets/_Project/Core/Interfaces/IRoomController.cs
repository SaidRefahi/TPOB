using System;
using Game.Core.Enums;

namespace Game.Core.Interfaces
{
    public interface IRoomController
    {
        RoomState CurrentState { get; }
        int RoomIndex { get; }
        string RoomName { get; }

        event Action<RoomState> OnRoomStateChanged;

        void ActivateRoom();
        void CompleteRoom();
        void ResetRoom();
    }
}

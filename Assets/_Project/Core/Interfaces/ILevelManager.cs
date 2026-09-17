using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core.Interfaces
{
    public interface ILevelManager
    {
        int CurrentRoomIndex { get; }
        string CurrentRoomName { get; }
        int TotalRooms { get; }
        bool IsLastRoom { get; }
        bool IsLoading { get; }

        event Action<int, string> OnRoomLoaded;
        event Action<int, string> OnRoomUnloaded;

        UniTask LoadRoomAsync(int roomIndex, CancellationToken ct = default);
        UniTask AdvanceToNextRoomAsync(CancellationToken ct = default);
    }
}

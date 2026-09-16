using System;

namespace Game.Core.Interfaces
{
    public interface INetworkService
    {
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsConnected { get; }

        event Action OnConnected;
        event Action OnDisconnected;
        event Action<int, bool> OnPlayerConnected;
        event Action<int> OnPlayerDisconnected;

        void StartHost();
        void StartClient();
        void Disconnect();
    }
}

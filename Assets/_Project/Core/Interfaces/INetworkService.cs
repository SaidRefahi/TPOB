using System;

namespace Game.Core.Interfaces
{
    public interface INetworkService
    {
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsConnected { get; }

        string ServerAddress { get; set; }
        ushort ServerPort { get; set; }

        event Action OnConnected;
        event Action OnDisconnected;
        event Action<int, bool> OnPlayerConnected;
        event Action<int> OnPlayerDisconnected;

        void StartHost();
        void StartHost(ushort port);
        void StartClient();
        void StartClient(string address, ushort port);
        void Disconnect();
    }
}

using System;

namespace Game.Core.Interfaces
{
    public interface INetworkService
    {
        bool IsServer { get; }
        bool IsClient { get; }
        bool IsConnected { get; }
        int LocalPlayerId { get; }

        string ServerAddress { get; set; }
        ushort ServerPort { get; set; }
        string RoomName { get; set; }

        event Action OnConnected;
        event Action OnDisconnected;
        event Action<int, bool> OnPlayerConnected;
        event Action<int> OnPlayerDisconnected;

        void StartHost();
        void StartHost(ushort port);
        void StartHostWithRoom(string roomName);
        void StartClient();
        void StartClient(string address, ushort port);
        void StartClientWithRoom(string roomName);
        void Disconnect();
    }
}

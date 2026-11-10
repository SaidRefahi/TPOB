using System;
using System.Collections.Generic;

namespace Game.Core.Interfaces
{
    public struct LanGameInfo : IEquatable<LanGameInfo>
    {
        public string HostName;
        public string IpAddress;
        public ushort Port;
        public string RoomCode;
        public float LastSeenTimestamp;

        public LanGameInfo(string hostName, string ipAddress, ushort port, string roomCode, float lastSeenTimestamp)
        {
            HostName = hostName;
            IpAddress = ipAddress;
            Port = port;
            RoomCode = roomCode;
            LastSeenTimestamp = lastSeenTimestamp;
        }

        public bool Equals(LanGameInfo other)
        {
            return IpAddress == other.IpAddress && Port == other.Port;
        }

        public override bool Equals(object obj)
        {
            return obj is LanGameInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IpAddress, Port);
        }
    }

    public interface ILanDiscoveryService : IDisposable
    {
        bool IsBroadcasting { get; }
        bool IsListening { get; }
        IReadOnlyList<LanGameInfo> DiscoveredGames { get; }

        event Action<LanGameInfo> OnGameDiscovered;
        event Action<LanGameInfo> OnGameLost;

        void StartBroadcasting(string hostName, ushort gamePort, string roomCode = "TPOB-LAN");
        void StopBroadcasting();
        void StartListening();
        void StopListening();
    }
}

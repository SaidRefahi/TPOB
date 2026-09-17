using Game.Core.Enums;
using PurrNet.Packing;
using PurrNet.Utils;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Core.Packing
{
    public static class GamePackerInitializer
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Initialize()
        {
            RegisterRoomState();
            RegisterPlayerRole();
            RegisterGameState();
            RegisterDeathCause();
        }

        private static void RegisterRoomState()
        {
            Hasher.PrepareType(typeof(RoomState));
            Packer<RoomState>.RegisterWriter(WriteRoomState);
            Packer<RoomState>.RegisterReader(ReadRoomState);
        }

        private static void WriteRoomState(BitPacker packer, RoomState value)
        {
            PackIntegers.Write(packer, (int)value);
        }

        private static void ReadRoomState(BitPacker packer, ref RoomState value)
        {
            int val = 0;
            PackIntegers.Read(packer, ref val);
            value = (RoomState)val;
        }

        private static void RegisterPlayerRole()
        {
            Hasher.PrepareType(typeof(PlayerRole));
            Packer<PlayerRole>.RegisterWriter(WritePlayerRole);
            Packer<PlayerRole>.RegisterReader(ReadPlayerRole);
        }

        private static void WritePlayerRole(BitPacker packer, PlayerRole value)
        {
            PackIntegers.Write(packer, (int)value);
        }

        private static void ReadPlayerRole(BitPacker packer, ref PlayerRole value)
        {
            int val = 0;
            PackIntegers.Read(packer, ref val);
            value = (PlayerRole)val;
        }

        private static void RegisterGameState()
        {
            Hasher.PrepareType(typeof(GameState));
            Packer<GameState>.RegisterWriter(WriteGameState);
            Packer<GameState>.RegisterReader(ReadGameState);
        }

        private static void WriteGameState(BitPacker packer, GameState value)
        {
            PackIntegers.Write(packer, (int)value);
        }

        private static void ReadGameState(BitPacker packer, ref GameState value)
        {
            int val = 0;
            PackIntegers.Read(packer, ref val);
            value = (GameState)val;
        }

        private static void RegisterDeathCause()
        {
            Hasher.PrepareType(typeof(DeathCause));
            Packer<DeathCause>.RegisterWriter(WriteDeathCause);
            Packer<DeathCause>.RegisterReader(ReadDeathCause);
        }

        private static void WriteDeathCause(BitPacker packer, DeathCause value)
        {
            PackIntegers.Write(packer, (int)value);
        }

        private static void ReadDeathCause(BitPacker packer, ref DeathCause value)
        {
            int val = 0;
            PackIntegers.Read(packer, ref val);
            value = (DeathCause)val;
        }
    }
}

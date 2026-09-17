using PurrNet.Packing;
using UnityEngine;

namespace Game.Core.Structs
{
    public struct PlayerCommandPacket : IPackedAuto
    {
        public byte CommandId;
        public Vector2 VectorData;
        public float FloatData;
        public int IntData;
        public bool BoolData;

        public PlayerCommandPacket(byte commandId, Vector2 vectorData = default, float floatData = 0f, int intData = 0, bool boolData = false)
        {
            CommandId = commandId;
            VectorData = vectorData;
            FloatData = floatData;
            IntData = intData;
            BoolData = boolData;
        }
    }
}

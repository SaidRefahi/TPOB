using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Robot;
using Game.Gameplay.Player.Torso;
using Game.Gameplay.Rooms;
using Game.Gameplay.Spawning;
using Game.Network.Events;
using TriInspector.Editors;
using UnityEditor;

namespace Game.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(NetworkEventRelay), true)]
    public sealed class NetworkEventRelayEditor : TriEditor
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(RoomController), true)]
    public sealed class RoomControllerEditor : TriEditor
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(LegsController), true)]
    public sealed class LegsControllerEditor : TriEditor
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(TorsoController), true)]
    public sealed class TorsoControllerEditor : TriEditor
    {
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(TPOBPlayerSpawner), true)]
    public sealed class TPOBPlayerSpawnerEditor : TriEditor
    {
    }
}

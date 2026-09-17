using Game.Gameplay.Player.Robot;
using TriInspector.Editors;
using UnityEditor;

namespace Game.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(RobotCoordinator), true)]
    public sealed class RobotCoordinatorEditor : TriEditor
    {
    }
}

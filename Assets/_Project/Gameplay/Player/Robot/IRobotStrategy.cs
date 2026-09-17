namespace Game.Gameplay.Player.Robot
{
    public interface IRobotStrategy
    {
        bool IsFused { get; }
        void OnEnter(RobotContext context);
        void OnExit(RobotContext context);
        void OnTick(RobotContext context, float deltaTime);
        void OnFixedTick(RobotContext context, float fixedDeltaTime);
    }
}

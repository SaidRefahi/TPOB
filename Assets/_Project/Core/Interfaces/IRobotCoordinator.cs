namespace Game.Core.Interfaces
{
    public interface IRobotCoordinator
    {
        bool IsFused { get; }
        bool CanFuse();
        void RequestFusion();
        void RequestSeparation();
    }
}

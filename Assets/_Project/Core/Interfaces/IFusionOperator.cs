namespace Game.Core.Interfaces
{
    public interface IFusionOperator
    {
        bool CanFuse { get; }
        bool IsFused { get; }
        void RequestFusion();
        void RequestSeparation();
    }
}

namespace Game.Core.Interfaces
{
    public interface ICatapultMechanism
    {
        bool IsTensioned { get; }
        void TriggerLaunch();
    }
}

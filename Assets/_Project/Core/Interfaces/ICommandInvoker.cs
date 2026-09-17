namespace Game.Core.Interfaces
{
    public interface ICommandInvoker
    {
        void Execute<TCommand>(TCommand command) where TCommand : struct, IPlayerCommand;
    }
}

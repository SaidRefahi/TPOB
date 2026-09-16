using System;

namespace Game.Core.Events
{
    public interface IGameEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T eventData);
    }
}

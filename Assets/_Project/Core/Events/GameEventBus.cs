using System;
using System.Collections.Generic;

namespace Game.Core.Events
{
    public sealed class GameEventBus : IGameEventBus
    {
        private readonly Dictionary<Type, Delegate> _subscribers = new(32);

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            var eventType = typeof(T);
            if (_subscribers.TryGetValue(eventType, out var existingDelegate))
            {
                _subscribers[eventType] = Delegate.Combine(existingDelegate, handler);
            }
            else
            {
                _subscribers[eventType] = handler;
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;

            var eventType = typeof(T);
            if (_subscribers.TryGetValue(eventType, out var existingDelegate))
            {
                var newDelegate = Delegate.Remove(existingDelegate, handler);
                if (newDelegate == null)
                {
                    _subscribers.Remove(eventType);
                }
                else
                {
                    _subscribers[eventType] = newDelegate;
                }
            }
        }

        public void Publish<T>(T eventData)
        {
            var eventType = typeof(T);
            if (_subscribers.TryGetValue(eventType, out var existingDelegate))
            {
                (existingDelegate as Action<T>)?.Invoke(eventData);
            }
        }
    }
}

using System;
using Game.Gameplay.Interactables;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Rooms.Conditions
{
    [DeclareBoxGroup("Condición de Socket")]
    public sealed class SocketCondition : ConditionBase
    {
        [Group("Condición de Socket")]
        [SerializeField] private PuzzleSocket _socket;

        public override event Action<bool> OnConditionChanged;

        public override bool IsSatisfied => _socket != null && _socket.IsActivated;

        private void OnEnable()
        {
            if (_socket != null)
            {
                _socket.OnStateChanged += HandleSocketStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_socket != null)
            {
                _socket.OnStateChanged -= HandleSocketStateChanged;
            }
        }

        private void HandleSocketStateChanged(bool _)
        {
            OnConditionChanged?.Invoke(IsSatisfied);
        }

        public void Configure(PuzzleSocket socket)
        {
            if (_socket != null)
            {
                _socket.OnStateChanged -= HandleSocketStateChanged;
            }

            _socket = socket;

            if (_socket != null && enabled)
            {
                _socket.OnStateChanged += HandleSocketStateChanged;
            }
        }
    }
}

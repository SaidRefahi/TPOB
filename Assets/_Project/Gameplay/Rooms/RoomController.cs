using System;
using Game.Core.Enums;
using Game.Core.Events;
using Game.Core.Interfaces;
using Game.Gameplay.Spawning;
using PurrNet;
using TriInspector;
using UnityEngine;
using VContainer;

namespace Game.Gameplay.Rooms
{
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Estado Sincronizado")]
    public sealed class RoomController : NetworkBehaviour, IRoomController
    {
        [Group("Configuración")]
        [SerializeField] private int _roomIndex;

        [Group("Configuración")]
        [SerializeField] private string _roomName = "Room_01";

        [Group("Estado Sincronizado")]
        [SerializeField] private SyncVar<RoomState> _roomState = new(RoomState.Inactive);

        public RoomState CurrentState => _roomState.value;
        public int RoomIndex => _roomIndex;
        public string RoomName => _roomName;

        public event Action<RoomState> OnRoomStateChanged;

        private SpawnPointManager _spawnPointManager;
        private IGameEventBus _eventBus;

        [Inject]
        public void Construct(SpawnPointManager spawnPointManager, IGameEventBus eventBus = null)
        {
            _spawnPointManager = spawnPointManager;
            _eventBus = eventBus;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _roomState.onChanged += HandleRoomStateChanged;

            if (isServer && _roomState.value == RoomState.Inactive)
            {
                ActivateRoom();
            }
        }

        protected override void OnDespawned()
        {
            _roomState.onChanged -= HandleRoomStateChanged;
            base.OnDespawned();
        }

        public void ActivateRoom()
        {
            if (!isServer) return;
            SetRoomState(RoomState.Active);
        }

        public void CompleteRoom()
        {
            if (!isServer) return;
            SetRoomState(RoomState.Completed);
        }

        public void ResetRoom()
        {
            if (!isServer) return;
            SetRoomState(RoomState.Active);
        }

        private void SetRoomState(RoomState newState)
        {
            if (_roomState.value == newState) return;
            _roomState.value = newState;
        }

        private void HandleRoomStateChanged(RoomState state)
        {
            OnRoomStateChanged?.Invoke(state);
            Debug.Log($"[RoomController] Room '{_roomName}' changed state to: {state}");

            if (state == RoomState.Completed)
            {
                _eventBus?.Publish(new RoomCompletedEvent(_roomIndex, Time.timeSinceLevelLoad));
            }
        }

        [Button("Activar Sala (Servidor)")]
        private void DebugActivateRoom() => ActivateRoom();

        [Button("Completar Sala (Servidor)")]
        private void DebugCompleteRoom() => CompleteRoom();

        [Button("Reiniciar Sala (Servidor)")]
        private void DebugResetRoom() => ResetRoom();
    }
}

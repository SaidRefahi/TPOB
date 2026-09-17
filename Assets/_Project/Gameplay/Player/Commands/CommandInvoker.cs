using System;
using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

namespace Game.Gameplay.Player.Commands
{
    [DisallowMultipleComponent]
    public sealed class CommandInvoker : NetworkBehaviour, ICommandInvoker
    {
        public delegate void CommandPacketHandler(in PlayerCommandPacket packet, PlayerContext context);

        private static readonly CommandPacketHandler[] _handlers = new CommandPacketHandler[256];
        private static bool _defaultHandlersRegistered;

        private PlayerContext _context;

        public PlayerContext Context => _context;

        public event Action<byte> OnCommandExecuted;

        static CommandInvoker()
        {
            EnsureDefaultHandlersRegistered();
        }

        private void Awake()
        {
            EnsureDefaultHandlersRegistered();

            var moveable = GetComponent<IMoveable>();
            var kicker = GetComponent<IKicker>();
            var grabber = GetComponent<IGrabber>();
            var thrower = GetComponent<IThrower>();
            var magnetOperator = GetComponent<IMagnetOperator>();
            var climber = GetComponent<IClimber>();
            var interactOperator = GetComponent<IInteractOperator>();
            var fusionOperator = GetComponent<IFusionOperator>();
            var rb = GetComponent<Rigidbody>();

            _context = new PlayerContext(
                gameObject,
                transform,
                rb,
                moveable,
                kicker,
                grabber,
                thrower,
                magnetOperator,
                climber,
                interactOperator,
                fusionOperator
            );
        }

        public void Execute<TCommand>(TCommand command) where TCommand : struct, IPlayerCommand
        {
            if (!isSpawned || isServer)
            {
                command.Execute(_context);
                OnCommandExecuted?.Invoke(command.CommandId);
            }
            else if (isOwner)
            {
                PlayerCommandPacket packet = command.ToPacket();
                if (command.IsReliable)
                {
                    SendReliableCommandServerRpc(packet);
                }
                else
                {
                    SendUnreliableCommandServerRpc(packet);
                }
            }
        }

        [ServerRpc(Channel.Unreliable)]
        private void SendUnreliableCommandServerRpc(PlayerCommandPacket packet, RPCInfo info = default)
        {
            ExecutePacket(packet);
        }

        [ServerRpc(Channel.ReliableOrdered)]
        private void SendReliableCommandServerRpc(PlayerCommandPacket packet, RPCInfo info = default)
        {
            ExecutePacket(packet);
        }

        public void ExecutePacket(in PlayerCommandPacket packet)
        {
            CommandPacketHandler handler = _handlers[packet.CommandId];
            if (handler != null && _context != null)
            {
                handler(in packet, _context);
                OnCommandExecuted?.Invoke(packet.CommandId);
            }
        }

        public static void RegisterHandler(byte commandId, CommandPacketHandler handler)
        {
            _handlers[commandId] = handler;
        }

        public static bool HasHandler(byte commandId)
        {
            return _handlers[commandId] != null;
        }

        public static void EnsureDefaultHandlersRegistered()
        {
            if (_defaultHandlersRegistered)
            {
                return;
            }

            _defaultHandlersRegistered = true;

            RegisterHandler(PlayerCommandType.Move, HandleMoveCommand);
            RegisterHandler(PlayerCommandType.Jump, HandleJumpCommand);
            RegisterHandler(PlayerCommandType.Kick, HandleKickCommand);
            RegisterHandler(PlayerCommandType.Grab, HandleGrabCommand);
            RegisterHandler(PlayerCommandType.Throw, HandleThrowCommand);
            RegisterHandler(PlayerCommandType.Climb, HandleClimbCommand);
            RegisterHandler(PlayerCommandType.Magnet, HandleMagnetCommand);
            RegisterHandler(PlayerCommandType.Interact, HandleInteractCommand);
            RegisterHandler(PlayerCommandType.Fuse, HandleFuseCommand);
            RegisterHandler(PlayerCommandType.Separate, HandleSeparateCommand);
        }

        private static void HandleMoveCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Moveable != null)
            {
                context.Moveable.SetMoveInput(packet.VectorData);
                context.Moveable.SetSprint(packet.BoolData);
            }
        }

        private static void HandleJumpCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Moveable != null)
            {
                context.Moveable.Jump();
            }
        }

        private static void HandleKickCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Kicker != null && context.Kicker.CanKick)
            {
                context.Kicker.Kick();
            }
        }

        private static void HandleGrabCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Grabber != null)
            {
                context.Grabber.TriggerGrab();
            }
        }

        private static void HandleThrowCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Thrower != null)
            {
                context.Thrower.Throw();
            }
        }

        private static void HandleClimbCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.Climber != null)
            {
                context.Climber.Climb(packet.VectorData);
            }
        }

        private static void HandleMagnetCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.MagnetOperator != null)
            {
                context.MagnetOperator.SetMagnetActive(packet.BoolData);
            }
        }

        private static void HandleInteractCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.InteractOperator != null)
            {
                context.InteractOperator.TriggerInteract();
            }
        }

        private static void HandleFuseCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.FusionOperator != null && context.FusionOperator.CanFuse)
            {
                context.FusionOperator.RequestFusion();
            }
        }

        private static void HandleSeparateCommand(in PlayerCommandPacket packet, PlayerContext context)
        {
            if (context.FusionOperator != null && context.FusionOperator.IsFused)
            {
                context.FusionOperator.RequestSeparation();
            }
        }
    }
}

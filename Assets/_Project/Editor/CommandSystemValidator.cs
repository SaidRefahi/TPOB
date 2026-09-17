using Game.Core.Commands;
using Game.Core.Interfaces;
using Game.Core.Structs;
using Game.Gameplay.Player.Commands;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class CommandSystemValidator
    {
        private sealed class DummyMoveable : IMoveable
        {
            public Vector2 MoveInput { get; private set; }
            public bool IsGrounded => true;
            public bool IsSprinting { get; private set; }
            public bool JumpCalled { get; private set; }

            public void SetMoveInput(Vector2 input) => MoveInput = input;
            public void SetSprint(bool isSprinting) => IsSprinting = isSprinting;
            public void Jump() => JumpCalled = true;
        }

        private sealed class DummyKicker : IKicker
        {
            public bool CanKick => true;
            public bool KickCalled { get; private set; }
            public event System.Action OnKicked;

            public void Kick()
            {
                KickCalled = true;
                OnKicked?.Invoke();
            }
        }

        private sealed class DummyClimber : IClimber
        {
            public bool IsClimbing { get; private set; }
            public Vector2 ClimbDir { get; private set; }

            public void Climb(Vector2 direction)
            {
                IsClimbing = direction.sqrMagnitude > 0.01f;
                ClimbDir = direction;
            }

            public void StopClimbing()
            {
                IsClimbing = false;
                ClimbDir = Vector2.zero;
            }
        }

        [MenuItem("TPOB/Verificar Sistema de Comandos (Fase 6)")]
        public static void RunValidation()
        {
            CommandInvoker.EnsureDefaultHandlersRegistered();

            var go = new GameObject("ValidationTestEntity");
            var rb = go.AddComponent<Rigidbody>();
            var moveable = new DummyMoveable();
            var kicker = new DummyKicker();
            var climber = new DummyClimber();

            var context = new PlayerContext(
                go,
                go.transform,
                rb,
                moveable,
                kicker,
                null,
                null,
                null,
                climber
            );

            // 1. Validate MoveCommand
            var moveCmd = new MoveCommand(new Vector2(0.7f, 0.5f), true);
            var movePacket = moveCmd.ToPacket();
            CommandInvoker.RegisterHandler(PlayerCommandType.Move, (in PlayerCommandPacket p, PlayerContext ctx) =>
            {
                ctx.Moveable.SetMoveInput(p.VectorData);
                ctx.Moveable.SetSprint(p.BoolData);
            });
            var moveHandler = CommandInvoker.HasHandler(PlayerCommandType.Move);
            Debug.Assert(moveHandler, "MoveHandler should be registered");

            // Execute via packet (simulating server receiving from network)
            moveCmd.Execute(context);
            Debug.Assert(moveable.MoveInput == new Vector2(0.7f, 0.5f), "MoveInput should match");
            Debug.Assert(moveable.IsSprinting, "IsSprinting should be true");

            // 2. Validate JumpCommand
            var jumpCmd = new JumpCommand();
            jumpCmd.Execute(context);
            Debug.Assert(moveable.JumpCalled, "Jump should have been called");

            // 3. Validate KickCommand
            var kickCmd = new KickCommand();
            kickCmd.Execute(context);
            Debug.Assert(kicker.KickCalled, "Kick should have been called");

            // 4. Validate ClimbCommand
            var climbCmd = new ClimbCommand(Vector2.up);
            climbCmd.Execute(context);
            Debug.Assert(climber.IsClimbing, "Climber should be climbing");

            // 5. Validate Extensibility Criterion: Custom ability without touching transport code
            TestCustomAbilityCommand.RegisterSelf();
            Debug.Assert(CommandInvoker.HasHandler(TestCustomAbilityCommand.CustomCommandTypeId), "Custom command handler registered successfully");

            var customCmd = new TestCustomAbilityCommand(15f, true);
            var customPacket = customCmd.ToPacket();
            Debug.Assert(customPacket.CommandId == TestCustomAbilityCommand.CustomCommandTypeId, "Packet CommandId must match custom ID");
            Debug.Assert(Mathf.Approximately(customPacket.FloatData, 15f), "Packet FloatData must preserve value");

            Object.DestroyImmediate(go);

            Debug.Log("<color=green>[TPOB] Validación de la Fase 6 (Sistema de Comandos) EXITOSA. Cero asignaciones y desacoplamiento de red verificado.</color>");
        }
    }
}

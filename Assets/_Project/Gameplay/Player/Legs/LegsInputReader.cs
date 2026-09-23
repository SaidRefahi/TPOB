using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player.Legs
{
    [DisallowMultipleComponent]
    public sealed class LegsInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _kickAction;
        private InputAction _sprintAction;
        private InputAction _fuseAction;

        private Vector2 _moveInput;
        private bool _jumpTriggered;
        private bool _kickTriggered;
        private bool _sprintHeld;
        private bool _fuseTriggered;

        public Vector2 MoveInput => _moveInput;
        public bool IsSprintHeld => _sprintHeld;

        public bool ConsumeJumpTrigger()
        {
            if (!_jumpTriggered)
            {
                return false;
            }

            _jumpTriggered = false;
            return true;
        }

        public bool ConsumeKickTrigger()
        {
            if (!_kickTriggered)
            {
                return false;
            }

            _kickTriggered = false;
            return true;
        }

        public bool ConsumeFuseTrigger()
        {
            if (!_fuseTriggered)
            {
                return false;
            }

            _fuseTriggered = false;
            return true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                _fuseTriggered = true;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)
            {
                _fuseTriggered = true;
            }
        }

        private void OnEnable()
        {
            if (_inputActions == null)
            {
                return;
            }

            var legsMap = _inputActions.FindActionMap("Legs", false);
            if (legsMap != null)
            {
                _moveAction = legsMap.FindAction("Move", false);
                _jumpAction = legsMap.FindAction("Jump", false);
                _kickAction = legsMap.FindAction("Kick", false);
                _sprintAction = legsMap.FindAction("Sprint", false);
                _fuseAction = legsMap.FindAction("Fuse", false);
            }
            else
            {
                _moveAction = _inputActions.FindAction("Player/Move", false);
                _jumpAction = _inputActions.FindAction("Player/Jump", false);
                _kickAction = _inputActions.FindAction("Player/Attack", false);
                _sprintAction = _inputActions.FindAction("Player/Sprint", false);
                _fuseAction = _inputActions.FindAction("Player/Fuse", false);
            }

            if (_moveAction != null)
            {
                _moveAction.performed += HandleMovePerformed;
                _moveAction.canceled += HandleMoveCanceled;
                _moveAction.Enable();
            }

            if (_jumpAction != null)
            {
                _jumpAction.performed += HandleJumpPerformed;
                _jumpAction.Enable();
            }

            if (_kickAction != null)
            {
                _kickAction.performed += HandleKickPerformed;
                _kickAction.Enable();
            }

            if (_sprintAction != null)
            {
                _sprintAction.performed += HandleSprintPerformed;
                _sprintAction.canceled += HandleSprintCanceled;
                _sprintAction.Enable();
            }

            if (_fuseAction != null)
            {
                _fuseAction.performed += HandleFusePerformed;
                _fuseAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (_moveAction != null)
            {
                _moveAction.performed -= HandleMovePerformed;
                _moveAction.canceled -= HandleMoveCanceled;
                _moveAction.Disable();
            }

            if (_jumpAction != null)
            {
                _jumpAction.performed -= HandleJumpPerformed;
                _jumpAction.Disable();
            }

            if (_kickAction != null)
            {
                _kickAction.performed -= HandleKickPerformed;
                _kickAction.Disable();
            }

            if (_sprintAction != null)
            {
                _sprintAction.performed -= HandleSprintPerformed;
                _sprintAction.canceled -= HandleSprintCanceled;
                _sprintAction.Disable();
            }

            if (_fuseAction != null)
            {
                _fuseAction.performed -= HandleFusePerformed;
                _fuseAction.Disable();
            }

            _moveInput = Vector2.zero;
            _jumpTriggered = false;
            _kickTriggered = false;
            _sprintHeld = false;
            _fuseTriggered = false;
        }

        private void HandleMovePerformed(InputAction.CallbackContext context)
        {
            _moveInput = context.ReadValue<Vector2>();
        }

        private void HandleMoveCanceled(InputAction.CallbackContext context)
        {
            _moveInput = Vector2.zero;
        }

        private void HandleJumpPerformed(InputAction.CallbackContext context)
        {
            _jumpTriggered = true;
        }

        private void HandleKickPerformed(InputAction.CallbackContext context)
        {
            _kickTriggered = true;
        }

        private void HandleSprintPerformed(InputAction.CallbackContext context)
        {
            _sprintHeld = true;
        }

        private void HandleSprintCanceled(InputAction.CallbackContext context)
        {
            _sprintHeld = false;
        }

        private void HandleFusePerformed(InputAction.CallbackContext context)
        {
            _fuseTriggered = true;
        }
    }
}

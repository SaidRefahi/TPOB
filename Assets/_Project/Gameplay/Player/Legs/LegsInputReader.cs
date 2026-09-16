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

        private Vector2 _moveInput;
        private bool _jumpTriggered;
        private bool _kickTriggered;
        private bool _sprintHeld;

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

        private void OnEnable()
        {
            if (_inputActions == null)
            {
                return;
            }

            _moveAction = _inputActions.FindAction("Player/Move", false);
            _jumpAction = _inputActions.FindAction("Player/Jump", false);
            _kickAction = _inputActions.FindAction("Player/Attack", false);
            _sprintAction = _inputActions.FindAction("Player/Sprint", false);

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

            _moveInput = Vector2.zero;
            _jumpTriggered = false;
            _kickTriggered = false;
            _sprintHeld = false;
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
    }
}

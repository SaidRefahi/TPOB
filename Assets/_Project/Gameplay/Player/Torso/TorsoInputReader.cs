using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Player.Torso
{
    [DisallowMultipleComponent]
    public sealed class TorsoInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _inputActions;

        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _grabAction;
        private InputAction _throwAction;
        private InputAction _magnetAction;

        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _grabTriggered;
        private bool _throwTriggered;
        private bool _magnetHeld;
        private bool _interactTriggered;

        public Vector2 MoveInput => _moveInput;
        public Vector2 LookInput => _lookInput;
        public bool IsMagnetHeld => _magnetHeld;

        public bool ConsumeGrabTrigger()
        {
            if (!_grabTriggered) return false;
            _grabTriggered = false;
            return true;
        }

        public bool ConsumeThrowTrigger()
        {
            if (!_throwTriggered) return false;
            _throwTriggered = false;
            return true;
        }

        public bool ConsumeInteractTrigger()
        {
            if (!_interactTriggered) return false;
            _interactTriggered = false;
            return true;
        }

        private void OnEnable()
        {
            if (_inputActions == null) return;

            var torsoMap = _inputActions.FindActionMap("Torso", false);
            if (torsoMap != null)
            {
                _moveAction = torsoMap.FindAction("Move", false);
                _lookAction = torsoMap.FindAction("Look", false);
                _grabAction = torsoMap.FindAction("Interact", false);
                _throwAction = torsoMap.FindAction("Throw", false);
                _magnetAction = torsoMap.FindAction("Magnet", false);
            }
            else
            {
                _moveAction = _inputActions.FindAction("Player/Move", false);
                _lookAction = _inputActions.FindAction("Player/Look", false);
                _grabAction = _inputActions.FindAction("Player/Interact", false);
                _throwAction = _inputActions.FindAction("Player/Attack", false);
                _magnetAction = _inputActions.FindAction("Player/Crouch", false);
            }

            if (_moveAction != null)
            {
                _moveAction.performed += HandleMovePerformed;
                _moveAction.canceled += HandleMoveCanceled;
                _moveAction.Enable();
            }

            if (_lookAction != null)
            {
                _lookAction.performed += HandleLookPerformed;
                _lookAction.canceled += HandleLookCanceled;
                _lookAction.Enable();
            }

            if (_grabAction != null)
            {
                _grabAction.performed += HandleGrabPerformed;
                _grabAction.Enable();
            }

            if (_throwAction != null)
            {
                _throwAction.performed += HandleThrowPerformed;
                _throwAction.Enable();
            }

            if (_magnetAction != null)
            {
                _magnetAction.performed += HandleMagnetPerformed;
                _magnetAction.canceled += HandleMagnetCanceled;
                _magnetAction.Enable();
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

            if (_lookAction != null)
            {
                _lookAction.performed -= HandleLookPerformed;
                _lookAction.canceled -= HandleLookCanceled;
                _lookAction.Disable();
            }

            if (_grabAction != null)
            {
                _grabAction.performed -= HandleGrabPerformed;
                _grabAction.Disable();
            }

            if (_throwAction != null)
            {
                _throwAction.performed -= HandleThrowPerformed;
                _throwAction.Disable();
            }

            if (_magnetAction != null)
            {
                _magnetAction.performed -= HandleMagnetPerformed;
                _magnetAction.canceled -= HandleMagnetCanceled;
                _magnetAction.Disable();
            }

            _moveInput = Vector2.zero;
            _lookInput = Vector2.zero;
            _grabTriggered = false;
            _throwTriggered = false;
            _magnetHeld = false;
            _interactTriggered = false;
        }

        private void HandleMovePerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
        private void HandleMoveCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;
        private void HandleLookPerformed(InputAction.CallbackContext ctx) => _lookInput = ctx.ReadValue<Vector2>();
        private void HandleLookCanceled(InputAction.CallbackContext ctx) => _lookInput = Vector2.zero;
        private void HandleGrabPerformed(InputAction.CallbackContext ctx) => _grabTriggered = true;
        private void HandleThrowPerformed(InputAction.CallbackContext ctx) => _throwTriggered = true;
        private void HandleMagnetPerformed(InputAction.CallbackContext ctx) => _magnetHeld = true;
        private void HandleMagnetCanceled(InputAction.CallbackContext ctx) => _magnetHeld = false;
    }
}

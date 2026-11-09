using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Gameplay.UI
{
    [DisallowMultipleComponent]
    public sealed class InputRebindRowUI : MonoBehaviour
    {
        [SerializeField] private string _actionPath = "Player/Jump";
        [SerializeField] private int _bindingIndex;
        [SerializeField] private string _displayName = "Saltar";
        [SerializeField] private TextMeshProUGUI _actionLabelText;
        [SerializeField] private TextMeshProUGUI _bindingValueText;
        [SerializeField] private Button _rebindButton;

        private InputAction _boundAction;
        private InputActionRebindingExtensions.RebindingOperation _rebindingOperation;
        private bool _wasActionEnabled;

        public event Action OnBindingUpdated;

        private void OnEnable()
        {
            if (_rebindButton != null)
            {
                _rebindButton.onClick.AddListener(HandleRebindButtonClicked);
            }

            RefreshDisplay();
        }

        private void OnDisable()
        {
            if (_rebindButton != null)
            {
                _rebindButton.onClick.RemoveListener(HandleRebindButtonClicked);
            }

            CancelActiveRebind();
        }

        public void BindToAsset(InputActionAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            _boundAction = asset.FindAction(_actionPath, false);
            if (_actionLabelText != null && !string.IsNullOrEmpty(_displayName))
            {
                _actionLabelText.text = _displayName;
            }

            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            if (_bindingValueText == null)
            {
                return;
            }

            if (_boundAction == null)
            {
                _bindingValueText.text = "---";
                return;
            }

            int safeIndex = ResolveValidBindingIndex();
            if (safeIndex < 0 || safeIndex >= _boundAction.bindings.Count)
            {
                _bindingValueText.text = "N/A";
                return;
            }

            string readable = InputControlPath.ToHumanReadableString(
                _boundAction.bindings[safeIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);

            if (string.IsNullOrEmpty(readable))
            {
                readable = _boundAction.GetBindingDisplayString(safeIndex);
            }

            _bindingValueText.text = string.IsNullOrEmpty(readable) ? "SIN ASIGNAR" : readable.ToUpperInvariant();
        }

        private int ResolveValidBindingIndex()
        {
            if (_boundAction == null || _boundAction.bindings.Count == 0)
            {
                return -1;
            }

            if (_bindingIndex >= 0 && _bindingIndex < _boundAction.bindings.Count && !_boundAction.bindings[_bindingIndex].isComposite)
            {
                return _bindingIndex;
            }

            for (int i = 0; i < _boundAction.bindings.Count; i++)
            {
                if (!_boundAction.bindings[i].isComposite)
                {
                    return i;
                }
            }

            return 0;
        }

        private void HandleRebindButtonClicked()
        {
            if (_boundAction == null || _rebindingOperation != null)
            {
                return;
            }

            int targetIndex = ResolveValidBindingIndex();
            if (targetIndex < 0)
            {
                return;
            }

            _wasActionEnabled = _boundAction.enabled;
            if (_wasActionEnabled)
            {
                _boundAction.Disable();
            }

            if (_bindingValueText != null)
            {
                _bindingValueText.text = "<color=#FFAA00>PRESS KEY...</color>";
            }

            if (_rebindButton != null)
            {
                _rebindButton.interactable = false;
            }

            _rebindingOperation = _boundAction.PerformInteractiveRebinding(targetIndex)
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(HandleRebindComplete)
                .OnCancel(HandleRebindCancel)
                .Start();
        }

        private void HandleRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishRebind(true);
        }

        private void HandleRebindCancel(InputActionRebindingExtensions.RebindingOperation operation)
        {
            FinishRebind(false);
        }

        private void FinishRebind(bool updated)
        {
            CancelActiveRebind();

            if (_boundAction != null && _wasActionEnabled)
            {
                _boundAction.Enable();
            }

            if (_rebindButton != null)
            {
                _rebindButton.interactable = true;
            }

            RefreshDisplay();

            if (updated && OnBindingUpdated != null)
            {
                OnBindingUpdated.Invoke();
            }
        }

        private void CancelActiveRebind()
        {
            if (_rebindingOperation != null)
            {
                _rebindingOperation.Dispose();
                _rebindingOperation = null;
            }
        }
    }
}

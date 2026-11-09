using Game.Core.Interfaces;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración del Enlace")]
    public sealed class DoorMechanismTrigger : MonoBehaviour
    {
        [Group("Configuración del Enlace")]
        [SerializeField] private MonoBehaviour _mechanism;

        [Group("Configuración del Enlace")]
        [SerializeField] private SlidingDoor _door;

        [Group("Configuración del Enlace")]
        [SerializeField] private bool _autoCloseOnDeactivate = true;

        [Group("Configuración del Enlace")]
        [SerializeField] private LineRenderer _conduitCable;

        [Group("Configuración del Enlace")]
        [SerializeField] private Color _activeCableColor = new Color(0.15f, 0.95f, 0.45f);

        [Group("Configuración del Enlace")]
        [SerializeField] private Color _inactiveCableColor = new Color(0.9f, 0.25f, 0.2f);

        private IInteractableMechanism _cachedMechanism;

        public MonoBehaviour Mechanism => _mechanism;
        public SlidingDoor Door => _door;
        public bool AutoCloseOnDeactivate => _autoCloseOnDeactivate;
        public LineRenderer ConduitCable => _conduitCable;

        private void Awake()
        {
            if (_door == null)
            {
                _door = GetComponent<SlidingDoor>();
            }

            ResolveMechanism();
        }

        private void OnEnable()
        {
            if (_cachedMechanism == null)
            {
                ResolveMechanism();
            }

            if (_cachedMechanism != null)
            {
                _cachedMechanism.OnStateChanged += HandleMechanismStateChanged;
                if (_cachedMechanism.IsActivated && _door != null)
                {
                    _door.OpenDoor();
                }
            }
        }

        private void OnDisable()
        {
            if (_cachedMechanism != null)
            {
                _cachedMechanism.OnStateChanged -= HandleMechanismStateChanged;
            }
        }

        private void ResolveMechanism()
        {
            if (_mechanism != null)
            {
                _cachedMechanism = _mechanism as IInteractableMechanism;
                if (_cachedMechanism == null)
                {
                    _cachedMechanism = _mechanism.GetComponent<IInteractableMechanism>();
                }
            }
        }

        private void HandleMechanismStateChanged(bool isActivated)
        {
            UpdateCableVisual(isActivated);

            if (_door == null) return;

            if (isActivated)
            {
                _door.OpenDoor();
            }
            else if (_autoCloseOnDeactivate)
            {
                _door.CloseDoor();
            }
        }

        public void UpdateCableVisual(bool isActivated)
        {
            if (_conduitCable == null) return;

            Color targetColor = isActivated ? _activeCableColor : _inactiveCableColor;
            _conduitCable.startColor = targetColor;
            _conduitCable.endColor = targetColor;
        }

        public void Configure(MonoBehaviour mechanism, SlidingDoor door, bool autoClose = true, LineRenderer cable = null)
        {
            _mechanism = mechanism;
            _door = door;
            _autoCloseOnDeactivate = autoClose;
            _conduitCable = cable;
            ResolveMechanism();

            if (_cachedMechanism != null)
            {
                UpdateCableVisual(_cachedMechanism.IsActivated);
            }
        }
    }
}

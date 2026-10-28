using Game.Core.Interfaces;
using PurrNet;
using PurrNet.Transports;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Interactables
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración Magnética")]
    [DeclareBoxGroup("Activación por Mecanismo")]
    [DeclareBoxGroup("Visuales")]
    public sealed class MagneticWall : NetworkBehaviour, IMagneticSurface
    {
        [Group("Configuración Magnética")]
        [SerializeField] private bool _initialActive = true;

        [Group("Configuración Magnética")]
        [SerializeField] private float _adhesionForce = 35f;

        [Group("Activación por Mecanismo")]
        [SerializeField] private LeverMechanism _linkedLever;

        [Group("Activación por Mecanismo")]
        [SerializeField] private bool _invertLeverSignal;

        [Group("Visuales")]
        [SerializeField] private Renderer _wallRenderer;

        [Group("Visuales")]
        [SerializeField] private Color _activeEmissionColor = new Color(0.1f, 0.75f, 1f, 1f);

        [Group("Visuales")]
        [SerializeField] private Color _inactiveEmissionColor = new Color(0.1f, 0.1f, 0.1f, 0f);

        [Group("Configuración Magnética")]
        [SerializeField] private SyncVar<bool> _isActive = new(true);

        private Collider _collider;
        private MaterialPropertyBlock _propBlock;
        private static readonly int EmissionColorPropId = Shader.PropertyToID("_EmissionColor");

        public bool IsActive => _isActive.value;
        public float AdhesionForce => _adhesionForce;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _propBlock = new MaterialPropertyBlock();

            if (_wallRenderer == null)
            {
                _wallRenderer = GetComponent<Renderer>();
            }

            if (!isSpawned)
            {
                _isActive.value = _initialActive;
            }
            UpdateVisuals();
        }

        private void Start()
        {
            if (_linkedLever != null)
            {
                _linkedLever.OnStateChanged += HandleLeverStateChanged;
                bool shouldBeActive = _invertLeverSignal ? !_linkedLever.IsActivated : _linkedLever.IsActivated;
                SetActive(shouldBeActive);
            }
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            _isActive.onChanged += HandleActiveStateChanged;
            UpdateVisuals();
        }

        protected override void OnDespawned()
        {
            _isActive.onChanged -= HandleActiveStateChanged;
            base.OnDespawned();
        }

        protected override void OnDestroy()
        {
            if (_linkedLever != null)
            {
                _linkedLever.OnStateChanged -= HandleLeverStateChanged;
            }

            _isActive.onChanged -= HandleActiveStateChanged;
            base.OnDestroy();
        }

        public Vector3 GetSurfaceNormal(Vector3 worldPoint)
        {
            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }

            if (_collider is BoxCollider box)
            {
                Vector3 localPoint = box.transform.InverseTransformPoint(worldPoint) - box.center;
                Vector3 halfSize = box.size * 0.5f;

                float dx = halfSize.x - Mathf.Abs(localPoint.x);
                float dy = halfSize.y - Mathf.Abs(localPoint.y);
                float dz = halfSize.z - Mathf.Abs(localPoint.z);

                Vector3 localNormal = Vector3.up;
                if (dx <= dy && dx <= dz)
                {
                    localNormal = new Vector3(Mathf.Sign(localPoint.x), 0f, 0f);
                }
                else if (dy <= dx && dy <= dz)
                {
                    localNormal = new Vector3(0f, Mathf.Sign(localPoint.y), 0f);
                }
                else
                {
                    localNormal = new Vector3(0f, 0f, Mathf.Sign(localPoint.z));
                }

                return box.transform.TransformDirection(localNormal).normalized;
            }

            return transform.forward;
        }

        [Button("Alternar Magnetismo")]
        public void Toggle()
        {
            SetActive(!_isActive.value);
        }

        public void SetActive(bool active)
        {
            if (isSpawned && !isServer)
            {
                RequestSetActiveServerRpc(active);
                return;
            }

            _isActive.value = active;
            UpdateVisuals();
        }

        [ServerRpc(requireOwnership: false)]
        private void RequestSetActiveServerRpc(bool active)
        {
            _isActive.value = active;
            UpdateVisuals();
        }

        private void HandleActiveStateChanged(bool active)
        {
            UpdateVisuals();
        }

        private void HandleLeverStateChanged(bool leverState)
        {
            bool shouldBeActive = _invertLeverSignal ? !leverState : leverState;
            SetActive(shouldBeActive);
        }

        private void UpdateVisuals()
        {
            if (_wallRenderer == null)
            {
                return;
            }

            _wallRenderer.GetPropertyBlock(_propBlock);
            Color targetColor = _isActive.value ? _activeEmissionColor : _inactiveEmissionColor;
            _propBlock.SetColor(EmissionColorPropId, targetColor);
            _wallRenderer.SetPropertyBlock(_propBlock);
        }
    }
}

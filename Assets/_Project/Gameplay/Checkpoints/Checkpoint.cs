using DG.Tweening;
using PurrNet;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Checkpoints
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Configuración")]
    [DeclareBoxGroup("Spawns")]
    [DeclareBoxGroup("Visuales")]
    public sealed class Checkpoint : NetworkBehaviour
    {
        [Group("Configuración")]
        [SerializeField] private int _priority = 10;

        [Group("Configuración")]
        [SerializeField] private bool _isActive;

        [Group("Spawns")]
        [SerializeField] private Transform _legsSpawnPoint;

        [Group("Spawns")]
        [SerializeField] private Transform _torsoSpawnPoint;

        [Group("Visuales")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Group("Visuales")]
        [SerializeField] private Color _inactiveColor = new Color(0.85f, 0.45f, 0.1f);

        [Group("Visuales")]
        [SerializeField] private Color _activeColor = new Color(0.1f, 0.9f, 0.85f);

        [Group("Visuales")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        private MaterialPropertyBlock _propBlock;
        private Tween _pulseTween;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        public bool IsActive => _isActive;
        public int Priority => _priority;
        public Transform LegsSpawnPoint => _legsSpawnPoint != null ? _legsSpawnPoint : transform;
        public Transform TorsoSpawnPoint => _torsoSpawnPoint != null ? _torsoSpawnPoint : transform;

        private void Awake()
        {
            if (_impulseSource == null)
            {
                _impulseSource = GetComponent<CinemachineImpulseSource>();
            }

            if (_indicatorRenderer == null)
            {
                _indicatorRenderer = GetComponentInChildren<Renderer>();
            }

            UpdateVisuals(_isActive);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            UpdateVisuals(_isActive);
        }

        protected override void OnDestroy()
        {
            _pulseTween?.Kill();
            base.OnDestroy();
        }

        public void SetActive(bool active)
        {
            if (_isActive == active) return;
            _isActive = active;

            if (!isSpawned)
            {
                UpdateVisuals(_isActive);
            }
            else if (isServer)
            {
                UpdateVisualsObserversRpc(_isActive);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSpawned && !isServer) return;
            if (_isActive) return;

            if (other.CompareTag("Player") ||
                other.GetComponentInParent<Core.Interfaces.IDamageable>() != null ||
                other.attachedRigidbody != null && other.attachedRigidbody.GetComponent<Core.Interfaces.IDamageable>() != null)
            {
                var system = FindFirstObjectByType<CheckpointSystem>();
                if (system != null)
                {
                    system.SetActiveCheckpoint(this);
                }
            }
        }

        private void UpdateVisuals(bool active)
        {
            _isActive = active;

            if (_indicatorRenderer != null)
            {
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
                _indicatorRenderer.GetPropertyBlock(_propBlock);

                Color targetColor = active ? _activeColor : _inactiveColor;
                _propBlock.SetColor(BaseColorProp, targetColor);
                _propBlock.SetColor(ColorProp, targetColor);
                _propBlock.SetColor(EmissionColorProp, targetColor * (active ? 2.5f : 0.6f));
                _indicatorRenderer.SetPropertyBlock(_propBlock);
            }

            if (active)
            {
                _pulseTween?.Kill();
                transform.localScale = Vector3.one;
                _pulseTween = transform.DOPunchScale(Vector3.up * 0.25f, 0.4f, 8, 0.6f);

                if (_impulseSource != null)
                {
                    _impulseSource.GenerateImpulse(0.5f);
                }
            }
        }

        [ObserversRpc(runLocally: true, bufferLast: true)]
        private void UpdateVisualsObserversRpc(bool active)
        {
            UpdateVisuals(active);
        }
    }
}

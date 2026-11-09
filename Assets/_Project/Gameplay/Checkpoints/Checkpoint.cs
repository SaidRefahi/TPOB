using DG.Tweening;
using Game.Core.Enums;
using Game.Gameplay.Player.Death;
using Game.Gameplay.Player.Legs;
using Game.Gameplay.Player.Torso;
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
        [SerializeField] private bool _hasLegsReached;

        [Group("Configuración")]
        [SerializeField] private bool _hasTorsoReached;

        [Group("Spawns")]
        [SerializeField] private Transform _legsSpawnPoint;

        [Group("Spawns")]
        [SerializeField] private Transform _torsoSpawnPoint;

        [Group("Visuales")]
        [SerializeField] private Renderer _indicatorRenderer;

        [Group("Visuales")]
        [SerializeField] private Renderer _legsIndicatorRenderer;

        [Group("Visuales")]
        [SerializeField] private Renderer _torsoIndicatorRenderer;

        [Group("Visuales")]
        [SerializeField] private Color _inactiveColor = new Color(0.35f, 0.35f, 0.35f);

        [Group("Visuales")]
        [SerializeField] private Color _legsColor = new Color(0.1f, 0.85f, 0.95f);

        [Group("Visuales")]
        [SerializeField] private Color _torsoColor = new Color(0.95f, 0.55f, 0.1f);

        [Group("Visuales")]
        [SerializeField] private Color _bothActiveColor = new Color(0.2f, 1f, 0.8f);

        [Group("Visuales")]
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        private MaterialPropertyBlock _propBlock;
        private Tween _pulseTween;

        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProp = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");

        public bool IsActive => _hasLegsReached || _hasTorsoReached;
        public bool IsFullyActive => _hasLegsReached && _hasTorsoReached;
        public bool HasLegsReached => _hasLegsReached;
        public bool HasTorsoReached => _hasTorsoReached;
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

            UpdateVisuals(_hasLegsReached, _hasTorsoReached);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            UpdateVisuals(_hasLegsReached, _hasTorsoReached);
        }

        protected override void OnDestroy()
        {
            _pulseTween?.Kill();
            base.OnDestroy();
        }

        public void SetRoleReached(PlayerRole role, bool reached)
        {
            bool changed = false;

            if (role == PlayerRole.Legs)
            {
                if (_hasLegsReached != reached)
                {
                    _hasLegsReached = reached;
                    changed = true;
                }
            }
            else if (role == PlayerRole.Torso)
            {
                if (_hasTorsoReached != reached)
                {
                    _hasTorsoReached = reached;
                    changed = true;
                }
            }

            if (!changed) return;

            if (!isSpawned)
            {
                UpdateVisuals(_hasLegsReached, _hasTorsoReached);
            }
            else if (isServer)
            {
                UpdateVisualsObserversRpc(_hasLegsReached, _hasTorsoReached);
            }
        }

        public void SetBothReached(bool reached)
        {
            if (_hasLegsReached == reached && _hasTorsoReached == reached) return;

            _hasLegsReached = reached;
            _hasTorsoReached = reached;

            if (!isSpawned)
            {
                UpdateVisuals(_hasLegsReached, _hasTorsoReached);
            }
            else if (isServer)
            {
                UpdateVisualsObserversRpc(_hasLegsReached, _hasTorsoReached);
            }
        }

        public void SetActive(bool active)
        {
            SetBothReached(active);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isSpawned && !isServer) return;

            if (!TryResolvePlayerRole(other, out PlayerRole role, out bool isFused))
            {
                return;
            }

            if (isFused && _hasLegsReached && _hasTorsoReached) return;
            if (!isFused && role == PlayerRole.Legs && _hasLegsReached) return;
            if (!isFused && role == PlayerRole.Torso && _hasTorsoReached) return;

            var system = FindFirstObjectByType<CheckpointSystem>();
            if (system != null)
            {
                system.RegisterCheckpointReached(this, role, isFused);
            }
        }

        private static bool TryResolvePlayerRole(Collider other, out PlayerRole role, out bool isFused)
        {
            role = PlayerRole.None;
            isFused = false;

            if (other.TryGetComponent<PlayerDeathHandler>(out var deathHandler) ||
                (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out deathHandler)) ||
                (other.transform.parent != null && other.transform.parent.TryGetComponent(out deathHandler)))
            {
                role = deathHandler.Role;
                isFused = deathHandler.IsFused;
                return role != PlayerRole.None;
            }

            if (other.TryGetComponent<LegsController>(out var legs) ||
                (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out legs)))
            {
                role = PlayerRole.Legs;
                isFused = legs.IsFused;
                return true;
            }

            if (other.TryGetComponent<TorsoController>(out var torso) ||
                (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent(out torso)))
            {
                role = PlayerRole.Torso;
                isFused = torso.IsFused;
                return true;
            }

            return false;
        }

        private void UpdateVisuals(bool legsReached, bool torsoReached)
        {
            _hasLegsReached = legsReached;
            _hasTorsoReached = torsoReached;

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            // Individual Sub-Indicators if assigned
            if (_legsIndicatorRenderer != null)
            {
                _legsIndicatorRenderer.GetPropertyBlock(_propBlock);
                Color legsTarget = legsReached ? _legsColor : _inactiveColor;
                _propBlock.SetColor(BaseColorProp, legsTarget);
                _propBlock.SetColor(ColorProp, legsTarget);
                _propBlock.SetColor(EmissionColorProp, legsTarget * (legsReached ? 2.2f : 0.4f));
                _legsIndicatorRenderer.SetPropertyBlock(_propBlock);
            }

            if (_torsoIndicatorRenderer != null)
            {
                _torsoIndicatorRenderer.GetPropertyBlock(_propBlock);
                Color torsoTarget = torsoReached ? _torsoColor : _inactiveColor;
                _propBlock.SetColor(BaseColorProp, torsoTarget);
                _propBlock.SetColor(ColorProp, torsoTarget);
                _propBlock.SetColor(EmissionColorProp, torsoTarget * (torsoReached ? 2.2f : 0.4f));
                _torsoIndicatorRenderer.SetPropertyBlock(_propBlock);
            }

            // Main Indicator
            if (_indicatorRenderer != null)
            {
                _indicatorRenderer.GetPropertyBlock(_propBlock);

                Color targetColor;
                float emissionMult;

                if (legsReached && torsoReached)
                {
                    targetColor = _bothActiveColor;
                    emissionMult = 2.6f;
                }
                else if (legsReached)
                {
                    targetColor = _legsColor;
                    emissionMult = 1.8f;
                }
                else if (torsoReached)
                {
                    targetColor = _torsoColor;
                    emissionMult = 1.8f;
                }
                else
                {
                    targetColor = _inactiveColor;
                    emissionMult = 0.4f;
                }

                _propBlock.SetColor(BaseColorProp, targetColor);
                _propBlock.SetColor(ColorProp, targetColor);
                _propBlock.SetColor(EmissionColorProp, targetColor * emissionMult);
                _indicatorRenderer.SetPropertyBlock(_propBlock);
            }

            if (legsReached || torsoReached)
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
        private void UpdateVisualsObserversRpc(bool legsReached, bool torsoReached)
        {
            UpdateVisuals(legsReached, torsoReached);
        }
    }
}

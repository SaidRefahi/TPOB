using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Core.Enums;
using Game.Network.Audio;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [DeclareBoxGroup("Configuración de Descontrol")]
    public sealed class TumbleController : MonoBehaviour
    {
        [Group("Configuración de Descontrol")]
        [SerializeField] private float _maxTumbleDuration = 1.2f;

        [Group("Configuración de Descontrol")]
        [SerializeField] private float _torqueStrength = 14f;

        [Group("Configuración de Descontrol")]
        [SerializeField] private float _recoveryDuration = 0.3f;

        [Group("Configuración de Descontrol")]
        [SerializeField] private float _cooldown = 1.8f;

        private Rigidbody _rigidbody;
        private NetworkAudioRelay _audioRelay;
        private Transform _visualModel;
        private Vector3 _initialVisualScale = Vector3.one;
        private bool _isTumbling;
        private float _lastTumbleTime = -10f;
        private Tween _recoveryTween;

        public bool IsTumbling => _isTumbling;
        public event Action OnTumbleStarted;
        public event Action OnTumbleFinished;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            var mesh = transform.Find("LegsMesh");
            if (mesh == null) mesh = transform.Find("TorsoMesh");
            if (mesh == null) mesh = transform.Find("Visual");
            _visualModel = mesh != null ? mesh : transform;
            if (_visualModel != null)
            {
                _initialVisualScale = _visualModel.localScale;
            }
        }

        private void Start()
        {
            _audioRelay = FindFirstObjectByType<NetworkAudioRelay>();
        }

        private void OnDestroy()
        {
            _recoveryTween?.Kill();
        }

        public void TriggerTumble(Vector3 impactVelocity)
        {
            if (_isTumbling || Time.time < _lastTumbleTime + _cooldown || _rigidbody == null || _rigidbody.isKinematic)
            {
                return;
            }

            _isTumbling = true;
            _lastTumbleTime = Time.time;
            OnTumbleStarted?.Invoke();

            // Release rotational constraints for comedic rolling
            _rigidbody.constraints = RigidbodyConstraints.None;

            // Apply angular impulse perpendicular to impact
            Vector3 torqueAxis = Vector3.Cross(impactVelocity.normalized, Vector3.up);
            if (torqueAxis.sqrMagnitude < 0.01f)
            {
                torqueAxis = UnityEngine.Random.onUnitSphere;
            }

            _rigidbody.AddTorque(torqueAxis.normalized * _torqueStrength, ForceMode.Impulse);

            // Audio effect
            if (_audioRelay != null)
            {
                _audioRelay.PlayNetworkAudio(AudioCue.PlayerTumble, transform.position, 0.85f, UnityEngine.Random.Range(0.9f, 1.15f));
            }

            WaitForRecoveryAsync().Forget();
        }

        private async UniTaskVoid WaitForRecoveryAsync()
        {
            float elapsed = 0f;
            var ct = this.GetCancellationTokenOnDestroy();

            while (elapsed < _maxTumbleDuration)
            {
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, ct);
                elapsed += Time.fixedDeltaTime;

                if (elapsed >= 0.45f && _rigidbody != null && _rigidbody.linearVelocity.sqrMagnitude < 0.25f)
                {
                    break;
                }
            }

            RecoverFromTumble();
        }

        private void RecoverFromTumble()
        {
            if (!_isTumbling || _rigidbody == null) return;

            _isTumbling = false;

            // Stand upright
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

            // Zero angular velocity and restore normal movement constraints
            if (!_rigidbody.isKinematic)
            {
                _rigidbody.angularVelocity = Vector3.zero;
            }
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Elastic pop recovery juicing with DOTween
            if (_visualModel != null)
            {
                _recoveryTween?.Kill();
                _visualModel.localScale = _initialVisualScale;
                _recoveryTween = _visualModel.DOPunchScale(new Vector3(-0.2f, 0.35f, -0.2f), _recoveryDuration, 7, 0.9f)
                    .OnComplete(() =>
                    {
                        if (_visualModel != null) _visualModel.localScale = _initialVisualScale;
                    });
            }

            OnTumbleFinished?.Invoke();
        }
    }
}

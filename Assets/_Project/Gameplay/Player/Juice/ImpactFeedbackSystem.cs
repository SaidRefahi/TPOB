using DG.Tweening;
using Game.Core.Audio;
using Game.Core.Enums;
using Game.Network.Audio;
using TriInspector;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Feedback de Impacto")]
    [DeclareBoxGroup("Squash & Stretch")]
    public sealed class ImpactFeedbackSystem : MonoBehaviour
    {
        [Group("Feedback de Impacto")]
        [SerializeField] private float _minVelocity = 2.5f;

        [Group("Feedback de Impacto")]
        [SerializeField] private float _maxVelocity = 14f;

        [Group("Feedback de Impacto")]
        [SerializeField] private float _impulseMultiplier = 0.15f;

        [Group("Feedback de Impacto")]
        [SerializeField] private float _cooldown = 0.12f;

        [Group("Squash & Stretch")]
        [SerializeField] private Transform _visualModel;

        [Group("Squash & Stretch")]
        [SerializeField] private float _squashStrength = 0.35f;

        [Group("Squash & Stretch")]
        [SerializeField] private float _squashDuration = 0.25f;

        [Group("Squash & Stretch")]
        [SerializeField] private int _squashVibrato = 8;

        private CinemachineImpulseSource _impulseSource;
        private NetworkAudioRelay _audioRelay;
        private TumbleController _tumbleController;
        private Vector3 _initialScale = Vector3.one;
        private Tween _squashTween;
        private float _lastImpactTime;

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            if (_impulseSource == null)
            {
                _impulseSource = GetComponentInChildren<CinemachineImpulseSource>();
            }

            _tumbleController = GetComponent<TumbleController>();

            if (_visualModel == null)
            {
                var mesh = transform.Find("LegsMesh");
                if (mesh == null) mesh = transform.Find("TorsoMesh");
                if (mesh == null) mesh = transform.Find("Visual");
                if (mesh != null)
                {
                    _visualModel = mesh;
                }
                else
                {
                    _visualModel = transform;
                }
            }

            if (_visualModel != null)
            {
                _initialScale = _visualModel.localScale;
            }
        }

        private void Start()
        {
            _audioRelay = FindFirstObjectByType<NetworkAudioRelay>();
        }

        private void OnDestroy()
        {
            _squashTween?.Kill();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < _lastImpactTime + _cooldown)
            {
                return;
            }

            float speed = collision.relativeVelocity.magnitude;
            if (speed < _minVelocity)
            {
                return;
            }

            _lastImpactTime = Time.time;
            float intensity = Mathf.Clamp01((speed - _minVelocity) / (_maxVelocity - _minVelocity));

            Vector3 contactPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
            Vector3 normal = collision.contacts.Length > 0 ? collision.contacts[0].normal : -collision.relativeVelocity.normalized;

            // 1. Cinemachine Impulse Shake
            if (_impulseSource != null)
            {
                Vector3 impulseDir = -collision.relativeVelocity.normalized;
                _impulseSource.GenerateImpulse(impulseDir * (_impulseMultiplier * (1f + intensity * 2.5f)));
            }

            // 2. Procedural Squash & Stretch with DOTween
            ApplySquashAndStretch(normal, intensity);

            // 3. Replicated Sound Effect
            PlayImpactAudio(contactPoint, speed, intensity);

            // 4. Trigger Comedic Tumble if severe
            if (_tumbleController != null && speed >= 7.5f)
            {
                _tumbleController.TriggerTumble(collision.relativeVelocity);
            }
        }

        private void ApplySquashAndStretch(Vector3 normal, float intensity)
        {
            if (_visualModel == null) return;

            _squashTween?.Kill();
            _visualModel.localScale = _initialScale;

            float factor = _squashStrength * (0.5f + intensity * 0.5f);
            Vector3 punch = new Vector3(
                Mathf.Abs(normal.x) > 0.5f ? -factor : factor * 0.5f,
                Mathf.Abs(normal.y) > 0.5f ? -factor : factor * 0.5f,
                Mathf.Abs(normal.z) > 0.5f ? -factor : factor * 0.5f
            );

            _squashTween = _visualModel.DOPunchScale(punch, _squashDuration, _squashVibrato, 0.8f)
                .OnComplete(() =>
                {
                    if (_visualModel != null) _visualModel.localScale = _initialScale;
                });
        }

        private void PlayImpactAudio(Vector3 position, float speed, float intensity)
        {
            AudioCue cue;
            if (speed > 8f)
            {
                cue = AudioCue.ImpactHeavy;
            }
            else if (speed > 5f)
            {
                cue = AudioCue.ImpactMetal;
            }
            else
            {
                cue = AudioCue.ImpactLight;
            }

            float volume = Mathf.Clamp(0.3f + intensity * 0.7f, 0.2f, 1f);
            float pitch = UnityEngine.Random.Range(0.9f, 1.12f);

            if (_audioRelay != null)
            {
                _audioRelay.PlayNetworkAudio(cue, position, volume, pitch);
            }
            else
            {
                var audioService = FindFirstObjectByType<AudioService>();
                audioService?.PlaySfx(cue, position, volume, pitch);
            }
        }
    }
}

using DG.Tweening;
using TriInspector;
using UnityEngine;

namespace Game.Gameplay.Player.Robot
{
    [DisallowMultipleComponent]
    [DeclareBoxGroup("Socket Configuration")]
    [DeclareBoxGroup("DOTween Juicing")]
    [DeclareBoxGroup("Impulsos de Cámara")]
    public sealed class FusionSocket : MonoBehaviour
    {
        [Group("Socket Configuration")]
        [SerializeField] private Transform _attachPoint;

        [Group("Socket Configuration")]
        [SerializeField] private Vector3 _separationOffset = new Vector3(0f, 0.25f, 1.5f);

        [Group("DOTween Juicing")]
        [SerializeField] private Transform _visualJoint;

        [Group("DOTween Juicing")]
        [SerializeField] private Vector3 _punchScale = new Vector3(0.2f, 0.2f, 0.2f);

        [Group("DOTween Juicing")]
        [SerializeField] private float _punchDuration = 0.3f;

        [Group("DOTween Juicing")]
        [SerializeField] private int _punchVibrato = 10;

        [Group("DOTween Juicing")]
        [SerializeField] private float _punchElasticity = 1f;

        [Group("Impulsos de Cámara")]
        [SerializeField] private Unity.Cinemachine.CinemachineImpulseSource _dockImpulseSource;

        [Group("Impulsos de Cámara")]
        [SerializeField] private float _dockImpulseForce = 0.8f;

        private Tween _punchTween;

        public Transform AttachPoint => _attachPoint != null ? _attachPoint : transform;
        public Vector3 SeparationOffset => _separationOffset;

        private void Awake()
        {
            if (_attachPoint == null)
            {
                _attachPoint = transform;
            }

            if (_visualJoint == null)
            {
                _visualJoint = transform;
            }

            if (_dockImpulseSource == null)
            {
                _dockImpulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            }
        }

        private void OnDestroy()
        {
            _punchTween?.Kill();
        }

        public void PlayDockJuice()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_dockImpulseSource != null)
            {
                _dockImpulseSource.GenerateImpulse(Vector3.down * _dockImpulseForce);
            }

            _punchTween?.Kill();

            if (_visualJoint != null)
            {
                _punchTween = _visualJoint.DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity);
            }
        }
    }
}

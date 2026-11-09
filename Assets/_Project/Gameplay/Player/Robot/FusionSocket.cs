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
        private Vector3 _initialVisualJointScale = Vector3.one;

        public Transform AttachPoint => _attachPoint != null ? _attachPoint : transform;
        public Vector3 SeparationOffset => _separationOffset;

        private void Awake()
        {
            if (_visualJoint == null)
            {
                _visualJoint = transform;
            }
            _initialVisualJointScale = _visualJoint.localScale;

            if (_attachPoint == null)
            {
                _attachPoint = transform;
            }

            // Isolation guard: AttachPoint must not be deformed by visual joint juice
            if (_attachPoint == _visualJoint)
            {
                var apChild = transform.Find("AttachPoint");
                if (apChild == null)
                {
                    var go = new GameObject("AttachPoint");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                    _attachPoint = go.transform;
                }
                else
                {
                    _attachPoint = apChild;
                }
            }

            if (_dockImpulseSource == null)
            {
                _dockImpulseSource = GetComponent<Unity.Cinemachine.CinemachineImpulseSource>();
            }
        }

        private void OnDestroy()
        {
            if (_punchTween != null && _punchTween.IsActive())
            {
                _punchTween.Kill();
            }
            ResetVisualJointScale();
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

            if (_punchTween != null && _punchTween.IsActive())
            {
                _punchTween.Kill();
                ResetVisualJointScale();
            }

            if (_visualJoint != null)
            {
                _visualJoint.localScale = _initialVisualJointScale;
                _punchTween = _visualJoint.DOPunchScale(_punchScale, _punchDuration, _punchVibrato, _punchElasticity)
                    .OnKill(ResetVisualJointScale)
                    .OnComplete(ResetVisualJointScale);
            }
        }

        private void ResetVisualJointScale()
        {
            if (_visualJoint != null)
            {
                _visualJoint.localScale = _initialVisualJointScale;
            }
        }
    }
}

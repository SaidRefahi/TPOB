using DG.Tweening;
using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    public sealed class MagneticBootsVisuals : MonoBehaviour
    {
        [SerializeField] private Light _magnetLight;
        [SerializeField] private Color _magnetColor = new Color(0.15f, 0.75f, 1f);
        [SerializeField] private float _maxIntensity = 3.5f;
        [SerializeField] private float _fadeDuration = 0.2f;

        private Tween _lightTween;
        private bool _isAttached;

        public bool IsAttached => _isAttached;

        private void Awake()
        {
            if (_magnetLight == null)
            {
                var lightGo = new GameObject("MagneticBootsLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.15f, 0f);
                _magnetLight = lightGo.AddComponent<Light>();
                _magnetLight.type = LightType.Point;
                _magnetLight.range = 2.5f;
                _magnetLight.color = _magnetColor;
                _magnetLight.intensity = 0f;
                _magnetLight.shadows = LightShadows.None;
            }
            else
            {
                _magnetLight.intensity = 0f;
            }
        }

        private void OnDestroy()
        {
            _lightTween?.Kill();
        }

        public void SetMagneticState(bool isAttached)
        {
            if (_isAttached == isAttached)
            {
                return;
            }

            _isAttached = isAttached;
            _lightTween?.Kill();

            if (_magnetLight != null)
            {
                float targetIntensity = isAttached ? _maxIntensity : 0f;
                _lightTween = _magnetLight.DOIntensity(targetIntensity, _fadeDuration).SetEase(Ease.OutQuad);
            }
        }
    }
}

using DG.Tweening;
using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    public sealed class KickChargeFlash : MonoBehaviour
    {
        [SerializeField] private Light _flashLight;
        [SerializeField] private Transform _visualModel;
        [SerializeField] private Color _flashColor = new Color(1f, 0.95f, 0.5f);
        [SerializeField] private float _peakIntensity = 6.0f;
        [SerializeField] private float _flashDuration = 0.15f;

        private GameObject _sparkObject;
        private Tween _flashTween;
        private Tween _punchTween;
        private Tween _sparkTween;
        private Vector3 _initialScale = Vector3.one;

        private void Awake()
        {
            if (_flashLight == null)
            {
                var existingLight = GetComponentInChildren<Light>();
                if (existingLight != null)
                {
                    _flashLight = existingLight;
                }
            }

            if (_flashLight == null)
            {
                var lightGo = new GameObject("KickFlashLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.2f, 0.5f);
                _flashLight = lightGo.AddComponent<Light>();
                _flashLight.type = LightType.Point;
                _flashLight.range = 3.5f;
                _flashLight.color = _flashColor;
                _flashLight.intensity = 0f;
                _flashLight.shadows = LightShadows.None;
            }
            else
            {
                _flashLight.intensity = 0f;
            }

            // Create persistent spark billboard object for guaranteed visibility
            if (_sparkObject == null)
            {
                _sparkObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sparkObject.name = "KickFlashSpark";
                _sparkObject.transform.SetParent(transform, false);
                _sparkObject.transform.localScale = Vector3.zero;

                var col = _sparkObject.GetComponent<Collider>();
                if (col != null)
                {
                    Destroy(col);
                }

                var renderer = _sparkObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlit == null) unlit = Shader.Find("Sprites/Default");
                    if (unlit == null) unlit = Shader.Find("Hidden/Internal-Colored");

                    if (unlit != null)
                    {
                        var mat = new Material(unlit)
                        {
                            name = "Mat_KickFlashSpark",
                            color = _flashColor
                        };
                        if (mat.HasProperty("_BaseColor"))
                        {
                            mat.SetColor("_BaseColor", _flashColor);
                        }
                        renderer.sharedMaterial = mat;
                    }
                }

                _sparkObject.SetActive(false);
            }

            if (_visualModel != null)
            {
                _initialScale = _visualModel.localScale;
            }
        }

        private void OnDisable()
        {
            _flashTween?.Kill();
            _punchTween?.Kill();
            _sparkTween?.Kill();

            if (_flashLight != null)
            {
                _flashLight.intensity = 0f;
            }

            if (_sparkObject != null)
            {
                _sparkObject.transform.localScale = Vector3.zero;
                _sparkObject.SetActive(false);
            }

            if (_visualModel != null)
            {
                _visualModel.localScale = _initialScale;
            }
        }

        public void TriggerFlash()
        {
            Vector3 defaultPos = transform.position + transform.forward * 0.8f + Vector3.up * 0.15f;
            TriggerFlash(defaultPos);
        }

        public void TriggerFlash(Vector3 footWorldPosition)
        {
            if (_flashLight != null)
            {
                _flashTween?.Kill();
                _flashLight.transform.position = footWorldPosition + Vector3.up * 0.15f;
                _flashLight.color = _flashColor;
                _flashLight.intensity = _peakIntensity;
                _flashTween = DOTween.To(() => _flashLight.intensity, x => _flashLight.intensity = x, 0f, _flashDuration)
                    .SetEase(Ease.OutQuad);
            }

            if (_sparkObject != null)
            {
                _sparkTween?.Kill();
                _sparkObject.transform.position = footWorldPosition;
                _sparkObject.transform.localScale = Vector3.zero;
                _sparkObject.SetActive(true);

                Sequence seq = DOTween.Sequence();
                seq.Append(_sparkObject.transform.DOScale(Vector3.one * 0.45f, 0.04f).SetEase(Ease.OutQuad));
                seq.Append(_sparkObject.transform.DOScale(Vector3.zero, _flashDuration).SetEase(Ease.InQuad));
                seq.OnComplete(() =>
                {
                    if (_sparkObject != null)
                    {
                        _sparkObject.SetActive(false);
                    }
                });
                _sparkTween = seq;
            }

            if (_visualModel != null)
            {
                _punchTween?.Kill();
                _visualModel.localScale = _initialScale;
                _punchTween = _visualModel.DOPunchScale(Vector3.one * 0.12f, _flashDuration, 4, 0.5f)
                    .OnComplete(() =>
                    {
                        if (_visualModel != null)
                        {
                            _visualModel.localScale = _initialScale;
                        }
                    });
            }
        }
    }
}

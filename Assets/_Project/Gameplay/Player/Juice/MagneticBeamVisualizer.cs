using DG.Tweening;
using Game.Gameplay.Player.Torso;
using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    public sealed class MagneticBeamVisualizer : MonoBehaviour
    {
        [SerializeField] private TorsoController _controller;
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Light _beamLight;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _beamColor = new Color(0.15f, 0.88f, 1f, 1f);
        [SerializeField] private Color _beamCoreColor = new Color(0.9f, 0.98f, 1f, 1f);
        [SerializeField] private float _startWidth = 0.14f;
        [SerializeField] private float _endWidth = 0.05f;
        [SerializeField] private int _segments = 16;
        [SerializeField] private float _waveFrequency = 18f;
        [SerializeField] private float _waveAmplitude = 0.08f;
        [SerializeField] private float _maxLightIntensity = 2.5f;
        [SerializeField] private float _fadeDuration = 0.15f;

        private const int MaxPoints = 25;
        private readonly Vector3[] _pointsBuffer = new Vector3[MaxPoints];

        private Material _runtimeMaterial;
        private Tween _lightTween;
        private bool _wasActive;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<TorsoController>();
            }

            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
            }

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            ConfigureLineRenderer();
            ConfigureBeamLight();
        }

        private void OnEnable()
        {
            if (_controller != null)
            {
                _controller.OnMagnetStateChanged += HandleMagnetStateChanged;
            }
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnMagnetStateChanged -= HandleMagnetStateChanged;
            }

            ClearBeam();
        }

        private void OnDestroy()
        {
            _lightTween?.Kill();

            if (_runtimeMaterial != null)
            {
                Destroy(_runtimeMaterial);
                _runtimeMaterial = null;
            }
        }

        private void ConfigureLineRenderer()
        {
            if (_lineRenderer == null) return;

            _lineRenderer.useWorldSpace = true;
            _lineRenderer.alignment = LineAlignment.View;
            _lineRenderer.startWidth = _startWidth;
            _lineRenderer.endWidth = _endWidth;
            _lineRenderer.numCapVertices = 4;
            _lineRenderer.numCornerVertices = 4;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.positionCount = 0;

            if (_lineMaterial != null)
            {
                _lineRenderer.sharedMaterial = _lineMaterial;
            }
            else
            {
                Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
                if (unlitShader == null) unlitShader = Shader.Find("Hidden/Internal-Colored");

                if (unlitShader != null)
                {
                    _runtimeMaterial = new Material(unlitShader)
                    {
                        name = "Runtime_MagneticBeamMaterial",
                        color = _beamColor
                    };

                    if (_runtimeMaterial.HasProperty("_BaseColor"))
                    {
                        _runtimeMaterial.SetColor("_BaseColor", _beamColor);
                    }

                    _lineRenderer.material = _runtimeMaterial;
                }
            }

            _lineRenderer.startColor = _beamCoreColor;
            _lineRenderer.endColor = _beamColor;
            _lineRenderer.enabled = false;
        }

        private void ConfigureBeamLight()
        {
            if (_beamLight != null)
            {
                _beamLight.color = _beamColor;
                _beamLight.intensity = 0f;
                return;
            }

            Transform parent = _controller != null && _controller.MagnetOrigin != null
                ? _controller.MagnetOrigin
                : transform;

            var lightGo = new GameObject("MagneticBeamLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.localPosition = Vector3.zero;

            _beamLight = lightGo.AddComponent<Light>();
            _beamLight.type = LightType.Point;
            _beamLight.range = 3f;
            _beamLight.color = _beamColor;
            _beamLight.intensity = 0f;
            _beamLight.shadows = LightShadows.None;
        }

        private void HandleMagnetStateChanged(bool active)
        {
            if (active)
            {
                _wasActive = true;
                if (_lineRenderer != null)
                {
                    _lineRenderer.enabled = true;
                }

                if (_beamLight != null)
                {
                    _lightTween?.Kill();
                    _lightTween = _beamLight.DOIntensity(_maxLightIntensity, _fadeDuration).SetEase(Ease.OutQuad);
                }
            }
            else
            {
                ClearBeam();
            }
        }

        private void ClearBeam()
        {
            _wasActive = false;

            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = false;
            }

            if (_beamLight != null)
            {
                _lightTween?.Kill();
                _lightTween = _beamLight.DOIntensity(0f, _fadeDuration).SetEase(Ease.OutQuad);
            }
        }

        private void LateUpdate()
        {
            if (_controller == null)
            {
                return;
            }

            bool isActive = _controller.IsMagnetActive;
            if (!isActive)
            {
                if (_wasActive)
                {
                    ClearBeam();
                }
                return;
            }

            _wasActive = true;
            if (_lineRenderer != null && !_lineRenderer.enabled)
            {
                _lineRenderer.enabled = true;
            }

            RenderBeam();
        }

        private void RenderBeam()
        {
            if (_lineRenderer == null)
            {
                return;
            }

            Vector3 start = _controller.MagnetOrigin != null
                ? _controller.MagnetOrigin.position
                : transform.position;

            Vector3 end = _controller.MagnetBeamEndPoint;
            Vector3 diff = end - start;
            float totalDist = diff.magnitude;

            if (totalDist < 0.05f)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            Vector3 dir = diff / totalDist;
            Vector3 normal = Vector3.Cross(dir, Vector3.up);
            if (normal.sqrMagnitude < 0.001f)
            {
                normal = Vector3.Cross(dir, Vector3.right);
            }
            normal.Normalize();

            int pointCount = Mathf.Clamp(_segments + 1, 2, MaxPoints);
            float time = Time.time * _waveFrequency;

            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / (pointCount - 1);
                Vector3 basePos = Vector3.Lerp(start, end, t);

                // Damp displacement at the endpoints (start and end anchor firmly)
                float envelope = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin(time + (t * 12f)) * _waveAmplitude * envelope;
                float jitter = (Mathf.PingPong(time * 1.5f + (i * 0.7f), 1f) - 0.5f) * _waveAmplitude * 0.5f * envelope;

                _pointsBuffer[i] = basePos + (normal * (wave + jitter));
            }

            _lineRenderer.positionCount = pointCount;
            _lineRenderer.SetPositions(_pointsBuffer);

            if (_beamLight != null)
            {
                _beamLight.transform.position = start + (dir * 0.25f);
            }
        }
    }
}

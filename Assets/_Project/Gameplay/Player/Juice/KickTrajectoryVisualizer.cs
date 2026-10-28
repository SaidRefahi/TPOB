using UnityEngine;

namespace Game.Gameplay.Player.Juice
{
    [DisallowMultipleComponent]
    public sealed class KickTrajectoryVisualizer : MonoBehaviour
    {
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private Material _lineMaterial;
        [SerializeField] private Color _trajectoryColor = new Color(1f, 0.12f, 0.12f, 1f);
        [SerializeField] private float _startWidth = 0.12f;
        [SerializeField] private float _endWidth = 0.05f;
        [SerializeField] private int _resolution = 30;
        [SerializeField] private float _timeStep = 0.045f;

        private const int MaxPoints = 36;
        private readonly Vector3[] _pointsBuffer = new Vector3[MaxPoints];
        private readonly RaycastHit[] _raycastHits = new RaycastHit[6];

        private void Awake()
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
            }

            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            ConfigureLineRenderer();
        }

        private void OnDisable()
        {
            ClearTrajectory();
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
                    _lineMaterial = new Material(unlitShader)
                    {
                        name = "Runtime_KickTrajectoryMaterial",
                        color = _trajectoryColor
                    };
                    if (_lineMaterial.HasProperty("_BaseColor"))
                    {
                        _lineMaterial.SetColor("_BaseColor", _trajectoryColor);
                    }
                    _lineRenderer.material = _lineMaterial;
                }
            }

            _lineRenderer.startColor = _trajectoryColor;
            _lineRenderer.endColor = _trajectoryColor;
            _lineRenderer.enabled = false;
        }

        public void RenderTrajectory(Vector3 startPoint, Vector3 launchVelocity, LayerMask collisionMask, Collider ignoredTarget = null, Collider ignoredSelf = null)
        {
            if (_lineRenderer == null) return;

            int resolution = Mathf.Clamp(_resolution, 4, MaxPoints);
            _pointsBuffer[0] = startPoint;
            int pointCount = 1;
            Vector3 currentPoint = startPoint;
            Vector3 gravity = Physics.gravity;

            for (int i = 1; i < resolution; i++)
            {
                float t = i * _timeStep;
                Vector3 nextPoint = startPoint + (launchVelocity * t) + (0.5f * gravity * (t * t));

                Vector3 segment = nextPoint - currentPoint;
                float sqrDist = segment.sqrMagnitude;

                if (sqrDist > 0.0001f)
                {
                    float dist = Mathf.Sqrt(sqrDist);
                    Vector3 dir = segment / dist;

                    int hitCount = Physics.RaycastNonAlloc(currentPoint, dir, _raycastHits, dist, collisionMask);
                    if (hitCount > 0)
                    {
                        bool foundObstacle = false;
                        for (int h = 0; h < hitCount; h++)
                        {
                            Collider hitCol = _raycastHits[h].collider;
                            if (hitCol == null) continue;

                            // Skip Torso or Legs colliders
                            if (hitCol == ignoredTarget || (ignoredTarget != null && hitCol.transform.IsChildOf(ignoredTarget.transform)))
                            {
                                continue;
                            }
                            if (hitCol == ignoredSelf || (ignoredSelf != null && hitCol.transform.IsChildOf(ignoredSelf.transform)))
                            {
                                continue;
                            }

                            _pointsBuffer[pointCount++] = _raycastHits[h].point;
                            foundObstacle = true;
                            break;
                        }

                        if (foundObstacle)
                        {
                            break;
                        }
                    }
                }

                _pointsBuffer[pointCount++] = nextPoint;
                currentPoint = nextPoint;
            }

            if (pointCount >= 2)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.positionCount = pointCount;
                _lineRenderer.SetPositions(_pointsBuffer);
            }
            else
            {
                ClearTrajectory();
            }
        }

        public void ClearTrajectory()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
                _lineRenderer.enabled = false;
            }
        }
    }
}

using System.Collections.Generic;
using TriInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Gameplay.Interactables
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CatapultMechanism))]
    [DeclareBoxGroup("Configuración de Simulación")]
    [DeclareBoxGroup("Visualización en Scene View")]
    [DeclareBoxGroup("Diagnóstico de Impacto")]
    [DeclareBoxGroup("Calculadora de Potencia")]
    public sealed class CatapultTrajectoryVisualizer : MonoBehaviour
    {
        public enum LaunchForwardSource
        {
            NegativeAnchorForward = 0,
            AnchorForward = 1,
            CatapultForward = 2
        }

        [Group("Configuración de Simulación")]
        [SerializeField] private CatapultMechanism _catapult;

        [Group("Configuración de Simulación")]
        [Tooltip("Dirección frontal hacia donde se dispara la catapulta")]
        [SerializeField] private LaunchForwardSource _forwardSource = LaunchForwardSource.NegativeAnchorForward;

        [Group("Configuración de Simulación")]
        [Range(0f, 1f)]
        [Tooltip("Nivel de tensión interactivo para probar cargas intermedias")]
        [SerializeField] private float _previewTension = 1f;

        [Group("Configuración de Simulación")]
        [Tooltip("Compensación de gravedad en caída (LegsController._fallMultiplier = 2.5f)")]
        [SerializeField] private float _fallMultiplier = 2.5f;

        [Group("Configuración de Simulación")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        [Group("Visualización en Scene View")]
        [SerializeField] private bool _showMaxTensionArc = true;

        [Group("Visualización en Scene View")]
        [SerializeField] private bool _showMinTensionArc = true;

        [Group("Visualización en Scene View")]
        [SerializeField] private bool _showPreviewTensionArc = true;

        [Group("Visualización en Scene View")]
        [SerializeField] private Color _maxArcColor = new Color(0f, 1f, 0.4f, 0.9f);

        [Group("Visualización en Scene View")]
        [SerializeField] private Color _minArcColor = new Color(1f, 0.6f, 0f, 0.75f);

        [Group("Visualización en Scene View")]
        [SerializeField] private Color _previewArcColor = new Color(0f, 0.85f, 1f, 1f);

        [Group("Visualización en Scene View")]
        [SerializeField] private float _impactDiscRadius = 0.8f;

        [Group("Visualización en Scene View")]
        [SerializeField] private bool _showSceneLabels = true;

        [Group("Diagnóstico de Impacto")]
        [ReadOnly] [SerializeField] private float _horizontalDistance;

        [Group("Diagnóstico de Impacto")]
        [ReadOnly] [SerializeField] private float _maxApexHeight;

        [Group("Diagnóstico de Impacto")]
        [ReadOnly] [SerializeField] private float _flightTime;

        [Group("Diagnóstico de Impacto")]
        [ReadOnly] [SerializeField] private string _hitSurfaceName = "Ninguno (Al infinito)";

        [Group("Diagnóstico de Impacto")]
        [ReadOnly] [SerializeField] private Vector3 _landingPosition;

        [Group("Calculadora de Potencia")]
        [Tooltip("Arrastra una plataforma o destino aquí para calcular la potencia necesaria")]
        [SerializeField] private Transform _targetObjective;

        [Group("Calculadora de Potencia")]
        [ReadOnly] [SerializeField] private float _calculatedRequiredSpeed;

        private readonly List<Vector3> _arcPointsBuffer = new(256);

        private void Reset()
        {
            if (_catapult == null)
            {
                _catapult = GetComponent<CatapultMechanism>();
            }
        }

        private void OnValidate()
        {
            if (_catapult == null)
            {
                _catapult = GetComponent<CatapultMechanism>();
            }
        }

#if UNITY_EDITOR
        [Group("Calculadora de Potencia")]
        [Button(ButtonSizes.Medium, "Calcular y Ajustar Velocidad para Objetivo")]
        public void CalculateSpeedForTarget()
        {
            if (_catapult == null) _catapult = GetComponent<CatapultMechanism>();
            if (_catapult == null || _targetObjective == null)
            {
                Debug.LogWarning("[CatapultTrajectoryVisualizer] Asigna el CatapultMechanism y un TargetObjective primero.");
                return;
            }

            Vector3 startPos = GetLaunchOrigin();
            Vector3 targetPos = _targetObjective.position;
            Vector3 forwardDir = GetLaunchForward();

            float elevationRad = Mathf.Clamp(_catapult.LaunchElevationAngle, 10f, 60f) * Mathf.Deg2Rad;
            Vector3 launchDir = (forwardDir + Vector3.up * Mathf.Tan(elevationRad)).normalized;

            // Bisección numérica precisa teniendo en cuenta el multiplicador de caída
            float minSpeed = 5f;
            float maxSpeed = 70f;
            float bestSpeed = minSpeed;
            float minError = float.MaxValue;

            for (float testSpeed = minSpeed; testSpeed <= maxSpeed; testSpeed += 0.25f)
            {
                SimulateTrajectory(startPos, launchDir * testSpeed, out var hitPos, out _, out _, out _, out bool hit);
                float distToTarget = Vector3.Distance(new Vector3(hitPos.x, targetPos.y, hitPos.z), targetPos);
                if (distToTarget < minError)
                {
                    minError = distToTarget;
                    bestSpeed = testSpeed;
                }
            }

            _calculatedRequiredSpeed = bestSpeed;

            SerializedObject so = new SerializedObject(_catapult);
            var maxSpeedProp = so.FindProperty("_maxLaunchSpeed");
            if (maxSpeedProp != null)
            {
                maxSpeedProp.floatValue = Mathf.Round(bestSpeed * 10f) / 10f;
                so.ApplyModifiedProperties();
                Debug.Log($"<color=green>[CatapultTrajectoryVisualizer] Velocidad calculada: {bestSpeed:F1} m/s asignada a MaxLaunchSpeed en {_catapult.name}!</color>");
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawTrajectoryGizmos();
        }

        private void DrawTrajectoryGizmos()
        {
            if (_catapult == null)
            {
                _catapult = GetComponent<CatapultMechanism>();
                if (_catapult == null) return;
            }

            Vector3 startPos = GetLaunchOrigin();
            Vector3 forwardDir = GetLaunchForward();
            float elevationRad = Mathf.Clamp(_catapult.LaunchElevationAngle, 10f, 60f) * Mathf.Deg2Rad;
            Vector3 launchDir = (forwardDir + Vector3.up * Mathf.Tan(elevationRad)).normalized;

            // 1. Arco de Mínima Tensión
            if (_showMinTensionArc)
            {
                float minSpeed = _catapult.MinLaunchSpeed;
                SimulateTrajectory(startPos, launchDir * minSpeed, out var minHitPos, out var minHitNormal, out _, out _, out bool minHit);
                DrawArcGizmo(_arcPointsBuffer, _minArcColor, minHitPos, minHitNormal, minHit, "Mín (0%)");
            }

            // 2. Arco de Máxima Tensión (100%)
            if (_showMaxTensionArc)
            {
                float maxSpeed = _catapult.MaxLaunchSpeed;
                SimulateTrajectory(startPos, launchDir * maxSpeed, out var maxHitPos, out var maxHitNormal, out float maxHoriz, out float maxApex, out bool maxHit);
                DrawArcGizmo(_arcPointsBuffer, _maxArcColor, maxHitPos, maxHitNormal, maxHit, "Máx (100%)");

                if (Mathf.Abs(_previewTension - 1f) < 0.01f)
                {
                    UpdateDiagnostics(maxHitPos, maxHitNormal, maxHoriz, maxApex, _arcPointsBuffer.Count * 0.02f, maxHit);
                }
            }

            // 3. Arco Interactivo (Scrubbable Preview Tension)
            if (_showPreviewTensionArc && Mathf.Abs(_previewTension - 1f) >= 0.01f && Mathf.Abs(_previewTension) >= 0.01f)
            {
                float previewSpeed = Mathf.Lerp(_catapult.MinLaunchSpeed, _catapult.MaxLaunchSpeed, _previewTension);
                SimulateTrajectory(startPos, launchDir * previewSpeed, out var prevHitPos, out var prevHitNormal, out float prevHoriz, out float prevApex, out bool prevHit);
                DrawArcGizmo(_arcPointsBuffer, _previewArcColor, prevHitPos, prevHitNormal, prevHit, $"Preview ({_previewTension * 100f:F0}%)");
                UpdateDiagnostics(prevHitPos, prevHitNormal, prevHoriz, prevApex, _arcPointsBuffer.Count * 0.02f, prevHit);
            }
        }

        private Vector3 GetLaunchOrigin()
        {
            Transform anchor = _catapult.AnchorPoint != null ? _catapult.AnchorPoint : transform;
            return anchor.position + Vector3.up * 0.5f;
        }

        private Vector3 GetLaunchForward()
        {
            Transform anchor = _catapult.AnchorPoint != null ? _catapult.AnchorPoint : transform;
            Vector3 fwd;

            switch (_forwardSource)
            {
                case LaunchForwardSource.AnchorForward:
                    fwd = anchor.forward;
                    break;
                case LaunchForwardSource.CatapultForward:
                    fwd = transform.forward;
                    break;
                case LaunchForwardSource.NegativeAnchorForward:
                default:
                    fwd = -anchor.forward;
                    break;
            }

            fwd.y = 0f;
            return fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
        }

        private void SimulateTrajectory(Vector3 startPoint, Vector3 launchVelocity, out Vector3 hitPoint, out Vector3 hitNormal, out float horizDist, out float apexHeight, out bool hasHit)
        {
            _arcPointsBuffer.Clear();
            _arcPointsBuffer.Add(startPoint);

            float dt = 0.02f;
            int maxSteps = 250; // 5 segundos max
            Vector3 currentPos = startPoint;
            Vector3 currentVel = launchVelocity;
            float peakY = startPoint.y;

            hitPoint = startPoint;
            hitNormal = Vector3.up;
            hasHit = false;

            for (int i = 0; i < maxSteps; i++)
            {
                float grav = currentVel.y < 0f ? Physics.gravity.y * _fallMultiplier : Physics.gravity.y;
                Vector3 stepVel = currentVel + Vector3.up * (grav * dt);
                Vector3 nextPos = currentPos + (currentVel + stepVel) * 0.5f * dt;
                currentVel = stepVel;

                if (nextPos.y > peakY)
                {
                    peakY = nextPos.y;
                }

                Vector3 delta = nextPos - currentPos;
                float segLen = delta.magnitude;

                if (segLen > 0.001f)
                {
                    if (Physics.SphereCast(currentPos, 0.35f, delta / segLen, out RaycastHit hit, segLen, _collisionMask))
                    {
                        hitPoint = hit.point;
                        hitNormal = hit.normal;
                        hasHit = true;
                        _arcPointsBuffer.Add(hitPoint);
                        _hitSurfaceName = hit.collider != null ? hit.collider.gameObject.name : "Superficie";
                        break;
                    }
                }

                currentPos = nextPos;
                _arcPointsBuffer.Add(currentPos);
            }

            if (!hasHit)
            {
                hitPoint = currentPos;
                _hitSurfaceName = "Ninguno (Al infinito)";
            }

            Vector3 horizDelta = new Vector3(hitPoint.x - startPoint.x, 0f, hitPoint.z - startPoint.z);
            horizDist = horizDelta.magnitude;
            apexHeight = peakY - startPoint.y;
        }

        private void DrawArcGizmo(List<Vector3> points, Color color, Vector3 hitPos, Vector3 hitNormal, bool hasHit, string tag)
        {
            if (points == null || points.Count < 2) return;

            Handles.color = color;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Handles.DrawLine(points[i], points[i + 1]);
            }

            // Diana de Aterrizaje
            if (hasHit)
            {
                Handles.color = color;
                Handles.DrawWireDisc(hitPos, hitNormal, _impactDiscRadius);
                Handles.DrawWireDisc(hitPos, hitNormal, _impactDiscRadius * 0.5f);
                Handles.DrawSolidDisc(hitPos, hitNormal, 0.12f);

                if (_showSceneLabels)
                {
                    Vector3 labelPos = hitPos + hitNormal * 0.4f + Vector3.up * 0.3f;
                    GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = color },
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft
                    };

                    float dist = Vector3.Distance(new Vector3(hitPos.x, 0f, hitPos.z), new Vector3(points[0].x, 0f, points[0].z));
                    Handles.Label(labelPos, $"🎯 {tag}: {dist:F1}m\n[{_hitSurfaceName}]", style);
                }
            }
        }

        private void UpdateDiagnostics(Vector3 hitPos, Vector3 hitNormal, float horiz, float apex, float time, bool hasHit)
        {
            _landingPosition = hitPos;
            _horizontalDistance = horiz;
            _maxApexHeight = apex;
            _flightTime = time;
        }
#endif
    }
}

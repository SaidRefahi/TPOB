using System.Runtime.CompilerServices;
using UnityEngine;

namespace Game.Gameplay.Player.Legs
{
    /// <summary>
    /// Pure mathematical utility for evaluating cubic Bezier curves and tangents with zero allocations.
    /// Used to simulate flexible metallic hoses / quadruped legs.
    /// </summary>
    public static class BezierSplineHose
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 EvaluatePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            return (uuu * p0) + (3f * uu * t * p1) + (3f * u * tt * p2) + (ttt * p3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 EvaluateTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 tangent = (3f * uu * (p1 - p0)) + (6f * u * t * (p2 - p1)) + (3f * tt * (p3 - p2));
            if (tangent.sqrMagnitude < 0.0001f)
            {
                Vector3 fallback = p3 - p0;
                return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.down;
            }

            return tangent.normalized;
        }

        /// <summary>
        /// Evaluates a point along the curve and computes an orientation following the curve tangent.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EvaluateTransform(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
            float t, Vector3 upReference,
            out Vector3 position, out Quaternion rotation)
        {
            position = EvaluatePoint(p0, p1, p2, p3, t);
            Vector3 tangent = EvaluateTangent(p0, p1, p2, p3, t);

            // Compute orthogonal rotation along the curve tangent
            if (upReference.sqrMagnitude < 0.0001f)
            {
                upReference = Vector3.up;
            }

            // If tangent is almost parallel to upReference, choose fallback perpendicular
            if (Mathf.Abs(Vector3.Dot(tangent, upReference)) > 0.98f)
            {
                upReference = Vector3.forward;
            }

            rotation = Quaternion.LookRotation(tangent, upReference);
        }
    }
}

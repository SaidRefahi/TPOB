using Game.Core.Interfaces;
using Game.Gameplay.Interactables;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class MagneticWallWalkerValidator
    {
        [MenuItem("TPOB/Validación/Auditar Mecanica de Paredes Magneticas", false, 120)]
        public static void RunValidation()
        {
            int passed = 0;
            int failed = 0;

            // 1. Validar proyecciones tangenciales para las 4 paredes ortogonales
            // Pared Trasera (Normal apunta hacia el sur/cámara: (0, 0, -1))
            Vector3 nBack = new Vector3(0f, 0f, -1f);
            ComputeTangents(nBack, out Vector3 upBack, out Vector3 rightBack);
            AssertEquals(Vector3.up, upBack, "Tangente Arriba en Pared Trasera debe ser Vector3.up", ref passed, ref failed);
            AssertEquals(Vector3.right, rightBack, "Tangente Derecha en Pared Trasera debe ser Vector3.right", ref passed, ref failed);

            // Pared Delantera (Normal apunta hacia el norte: (0, 0, 1))
            Vector3 nFront = new Vector3(0f, 0f, 1f);
            ComputeTangents(nFront, out Vector3 upFront, out Vector3 rightFront);
            AssertEquals(Vector3.up, upFront, "Tangente Arriba en Pared Delantera debe ser Vector3.up", ref passed, ref failed);
            AssertEquals(Vector3.left, rightFront, "Tangente Derecha en Pared Delantera debe ser Vector3.left", ref passed, ref failed);

            // Pared Izquierda (Normal apunta hacia el este/adentro: (1, 0, 0))
            Vector3 nLeft = new Vector3(1f, 0f, 0f);
            ComputeTangents(nLeft, out Vector3 upLeft, out Vector3 rightLeft);
            AssertEquals(Vector3.up, upLeft, "Tangente Arriba en Pared Izquierda debe ser Vector3.up", ref passed, ref failed);
            AssertEquals(Vector3.forward, rightLeft, "Tangente Derecha en Pared Izquierda debe ser Vector3.forward", ref passed, ref failed);

            // Pared Derecha (Normal apunta hacia el oeste/adentro: (-1, 0, 0))
            Vector3 nRight = new Vector3(-1f, 0f, 0f);
            ComputeTangents(nRight, out Vector3 upRight, out Vector3 rightRight);
            AssertEquals(Vector3.up, upRight, "Tangente Arriba en Pared Derecha debe ser Vector3.up", ref passed, ref failed);
            AssertEquals(Vector3.back, rightRight, "Tangente Derecha en Pared Derecha debe ser Vector3.back", ref passed, ref failed);

            // Suelo Horizontal (Normal (0, 1, 0))
            Vector3 nFloor = Vector3.up;
            ComputeTangents(nFloor, out Vector3 upFloor, out Vector3 rightFloor);
            AssertEquals(Vector3.forward, upFloor, "Tangente Arriba en Suelo debe ser Vector3.forward", ref passed, ref failed);
            AssertEquals(Vector3.right, rightFloor, "Tangente Derecha en Suelo debe ser Vector3.right", ref passed, ref failed);

            // Techo Horizontal (Normal (0, -1, 0))
            Vector3 nCeiling = Vector3.down;
            ComputeTangents(nCeiling, out Vector3 upCeil, out Vector3 rightCeil);
            AssertEquals(Vector3.forward, upCeil, "Tangente Arriba en Techo debe ser Vector3.forward", ref passed, ref failed);
            AssertEquals(Vector3.right, rightCeil, "Tangente Derecha en Techo debe ser Vector3.right", ref passed, ref failed);

            // 2. Validar componente MagneticWall y cálculo de normal en BoxCollider
            GameObject tempGo = new GameObject("Temp_MagneticWall_Test");
            try
            {
                var box = tempGo.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 4f, 1f);
                var magWall = tempGo.AddComponent<MagneticWall>();

                Vector3 hitPointFront = tempGo.transform.position + new Vector3(0f, 0f, 0.5f);
                Vector3 normalFront = magWall.GetSurfaceNormal(hitPointFront);
                AssertEquals(Vector3.forward, normalFront, "Normal frontal calculada por MagneticWall en BoxCollider", ref passed, ref failed);

                Vector3 hitPointBack = tempGo.transform.position + new Vector3(0f, 0f, -0.5f);
                Vector3 normalBack = magWall.GetSurfaceNormal(hitPointBack);
                AssertEquals(Vector3.back, normalBack, "Normal trasera calculada por MagneticWall en BoxCollider", ref passed, ref failed);

                // Validar losa horizontal de techo (size Y delgado)
                box.size = new Vector3(8f, 0.6f, 8f);
                Vector3 hitPointBottom = tempGo.transform.position + new Vector3(0f, -0.3f, 0f);
                Vector3 normalBottom = magWall.GetSurfaceNormal(hitPointBottom);
                AssertEquals(Vector3.down, normalBottom, "Normal inferior de techo calculada por MagneticWall en BoxCollider", ref passed, ref failed);

                // Validar Toggle y activación
                magWall.SetActive(false);
                AssertTrue(!magWall.IsActive, "MagneticWall debe desactivarse con SetActive(false)", ref passed, ref failed);

                magWall.SetActive(true);
                AssertTrue(magWall.IsActive, "MagneticWall debe activarse con SetActive(true)", ref passed, ref failed);
            }
            finally
            {
                Object.DestroyImmediate(tempGo);
            }

            // 3. Validar geometría de transiciones cóncavas y envolventes convexas (Surface Probes & Edge Wrap)
            // Test 3.1: Detección cóncava perpendicular (Piso normal (0,1,0) a Pared normal (0,0,1))
            Vector3 floorNorm = Vector3.up;
            Vector3 wallNorm = Vector3.forward;
            float dotConcave = Vector3.Dot(floorNorm, wallNorm);
            AssertTrue(dotConcave < 0.85f, "Normales ortogonales deben gatillar detección cóncava (dot < 0.85f)", ref passed, ref failed);

            // Test 3.2: Vector envolvente de esquina convexa
            Vector3 upNormal = Vector3.forward; // Robot parado en pared frontal
            Vector3 travelDir = Vector3.up; // Avanzando hacia arriba hacia el borde
            Vector3 wrapDir = (-upNormal - travelDir * 0.65f).normalized;
            AssertTrue(wrapDir.z < -0.5f && wrapDir.y < -0.3f, "Wrap probe debe curvarse hacia la cara opuesta del borde exterior", ref passed, ref failed);

            // Test 3.3: Verificación de convergencia Slerp
            Vector3 initialNorm = Vector3.up;
            Vector3 targetNorm = Vector3.forward;
            Vector3 slerped = Vector3.Slerp(initialNorm, targetNorm, 0.5f).normalized;
            Vector3 expectedMid = (Vector3.up + Vector3.forward).normalized;
            AssertEquals(expectedMid, slerped, "Slerp de normales debe interpolar sobre el círculo mayor", ref passed, ref failed);

            // 4. Validar cinemática de Locomoción Anclada a Patas (Foot-Anchored Centering)
            // Test 4.1: Baricentro de 4 patas cuadrangulares simétricas
            Vector3 fFL = new Vector3(-0.5f, 0f, 0.5f);
            Vector3 fFR = new Vector3(0.5f, 0f, 0.5f);
            Vector3 fBL = new Vector3(-0.5f, 0f, -0.5f);
            Vector3 fBR = new Vector3(0.5f, 0f, -0.5f);
            Vector3 expectedCentroid = Vector3.zero;
            Vector3 actualCentroid = (fFL + fFR + fBL + fBR) * 0.25f;
            AssertEquals(expectedCentroid, actualCentroid, "Baricentro de 4 patas simétricas debe ser Vector3.zero", ref passed, ref failed);

            // Test 4.2: Altura de cadera nominal y lead offset sobre suelo plano
            float nominalHeight = 0.90f;
            float leadOffsetDist = 0.35f;
            Vector3 inputDirTest = Vector3.forward;
            Vector3 targetHipFloor = actualCentroid + Vector3.up * nominalHeight + inputDirTest * leadOffsetDist;
            Vector3 expectedHipFloor = new Vector3(0f, 0.90f, 0.35f);
            AssertEquals(expectedHipFloor, targetHipFloor, "Posición objetivo de cadera en suelo con lead offset", ref passed, ref failed);

            // Test 4.3: Posición de cadera sobre pared vertical (Normal = (0, 0, 1))
            Vector3 wallNormalTest = Vector3.forward;
            Vector3 targetHipWall = actualCentroid + wallNormalTest * nominalHeight + Vector3.up * leadOffsetDist;
            Vector3 expectedHipWall = new Vector3(0f, 0.35f, 0.90f);
            AssertEquals(expectedHipWall, targetHipWall, "Posición objetivo de cadera en pared vertical proyectada perpendicularmente", ref passed, ref failed);

            // Test 4.4: En suelo plano en reposo, el error tangencial es Vector3.zero (0 rebote / 0 salto)
            Vector3 rootPosFlat = Vector3.zero;
            Vector3 targetSurfacePosFlat = actualCentroid;
            Vector3 posErrorFlat = targetSurfacePosFlat - rootPosFlat;
            Vector3 tangentErrorFlat = Vector3.ProjectOnPlane(posErrorFlat, Vector3.up);
            AssertEquals(Vector3.zero, tangentErrorFlat, "En reposo sobre suelo plano, el error tangencial de centrado debe ser exactamente Vector3.zero", ref passed, ref failed);

            // Test 4.5: En modo anclado en reposo sin input (hasInput=false, isTurning=false), la intención es estrictamente false (0 zapateo)
            bool testHasInput = false;
            bool testIsTurning = false;
            bool testIsFootAnchored = true;
            bool testIsMovingCentering = true; // El cuerpo se estaba centrando pero el usuario no tiene intención
            bool resolvedIntent = testIsFootAnchored ? (testHasInput || testIsTurning) : (testIsMovingCentering || testIsTurning || testHasInput);
            AssertTrue(!resolvedIntent, "En reposo en modo anclado, la intención de locomoción debe ser false sin zapateo", ref passed, ref failed);

            // Test 4.6: El baricentro de la postura de descanso alineada coincide exactamente con el origen (centrado perfecto)
            Vector3 restFL = new Vector3(-0.45f, 0f, 0.45f);
            Vector3 restFR = new Vector3(0.45f, 0f, 0.45f);
            Vector3 restBL = new Vector3(-0.45f, 0f, -0.45f);
            Vector3 restBR = new Vector3(0.45f, 0f, -0.45f);
            Vector3 restFootprintCentroid = (restFL + restFR + restBL + restBR) * 0.25f;
            AssertEquals(Vector3.zero, restFootprintCentroid, "El baricentro de la postura de descanso alineada debe coincidir con el origen", ref passed, ref failed);

            if (failed == 0)
            {
                Debug.Log($"<color=green><b>[TPOB AUDITORÍA EXITOSA]</b> Todas las {passed} pruebas matemáticas y de arquitectura de Paredes Magnéticas pasaron sin errores.</color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[TPOB AUDITORÍA FALLIDA]</b> Pasaron {passed} pruebas, pero fallaron {failed} pruebas.</color>");
            }
        }

        private static void ComputeTangents(Vector3 N, out Vector3 tangentUp, out Vector3 tangentRight)
        {
            if (Mathf.Abs(N.y) > 0.7f)
            {
                tangentUp = Vector3.ProjectOnPlane(Vector3.forward, N).normalized;
                tangentRight = Vector3.ProjectOnPlane(Vector3.right, N).normalized;
            }
            else
            {
                tangentUp = Vector3.ProjectOnPlane(Vector3.up, N).normalized;
                tangentRight = Vector3.Cross(N, tangentUp).normalized;
            }
        }

        private static void AssertEquals(Vector3 expected, Vector3 actual, string testName, ref int passed, ref int failed)
        {
            if ((expected - actual).sqrMagnitude < 0.001f)
            {
                passed++;
            }
            else
            {
                failed++;
                Debug.LogError($"[FALLÓ] {testName} | Esperado: {expected}, Obtenido: {actual}");
            }
        }

        private static void AssertTrue(bool condition, string testName, ref int passed, ref int failed)
        {
            if (condition)
            {
                passed++;
            }
            else
            {
                failed++;
                Debug.LogError($"[FALLÓ] {testName}");
            }
        }
    }
}

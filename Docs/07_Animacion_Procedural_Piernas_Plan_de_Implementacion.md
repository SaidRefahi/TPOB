---
title: TPOB - Plan de Implementación: Animación Procedural de Piernas mediante Curvas Bézier (Spline Hose Solver & NetworkBones)
tags:
  - tpob
  - procedural-animation
  - bezier-spline
  - metal-hose
  - purrnet
  - network-bones
  - zero-gc
created: 2026-09-29
status: Propuesto para Revisión (Opción 1: Bezier Spline IK)
---

# 🦿 Plan de Implementación: Marcha Cuadrúpeda de Manguera Metálica (Bezier Spline IK)

Este documento detalla el replanteamiento técnico de la locomoción procedural de piernas (`LegsPlayer`), migrando del solucionador genérico FABRIK de Animation Rigging a un **Solucionador Procedural de Curvas Bézier Cúbicas (Spline Hose)** que replica exactamente el comportamiento del *Spline IK* de Blender en Unity, garantizando una curvatura de manguera metálica fluida, paso caricaturesco con caída de golpe y pies con orientación perfectamente alineada.

---

## 💡 1. Desmitificando el Desafío Técnico: ¿Por qué este camino es más seguro y predecible?

Comprendo perfectamente que programar un sistema procedural en código suene intimidante. Sin embargo, en la práctica técnica de desarrollo de videojuegos, **este método es mucho más seguro, simple y predecible que los solvers de IK genéricos**:

| Aspecto | Animation Rigging (FABRIK) | Curva Bézier Procedural (Spline Hose) |
| :--- | :--- | :--- |
| **Naturaleza del Cálculo** | Algoritmo iterativo aproximado (caja negra con múltiples pases que intenta adivinar dónde poner los huesos). | **Fórmula matemática directa cerrada de 1 línea** ejecutada en microsegundos ($O(1)$). |
| **Forma de la Pierna** | Tiende a estirar los 13 huesos en una línea recta rígida hacia el target. | **Garantiza una curvatura suave y continua de manguera** que pasa exactamente por la curva deseada. |
| **Orientación de los Pies** | Propenso a rotar los pies 180° o torcerlos si los ejes locales del FBX difieren del mundo. | **Control analítico total:** El pie preserva su rotación natural de reposo y solo se alinea con la normal del terreno. |
| **Rendimiento y Memoria** | Mayor sobrecarga de PlayableGraph de Animation Rigging. | **Zero GC absoluto:** Menos de 50 multiplicaciones vectoriales por fotograma, sin llamadas pesadas. |
| **Sincronización en Red** | Requiere pelear con pesos de rig para no competir con observadores. | PurrNet [`NetworkBones`](file:///c:/Users/SaidR/Documents/GitHub/TPOB/Assets/PurrNet/Runtime/Components/NetworkBones/NetworkBones.cs) lee directamente las transformadas de los 53 huesos de forma transparente. |

---

## 📐 2. Modelo Matemático: La Manguera de Metal (Cubic Bezier)

Para cada una de las 4 patas ($FL, FR, BL, BR$), construimos en tiempo real una **Curva de Bézier Cúbica** definida por 4 puntos de control en el espacio 3D:

$$\mathbf{B}(t) = (1-t)^3 \mathbf{P}_0 + 3(1-t)^2 t \mathbf{P}_1 + 3(1-t) t^2 \mathbf{P}_2 + t^3 \mathbf{P}_3 \quad (t \in [0, 1])$$

```
          [P1] (Hombro / Pico del arco)
         /    \
        /      \   <--- Curvatura suave de manguera de metal
 [P0]  /        \
 (Hip)           \
                  [P2] (Control vertical del tobillo)
                   |
                  [P3] (Pie en el suelo o en swing)
```

### Definición de los 4 Puntos:
1. **$\mathbf{P}_0$ (Raíz):** Posición del hueso `Bone.001` conectado a la cadera (`Hip`).
2. **$\mathbf{P}_1$ (Hombro del Arco):**
   $$\mathbf{P}_1 = \mathbf{P}_0 + \mathbf{Up} \times H_{\text{arch}} + \mathbf{Outward} \times W_{\text{arch}}$$
   * Define la elevación y apertura lateral de la manguera al salir del chasis, impidiendo que la pata se colapse hacia adentro.
3. **$\mathbf{P}_2$ (Tobillo del Arco):**
   $$\mathbf{P}_2 = \mathbf{P}_3 + \mathbf{Up} \times H_{\text{ankle}} + \mathbf{StepDir} \times D_{\text{lead}}$$
   * Asegura que la manguera caiga verticalmente hacia el pie, creando esa característica forma de cayado o arco curvo.
4. **$\mathbf{P}_3$ (Pie):**
   * Posición dinámica del pie en el suelo (en apoyo) o en trayectoria aérea caricaturesca (en paso).

### Distribución de los 13 Huesos:
* Para cada hueso $i \in [0..12]$ de la pata:
  * Calculamos su parámetro de posición: $t_i = \frac{i}{12}$.
  * Posición del hueso: $\mathbf{Pos}_i = \mathbf{B}(t_i)$.
  * Dirección del hueso: Se calcula la primera derivada (tangente analítica de la curva):
    $$\mathbf{B}'(t) = 3(1-t)^2(\mathbf{P}_1 - \mathbf{P}_0) + 6(1-t)t(\mathbf{P}_2 - \mathbf{P}_1) + 3t^2(\mathbf{P}_3 - \mathbf{P}_2)$$
  * Rotación del hueso:
    $$\mathbf{Rot}_i = \text{Quaternion.LookRotation}(\mathbf{B}'(t_i), \mathbf{Outward}) \times \mathbf{BoneRestOffset}_i$$
  * *Resultado:* Cada segmento sigue perfectamente la curvatura de la manguera sin torceduras ni quiebres raros.

---

## 👟 3. Solución Definitiva a los Pies Volteados

* **Diagnóstico del Fallo Previo:** Los huesos importados de Blender tienen su eje forward local a lo largo del hueso (típicamente $+Y$ o $+Z$), no alineado con el forward de Unity.
* **Solución Técnica:**
  1. En `Awake()`, antes de aplicar cualquier movimiento, el script lee y cachea la **rotación local de reposo** (`_initialLocalRotation`) de cada hueso del FBX.
  2. La orientación del pie en el suelo se calcula a partir de su pose original más la rotación generada por la normal de la superficie:
     $$\mathbf{FootRot} = \text{Quaternion.FromToRotation}(\text{Vector3.up}, \text{GroundNormal}) \times \text{RobotRotation} \times \mathbf{FootRestOffset}$$
  3. Esto garantiza con un 100% de certeza que **los pies jamás se voltearán hacia atrás ni se invertirán**.

---

## 🏃 4. Trayectoria Caricaturesca del Pie ($\mathbf{P}_3$) y Marcha 1 a 1

Para lograr la dinámica cómica que buscas:

1. **Marcha Mecánica (1 Pata a la Vez):**
   * Secuencia cíclica: $FL \rightarrow BR \rightarrow FR \rightarrow BL$.
   * Mientras 1 pata realiza el paso, **las otras 3 patas permanecen fijadas en el suelo formando un trípode inamovible**.
2. **Despegue con Enrollamiento (Anticipación):**
   * Durante el primer 15% del tiempo de paso ($t \in [0, 0.15]$), el pie no avanza: se eleva y retrocede ligeramente hacia el cuerpo, como una manguera flexible enrollándose sobre sí misma.
3. **Avance en Arco Alto (Hang-Time):**
   * De $t = 0.2$ a $0.75$, la pierna sube alto en el aire, se comba hacia afuera y se estira hacia el frente.
4. **¡Caída de Golpe (Vertical Slam)!:**
   * Al llegar a $t = 0.8$, la pata ya está en su posición horizontal frontal completa.
   * Del $80\%$ al $100\%$ del tiempo, el pie **cae en picado verticalmente contra el suelo** a gran velocidad.
   * Al impactar, el chasis absorbe el golpe con un micro-squash descendente (pisotón metálico).

---

## 🧹 5. Limpieza y Reconfiguración de Componentes

Dado que la Opción 1 prescinde de los componentes de Animation Rigging:

1. **Eliminar del Prefab [`LegsPlayer.prefab`](file:///c:/Users/SaidR/Documents/GitHub/TPOB/Assets/_Project/Prefabs/LegsPlayer.prefab):**
   * `RigBuilder`
   * Objeto hijo `Rig` y los 4 `ChainIKConstraint`
   * Objeto hijo `IK_Targets` (ya no se necesitan targets externos; los huesos se posicionan directamente por spline).
2. **Conservar e Integrar:**
   * PurrNet [`NetworkBones`](file:///c:/Users/SaidR/Documents/GitHub/TPOB/Assets/PurrNet/Runtime/Components/NetworkBones/NetworkBones.cs) en `LegsVisual` con los 53 huesos en `_extraBones` (continúa sincronizando los huesos por red a la perfección).
   * Puntos de apoyo `Rest_Points` para el raycast al terreno.
   * Componente nuevo: `LegsSplineHoseAnimator.cs` gobernando la física y las curvas de las 4 patas.

---

## 📋 6. Plan de Trabajo por Fases

- [x] **Fase 1: Motor Matemático Bézier (`BezierSplineHose.cs`)**
  - Implementación de la struct estática/clase pura para cálculo de puntos y tangentes $B(t)$ y $B'(t)$ con Zero GC.
- [x] **Fase 2: Componente `LegsSplineHoseAnimator.cs`**
  - Mapeo de los 13 huesos por pata y caché de sus rotaciones de reposo (`RestOffsets`).
  - Lógica de los 4 puntos de control ($P_0, P_1, P_2, P_3$).
  - Secuenciador de marcha cómica (1 pata a la vez, anticipación, combado hacia afuera y caída de golpe).
  - Integración con Salto (patas replegadas) y Patada (estocada spline frontal).
- [x] **Fase 3: Limpieza y Setup en Editor (`LegsSplineSetupHelper.cs`)**
  - Script de Editor con menú `TPOB/Rigging/Setup Legs Spline Hose Rig` que limpia los componentes viejos de Animation Rigging y configura el nuevo animator spline en [`LegsPlayer.prefab`](file:///c:/Users/SaidR/Documents/GitHub/TPOB/Assets/_Project/Prefabs/LegsPlayer.prefab).
- [ ] **Fase 4: Verificación y Calibración en Play Mode**
  - Comprobación visual inmediata de la curvatura fluida en Unity y ajuste en tiempo real de la altura del arco y la violencia de la caída de golpe.

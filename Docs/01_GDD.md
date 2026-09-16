---
title: Two Pilots, One Robot - Game Design Document (GDD)
tags:
  - gdd
  - mechanics
  - design
  - tpob
created: 2026-09-16
updated: 2026-09-16
---

# 🎮 Game Design Document (GDD): Two Pilots, One Robot (TPOB)

## 📌 1. Visión General del Proyecto

* **Título:** Two Pilots, One Robot (TPOB)
* **Género:** Cooperativo Asimétrico de Puzzles basado en Física
* **Plataforma:** PC (Steam / LAN / Red Privada)
* **Jugadores:** 2 Jugadores (Co-op estricto)
* **Cámara:** 2.5D / 3D con perspectiva isométrica dinámica gestionada por [[02_Arquitectura#cinemachine-3x|Cinemachine 3.x]]
* **Premisa Central:** Dos jugadores controlan partes distintas de un mismo cuerpo mecánico. Deben alternar constantemente entre operar como dos módulos separados independientes o fusionarse en un único coloso bípedo para superar desafíos de peso, alcance y sincronización.

---

## 👥 2. Roles de Jugador y Capacidades

```
                       ┌──────────────────────────────┐
                       │      ROBOT (FUSIONADO)       │
                       └──────────────┬───────────────┘
                                      │
              ┌───────────────────────┴───────────────────────┐
              ▼                                               ▼
┌───────────────────────────┐                   ┌───────────────────────────┐
│   JUGADOR 1: PIERNAS      │                   │    JUGADOR 2: TORSO       │
│  (Locomoción y Fuerza)    │                   │(Manipulación y Precisión) │
├───────────────────────────┤                   ├───────────────────────────┤
│ • Caminar / Correr        │                   │ • Agarre y Posicionado    │
│ • Salto normal y cargado  │                   │ • Lanzamiento balístico   │
│ • Patada física contundente│                  │ • Rayo Magnético          │
│ • Empujar objetos pesados │                   │ • Trepar rejas/andamios   │
│ • Anclaje al suelo        │                   │ • Accionar palancas       │
│ • Servir de contrapeso    │                   │ • Sujetar mecanismos      │
└───────────────────────────┘                   └───────────────────────────┘
```

### 🦿 Jugador 1: Piernas (Locomoción y Tracción)
* **Función:** Es el motor físico del equipo. Administra la masa, la inercia, la velocidad horizontal/vertical y el impacto contundente.
* **Habilidades Clave:**
  1. **Locomoción (Caminar/Correr):** Movimiento analógico omnidireccional con aceleración y desaceleración basadas en fricción física.
  2. **Salto (Impulso Vertical):** Permite superar desniveles. Al estar fusionado, el salto eleva el peso conjunto de ambos jugadores.
  3. **Patada Frontal:** Impulso cinético de alta energía que desplaza objetos móviles ([[02_Arquitectura#patrón-command|KickCommand]]), activa botones de impacto y destruye obstáculos estructuralmente débiles.
  4. **Empujar/Tirar Cajas Pesadas:** Capacidad de empujar bloques y maquinaria con gran masa que Torso no puede desplazar solo.
  5. **Anclaje al Suelo (Lock):** Bloquea las extremidades inferiores contra el pavimento para resistir fuerzas de retroceso, viento o la atracción del imán de Torso.
  6. **Contrapeso:** Su masa actúa como peso estabilizador en plataformas basculantes y balancines.

### 🦾 Jugador 2: Torso, Brazos y Cabeza (Manipulación y Precisión)
* **Función:** Es la herramienta de interacción fina, visión y alcance del equipo.
* **Habilidades Clave:**
  1. **Apuntar y Orientar:** Rotación independiente de 360° para la parte superior del cuerpo.
  2. **Agarrar y Colocar:** Sujeta objetos físicos interactivos (`IGrabbable`), llaves, baterías o cubos y los deposita en sockets o receptáculos con precisión milimétrica.
  3. **Lanzamiento con Arco Parabólico:** Proyecta objetos en trayectorias balísticas calculadas (incluyendo al propio Torso si es catapultado por Piernas).
  4. **Atracción Magnética:** Emite un pulso/haz electromagnético que atrae objetos metálicos ligeros (`IMagnetic`) desde distancias inalcanzables.
  5. **Trepar:** Se adhiere a mallas, escalerillas y vigas metálicas para alcanzar interruptores altos a los que Piernas no puede acceder.
  6. **Operar Palancas y Manivelas:** Gira y sostiene válvulas de flujo continuo mientras Piernas aprovecha la ventana de tiempo.
  7. **Mantener en Posición:** Mantiene compuertas mecánicas abiertas mientras se agota su resistencia o Piernas cruza.

---

## 🔄 3. Estados del Robot: Fusión y Separación

El núcleo estratégico del juego reside en saber **cuándo permanecer unidos** y **cuándo dividirse**:

| Estado | Características Mecánicas | Ventajas Tácticas | Desventajas / Riesgos |
| :--- | :--- | :--- | :--- |
| **Separados** | Dos entidades físicas independientes con su propio `NetworkRigidbody`. Piernas tiene libre movimiento pero no manipula; Torso tiene movilidad limitada (rodar/arrastrarse/trepar) pero gran alcance interactivo. | • Cubrir dos zonas simultáneas de la sala.<br>• Torso puede cruzar conductos estrechos.<br>• Presionar botones lejanos a la vez. | • Torso es vulnerable y lento en el suelo.<br>• Piernas no puede activar mecanismos altos.<br>• La masa de cada parte es menor ante contrapesos. |
| **Fusionados** | Torso se acopla físicamente al conector superior de Piernas. Piernas comanda la locomoción física total mientras Torso apunta, dispara el imán y manipula objetos en movimiento. | • Fuerza y masa máxima unificada.<br>• Posibilidad de correr y disparar/lanzar a la vez.<br>• Estabilidad ante corrientes o trampas.<br>• Salto conjunto de gran potencia. | • Un solo foco de presencia espacial.<br>• No pueden cubrir extremos opuestos.<br>• Requiere alta coordinación para no entorpecer el apuntado. |

---

## 🧩 4. Matriz de 10 Interacciones Cooperativas

Cada sala de prueba explota una o más combinaciones de la siguiente matriz:

1. **La Puerta Pesada:**
   * *Mecánica:* Torso se cuelga de una manivela de contrapeso o palanca superior y la sostiene; Piernas empuja la compuerta por la base con su fuerza motriz.
2. **El Botón en Altura:**
   * *Mecánica:* Piernas patea hacia arriba un bloque pesado o lanza a Torso; Torso en el aire o posado sobre Piernas acciona el botón elevado.
3. **Objeto Pesado en Rampa:**
   * *Mecánica:* Piernas bloquea el retroceso del objeto haciendo de calzo/anclaje; Torso lo engancha con el imán para subirlo paso a paso.
4. **Palanca Combinada:**
   * *Mecánica:* Torso rota la palanca a una posición angular exacta mientras Piernas presiona un interruptor de pie en el instante en que el mecanismo se alinea.
5. **El Foso Ancho:**
   * *Mecánica:* Piernas carga a Torso, toma carrera y patea/catapulta a Torso hacia el otro lado; Torso baja un puente levadizo accionando una cadena.
6. **El Conducto Estrecho:**
   * *Mecánica:* Solo Torso puede deslizarse por un tubo de ventilación para desbloquear la puerta principal desde el interior de la sala de control.
7. **Interruptores Simultáneos Distantes:**
   * *Mecánica:* Los jugadores deben separarse, colocarse en extremos opuestos del mapa y activar sus respectivos sensores con una ventana de tolerancia menor a 1 segundo.
8. **Lanzamiento y Patada (Combo Volea):**
   * *Mecánica:* Torso lanza un cubo metálico al aire; Piernas lo patea en el aire con una patada giratoria para impactar un blanco lejano.
9. **Magnetismo y Anclaje:**
   * *Mecánica:* Piernas se ancla firmemente al piso; Torso activa el electroimán a máxima potencia para arrastrar un objeto masivo que, de otro modo, arrastraría al robot hacia el foso.
10. **El Balancín de Equilibrio:**
    * *Mecánica:* Una plataforma basculante requiere que Piernas ajuste su distancia al fulcro mientras Torso traslada cargas de un extremo al otro para mantener la estabilidad horizontal.

---

## 🏛️ 5. Progresión de Niveles (10 Salas de Prueba)

El juego cuenta con un arco de aprendizaje estructurado en tres niveles de complejidad:

### 🟢 Salas Simples (Salas 1 a 4: Fundamentos)
* **Sala 1: El Despertar:** Movimiento básico de Piernas, agarre simple de Torso, primer botón y salida.
* **Sala 2: Primera Fusión:** Introducción a la mecánica de Fusión/Separación. Conectar para obtener masa suficiente y abrir una compuerta.
* **Sala 3: La Patada y la Diana:** Piernas aprende a patear objetos para romper una barrera débil; Torso coloca la llave en el socket.
* **Sala 4: Magneto-básico:** Torso utiliza el rayo magnético para recuperar una celda de energía a través de una reja infranqueable.

### 🟡 Salas Intermedias (Salas 5 a 8: Coordinación Asíncrona)
* **Sala 5: El Abismo y el Puente:** Torso es lanzado por Piernas para cruzar un foso y activar la plataforma móvil.
* **Sala 6: Doble Conmutador:** Separación obligatoria para coordinar pulsadores sincronizados bajo temporizador.
* **Sala 7: La Torre Vertical:** Torso trepa andamios para desenganchar poleas mientras Piernas corre en la planta baja para recibir los pesos.
* **Sala 8: El Calzo Móvil:** Transportar una esfera rodante cuesta arriba mediante patadas de Piernas y retención magnética de Torso.

### 🔴 Salas Avanzadas (Salas 9 y 10: Maestría Dinámica)
* **Sala 9: El Circuito de Balancines:** Plataformas oscilantes donde el fallo de peso arroja a los jugadores al vacío, requiriendo anclaje de Piernas y reubicación rápida de Torso.
* **Sala 10: El Reactor:** Gran puzzle de varias fases que combina ventilaciones estrechas, combos de volea (lanzar + patear proyectiles a receptores móviles) y una secuencia final de escape fusionados a toda velocidad.

---

## 🎪 6. Filosofía de Diseño y Humor Físico

* **"Fallar es Divertido":** Si Piernas patea mientras Torso intenta encajar una pieza con precisión, la pieza saldrá volando cómicamente contra la pared. Las animaciones ragdoll y los efectos de sonido enfatizan el choque cómico sin penalizar con frustración.
* **Tolerancia a la Desincronización:** Reaparición instantánea sin reiniciar la progresión global de la sala. Si un jugador cae a un foso, reaparece en el último checkpoint seguro en menos de 1.5 segundos.
* **Cero Tiempos Muertos:** Cuando un jugador está realizando una tarea especializada, el otro siempre tiene una subtarea activa (sostener, vigilar, posicionarse o preparar el siguiente elemento).

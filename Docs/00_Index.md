---
title: Two Pilots, One Robot - Centro de Documentación
tags:
  - hub
  - tpob
  - coop
  - architecture
  - gdd
created: 2026-09-16
updated: 2026-09-16
---

# 🤖 Two Pilots, One Robot (TPOB) — Centro de Documentación

Bienvenido a la base de conocimiento y diseño de **Two Pilots, One Robot (TPOB)**, un videojuego multijugador cooperativo asimétrico donde dos jugadores pilotean de forma coordinada distintas mitades físicas de un mismo robot para resolver puzzles mecánicos, sortear peligros ambientales y superar salas de prueba.

---

## 🧭 Mapa de Contenido (MOC)

Accede a las diferentes secciones del proyecto mediante los enlaces locales de Obsidian:

| Documento | Descripción | Estado |
| :--- | :--- | :--- |
| [[01_GDD]] | **Game Design Document (GDD)** completo: Concepto, roles (Piernas y Torso), estados de Fusión/Separación, matriz de 10 interacciones, filosofía de comedia física y diseño de 10 salas. | `Aprobado` |
| [[02_Arquitectura]] | **Informe de Arquitectura de Software**: Integración autoritativa con **PurrNet**, Inyección de Dependencias con **VContainer**, Modelo asíncrono Zero-GC con **UniTask**, Cámara dinámica con **Cinemachine 3.x**, Juicing con **DOTween**, Inspector tooling con **Tri-Inspector** y Patrones de Diseño (Command, Strategy, Observer, Composite, State). | `Completado` |
| [[03_Diagramas]] | **Diagramas de Sistema**: Diagrama de Clases Mermaid y Flujograma del Loop de Gameplay interactivo con soporte nativo de Obsidian. | `Actualizado` |
| [[04_Plan_de_Implementacion]] | **Programa de Implementación (14 Fases)**: Roadmap de desarrollo paso a paso, complementado para explotar al máximo todos los addons y garantizar Zero-GC en hot paths. | `Producción` |
| [[05_Plantilla_Para_Notion]] | **Plantilla para Notion**: Documento formateado con callouts, tablas y checkboxes listo para ser importado o copiado a Notion. | `Listo para Exportar` |

---

## 🛠️ Stack Tecnológico y Addons Activos

```
Unity Engine (URP)
├── Red y Multiplay: PurrNet (Física Servidor-Autoritativa, NetworkTransform, NetworkRigidbody)
├── Inyección de Dependencias: VContainer (GameLifetimeScope & RoomLifetimeScope)
├── Tareas Asíncronas: UniTask (Zero GC scene loading, delays & transitions)
├── Control de Cámara: Cinemachine 3.1.7 (Target Group dinámico para Fusión y Separación + Impulses)
├── Animación Procedural y Jugo: DOTween (Demigiant)
├── Ergonomía del Editor: Tri-Inspector (Atributos limpios y Runtime Debug Buttons)
└── Entrada de Jugador: UnityEngine.InputSystem 1.18.0 (Eventos dumb con structs de datos)
```

---

## 🎯 Pilares del Proyecto

1. **Cooperación Asimétrica Real:** Ningún jugador es secundario ni mero espectador; la victoria exige sincronía continua de acciones físicas simultáneas.
2. **Física Emergente y Humor:** La descoordinación genera fallos cómicos y momentos emergentes sin que los controles se sientan toscos.
3. **Fusión & Separación Táctica:** La alternancia entre un robot pesado unificado y dos módulos especializados ágiles es la llave de cada puzzle.
4. **Cero Garbage Collection en Loops Calientes:** Cumplimiento irrestricto del *Master Engineering Manifesto* de Unity (structs, pools nativos, queries `NonAlloc`).

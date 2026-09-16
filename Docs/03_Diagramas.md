---
title: Two Pilots, One Robot - Diagramas de Sistema
tags:
  - diagrams
  - mermaid
  - architecture
  - tpob
created: 2026-09-16
updated: 2026-09-16
---

# 📊 Diagramas de Sistema: Two Pilots, One Robot (TPOB)

Esta sección contiene los diagramas técnicos del proyecto expresados en sintaxis nativa de **Mermaid**, renderizables directamente en Obsidian y Notion.

---

## 🏛️ 1. Diagrama de Clases (Arquitectura de Entidades y Puzzles)

```mermaid
classDiagram
    %% Interfaces Fundamentales
    class IInteractable {
        <<interface>>
        +Interact(interactor: Player) void
    }
    class IGrabbable {
        <<interface>>
        +Grab(grabber: Torso) void
        +Release() void
    }
    class IThrowable {
        <<interface>>
        +Throw(direction: Vector3, force: float) void
    }
    class IPushable {
        <<interface>>
        +Push(direction: Vector3, force: float) void
    }
    class IKickable {
        <<interface>>
        +Kick(direction: Vector3, force: float) void
    }
    class IMagnetic {
        <<interface>>
        +Attract(target: Vector3, strength: float) void
    }
    class IClimbable {
        <<interface>>
        +Climb(climber: Torso) void
    }

    %% Jerarquía de Mecanismos y Objetos de Física
    class PhysicsObject {
        -Rigidbody _rb
        -NetworkRigidbody _netRb
        +ApplyForce(force: Vector3) void
    }
    class Mechanism {
        <<abstract>>
        -bool _isActivated
        +Activate() void
        +Deactivate() void
    }
    class Door {
        -float _openSpeed
        +Open() void
        +Close() void
    }
    class Lever {
        -float _angle
        +Toggle() void
    }
    class TargetButton {
        -bool _isPressed
        +Press() void
    }
    class WeightPlate {
        -float _currentWeight
        -float _requiredWeight
        +CheckWeight() void
    }
    class Puzzle {
        -List~Mechanism~ _mechanisms
        -bool _isSolved
        +EvaluateConditions() bool
    }

    %% Jugadores y Robot
    class Player {
        -int _playerId
        -InputReader _inputReader
        +ReadInput() void
    }
    class RobotPart {
        <<abstract>>
        -Player _pilot
        -bool _isFused
        +SetPilot(player: Player) void
    }
    class Legs {
        -float _moveSpeed
        -float _jumpForce
        +Move(direction: Vector2) void
        +Jump() void
        +Kick() void
    }
    class Torso {
        -float _armLength
        -float _magneticPower
        +Aim(direction: Vector2) void
        +GrabObject() void
        +ThrowObject() void
        +ActivateMagnet() void
    }
    class RobotCoordinator {
        -Legs _legs
        -Torso _torso
        -bool _isFused
        +Fuse() void
        +Separate() void
    }

    %% Gestión Global y de Red
    class GameManager {
        -GameState _currentState
        +ChangeState(newState: GameState) void
    }
    class LevelManager {
        -int _currentRoomIndex
        +LoadRoomAsync(roomIndex: int) UniTask
        +AdvanceRoom() void
    }

    %% Relaciones
    IInteractable <|.. Mechanism
    IGrabbable <|.. PhysicsObject
    IThrowable <|.. PhysicsObject
    IPushable <|.. PhysicsObject
    IKickable <|.. PhysicsObject
    IMagnetic <|.. PhysicsObject
    IClimbable <|.. Mechanism

    Mechanism <|-- Door
    Mechanism <|-- Lever
    Mechanism <|-- TargetButton
    Mechanism <|-- WeightPlate

    Puzzle --> Mechanism : coordina

    RobotPart <|-- Legs
    RobotPart <|-- Torso
    Player --> RobotPart : pilota

    RobotCoordinator --> Legs : controla
    RobotCoordinator --> Torso : controla

    GameManager --> LevelManager : orquesta
    LevelManager --> Puzzle : evalúa
```

---

## 🔁 2. Flujograma del Loop de Gameplay (Decision Loop)

```mermaid
flowchart TD
    Start([Inicio de la Sala]) --> Spawn[Spawn de Jugadores en Checkpoint]
    Spawn --> Analyze{Analizar Obstáculo}

    Analyze -->|Requiere Masa / Salto Alto| OptFuse[Decisión: Fusionarse]
    Analyze -->|Requiere Tareas Simultáneas / Espacios Estrechos| OptSep[Decisión: Separarse]

    %% Ruta Fusionados
    OptFuse --> FusedState[Robot Unificado]
    FusedState --> MoveRobot[Piernas: Desplazamiento y Saltos]
    MoveRobot --> TorsoActionFused[Torso: Apuntar, Agarrar o Usar Imán]
    TorsoActionFused --> CheckPuzzleFused{¿Puzzle Resuelto?}

    %% Ruta Separados
    OptSep --> SepState[Entidades Independientes]
    SepState --> P1Actions[Piernas: Correr, Patear o Empujar Cajas]
    SepState --> P2Actions[Torso: Trepar, Tirar Palancas o Cruzar Ductos]
    P1Actions --> SyncAction{¿Acción Sincronizada?}
    P2Actions --> SyncAction

    SyncAction -->|Coordinación Exitosa| CheckPuzzleSep{¿Puzzle Resuelto?}
    SyncAction -->|Fallo / Desincronía| Failure[Fallo Físico Cómico / Caída]
    Failure --> RespawnDelay[Respawn Inmediato en Checkpoint]
    RespawnDelay --> Analyze

    CheckPuzzleFused -->|No| FusedState
    CheckPuzzleFused -->|Sí| DoorOpen[Puerta de Salida se Abre]

    CheckPuzzleSep -->|No| SepState
    CheckPuzzleSep -->|Sí| DoorOpen

    DoorOpen --> ReachExit{¿Ambos en la Zona de Salida?}
    ReachExit -->|No| WaitForPartner[Esperar al Compañero]
    WaitForPartner --> ReachExit
    ReachExit -->|Sí| NextLevel[LevelManager: Cargar Siguiente Sala]
    NextLevel --> EndRoom([Sala Superada])
```

---

## 📡 3. Secuencia de Red: Fusión Autoritativa con PurrNet y UniTask

```mermaid
sequenceDiagram
    autonumber
    actor C1 as Cliente 1 (Piernas)
    actor C2 as Cliente 2 (Torso)
    participant SRV as Servidor PurrNet
    participant CAM as Cinemachine Camera

    Note over C1,C2: Ambos jugadores deciden fusionarse (Input: Botón de Fusión)
    C1->>SRV: ServerRpc: RequestFusion()
    C2->>SRV: ServerRpc: RequestFusion()
    
    SRV->>SRV: Valida distancia física (sqrMagnitude < MaxFuseDistance)
    alt Distancia válida
        SRV->>SRV: Emparenta Torso a Socket de Piernas
        SRV->>SRV: Torso.Rigidbody.isKinematic = true
        SRV->>SRV: Suma masas en Rigidbody de Piernas
        SRV-->>C1: ObserversRpc: OnRobotFused()
        SRV-->>C2: ObserversRpc: OnRobotFused()
        Note over C1,C2: Feedback DOTween (PunchScale en conector)
        CAM->>CAM: Ajusta TargetGroup (Torso Weight = 0, zoom cerrado)
    else Demasiado lejos
        SRV-->>C1: ObserversRpc: OnFusionFailed()
        SRV-->>C2: ObserversRpc: OnFusionFailed()
        Note over C1,C2: Feedback auditivo de fallo cómico
    end
```

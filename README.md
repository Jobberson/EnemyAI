# Enemy AI Vision & Hearing (Unity 6)

Robust, drop‑in **Enemy Perception** for Unity 6: **Vision (FOV + LOS)**, **Hearing (events + occlusion + prioritization)**, **Threat Memory** with **prediction**, **Patrol (Waypoints/Random)**, **Investigate / Chase / Search**, **Suspicion zones**, and a **Bracken‑like Hide‑From‑Player** behavior — all **NavMesh‑first** and **Asset‑Store‑ready** with a **Setup Wizard** and polished custom inspectors.

<p align="center">
  <a href="https://unity.com/releases/editor/whats-new/6000"><img alt="Unity" src="https://img.shields.io/badge/Unity-6000.x%20%7C%202023%20LTS-000?logo=unity&logoColor=white"></a>
  <img alt="NavMesh" src="https://img.shields.io/badge/NavMesh-built--in-blue">
  <img alt="Status" src="https://img.shields.io/badge/status-active-brightgreen">
  <img alt="PRs" src="https://img.shields.io/badge/PRs-welcome-success">
  <a href="#license"><img alt="License" src="https://img.shields.io/badge/license-MIT-informational"></a>
</p>

---

## Table of Contents

- [Features](#features)
- [Quick Start (5 minutes)](#quick-start-5-minutes)
- [Setup Wizard](#setup-wizard)
- [Project Structure](#project-structure)
- [Core Components](#core-components)
  - [VisionSensor](#visionsensor)
  - [HearingSensor](#hearingsensor)
  - [ThreatMemory](#threatmemory)
  - [EnemyAIController](#enemyaicontroller)
  - [SuspicionSystem](#suspicionsystem)
- [Player Setup](#player-setup)
- [Samples](#samples)
- [Performance Tips](#performance-tips)
- [Roadmap](#roadmap)
- [FAQ](#faq)
- [Contributing](#contributing)
- [Support](#support)

---

## Features

- **Vision**
  - Focus (narrow, fast) & Peripheral (wide, slow) **FOV zones**
  - Robust **Line of Sight**: SingleRay, **MultiRay** (default), Capsule
  - Tunable **angle/distance curves**, detection build‑up & decay
- **Hearing**
  - Event‑based with **loudness**, **range**, **occlusion attenuation**
  - **Prioritization queue** + **per‑type cooldowns** (footstep, gunshot, etc.)
- **AI Brain (NavMesh)**
  - States: **Idle / Patrol / Investigate / Chase / Search**
  - **Patrol**: Waypoints or Random (ground‑only sampling)
  - **Suspicion System**: world hotspots that **accumulate & decay**
  - **Prediction**: last‑known **velocity** → **intercept** target during chase
  - **Hide From Player**: if **Camera.main** is watching, **Freeze** or **Flee to Occlusion**
- **Polish**
  - **Setup Wizard** (layers/tags, player/enemies, scene systems, nav help)
  - **Custom Inspectors** with collapsible sections & gizmo toggles
  - Clear gizmos (FOV, hearing discs, patrol areas, waypoints)

---

## Quick Start (5 minutes)

1. **Open the Wizard** → `SnogTools ▸ AI ▸ Stealth Setup Wizard`  
2. **Create Layers/Tag** → `Ground`, `Obstacles`, `PerceptionTarget`, `Player`  
3. **Mark Player** → adds `PerceptionTarget` + `SoundEmitter` (+ optional test emitter)  
4. **Create SuspicionSystem** in scene  
5. **Configure Enemies** → ensures/sets `NavMeshAgent`, `EnemyAIController`, `VisionSensor`, `HearingSensor`, `ThreatMemory`, masks & layers  
6. **Bake NavMesh** (Window ▸ AI ▸ Navigation ▸ Bake)  
7. **Play** → look at enemies (Camera.main): they **hide**; press **Space** to emit footsteps and see **investigation**

> Prefer a full guide? See **`Documentation/Enemy_AI_Vision_Hearing_User_Guide.md`**.

---

## Setup Wizard

**Menu:** `SnogTools ▸ AI ▸ Stealth Setup Wizard`

- **Project Setup**: creates **Ground**, **Obstacles**, **PerceptionTarget** layers + **Player** tag
- **Scene Setup**: marks **Player**, adds **PerceptionTarget** / **SoundEmitter** / optional **FootstepTestEmitter**
- **Systems**: drops a singleton **SuspicionSystem**
- **Enemy Setup**: ensures & configures core components and masks; option to **avoid player LoS** in Random Patrol
- **Navigation**: opens Nav window, select all agents

---

## Project Structure

```
Assets/
  SnogTools/AI/
    Runtime/
      EnemyAIController.cs
      VisionSensor.cs
      HearingSensor.cs
      ThreatMemory.cs
      PerceptionTarget.cs
      SoundSystem.cs
      SoundEmitter.cs
      SuspicionSystem.cs
      FootstepTestEmitter.cs
    Editor/
      StealthSetupWizard.cs
      TagLayerUtility.cs
      EnemyAIControllerEditor.cs
      VisionSensorEditor.cs
      HearingSensorEditor.cs
  Documentation/
    Enemy_AI_Vision_Hearing_User_Guide.md
```

---

## Core Components

### VisionSensor

Focus/Peripheral FOV zones with robust LOS and detection curves.

```csharp
// Example: quick LOS test in play mode
if (sensor.HasLineOfSight(playerTransform))
{
    Debug.Log("Visible!");
}
```

**Key fields:** `focusFOV`, `peripheralFOV`, `viewDistance`, `scanInterval`, `losMode`, `losSamples`, `targetMask`, `occluderMask`.

---

### HearingSensor

Event‑based hearing with prioritization and per‑type cooldowns.

```csharp
// Emit a footstep from a player controller
emitter.Emit(loudness: 1.2f, maxRange: 12f, type: SoundType.Footstep);
```

**Key fields:** `baseHearingRadius`, `minLoudness`, `occluderMask`, `occlusionAttenuation`, `maxEventsPerFrame`, weights/cooldowns.

---

### ThreatMemory

Stores last‑known **position** and **velocity**, provides **predicted** intercept.

```csharp
Vector3 intercept = memory.GetPredictedPosition();
```

---

### EnemyAIController

NavMesh‑driven state machine: **Patrol (Waypoints/Random)**, **Investigate**, **Chase** (with prediction), **Search** (with suspicion), and **Hide‑From‑Player**.

**Highlights**
- Random Patrol picks **ground‑only** NavMesh points
- Optional **Avoid Player LoS** for stealthy wandering
- Hide mode uses **Camera.main** to detect being watched

---

### SuspicionSystem

Lightweight world hotspots: raise on hearing/vision drop‑off, decay over time; queried during **Search**.

```csharp
SuspicionSystem.Instance?.Raise(position, amount: 1.0f, radius: 6f, decayPerSecond: 1.0f);
```

---

## Player Setup

- Add **PerceptionTarget** to the Player (wizard can do this)
- Add **SoundEmitter** and emit on footsteps / interactions
- Optional **FootstepTestEmitter** for quick validation (press **Space**)

---

## Samples

- **Basic Guard**: Waypoints, moderate vision, conservative hearing
- **Bracken‑like**: Random Patrol + Avoid LoS + Hide (FleeToOcclusion)
- **Noisy Arena**: Hearing priorities & cooldowns emphasized

> (You can keep sample scenes under `Samples~/` for Unity Package Manager‑style distribution.)

---

## Performance Tips

- Vision: raise `scanInterval` (e.g., 0.15–0.25s); keep `losSamples` ~2–3; right‑size `viewDistance`
- Hearing: limit `maxEventsPerFrame` (2–4); tune per‑type cooldowns
- NavMesh: `repathInterval` ~0.2–0.4s; ensure agent radius/height fit your level
- Occluders: keep **Obstacles** masks tight to reduce ray hits

> Scaling to **50–100** enemies? Add a **Perception Scheduler** (round‑robin scans, optional `RaycastCommand` batching).

---

## Roadmap

- Global **Perception Scheduler** (stagger sensors, optional jobified LOS)
- **Light & Motion sensitivity** (stealth depth via target modifiers)
- **Door/Window portals** (visual + acoustic logic)
- **Profiles** (Scriptable presets) + **Setup Wizard** sample scene generator
- Debug overlays (score breakdowns, LOS rays, heatmaps)

---

## FAQ

**Q:** The enemy won’t move.  
**A:** Ensure `NavMeshAgent` exists and **bake** the NavMesh (Window ▸ AI ▸ Navigation). Confirm the agent is **on** the NavMesh.

**Q:** The enemy never sees the player.  
**A:** Player needs **PerceptionTarget** on **PerceptionTarget** layer; `VisionSensor.targetMask` must include it; verify `occluderMask` is correct.

**Q:** Hide‑From‑Player doesn’t trigger.  
**A:** Uses **Camera.main** (or assign `playerEye`). Tune `playerFOV`, `watchedDistance`, and `playerOccluderMask`. Ensure no occluder blocks camera→enemy.

**Q:** Hearing floods the AI.  
**A:** Lower `maxEventsPerFrame`, increase per‑type cooldowns, and validate event `maxRange` vs scene scale.

---

## Contributing

PRs are welcome! Please:

1. Open an issue describing the change (feature/bug/perf).
2. Target Unity **6000.x**/**2023 LTS** and avoid external dependencies.

---

## Support

- 📄 **Full guide:** `Documentation/Enemy_AI_Vision_Hearing_User_Guide.md`
- ✉️ **Contact:** snogdev@gmail.com

<p align="center">
  <sub>Built with 💜 for stealth & horror. Contributions welcome!</sub>
</p>

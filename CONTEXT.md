# MazeRunner - Technical Context

## Project

| Field | Value |
|---|---|
| Project Name | MazeRunner |
| Project Path | `C:\Users\User\UnityProjects\MazeRunner` |
| Platform Target | Android |
| Genre | 3D Procedural Maze |

## Unity Environment

| Field | Value |
|---|---|
| Unity Version | 6000.0 LTS |
| Render Pipeline | Universal Render Pipeline (URP) |
| Scripting Backend | IL2CPP (to be configured for build) |
| Target Architecture | ARM64 |

## Installed Packages

| Package | Version | Notes |
|---|---|---|
| Cinemachine | — | Third-person camera, FreeLook/Follow, Impulse for camera shake |
| Starter Assets ThirdPerson URP | 1.1.7 | `ThirdPersonController` prefab, Animator Controller, input handling |
| DOTween | 1.2.825 | Tween animations for wall shifting mechanic |
| Google Play Billing | TBD | To be added in Stage 7 |

## Character

| Field | Value |
|---|---|
| Asset | Arissa.fbx |
| Location | `Assets/Arissa.fbx` |
| Rig Type | Humanoid |
| Avatar | Defined from Arissa.fbx import |
| Usage | Player character; skinned mesh swapped into Starter Assets ThirdPersonController prefab |

## Current Progress

| Field | Value |
|---|---|
| Current Stage | Stage 2 — Third Person Character Controller |
| Stage Status | In Progress |

## Completed Scripts

| Script | Stage | Notes |
|---|---|---|
| `Assets/Scripts/MazeGenerator.cs` | 1 | Recursive Backtracker DFS; builds wall/floor/ceiling geometry from custom box meshes; exposes `GenerateMaze()`, `RegenerateMaze()`, `StartWorldPosition`, `ExitWorldPosition` |
| `Assets/Scripts/MazeManager.cs` | 1 | Scene bootstrap; holds `MazeGenerator` reference; calls `GenerateMaze()` in `Start()`; exposes `StartPosition` / `ExitPosition` for other systems |
| `Assets/Scripts/PlayerSpawner.cs` | 2 | Finds `MazeManager`, spawns `playerPrefab` at `StartPosition`, wires Cinemachine Follow/LookAt to spawned player's `PlayerCameraRoot`; supports CM 3.x and CM 2.x via preprocessor |
| `Assets/Scripts/CameraFollow.cs` | 2 | Fallback only — self-disables if any Cinemachine Virtual Camera is found in the scene; attach to Main Camera |

## Architecture Decisions

| Decision | Reason |
|---|---|
| Custom box meshes instead of `GameObject.CreatePrimitive` | `CreatePrimitive` adds a default physics material and uses a shared mesh not suitable for GPU instancing; custom meshes let all walls share one `sharedMesh` reference |
| Walls use `sharedMaterial` (not `material`) | Avoids per-instance material copies; required for GPU instancing to batch draw calls |
| Iterative DFS instead of recursive | Avoids `StackOverflowException` on large grids (15×15 = 225 cells; larger grids would recurse deeper than Unity's default stack allows) |
| South + West wall placement per cell, North/East only on border | Each wall segment is placed exactly once — avoids duplicate geometry at shared cell edges |
| `StartWorldPosition` / `ExitWorldPosition` as public properties on `MazeGenerator` | Lets `MazeManager`, `GameManager`, and player spawning read positions without coupling to grid coordinates |
| Cinemachine version detected via preprocessor (`UNITY_CINEMACHINE` vs `CINEMACHINE`) | Unity 6 ships Cinemachine 3.x under the `Unity.Cinemachine` namespace; CM 2.x uses `Cinemachine` — both supported without forking scripts |
| `PlayerCameraRoot` child used as camera Follow target | Starter Assets places this bone at head height; following root transform causes camera to dip when crouching |
| `CameraFollow` self-disables when Cinemachine is present | Zero per-frame cost when not needed; no manual toggling required |

## Key Scene Structure (planned)

```
Scenes/
  MainMenu       — title, difficulty select, play button
  Gameplay       — maze, player, HUD, minimap
```

## Key Script Locations (planned)

```
Assets/Scripts/
  MazeGenerator.cs       — Stage 1: procedural maze generation
  ShiftingWall.cs        — Stage 4: DOTween open/close + impulse + audio
  GameManager.cs         — Stage 5: state machine, timer, difficulty config
  StoreManager.cs        — Stage 7: Google Play Billing / IAP
```

## Notes

- DOTween must be initialized via `DOTween.Init()` before any tweens run — use a bootstrap MonoBehaviour in the Gameplay scene.
- Cinemachine Impulse requires an **Impulse Source** on the event emitter and an **Impulse Listener** on the Virtual Camera.
- Starter Assets ThirdPerson uses the new Unity Input System — ensure Input System package is installed and the Player Input component is configured.
- All URP post-processing effects (fog, vignette, chromatic aberration) are applied via a **Global Volume** with a **Volume Profile** asset.
- GPU Instancing on shared materials is critical for maze performance on Android — walls/floor/ceiling all reuse the same material instances.

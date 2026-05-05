# MazeRunner — Claude Code Instructions

## Обязательно читать при каждой новой сессии

Перед любым ответом или кодом прочитай:
1. `CONTEXT.md` — технический контекст, архитектура, список готовых скриптов
2. `PLAN.md` — полный план разработки по этапам с чекбоксами
3. Memory-файл: `C:\Users\User\.claude\projects\C--Users-User-UnityProjects-MazeRunner\memory\project_mazerunner.md` — актуальный статус этапов и подтверждённые рабочие системы

---

## Текущий статус (обновляется в memory)

- ✅ Stage 1 — Procedural Maze Generation
- ✅ Stage 2 — Third Person Character Controller
- ✅ Stage 3 — Atmosphere
- ⬜ Stage 4 — Wall Shifting Mechanic ← **СЛЕДУЮЩИЙ**
- ⬜ Stage 5 — Gameplay Systems
- ⬜ Stage 6 — UI
- ⬜ Stage 7 — Monetization
- ⬜ Stage 8 — Polish & Android Build

---

## Ключевая архитектура

### Сцена: `Assets/Scenes/SampleScene.unity`

| GameObject | Компоненты |
|---|---|
| Main Camera | Camera, AudioListener, URP Camera Data, **FollowCamera** |
| MazeManager | Transform, MazeGenerator, MazeManager, **AtmosphereSetup** |
| PlayerSpawner | Transform, PlayerSpawner, **PlayerTorch**, **MinimapSystem** |
| Directional Light | Light (intensity 0.15, тёмный) |
| Global Volume | Volume (существующий профиль, priority=0) |

### Скрипты: `Assets/Scripts/`

| Скрипт | Назначение |
|---|---|
| `MazeGenerator.cs` | Процедурная генерация. cellSize=4, wallHeight=4, grid 15×15. SpawnLights() — fill lights каждые 2 клетки |
| `MazeManager.cs` | Запускает GenerateMaze() в Start(). Устанавливает fog (Exponential, density=0.01) |
| `PlayerSpawner.cs` | Спавн игрока на StartPosition. **Статическое событие `OnPlayerSpawned`** — точка интеграции для всех систем |
| `PlayerMovement.cs` | CharacterController + new Input System (Keyboard.current). Camera-relative движение |
| `FollowCamera.cs` | Third-person. SphereCast wall collision. DOTween zoom. Slerp yaw lag 0.12s |
| `PlayerTorch.cs` | Дочерний Point Light на игроке. intensity=5, range=18 |
| `MinimapSystem.cs` | Круглая мини-карта (GTA-style). RenderPipelineManager boost ambient только для minimap camera |
| `AtmosphereSetup.cs` | Runtime VolumeProfile priority=1. Vignette+ChromaticAberration+ColorAdjustments |

### Паттерн интеграции новых систем

```
1. Создать MonoBehaviour с подпиской на PlayerSpawner.OnPlayerSpawned
2. Добавить компонент на PlayerSpawner GameObject в сцене
3. Никаких ручных шагов в Editor не нужно
```

---

## Правила работы

- **Помечать этап выполненным только после того, как пользователь подтвердил что работает**
- CONTEXT.md и PLAN.md — источник правды по плану, memory — по статусу
- Нет Cinemachine (удалён). Нет ручных шагов в Editor — всё через код
- DOTween установлен (1.2.825) — использовать для анимаций
- Платформа Android, URP, IL2CPP, ARM64
- При добавлении скриптов в сцену через YAML — создавать .meta файл с GUID
- Свет: shadows=None везде (мобильный бюджет)
- При редактировании сцены всегда читать нужный блок сначала, потом редактировать

---

## Референс игры

**"Бегущий в лабиринте" (The Maze Runner)** — тёмные каменные коридоры, слабое освещение, клаустрофобия, напряжение. Атмосфера важнее красоты.

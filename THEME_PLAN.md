# План: Система тематических лабиринтов (Maze Themes)

## Идея

Игрок выбирает «карту» (тему лабиринта) перед игрой. Базовая тема бесплатная, остальные — за IAP.
Каждая тема меняет геометрию, материалы, атмосферу, врагов и звуки — без новых сцен.

---

## Анализ текущей архитектуры

| Система | Файл | Что нужно менять |
|---|---|---|
| Геометрия | `MazeGenerator.cs` | `wallMaterial`, `floorMaterial`, `ceilingMaterial`, `wallHeight`, `spawnCeilings` |
| Атмосфера | `AtmosphereSetup.cs` | Vignette, Bloom, ColorAdjustments, fog density в `MazeManager` |
| Враги | `EnemySpawner.cs` | Тип, скорость, количество — сейчас hardcoded |
| Выбор сложности | `MainMenuManager.cs` | Туда же добавить выбор темы |
| Сохранение | `PlayerPrefs` | `"MapTheme"` int, аналогично `"Difficulty"` |
| IAP | `ArtifactInventory.cs` паттерн | Новый `ThemeStore.cs` по аналогии |

**Важно:** в проекте нет ScriptableObject-ов — план намеренно держится в рамках
паттерна «всё через код», как принято в этом проекте.

---

## Темы

### 0 — Dungeon (базовая, бесплатно)
Текущий лабиринт без изменений. Серые каменные стены, высота 4f.

### 1 — Cave / Скала (платная, IAP)
| Параметр | Значение |
|---|---|
| `wallHeight` | 2.8f (низкие потолки, клаустрофобия) |
| `spawnCeilings` | true |
| Цвет стен | тёмно-коричневый, влажный камень |
| Цвет пола | мокрый гранит, тёмный |
| Fog | `Exponential`, density=0.025f (гуще) |
| Vignette | intensity=0.32f (темнее по краям) |
| ColorFilter | холодный сине-серый `(0.75, 0.87, 0.95)` |
| Bloom | threshold=0.55f, intensity=2.2f |
| Частицы | капли воды сверху (процедурные, DOTween) |
| Враги | + пауки (быстрые, маленькие, другая анимация) |
| Звук | капли воды, эхо, скрежет камня |
| Torch range | уменьшить с 18 до 12 (темнее) |

### (будущие темы — не реализуются сейчас)
- **Sewer** — канализация, зелёный туман
- **Temple** — древний храм, ловушки

---

## Этапы реализации

### Этап A — Данные темы `MazeThemeData` (1 файл)

Создать `Assets/Scripts/MazeThemeData.cs`:

```csharp
[System.Serializable]
public class MazeThemeData
{
    public string id;           // "dungeon", "cave"
    public string displayName;  // "Подземелье", "Пещера"
    public bool   isPremium;    // нужен IAP

    // Геометрия
    public float wallHeight;
    public bool  spawnCeilings;

    // Материалы (строки — Resources.Load путь, напр. "Themes/cave_wall")
    public string wallMatPath;
    public string floorMatPath;
    public string ceilMatPath;

    // Атмосфера
    public float fogDensity;
    public float vignette;
    public Color colorFilter;
    public float bloomThreshold;
    public float bloomIntensity;

    // Освещение факела
    public float torchRange;
    public float torchIntensity;

    // Враги
    public float enemySpeedMult;  // множитель скорости текущей сложности
    public bool  spawnSpiders;
}
```

Статический реестр тем — `MazeThemeRegistry.cs`:
```csharp
public static class MazeThemeRegistry
{
    public static readonly MazeThemeData[] All = { Dungeon, Cave };
    public static MazeThemeData Current => All[PlayerPrefs.GetInt("MapTheme", 0)];
    ...
}
```

---

### Этап B — Применение темы в существующих системах

**`MazeGenerator.cs`** — в `Start()` или новый метод `ApplyTheme(MazeThemeData t)`:
- `wallHeight = t.wallHeight`
- `spawnCeilings = t.spawnCeilings`
- загружать материалы через `Resources.Load<Material>(t.wallMatPath)` если путь не пустой

**`AtmosphereSetup.cs`** — читать параметры из `MazeThemeRegistry.Current` вместо хардкода.

**`MazeManager.cs`** — `fogDensity` из темы.

**`PlayerTorch.cs`** — `range` и `intensity` из темы.

**`EnemySpawner.cs`** — множитель `t.enemySpeedMult`, флаг `t.spawnSpiders`.

---

### Этап C — Материалы Cave темы

В `Assets/Resources/Themes/`:
- `cave_wall.mat` — URP Lit, тёмно-коричневый `(0.12, 0.08, 0.05)`, roughness 0.9
- `cave_floor.mat` — мокрый камень `(0.08, 0.07, 0.06)`, metallic 0.1
- `cave_ceil.mat` — почти чёрный `(0.06, 0.05, 0.04)`

Материалы создаются вручную в Unity Editor (5 минут),
или процедурно в коде — как в MazeGenerator.

---

### Этап D — Частицы воды (Cave)

Новый `CaveAtmosphere.cs` — добавляется автоматически если `t.id == "cave"`:
- каждые 0.5с спавнит «каплю» на случайной позиции потолка
- капля падает вниз через DOTween `MoveY`, уничтожается при приземлении
- звук капли через `SoundManager`

---

### Этап E — UI выбора темы в главном меню

В `MainMenuManager.cs` добавить третью секцию «ЛОКАЦИЯ» между заголовком и кнопками сложности:

```
[ Подземелье  ]  [ Пещера 🔒 ]
```

- Lock-иконка если `isPremium && !ThemeStore.IsUnlocked("cave")`
- Тап на заблокированную → показать `IAP popup`
- Выбранная тема сохраняется в `PlayerPrefs("MapTheme")`

---

### Этап F — IAP интеграция

`ThemeStore.cs` (аналог `ArtifactInventory`):
```csharp
public static class ThemeStore
{
    public static bool IsUnlocked(string themeId)
    {
        if (themeId == "dungeon") return true;
        return PlayerPrefs.GetInt("Theme_" + themeId, 0) == 1;
    }
    public static void Unlock(string themeId) { ... PlayerPrefs.Save(); }
}
```

IAP product ID: `"com.mazegame.theme.cave"` — добавить в `StoreManager.cs` на этапе 7.
До этого — отладочная кнопка `[РАЗБЛОКИРОВАТЬ (DEBUG)]` в меню.

---

## Порядок реализации (приоритет)

```
[ ] A. MazeThemeData + MazeThemeRegistry (только данные, без UI)
[ ] B. Применение темы в MazeGenerator + AtmosphereSetup + MazeManager
[ ] C. Создать материалы Cave в Editor
[ ] D. Применение темы в PlayerTorch + EnemySpawner
[ ] E. UI выбора темы в MainMenuManager
[ ] F. CaveAtmosphere.cs (частицы воды)
[ ] G. ThemeStore + IAP-заглушка
```

Этапы A–D — чисто код, никаких шагов в Editor.
Этап C — единственный где нужно создать материалы руками (или сделать процедурно).

---

## Риски

| Риск | Митигация |
|---|---|
| Материалы Cave не загружаются → розовый | Fallback: если `Resources.Load` вернул null — использовать текущий материал MazeGenerator |
| Низкий потолок (2.8f) режет камеру | FollowCamera.SphereCast уже обрабатывает коллизии; проверить при тестировании |
| Пауки — нет отдельной модели | Использовать `MazeEnemy.cs` с другими параметрами + другой цвет; 3D модель — отдельная задача |
| IAP интеграция Google Play | Откладывается на Stage 7 — до тех пор `ThemeStore` читает PlayerPrefs |

---

## Что НЕ входит в план

- Новые сцены Unity — не нужны, всё в одной сцене
- Новый Animator Controller для пауков — используем существующий с другими параметрами
- Нет Cinemachine, нет ручных шагов в Editor (кроме материалов)

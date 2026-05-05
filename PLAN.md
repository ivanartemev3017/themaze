# MazeRunner - Development Plan

A 3D procedural maze game for Android built with Unity 6 URP.

---

## Stage 1: Procedural Maze Generation

Generate a 3D maze using cubes for walls, floor, and ceiling.

- [ ] Create `MazeGenerator.cs` script
- [ ] Implement recursive backtracker (or Eller's) maze algorithm on a 2D grid
- [ ] Define cell size constant and grid dimensions (width x height)
- [ ] Instantiate wall prefabs (unit cubes) for each wall segment (N/S/E/W per cell)
- [ ] Instantiate floor tile prefabs across the full grid
- [ ] Instantiate ceiling tile prefabs across the full grid
- [ ] Create wall, floor, and ceiling prefabs with URP materials
- [ ] Assign distinct URP Lit materials to walls vs floor vs ceiling
- [ ] Parent all generated objects under a `Maze` root GameObject
- [ ] Expose grid width, height, and cell size as serialized fields
- [ ] Test generation in Play Mode — confirm no holes or orphaned walls
- [ ] Verify maze is always fully connected (every cell reachable)

---

## Stage 2: Third Person Character Controller

Integrate Arissa FBX with Starter Assets and Cinemachine for third-person movement.

- [ ] Verify Arissa.fbx is imported as Humanoid rig in Import Settings
- [ ] Create an Avatar Definition from Arissa.fbx and confirm all bones map correctly
- [ ] Duplicate the Starter Assets `ThirdPersonController` prefab, rename to `Player`
- [ ] Replace the default mesh/skinned mesh renderer with Arissa's skinned mesh
- [ ] Assign Arissa's Avatar to the Animator component on the Player prefab
- [ ] Confirm the Starter Assets Animator Controller works with Arissa's bone hierarchy
- [ ] Place Player prefab at maze start position
- [ ] Configure Cinemachine Virtual Camera (FreeLook or Follow) targeting Player
- [ ] Set appropriate camera distance, vertical axis limits, and damping values
- [ ] Enable `CharacterController` collision detection with generated maze walls
- [ ] Test movement: walk, run, turn — no clipping through walls
- [ ] Test camera: no clipping through walls or ceiling

---

## Stage 3: Atmosphere

Apply URP post-processing and lighting to create a dark, tense atmosphere.

- [ ] Enable URP Global Volume in the scene
- [ ] Add **Fog** override — use linear or exponential fog, tune density
- [ ] Add **Vignette** override — dark edges, intensity ~0.4
- [ ] Add **Chromatic Aberration** override — subtle, intensity ~0.2
- [ ] Add **Color Adjustments** override — desaturate slightly, lower exposure
- [ ] Add **Bloom** override — low intensity for dim glow on emissive surfaces
- [ ] Set scene ambient light to very low (dark grey or near-black)
- [ ] Add a single dim Point Light or Spot Light attached to the Player (torch effect)
- [ ] Optionally add flickering light script for tension
- [ ] Set Directional Light intensity to near-zero or disable it
- [ ] Create emissive URP material for any accent surfaces (e.g. exit marker)
- [ ] Test atmosphere in Play Mode on target Android resolution (1080x2400)

---

## Stage 4: Wall Shifting Mechanic

Walls animate open/closed using DOTween, with camera shake and audio feedback.

- [ ] Install and confirm DOTween 1.2.825 is initialized (`DOTween.Init()` in a bootstrap script)
- [ ] Create `ShiftingWall.cs` — stores open/closed positions, exposes `Open()` / `Close()` methods
- [ ] Implement `Open()`: DOTween move wall to open position over configurable duration (Ease.InOutQuad)
- [ ] Implement `Close()`: DOTween move wall back to closed position
- [ ] Mark certain wall segments as ShiftingWalls during maze generation (random or designed)
- [ ] Create a trigger volume in front of each ShiftingWall to detect Player proximity
- [ ] Wire trigger → call `ShiftingWall.Open()` on enter, `Close()` on exit
- [ ] Add Cinemachine Impulse Source component to ShiftingWall
- [ ] Add Cinemachine Impulse Listener to the Cinemachine Virtual Camera
- [ ] Fire impulse on wall open and wall close events for camera shake
- [ ] Add AudioSource to ShiftingWall with a rumble/grind sound clip
- [ ] Play sound on open, play reversed or different clip on close
- [ ] Expose shift duration, shake force, and audio volume as serialized fields
- [ ] Test: walk into trigger → wall opens with shake and sound → walk away → wall closes

---

## Stage 5: Gameplay Systems

Start/exit points, a level timer, difficulty settings, and win/lose conditions.

- [ ] Create `GameManager.cs` singleton — manages game state (Playing, Won, Lost)
- [ ] Define a `Difficulty` enum: Easy, Medium, Hard
- [ ] Expose maze size and time limit per difficulty in a `[System.Serializable]` config struct
  - Easy: small grid, generous timer
  - Medium: medium grid, moderate timer
  - Hard: large grid, tight timer
- [ ] Place a **Start Point** marker in the maze (first cell of generation)
- [ ] Place an **Exit Point** marker in the maze (last/far cell), with a distinct emissive material
- [ ] Spawn Player at Start Point on game begin
- [ ] Implement countdown timer in GameManager — decrements each second
- [ ] Trigger **Win** state when Player enters Exit Point trigger
- [ ] Trigger **Lose** state when timer reaches zero
- [ ] Create `WinScreen` UI Canvas — shows time remaining, a "Next Level" button
- [ ] Create `LoseScreen` UI Canvas — shows "Time's Up", a "Retry" button
- [ ] Wire GameManager state changes to show/hide Win/Lose canvases
- [ ] Implement scene reload for Retry and difficulty progression for Next Level
- [ ] Test all three difficulties: maze generates correctly sized, timer enforced

---

## Stage 6: UI

Minimap, HUD, and main menu.

**HUD**
- [ ] Create HUD Canvas (Screen Space - Overlay)
- [ ] Add timer display (TextMeshPro) — updates every second from GameManager
- [ ] Add difficulty label display
- [ ] Add a subtle dark panel background for readability

**Minimap**
- [ ] Create a second Camera (`MinimapCamera`) — orthographic, top-down, following Player X/Z
- [ ] Set MinimapCamera to render only a `Minimap` layer
- [ ] Assign maze ceiling and walls to the Minimap layer (or create minimap-only icon sprites)
- [ ] Create a RenderTexture for MinimapCamera output
- [ ] Add a RawImage UI element in HUD canvas displaying the RenderTexture
- [ ] Add a Player dot (small icon or sprite) on the minimap
- [ ] Mask minimap to a circular or square shape
- [ ] Test minimap scrolls correctly as Player moves through the maze

**Main Menu**
- [ ] Create `MainMenu` scene
- [ ] Design main menu Canvas: title, Play button, difficulty selector (Easy / Medium / Hard), Quit button
- [ ] Implement difficulty selection — store chosen difficulty in a persistent `GameSettings` singleton or PlayerPrefs
- [ ] Wire Play button → load Gameplay scene with selected difficulty
- [ ] Add a background image or blurred maze screenshot as menu backdrop
- [ ] Implement scene transition (simple fade via CanvasGroup alpha or Animator)

---

## Stage 7: Monetization

Free easy mode; paywall for medium/hard via Google Play Billing.

- [ ] Add Google Play Billing package to the project (via Package Manager or `.unitypackage`)
- [ ] Create `StoreManager.cs` — initializes IAP, defines product IDs
  - Product: `com.yourcompany.mazerunner.unlock_full` (non-consumable)
- [ ] Implement purchase flow: user selects Medium/Hard → if not unlocked → show purchase dialog
- [ ] Implement restore purchases (required for Google Play)
- [ ] Store unlock state in PlayerPrefs and verify against IAP receipt on app start
- [ ] Show a "Unlock Full Game" prompt UI with price fetched dynamically from store
- [ ] Gate Medium and Hard difficulty buttons in Main Menu behind unlock check
- [ ] Easy level remains always free and playable without purchase
- [ ] Test with Google Play sandbox/test accounts
- [ ] Confirm `AndroidManifest.xml` includes `BILLING` permission

---

## Stage 7B: Internal Store

In-game shop where players can browse and unlock characters and artifacts using IAP or earned currency.

**Store UI**
- [ ] Create `StoreScreen` canvas — scrollable grid of items (characters + artifacts)
- [ ] Each item card: thumbnail, name, price/unlock condition, "Buy" or "Owned" button
- [ ] Tabs or sections: Characters | Artifacts
- [ ] Accessible from Main Menu via "Store" button (bottom of screen)

**Characters**
- [ ] Arissa — default, always unlocked
- [ ] Additional character slots (placeholder art ready) — unlockable via IAP or high score milestone
- [ ] Selected character saved to PlayerPrefs, loaded by PlayerSpawner

**Artifacts (consumable packs)**
- [ ] Buy packs of 3 TimeBonus artifacts (adds to ArtifactInventory)
- [ ] Buy packs of 3 EnemyFreeze artifacts
- [ ] "Starter Pack" bundle: 3 Time + 3 Freeze + cosmetic at discount

**Currency / Pricing**
- [ ] Two tiers: real-money IAP (Google Play Billing) + free daily reward stars
- [ ] Daily login reward: 1 free artifact per day (drives retention)
- [ ] Stars earned in-game (1★/2★/3★ per run) can purchase cosmetics but not consumables

**Integration**
- [ ] `StoreManager.cs` handles IAP init, product listing, purchase callbacks
- [ ] `ArtifactInventory` already handles persistent storage (PlayerPrefs) — store just calls Add()
- [ ] Purchased characters unlock flag stored in PlayerPrefs: `Char_unlocked_1`, etc.

---

## Stage 8: Polish and Android Build

Final polish, optimization, and release-ready Android build.

**Polish**
- [ ] Add particle effect at Exit Point (subtle glow or floating particles)
- [ ] Add footstep audio on Player (surface-aware if time allows)
- [ ] Add ambient looping soundtrack (eerie/tense, loop-point set correctly)
- [ ] Smooth out any camera jitter — tune Cinemachine damping
- [ ] Review all materials — ensure no magenta/missing shader errors on Android
- [ ] Add loading screen between Main Menu and Gameplay scenes

**Optimization**
- [ ] Enable GPU Instancing on wall/floor/ceiling materials
- [ ] Combine static maze geometry using Static Batching or `MeshCombiner`
- [ ] Set texture compression to ASTC for Android in Project Settings
- [ ] Profile with Unity Profiler on target device — hit 60 FPS on Easy, 30+ on Hard
- [ ] Reduce draw calls below 200 for mid-range Android target

**Android Build**
- [ ] Set Bundle Identifier: `com.yourcompany.mazerunner`
- [ ] Set minimum API level to Android 8.0 (API 26) or as required by Google Play Billing
- [ ] Set target API level to latest supported
- [ ] Configure app icon (all required densities)
- [ ] Configure splash screen
- [ ] Enable IL2CPP scripting backend + ARM64 architecture
- [ ] Build signed AAB (Android App Bundle) with a keystore
- [ ] Test AAB on physical device via internal test track on Google Play Console
- [ ] Submit to Google Play internal testing track

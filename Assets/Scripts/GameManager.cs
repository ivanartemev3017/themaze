using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Stage 5 — Gameplay Systems.
/// Singleton. Add to MazeManager GameObject in the scene.
///
/// Flow:
///   Awake()  → reads difficulty from PlayerPrefs, resizes maze grid BEFORE MazeManager.Start()
///   OnPlayerSpawned → sets MazeShifter interval, spawns exit trigger, builds HUD
///   Update() → counts down timer; triggers Lose() at zero
///   Win()    → called by ExitTriggerHandler when Player reaches exit
/// </summary>
public class GameManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static GameManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Difficulty
    // -------------------------------------------------------------------------

    public enum Difficulty { Easy, Medium, Hard }

    [System.Serializable]
    public struct DifficultyConfig
    {
        public int   mazeWidth;
        public int   mazeHeight;
        public float timeLimit;      // seconds
        public float shiftInterval;  // seconds between wall shifts
    }

    public static Difficulty CurrentDifficulty { get; private set; } = Difficulty.Easy;

    private static readonly DifficultyConfig[] Configs =
    {
        new DifficultyConfig { mazeWidth = 15, mazeHeight = 15, timeLimit = 240f, shiftInterval = 30f },  // Easy
        new DifficultyConfig { mazeWidth = 20, mazeHeight = 20, timeLimit = 180f, shiftInterval = 20f },  // Medium
        new DifficultyConfig { mazeWidth = 25, mazeHeight = 25, timeLimit = 150f, shiftInterval = 12f },  // Hard
    };

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public enum GameState { Playing, Won, Lost }
    public GameState State { get; private set; } = GameState.Playing;

    private float _timeRemaining;
    private bool  _timerRunning;

    /// <summary>Remaining time — read by star system.</summary>
    public float TimeRemaining => _timeRemaining;

    /// <summary>Add bonus time (e.g. from artifact).</summary>
    public void AddTime(float seconds)
    {
        _timeRemaining += seconds;
    }

    // -------------------------------------------------------------------------
    // UI references
    // -------------------------------------------------------------------------

    private TextMeshProUGUI _timerText;
    private TextMeshProUGUI _winTimeLabel;
    private TextMeshProUGUI _winBestLabel;
    private Image[]          _starImages = new Image[3];
    private TextMeshProUGUI _loseTitleLabel;
    private TextMeshProUGUI _loseSubLabel;
    private GameObject      _winPanel;
    private GameObject      _losePanel;
    private GameObject      _pausePanel;
    private bool            _isPaused;
    private bool            _finalRushActive;
    private float           _finalRushTimer;
    private bool            _blackoutTriggered;
    private float           _blackoutTriggerTime;  // random time in second half

    // Artifact HUD
    private TextMeshProUGUI[] _artifactCountText  = new TextMeshProUGUI[2];
    private GameObject[]      _artifactOverlay    = new GameObject[2];
    private float[]           _artifactCollectTime = new float[2];  // Time.time when last collected

    // =========================================================================
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Screen.orientation = ScreenOrientation.LandscapeLeft;

        CurrentDifficulty = (Difficulty)PlayerPrefs.GetInt("Difficulty", 0);
        DifficultyConfig cfg = Configs[(int)CurrentDifficulty];

        // Apply maze dimensions BEFORE MazeManager.Start() calls GenerateMaze()
        var gen = FindAnyObjectByType<MazeGenerator>();
        if (gen != null)
        {
            gen.mazeWidth  = cfg.mazeWidth;
            gen.mazeHeight = cfg.mazeHeight;
        }

        _timeRemaining = cfg.timeLimit;
    }

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += HandlePlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= HandlePlayerSpawned;

    // =========================================================================
    private void HandlePlayerSpawned(GameObject player)
    {
        // Wrap entire handler so exceptions here don't block EnemySpawner / SoundManager
        try { HandlePlayerSpawnedInternal(player); }
        catch (System.Exception e)
        {
            Debug.LogError("[GameManager] HandlePlayerSpawned failed: " + e.Message + "\n" + e.StackTrace);
        }
    }

    private void HandlePlayerSpawnedInternal(GameObject player)
    {
        DifficultyConfig cfg = Configs[(int)CurrentDifficulty];

        var shifter = FindAnyObjectByType<MazeShifter>();
        shifter?.SetInterval(cfg.shiftInterval);


        CreateExitTrigger();
        BuildHUD();        // MUST run before SpawnArtifacts — event chain protection
        _timerRunning = true;

        try { SpawnArtifacts(); }
        catch (System.Exception e) { Debug.LogError("[GameManager] SpawnArtifacts failed: " + e.Message); }

        // Schedule blackout in second half of timer (random moment)
        _blackoutTriggered = false;
        float halfTime = cfg.timeLimit * 0.5f;
        _blackoutTriggerTime = Random.Range(halfTime * 0.3f, halfTime * 0.8f);
    }

    // =========================================================================
    void Update()
    {
        // Android back button / Escape
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (State == GameState.Playing) TogglePause();
        }

        if (_isPaused || !_timerRunning) return;

        _timeRemaining -= Time.deltaTime;

        if (_timerText != null)
        {
            _timerText.text  = FormatTime(_timeRemaining);
            _timerText.color = _timeRemaining <= 30f ? Color.red : Color.white;
        }

        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            Lose();
        }

        CheckFinalRush();
        CheckBlackout();
    }

    // =========================================================================
    // Win / Lose
    // =========================================================================

    public void Win()
    {
        if (State != GameState.Playing) return;
        State         = GameState.Won;
        _timerRunning = false;
        StopShifter();

        DifficultyConfig cfg = Configs[(int)CurrentDifficulty];
        float elapsed = cfg.timeLimit - _timeRemaining;

        if (_winTimeLabel != null)
            _winTimeLabel.text = "Time: " + FormatTime(elapsed);

        // Best time
        string bestKey = "BestTime_" + (int)CurrentDifficulty;
        float prevBest = PlayerPrefs.GetFloat(bestKey, float.MaxValue);
        bool isNewRecord = elapsed < prevBest;
        if (isNewRecord)
        {
            PlayerPrefs.SetFloat(bestKey, elapsed);
            PlayerPrefs.Save();
        }
        if (_winBestLabel != null)
        {
            if (prevBest < float.MaxValue && !isNewRecord)
                _winBestLabel.text = "Best: " + FormatTime(prevBest);
            else if (isNewRecord && prevBest < float.MaxValue)
                _winBestLabel.text = "NEW RECORD! (was " + FormatTime(prevBest) + ")";
            else
                _winBestLabel.text = "First clear!";
        }

        // Stars: 1 = completed, 2 = fast (under 60% of time limit), 3 = fast + all artifacts
        int stars = 1;
        bool fast = elapsed < cfg.timeLimit * 0.6f;
        bool allArtifacts = MazeArtifact.TotalSpawned == 0 || MazeArtifact.TotalCollected >= MazeArtifact.TotalSpawned;
        if (fast) stars = 2;
        if (fast && allArtifacts) stars = 3;

        for (int i = 0; i < 3; i++)
        {
            if (_starImages[i] == null) continue;
            bool lit = i < stars;
            _starImages[i].sprite = ArtifactIcons.MakeStar(lit);
            _starImages[i].color  = lit ? Color.white : new Color(0.25f, 0.25f, 0.25f);
        }

        // Persist best star count per difficulty
        string starsKey  = "Stars_" + (int)CurrentDifficulty;
        int    prevStars = PlayerPrefs.GetInt(starsKey, 0);
        if (stars > prevStars) { PlayerPrefs.SetInt(starsKey, stars); PlayerPrefs.Save(); }

        if (_winPanel != null) _winPanel.SetActive(true);
        SoundManager.Instance?.PlayWin();
    }

    public void Lose(string title = "TIME'S UP", string subtitle = "The maze consumed you.")
    {
        if (State != GameState.Playing) return;
        State         = GameState.Lost;
        _timerRunning = false;
        if (_isPaused) { Time.timeScale = 1f; _isPaused = false; }
        StopShifter();

        if (_loseTitleLabel != null) _loseTitleLabel.text = title;
        if (_loseSubLabel   != null) _loseSubLabel.text   = subtitle;
        if (_losePanel      != null) _losePanel.SetActive(true);
        SoundManager.Instance?.PlayLose();
    }

    public void TogglePause()
    {
        _isPaused      = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;
        if (_pausePanel != null) _pausePanel.SetActive(_isPaused);
    }

    // =========================================================================
    // Navigation
    // =========================================================================

    public void Retry()
    {
        DOTween.KillAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        int next = Mathf.Min((int)CurrentDifficulty + 1, (int)Difficulty.Hard);
        PlayerPrefs.SetInt("Difficulty", next);
        PlayerPrefs.Save();
        DOTween.KillAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // =========================================================================
    // Internals
    // =========================================================================

    private void CheckBlackout()
    {
        if (_blackoutTriggered) return;
        DifficultyConfig cfg = Configs[(int)CurrentDifficulty];
        float elapsed = cfg.timeLimit - _timeRemaining;
        float halfTime = cfg.timeLimit * 0.5f;

        // Only trigger in second half of timer
        if (elapsed < halfTime) return;
        if (_timeRemaining > _blackoutTriggerTime) return;

        _blackoutTriggered = true;
        StartCoroutine(BlackoutCoroutine());
    }

    private IEnumerator BlackoutCoroutine()
    {
        Debug.Log("[GameManager] BLACKOUT!");

        // Find all maze lights and turn them off
        var mazeRoot = GameObject.Find("Maze");
        var lights = new List<Light>();
        if (mazeRoot != null)
        {
            foreach (var lt in mazeRoot.GetComponentsInChildren<Light>())
                lights.Add(lt);
        }

        // Store original intensities
        var originals = new float[lights.Count];
        for (int i = 0; i < lights.Count; i++)
        {
            originals[i] = lights[i].intensity;
            lights[i].intensity = 0f;
        }

        // Also dim ambient
        Color origAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientLight = new Color(0.08f, 0.05f, 0.03f);

        yield return new WaitForSeconds(3f);

        // Restore
        for (int i = 0; i < lights.Count; i++)
            if (lights[i] != null)
                lights[i].intensity = originals[i];
        RenderSettings.ambientLight = origAmbient;

        Debug.Log("[GameManager] Blackout ended.");
    }

    private void CheckFinalRush()
    {
        if (_finalRushActive || CurrentDifficulty == Difficulty.Easy) return;

        var mgr = FindAnyObjectByType<MazeManager>();
        var spawner = FindAnyObjectByType<PlayerSpawner>();
        if (mgr == null || spawner == null || spawner.SpawnedPlayer == null) return;

        Vector3 exitPos   = mgr.ExitPosition;
        Vector3 playerPos = spawner.SpawnedPlayer.transform.position;

        // Calculate maze diagonal
        var gen = FindAnyObjectByType<MazeGenerator>();
        if (gen == null) return;
        float mazeDiag = gen.cellSize * Mathf.Sqrt(gen.mazeWidth * gen.mazeWidth + gen.mazeHeight * gen.mazeHeight);
        float triggerDist = mazeDiag * 0.3f;

        if (Vector3.Distance(playerPos, exitPos) < triggerDist)
        {
            _finalRushActive = true;
            Debug.Log("[GameManager] FINAL RUSH activated!");

            // Boost enemy speed
            var enemies = FindObjectsByType<MazeEnemy>(FindObjectsInactive.Exclude);
            foreach (var e in enemies)
                e.SetSpeedMultiplier(1.6f);

            // Max heartbeat
            SoundManager.Instance?.SetHeartbeatMax();

            // Reset enemy speed after 5 seconds
            StartCoroutine(ResetRushAfterDelay(5f));
        }
    }

    private IEnumerator ResetRushAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        var enemies = FindObjectsByType<MazeEnemy>(FindObjectsInactive.Exclude);
        foreach (var e in enemies)
            e.SetSpeedMultiplier(1f);
    }

    private static void StopShifter()
    {
        var shifter = FindAnyObjectByType<MazeShifter>();
        if (shifter != null) shifter.enabled = false;
    }

    private static void SpawnArtifacts()
    {
        MazeArtifact.TotalSpawned = 0;
        MazeArtifact.TotalCollected = 0;

        var maze = FindAnyObjectByType<MazeGenerator>();
        if (maze == null) return;

        var mgr = FindAnyObjectByType<MazeManager>();
        Vector3 playerStart = mgr != null ? mgr.StartPosition : Vector3.zero;
        Vector3 exitPos     = mgr != null ? mgr.ExitPosition  : Vector3.zero;

        // Easy: 1 time + 1 freeze. Medium/Hard: 2 time + 2 freeze.
        int timeArtifacts   = CurrentDifficulty == Difficulty.Easy ? 1 : 2;
        int freezeArtifacts = CurrentDifficulty == Difficulty.Easy ? 1 : 2;

        var usedCells = new HashSet<Vector2Int>();
        usedCells.Add(maze.WorldToCell(playerStart));
        usedCells.Add(maze.WorldToCell(exitPos));

        for (int i = 0; i < timeArtifacts; i++)
            SpawnOneArtifact(ArtifactType.TimeBonus, maze, usedCells);
        for (int i = 0; i < freezeArtifacts; i++)
            SpawnOneArtifact(ArtifactType.EnemyFreeze, maze, usedCells);

        Debug.Log($"[GameManager] Spawned {timeArtifacts} time + {freezeArtifacts} freeze artifacts ({CurrentDifficulty})");
    }

    private static void SpawnOneArtifact(ArtifactType type, MazeGenerator maze,
                                          HashSet<Vector2Int> used)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            int x = Random.Range(0, maze.mazeWidth);
            int y = Random.Range(0, maze.mazeHeight);
            var cell = new Vector2Int(x, y);
            if (used.Contains(cell)) continue;
            used.Add(cell);
            MazeArtifact.Create(type, maze.GetCellCentre(x, y));
            Debug.Log($"[GameManager] Artifact {type} spawned at cell ({x},{y})");
            return;
        }
    }

    private static void CreateExitTrigger()
    {
        var mgr = FindAnyObjectByType<MazeManager>();
        if (mgr == null) return;

        Vector3 pos   = mgr.ExitPosition;
        pos.y         = 1f;

        var go        = new GameObject("ExitTrigger");
        go.transform.position = pos;

        var sphere       = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius    = 1.8f;

        go.AddComponent<ExitTriggerHandler>();

        // Glowing green visual marker
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale    = Vector3.one * 0.6f;
        Object.Destroy(visual.GetComponent<Collider>());

        // Safe shader fallback — URP/Lit may be stripped in Android build
        var litShader  = Shader.Find("Universal Render Pipeline/Lit");
        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color");
        Material mat;
        if (litShader != null)
        {
            mat = new Material(litShader);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0f, 1f, 0.3f) * 3f);
        }
        else if (unlitShader != null)
        {
            mat = new Material(unlitShader);
            mat.color = new Color(0f, 1f, 0.3f);
        }
        else
        {
            mat = null;
        }
        if (mat != null)
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Pulse
        visual.transform.DOScale(Vector3.one * 0.85f, 0.9f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo);
    }

    // =========================================================================
    // HUD / UI
    // =========================================================================

    private void BuildHUD()
    {
        // EventSystem — required for button clicks
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            // Project uses New Input System — StandaloneInputModule uses legacy Input and spams errors
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // Root canvas
        var canvasGO       = new GameObject("GameHUD");
        var canvas         = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler                    = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution    = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight     = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // --- Timer — top-right, just below the minimap circle ---
        // Minimap: anchor(1,1), margin=16, UISize=180 → centre at x=-106 from right
        // Timer sits at y = -(16 + 180 + 8) = -204 from top
        var timerGO         = MakeNode("Timer", canvasGO);
        _timerText          = timerGO.AddComponent<TextMeshProUGUI>();
        _timerText.text     = FormatTime(_timeRemaining);
        _timerText.fontSize = 44;
        _timerText.fontStyle    = FontStyles.Bold;
        _timerText.alignment    = TextAlignmentOptions.Center;
        _timerText.color        = Color.white;
        SetRect(timerGO, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-106f, -210f), new Vector2(180f, 55f));

        // --- Artifact HUD — top-left, two tap-to-use slots ---
        BuildArtifactSlot(canvasGO, ArtifactType.TimeBonus,  new Vector2(58f,  -58f));
        BuildArtifactSlot(canvasGO, ArtifactType.EnemyFreeze, new Vector2(168f, -58f));

        // --- Win panel ---
        _winPanel = MakeFullscreenPanel(canvasGO, "WinPanel", new Color(0f, 0f, 0f, 0.88f));
        MakeLabel(_winPanel, "Title", "MAZE ESCAPED!", 80, new Color(0.1f, 1f, 0.4f), new Vector2(0, 180f), new Vector2(800, 110));

        // Star images (procedural sprites, no unicode dependency)
        for (int si = 0; si < 3; si++)
        {
            var sGO = MakeNode("Star" + si, _winPanel);
            var sRT = sGO.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.5f, 0.5f);
            sRT.anchorMax = new Vector2(0.5f, 0.5f);
            sRT.pivot     = new Vector2(0.5f, 0.5f);
            sRT.anchoredPosition = new Vector2((si - 1) * 66f, 95f);
            sRT.sizeDelta = new Vector2(56f, 56f);
            var img = sGO.AddComponent<Image>();
            img.sprite = ArtifactIcons.MakeStar(false);
            img.color  = new Color(0.25f, 0.25f, 0.25f);
            _starImages[si] = img;
        }

        _winTimeLabel  = MakeLabel(_winPanel, "TimeTaken", "",    40, Color.white,                   new Vector2(0,   30f), new Vector2(600,  55));
        _winBestLabel  = MakeLabel(_winPanel, "BestTime",  "",    32, new Color(0.7f, 0.7f, 0.7f),   new Vector2(0,  -20f), new Vector2(600,  45));
        MakeButton(_winPanel, "BtnRetry",    "RETRY",       new Vector2(0,  -90f), new Vector2(340, 90), () => Retry());
        MakeButton(_winPanel, "BtnMainMenu", "MAIN MENU",   new Vector2(0, -195f), new Vector2(340, 70), () => { DOTween.KillAll(); SceneManager.LoadScene(0); });
        _winPanel.SetActive(false);

        // --- Lose panel ---
        _losePanel      = MakeFullscreenPanel(canvasGO, "LosePanel", new Color(0f, 0f, 0f, 0.88f));
        _loseTitleLabel = MakeLabel(_losePanel, "Title", "TIME'S UP",              80, new Color(1f, 0.18f, 0.18f),   new Vector2(0, 130f), new Vector2(700, 110));
        _loseSubLabel   = MakeLabel(_losePanel, "Sub",   "The maze consumed you.", 36, new Color(0.6f, 0.6f, 0.6f),  new Vector2(0,  10f), new Vector2(650,  60));
        MakeButton(_losePanel, "BtnRetry",    "RETRY",     new Vector2(0,  -80f), new Vector2(340, 90), () => Retry());
        MakeButton(_losePanel, "BtnMainMenu", "MAIN MENU", new Vector2(0, -185f), new Vector2(340, 70), () => { DOTween.KillAll(); SceneManager.LoadScene(0); });
        _losePanel.SetActive(false);

        // --- Camera toggle button — bottom-left, circular lens style ---
        var camBtnGO = MakeNode("CamBtn", canvasGO);
        // 82×82 px circular button anchored bottom-left
        SetRect(camBtnGO, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(50f, 50f), new Vector2(82f, 82f));

        // Circular background (lens body)
        var camBgImg = camBtnGO.AddComponent<Image>();
        camBgImg.sprite = MakeCircleSprite(64);
        camBgImg.color  = new Color(0.05f, 0.05f, 0.07f, 0.90f);

        var camBtn   = camBtnGO.AddComponent<Button>();
        camBtn.targetGraphic = camBgImg;
        var camCb    = camBtn.colors;
        camCb.normalColor      = Color.white;
        camCb.highlightedColor = new Color(1.30f, 1.30f, 1.30f, 1.0f);
        camCb.pressedColor     = new Color(0.65f, 0.65f, 0.65f, 1.0f);
        camBtn.colors = camCb;

        // Outer lens ring
        var ringGO  = MakeNode("LensRing", camBtnGO);
        var ringImg = ringGO.AddComponent<Image>();
        ringImg.sprite        = MakeLensRingSprite(64);
        ringImg.color         = new Color(0.30f, 0.30f, 0.38f, 0.85f);
        ringImg.raycastTarget = false;
        var ringRT            = ringGO.GetComponent<RectTransform>();
        ringRT.anchorMin      = Vector2.zero;
        ringRT.anchorMax      = Vector2.one;
        ringRT.offsetMin      = new Vector2(-3f, -3f);
        ringRT.offsetMax      = new Vector2( 3f,  3f);

        // Camera icon (centered inside lens)
        var camIconGO  = MakeNode("CamIcon", camBtnGO);
        var camIconRT  = camIconGO.AddComponent<RectTransform>();
        camIconRT.anchorMin = new Vector2(0.14f, 0.24f);
        camIconRT.anchorMax = new Vector2(0.86f, 0.76f);
        camIconRT.offsetMin = Vector2.zero;
        camIconRT.offsetMax = Vector2.zero;
        var camIconImg = camIconGO.AddComponent<Image>();
        camIconImg.sprite        = MakeCameraIconSprite(64);
        camIconImg.color         = Color.white;
        camIconImg.raycastTarget = false;

        // Mode label "3P" / "1P" at the bottom of the lens
        var modeLblGO = MakeNode("ModeLabel", camBtnGO);
        SetRect(modeLblGO, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 9f), new Vector2(55f, 18f));
        var modeTmp           = modeLblGO.AddComponent<TextMeshProUGUI>();
        modeTmp.text          = "3P";
        modeTmp.fontSize      = 13f;
        modeTmp.fontStyle     = FontStyles.Bold;
        modeTmp.alignment     = TextAlignmentOptions.Center;
        modeTmp.color         = new Color(0.72f, 0.72f, 0.82f, 0.92f);
        modeTmp.raycastTarget = false;

        camBtn.onClick.AddListener(() => {
            FollowCamera.Instance?.ToggleCameraMode();
            if (modeTmp != null)
                modeTmp.text = (FollowCamera.Instance != null && FollowCamera.Instance.IsFirstPerson)
                    ? "1P" : "3P";
        });

        // --- Pause panel (back button) ---
        _pausePanel = MakeFullscreenPanel(canvasGO, "PausePanel", new Color(0f, 0f, 0f, 0.80f));
        MakeLabel(_pausePanel, "Title", "PAUSED", 80, Color.white, new Vector2(0, 130f), new Vector2(500, 110));
        MakeButton(_pausePanel, "BtnResume",   "RESUME",    new Vector2(0,  10f), new Vector2(340, 90), () => TogglePause());
        MakeButton(_pausePanel, "BtnMainMenu", "MAIN MENU", new Vector2(0, -100f), new Vector2(340, 90), () => { Time.timeScale = 1f; DOTween.KillAll(); SceneManager.LoadScene(0); });
        _pausePanel.SetActive(false);
    }

    // ---- UI helpers ----

    static GameObject MakeNode(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static GameObject MakeFullscreenPanel(GameObject parent, string name, Color color)
    {
        var go  = MakeNode(name, parent);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        return go;
    }

    static TextMeshProUGUI MakeLabel(GameObject parent, string name, string text,
                                     float fontSize, Color color,
                                     Vector2 pos, Vector2 size)
    {
        var go          = MakeNode(name, parent);
        var tmp         = go.AddComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = fontSize;
        tmp.alignment   = TextAlignmentOptions.Center;
        tmp.color       = color;
        SetRect(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        return tmp;
    }

    static void MakeButton(GameObject parent, string name, string label,
                           Vector2 pos, Vector2 size, System.Action onClick)
    {
        var go  = MakeNode(name, parent);
        SetRect(go, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

        var img         = go.AddComponent<Image>();
        img.color       = new Color(0.13f, 0.13f, 0.13f, 1f);

        var btn         = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb          = btn.colors;
        cb.highlightedColor = new Color(0.28f, 0.28f, 0.28f);
        cb.pressedColor     = new Color(0.45f, 0.45f, 0.45f);
        btn.colors      = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // Text inside button
        var lbl         = MakeNode("Label", go);
        var lrt         = lbl.AddComponent<RectTransform>();
        lrt.anchorMin   = Vector2.zero;
        lrt.anchorMax   = Vector2.one;
        lrt.offsetMin   = Vector2.zero;
        lrt.offsetMax   = Vector2.zero;
        var tmp         = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text        = label;
        tmp.fontSize    = 44;
        tmp.fontStyle   = FontStyles.Bold;
        tmp.alignment   = TextAlignmentOptions.Center;
        tmp.color       = Color.white;
    }

    static void SetRect(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        // Must use explicit == null check — Unity fake-null fools the ?? operator
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
    }

    static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    /// <summary>
    /// Procedural camera icon — white silhouette on transparent background.
    /// Body rectangle + lens ring + viewfinder bump + flash bump.
    /// </summary>
    private static Sprite MakeCameraIconSprite(int sz = 64)
    {
        var pixels = new Color[sz * sz];
        // All transparent to start
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

        void FillRect(int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (x >= 0 && x < sz && y >= 0 && y < sz)
                    pixels[y * sz + x] = Color.white;
        }
        void FillCircle(int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if ((x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r && x>=0 && x<sz && y>=0 && y<sz)
                    pixels[y * sz + x] = Color.white;
        }
        void ClearCircle(int cx, int cy, int r)
        {
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if ((x-cx)*(x-cx)+(y-cy)*(y-cy) <= r*r && x>=0 && x<sz && y>=0 && y<sz)
                    pixels[y * sz + x] = Color.clear;
        }

        // Camera body
        FillRect(2, 9, 61, 42);
        // Viewfinder bump (top-center)
        FillRect(14, 42, 46, 52);
        // Flash bump (top-right)
        FillRect(49, 42, 61, 52);
        // Lens ring
        FillCircle(31, 26, 14);
        ClearCircle(31, 26, 8);
        // Small shine dot inside lens
        FillCircle(25, 21, 3);

        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), Vector2.one * 0.5f);
    }

    /// <summary>Filled circle sprite — used as circular button background.</summary>
    private static Sprite MakeCircleSprite(int size = 64)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - half + 0.5f, dy = y - half + 0.5f;
            pixels[y * size + x] = dx * dx + dy * dy <= half * half ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Ring sprite (circle outline) — used as camera lens ring overlay.</summary>
    private static Sprite MakeLensRingSprite(int size = 64, int thickness = 5)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        var pixels = new Color[size * size];
        float r2Outer = half * half;
        float r2Inner = (half - thickness) * (half - thickness);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - half + 0.5f, dy = y - half + 0.5f;
            float d2 = dx * dx + dy * dy;
            pixels[y * size + x] = (d2 <= r2Outer && d2 >= r2Inner) ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // =========================================================================
    // Artifact HUD — top-left corner, two tap-to-use slots
    // =========================================================================

    private void BuildArtifactSlot(GameObject canvas, ArtifactType type, Vector2 pos)
    {
        int idx = (int)type;

        // Slot root
        var slotGO = MakeNode("ArtSlot_" + type, canvas);
        SetRect(slotGO, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, new Vector2(95f, 95f));

        var bg    = slotGO.AddComponent<Image>();
        bg.color  = new Color(0.04f, 0.04f, 0.10f, 0.88f);

        var btn   = slotGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb    = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cb;
        var capturedType = type;
        btn.onClick.AddListener(() => UseArtifact(capturedType));

        // Border glow
        Color borderCol = type == ArtifactType.TimeBonus
            ? new Color(0.8f, 0.55f, 0.0f, 0.7f)
            : new Color(0.0f, 0.6f, 0.9f, 0.7f);
        var borderGO = MakeNode("Border", slotGO);
        var bRT = borderGO.AddComponent<RectTransform>();
        bRT.anchorMin = Vector2.zero; bRT.anchorMax = Vector2.one;
        bRT.offsetMin = new Vector2(-2f, -2f); bRT.offsetMax = new Vector2(2f, 2f);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = borderCol;

        // Icon (procedural sprite)
        var iconGO = MakeNode("Icon", slotGO);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin        = new Vector2(0.5f, 0.5f);
        iconRT.anchorMax        = new Vector2(0.5f, 0.5f);
        iconRT.pivot            = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(0f, 6f);
        iconRT.sizeDelta        = new Vector2(68f, 68f);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite          = ArtifactIcons.Make(type);
        iconImg.preserveAspect  = true;

        // Count badge
        var badgeGO = MakeNode("Badge", slotGO);
        var badgeRT = badgeGO.AddComponent<RectTransform>();
        badgeRT.anchorMin        = new Vector2(1f, 0f);
        badgeRT.anchorMax        = new Vector2(1f, 0f);
        badgeRT.pivot            = new Vector2(1f, 0f);
        badgeRT.anchoredPosition = Vector2.zero;
        badgeRT.sizeDelta        = new Vector2(32f, 32f);
        var badgeImg = badgeGO.AddComponent<Image>();
        badgeImg.color = type == ArtifactType.TimeBonus
            ? new Color(0.55f, 0.35f, 0.0f, 1f)
            : new Color(0.0f,  0.25f, 0.45f, 1f);

        var cntGO = MakeNode("Cnt", badgeGO);
        var cntRT = cntGO.AddComponent<RectTransform>();
        cntRT.anchorMin = Vector2.zero; cntRT.anchorMax = Vector2.one;
        cntRT.offsetMin = Vector2.zero; cntRT.offsetMax = Vector2.zero;
        var cntTMP          = cntGO.AddComponent<TextMeshProUGUI>();
        cntTMP.fontSize     = 20f;
        cntTMP.fontStyle    = FontStyles.Bold;
        cntTMP.alignment    = TextAlignmentOptions.Center;
        cntTMP.color        = Color.white;
        _artifactCountText[idx] = cntTMP;

        // Disabled dark overlay (shown when count = 0)
        var ov = MakeNode("Overlay", slotGO);
        var ovRT = ov.AddComponent<RectTransform>();
        ovRT.anchorMin = Vector2.zero; ovRT.anchorMax = Vector2.one;
        ovRT.offsetMin = Vector2.zero; ovRT.offsetMax = Vector2.zero;
        ov.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.60f);
        _artifactOverlay[idx] = ov;

        RefreshOneArtifactSlot(type);
    }

    private void RefreshOneArtifactSlot(ArtifactType type)
    {
        int idx   = (int)type;
        int count = ArtifactInventory.GetCount(type);
        if (_artifactCountText[idx] != null)
            _artifactCountText[idx].text = count.ToString();
        if (_artifactOverlay[idx] != null)
            _artifactOverlay[idx].SetActive(count <= 0);
    }

    /// <summary>Called by MazeArtifact on pickup — refreshes HUD and stamps collect time to prevent same-touch use.</summary>
    public void OnArtifactCollected(ArtifactType type)
    {
        _artifactCollectTime[(int)type] = Time.time;
        RefreshArtifactHUD();
    }

    /// <summary>Refresh both artifact slot displays (call after collect or use).</summary>
    public void RefreshArtifactHUD()
    {
        RefreshOneArtifactSlot(ArtifactType.TimeBonus);
        RefreshOneArtifactSlot(ArtifactType.EnemyFreeze);
    }

    /// <summary>Spend one artifact from inventory and apply its effect.</summary>
    public void UseArtifact(ArtifactType type)
    {
        if (State != GameState.Playing) return;
        // Guard: ignore taps within 0.5s of collecting — prevents same-touch auto-use on Android
        if (Time.time - _artifactCollectTime[(int)type] < 0.5f) return;
        if (!ArtifactInventory.TryUse(type)) return;

        switch (type)
        {
            case ArtifactType.TimeBonus:
                AddTime(30f);
                ShowArtifactFeedback("+30 SECONDS", new Color(1f, 0.78f, 0.1f));
                break;
            case ArtifactType.EnemyFreeze:
                var enemies = FindObjectsByType<MazeEnemy>(FindObjectsInactive.Exclude);
                foreach (var e in enemies) e.Freeze(8f);
                ShowArtifactFeedback("ENEMIES FROZEN", new Color(0.2f, 0.85f, 1f));
                break;
        }

        RefreshArtifactHUD();
    }

    private static void ShowArtifactFeedback(string text, Color color)
    {
        var go     = new GameObject("ArtifactFeedback");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 25;
        go.AddComponent<CanvasScaler>();

        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f; cg.interactable = false; cg.blocksRaycasts = false;

        var txtGO = new GameObject("T");
        txtGO.transform.SetParent(go.transform, false);
        var rt          = txtGO.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.5f, 0.5f);
        rt.anchorMax    = new Vector2(0.5f, 0.5f);
        rt.pivot        = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta    = new Vector2(500f, 65f);

        var tmp       = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 44f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color;

        rt.DOAnchorPosY(110f, 2.0f).SetEase(Ease.OutCubic);
        DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 2.0f)
               .SetEase(Ease.InCubic)
               .OnComplete(() => Object.Destroy(go));
    }
}

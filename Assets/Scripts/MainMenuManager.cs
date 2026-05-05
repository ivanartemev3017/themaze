using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Main Menu — landscape layout (1920×1080 reference).
/// Design adapted from maze_redesign.html: dark theme, #E8510E orange accent,
/// grid background, reference-style diff cards and nav buttons.
/// Store: two-column landscape layout (chars left / maps right).
/// Records: full-width rows with colored dot + time.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    private const int GameplaySceneIndex = 1;

    // ── Palette (from maze_redesign.html) ───────────────────────────────────
    private static readonly Color ColBg        = new Color(0.031f, 0.024f, 0.016f); // #080604
    private static readonly Color ColAccent    = new Color(0.910f, 0.318f, 0.055f); // #E8510E
    private static readonly Color ColAccentDim = new Color(0.910f, 0.318f, 0.055f, 0.18f);
    private static readonly Color ColSurface   = new Color(0.094f, 0.067f, 0.031f); // #181108
    private static readonly Color ColSurface2  = new Color(0.133f, 0.086f, 0.031f); // #221608
    private static readonly Color ColGold      = new Color(0.961f, 0.773f, 0.094f); // #f5c518
    private static readonly Color ColText      = new Color(0.941f, 0.910f, 0.847f); // #f0e8d8
    private static readonly Color ColTextDim   = new Color(0.502f, 0.376f, 0.314f); // #806050

    // Difficulty fg/bg (reference: green / yellow / red)
    private static readonly Color ColEasyFg  = new Color(0.298f, 0.686f, 0.298f);         // #4caf4c
    private static readonly Color ColEasyBg  = new Color(0.298f, 0.686f, 0.298f, 0.10f);
    private static readonly Color ColMedFg   = new Color(0.961f, 0.773f, 0.094f);         // #f5c518
    private static readonly Color ColMedBg   = new Color(0.961f, 0.773f, 0.094f, 0.08f);
    private static readonly Color ColHardFg  = new Color(0.878f, 0.314f, 0.314f);         // #e05050
    private static readonly Color ColHardBg  = new Color(0.878f, 0.314f, 0.314f, 0.10f);

    private GameManager.Difficulty _selected;
    private Image[]  _cardBg     = new Image[3];
    private Image[]  _cardBorder = new Image[3];
    private CanvasGroup _fadeGroup;
    private TextMeshProUGUI _titleMaze;
    private GameObject _storePage;
    private GameObject _recordsPage;

    private string _selectedChar;
    private string _selectedMap;
    private Image[]           _charCardBgs   = new Image[2];
    private Image[]           _charCardBords = new Image[2];
    private TextMeshProUGUI[] _charBadges    = new TextMeshProUGUI[2];
    private Image[]           _mapCardBgs    = new Image[2];
    private Image[]           _mapCardBords  = new Image[2];
    private TextMeshProUGUI[] _mapBadges     = new TextMeshProUGUI[2];

    // =========================================================================
    void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        DOTween.Init();
        _selected     = (GameManager.Difficulty)PlayerPrefs.GetInt("Difficulty", 0);
        _selectedChar = PlayerPrefs.GetString("SelectedCharacter", "arissa");
        _selectedMap  = PlayerPrefs.GetString("SelectedBiome", "dungeon");
    }

    void Start()
    {
        EnsureEventSystem();

        var canvasGO = new GameObject("MainMenuCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        BuildBackground(canvasGO);
        BuildSeparator(canvasGO);
        BuildLeftPanel(canvasGO);
        BuildRightPanel(canvasGO);
        BuildVignette(canvasGO);
        BuildParticles(canvasGO);
        BuildStorePage(canvasGO);
        BuildRecordsPage(canvasGO);
        BuildFadeOverlay();

        RefreshDiffButtons();

        var cg   = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        DOTween.To(() => cg.alpha, x => cg.alpha = x, 1f, 0.7f)
               .SetEase(Ease.OutQuad)
               .OnComplete(() => DOVirtual.DelayedCall(0.1f, StartFlicker));
    }

    // =========================================================================
    // Background — subtle grid pattern (reference: bg-grid)
    // =========================================================================
    private static void BuildBackground(GameObject canvas)
    {
        var bg = MakeStretch("BG", canvas);
        bg.AddComponent<Image>().color = ColBg;

        // Horizontal grid lines
        int hLines = 8;
        for (int i = 1; i < hLines; i++)
            SpawnLine(canvas, new Vector2(0.5f, i / (float)hLines), new Vector2(1920f, 1f),
                      new Color(0.910f, 0.318f, 0.055f, 0.035f));

        // Vertical grid lines
        int vLines = 14;
        for (int i = 1; i < vLines; i++)
            SpawnLine(canvas, new Vector2(i / (float)vLines, 0.5f), new Vector2(1f, 1080f),
                      new Color(0.910f, 0.318f, 0.055f, 0.035f));
    }

    private static void SpawnLine(GameObject parent, Vector2 anchor, Vector2 size, Color col)
    {
        var go = MakeNode("Line", parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = size;
        go.AddComponent<Image>().color = col;
    }

    // Center vertical separator dividing left / right panels
    private static void BuildSeparator(GameObject canvas)
    {
        var go = MakeNode("Separator", canvas);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(2f, 820f);
        go.AddComponent<Image>().color = new Color(0.22f, 0.10f, 0.04f, 0.55f);
    }

    // =========================================================================
    // Left panel — THE / MAZE / tagline
    // =========================================================================
    private void BuildLeftPanel(GameObject canvas)
    {
        var panel   = MakeNode("LeftPanel", canvas);
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0f, 0f);
        panelRT.anchorMax = new Vector2(0.5f, 1f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // "T H E"
        var theGO  = MakeNode("TitleThe", panel);
        var theTMP = AddTMP(theGO, new Vector2(0.5f, 0.5f), new Vector2(0f, 335f), new Vector2(580f, 38f));
        theTMP.text             = "T H E";
        theTMP.fontSize         = 22f;
        theTMP.fontStyle        = FontStyles.Bold;
        theTMP.alignment        = TextAlignmentOptions.Center;
        theTMP.color            = ColTextDim;
        theTMP.characterSpacing = 12f;

        // "MAZE" — big accent title
        var mazeGO = MakeNode("TitleMaze", panel);
        _titleMaze = AddTMP(mazeGO, new Vector2(0.5f, 0.5f), new Vector2(0f, 185f), new Vector2(730f, 230f));
        _titleMaze.text             = "MAZE";
        _titleMaze.fontSize         = 172f;
        _titleMaze.fontStyle        = FontStyles.Bold;
        _titleMaze.alignment        = TextAlignmentOptions.Center;
        _titleMaze.color            = ColAccent;
        _titleMaze.characterSpacing = -3f;

        // Orange divider line (animates width in)
        var divGO = MakeNode("Divider", panel);
        var divRT = divGO.AddComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0.5f, 0.5f);
        divRT.anchorMax        = new Vector2(0.5f, 0.5f);
        divRT.pivot            = new Vector2(0.5f, 0.5f);
        divRT.anchoredPosition = new Vector2(0f, 72f);
        divRT.sizeDelta        = new Vector2(0f, 2f);
        divGO.AddComponent<Image>().color = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.7f);
        DOVirtual.DelayedCall(0.4f, () =>
            divRT.DOSizeDelta(new Vector2(520f, 2f), 0.55f).SetEase(Ease.OutExpo));

        // Tagline
        var tagGO  = MakeNode("Tagline", panel);
        var tagTMP = AddTMP(tagGO, new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(600f, 34f));
        tagTMP.text             = "ESCAPE BEFORE THE WALLS CLOSE IN";
        tagTMP.fontSize         = 17f;
        tagTMP.alignment        = TextAlignmentOptions.Center;
        tagTMP.color            = new Color(0.38f, 0.28f, 0.20f);
        tagTMP.characterSpacing = 5f;
    }

    // =========================================================================
    // Right panel — difficulty cards + PLAY + nav row
    // =========================================================================
    private void BuildRightPanel(GameObject canvas)
    {
        var panel   = MakeNode("RightPanel", canvas);
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0f);
        panelRT.anchorMax = new Vector2(1f, 1f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // "— SELECT DIFFICULTY —"
        var lblGO  = MakeNode("DiffLabel", panel);
        var lblTMP = AddTMP(lblGO, new Vector2(0.5f, 0.5f), new Vector2(0f, 348f), new Vector2(640f, 36f));
        lblTMP.text             = "— SELECT DIFFICULTY —";
        lblTMP.fontSize         = 19f;
        lblTMP.alignment        = TextAlignmentOptions.Center;
        lblTMP.color            = ColTextDim;
        lblTMP.characterSpacing = 4f;

        // 3 difficulty cards
        string[] names  = { "EASY",    "MEDIUM",   "HARD"    };
        string[] sizes  = { "15 × 15", "20 × 20",  "25 × 25" };
        string[] times  = { "4:00",    "3:00",     "2:30"    };
        Color[]  fgCols = { ColEasyFg, ColMedFg,   ColHardFg };
        Color[]  bgCols = { ColEasyBg, ColMedBg,   ColHardBg };
        float[]  cardX  = { -200f,     0f,          200f      };

        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            float best = PlayerPrefs.GetFloat("BestTime_" + idx, float.MaxValue);
            string bestStr = best < float.MaxValue ? MenuFormatTime(best) : "—";
            BuildDiffCard(panel, names[i], sizes[i], times[i], bestStr,
                          fgCols[i], bgCols[i], cardX[i], 205f, idx,
                          () => SelectDifficulty((GameManager.Difficulty)idx));
        }

        // ── PLAY button ──────────────────────────────────────────────────────
        var playGO = MakeNode("PlayBtn", panel);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0.5f);
        playRT.anchorMax        = new Vector2(0.5f, 0.5f);
        playRT.pivot            = new Vector2(0.5f, 0.5f);
        playRT.anchoredPosition = new Vector2(0f, 52f);
        playRT.sizeDelta        = new Vector2(490f, 88f);

        var playImg = playGO.AddComponent<Image>();
        playImg.color = ColAccent;
        var playBtn = playGO.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        var pcb = playBtn.colors;
        pcb.normalColor      = Color.white;
        pcb.highlightedColor = new Color(1.12f, 1.08f, 1.05f);
        pcb.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        playBtn.colors = pcb;
        playBtn.onClick.AddListener(StartGame);

        AddStretchLabel(playGO, "PLAY", 46f, FontStyles.Bold, Color.white, 10f);
        playGO.transform.DOScale(1.03f, 1.1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        // ── Nav row: STORE | RECORDS ─────────────────────────────────────────
        BuildNavButton(panel, "STORE", -128f, -68f, ShowStorePage);
        BuildNavButton(panel, "RECORDS", 128f, -68f, ShowRecordsPage);
    }

    // Reference-style nav button: surface bg + orange-dim border + bold label
    private void BuildNavButton(GameObject panel, string label, float x, float y, System.Action onClick)
    {
        // Outer border frame
        var frameGO = MakeNode("NavFrame_" + label, panel);
        var frameRT = frameGO.AddComponent<RectTransform>();
        frameRT.anchorMin        = new Vector2(0.5f, 0.5f);
        frameRT.anchorMax        = new Vector2(0.5f, 0.5f);
        frameRT.pivot            = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(x, y);
        frameRT.sizeDelta        = new Vector2(230f, 62f);
        frameGO.AddComponent<Image>().color = ColAccentDim;

        // Inner surface (inset 1px)
        var innerGO = MakeNode("Inner", frameGO);
        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(1f, 1f);
        innerRT.offsetMax = new Vector2(-1f, -1f);
        var innerImg = innerGO.AddComponent<Image>();
        innerImg.color = ColSurface;

        // Clickable button on the frame
        var btn = frameGO.AddComponent<Button>();
        btn.targetGraphic = innerImg;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        cb.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        AddStretchLabel(frameGO, label, 21f, FontStyles.Bold, ColTextDim, 5f);
    }

    // Reference-style diff card: tinted bg + colored border on select
    private void BuildDiffCard(GameObject panel,
                                string name, string size, string time, string best,
                                Color fgCol, Color bgCol,
                                float x, float y, int idx,
                                System.Action onClick)
    {
        // Outer border (transparent when deselected, fgCol when selected)
        var frame   = MakeNode("DiffFrame" + idx, panel);
        var frameRT = frame.AddComponent<RectTransform>();
        frameRT.anchorMin        = new Vector2(0.5f, 0.5f);
        frameRT.anchorMax        = new Vector2(0.5f, 0.5f);
        frameRT.pivot            = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(x, y);
        frameRT.sizeDelta        = new Vector2(178f, 148f);

        var frameBg = frame.AddComponent<Image>();
        frameBg.color       = Color.clear;
        _cardBorder[idx]    = frameBg;

        var btn = frame.AddComponent<Button>();
        btn.targetGraphic = frameBg;
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // Inner tinted bg (inset 2px)
        var inner   = MakeNode("Inner", frame);
        var innerRT = inner.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero;
        innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(2f, 2f);
        innerRT.offsetMax = new Vector2(-2f, -2f);
        var innerImg   = inner.AddComponent<Image>();
        innerImg.color = bgCol;
        _cardBg[idx]   = innerImg;

        // Name — top 38% (colored, bold)
        var nGO = MakeNode("Name", frame);
        var nRT = nGO.AddComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 0.62f); nRT.anchorMax = new Vector2(1f, 1f);
        nRT.offsetMin = Vector2.zero; nRT.offsetMax = Vector2.zero;
        var nTMP = nGO.AddComponent<TextMeshProUGUI>();
        nTMP.text = name; nTMP.fontSize = 28f; nTMP.fontStyle = FontStyles.Bold;
        nTMP.alignment = TextAlignmentOptions.Center; nTMP.color = fgCol;

        // Grid size — middle dim row
        var sGO = MakeNode("Size", frame);
        var sRT = sGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.44f); sRT.anchorMax = new Vector2(1f, 0.62f);
        sRT.offsetMin = Vector2.zero; sRT.offsetMax = Vector2.zero;
        var sTMP = sGO.AddComponent<TextMeshProUGUI>();
        sTMP.text = size; sTMP.fontSize = 15f;
        sTMP.alignment = TextAlignmentOptions.Center; sTMP.color = ColTextDim;

        // Time — colored, medium
        var tGO = MakeNode("Time", frame);
        var tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0f, 0.24f); tRT.anchorMax = new Vector2(1f, 0.44f);
        tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
        var tTMP = tGO.AddComponent<TextMeshProUGUI>();
        tTMP.text = time; tTMP.fontSize = 20f; tTMP.fontStyle = FontStyles.Bold;
        tTMP.alignment = TextAlignmentOptions.Center; tTMP.color = fgCol;

        // Best time — dim, small
        var bGO = MakeNode("Best", frame);
        var bRT = bGO.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0f, 0f); bRT.anchorMax = new Vector2(1f, 0.24f);
        bRT.offsetMin = Vector2.zero; bRT.offsetMax = Vector2.zero;
        var bTMP = bGO.AddComponent<TextMeshProUGUI>();
        bTMP.text = "Best  " + best; bTMP.fontSize = 13f;
        bTMP.alignment = TextAlignmentOptions.Center; bTMP.color = ColTextDim;
    }

    private void RefreshDiffButtons()
    {
        Color[] fgCols = { ColEasyFg, ColMedFg,  ColHardFg };
        Color[] bgCols = { ColEasyBg, ColMedBg,  ColHardBg };
        for (int i = 0; i < 3; i++)
        {
            bool on = (i == (int)_selected);
            if (_cardBg[i]     != null)
                _cardBg[i].color = on
                    ? new Color(fgCols[i].r, fgCols[i].g, fgCols[i].b, 0.20f)
                    : bgCols[i];
            if (_cardBorder[i] != null)
                _cardBorder[i].color = on ? fgCols[i] : Color.clear;
        }
    }

    private void SelectDifficulty(GameManager.Difficulty d)
    {
        _selected = d;
        RefreshDiffButtons();
    }

    // =========================================================================
    // Vignette (dark edges)
    // =========================================================================
    private static void BuildVignette(GameObject canvas)
    {
        var v = MakeStretch("Vignette", canvas);
        EdgePanel(v, "VL", new Vector2(0,0), new Vector2(0,1), new Vector2(0,0),    new Vector2(220,0),  new Color(0,0,0,0.80f));
        EdgePanel(v, "VR", new Vector2(1,0), new Vector2(1,1), new Vector2(-220,0), new Vector2(0,0),    new Color(0,0,0,0.80f));
        EdgePanel(v, "VT", new Vector2(0,1), new Vector2(1,1), new Vector2(0,-160), new Vector2(0,0),    new Color(0,0,0,0.65f));
        EdgePanel(v, "VB", new Vector2(0,0), new Vector2(1,0), new Vector2(0,0),    new Vector2(0,160),  new Color(0,0,0,0.65f));
    }

    private static void EdgePanel(GameObject parent, string name,
                                   Vector2 amin, Vector2 amax,
                                   Vector2 omin, Vector2 omax, Color col)
    {
        var go = MakeNode(name, parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = amin; rt.anchorMax = amax;
        rt.offsetMin = omin; rt.offsetMax = omax;
        go.AddComponent<Image>().color = col;
    }

    // =========================================================================
    // Floating ember particles
    // =========================================================================
    private static void BuildParticles(GameObject canvas)
    {
        var container = MakeStretch("Particles", canvas);
        var rng = new System.Random(7);
        for (int i = 0; i < 14; i++)
        {
            float sx   = (float)(rng.NextDouble() * 1800 - 900);
            float sy   = (float)(rng.NextDouble() * 800 - 500);
            float sz   = (float)(rng.NextDouble() * 5 + 2);
            float dur  = (float)(rng.NextDouble() * 6 + 4);
            float del  = (float)(rng.NextDouble() * 7);
            float rise = (float)(rng.NextDouble() * 260 + 160);
            Color col  = Color.Lerp(ColAccent, ColGold, (float)rng.NextDouble());
            col.a      = (float)(rng.NextDouble() * 0.35 + 0.08);

            var go  = MakeNode("E" + i, container);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(sx, sy);
            rt.sizeDelta        = new Vector2(sz, sz);
            var img = go.AddComponent<Image>();
            img.color = col;

            var seq = DOTween.Sequence().SetDelay(del).SetLoops(-1);
            seq.Append(rt.DOAnchorPosY(sy + rise, dur).SetEase(Ease.InOutSine));
            seq.Join(img.DOFade(0f, dur * 0.55f).SetDelay(dur * 0.45f));
            seq.AppendCallback(() => { rt.anchoredPosition = new Vector2(sx, sy); img.color = col; });
        }
    }

    // =========================================================================
    // Black fade overlay
    // =========================================================================
    private void BuildFadeOverlay()
    {
        var go = new GameObject("FadeCanvas");
        var fc = go.AddComponent<Canvas>();
        fc.renderMode   = RenderMode.ScreenSpaceOverlay;
        fc.sortingOrder = 100;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var panel   = MakeNode("P", go);
        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero; panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero; panelRT.offsetMax = Vector2.zero;
        panel.AddComponent<Image>().color = Color.black;

        _fadeGroup               = go.AddComponent<CanvasGroup>();
        _fadeGroup.alpha         = 0f;
        _fadeGroup.blocksRaycasts = false;
    }

    // =========================================================================
    // Title flicker animation
    // =========================================================================
    private void StartFlicker()
    {
        void Schedule()
        {
            DOVirtual.DelayedCall(Random.Range(3f, 7f), () =>
            {
                if (_titleMaze == null) return;
                Color dim = new Color(ColAccent.r * 0.35f, ColAccent.g * 0.35f, ColAccent.b * 0.35f);
                var seq = DOTween.Sequence();
                seq.Append(_titleMaze.DOColor(dim, 0.04f));
                seq.AppendInterval(Random.Range(0.04f, 0.10f));
                seq.Append(_titleMaze.DOColor(ColAccent, 0.03f));
                if (Random.value > 0.4f)
                {
                    seq.AppendInterval(0.07f);
                    seq.Append(_titleMaze.DOColor(dim, 0.04f));
                    seq.AppendInterval(0.05f);
                    seq.Append(_titleMaze.DOColor(ColAccent, 0.03f));
                }
                seq.OnComplete(() => Schedule());
            });
        }
        Schedule();
    }

    // =========================================================================
    // Start game
    // =========================================================================
    private void StartGame()
    {
        PlayerPrefs.SetInt("Difficulty", (int)_selected);
        PlayerPrefs.Save();
        if (_fadeGroup != null)
        {
            _fadeGroup.blocksRaycasts = true;
            DOTween.To(() => _fadeGroup.alpha, x => _fadeGroup.alpha = x, 1f, 0.5f)
                   .SetEase(Ease.InQuad)
                   .OnComplete(() => SceneManager.LoadScene(GameplaySceneIndex));
        }
        else
        {
            SceneManager.LoadScene(GameplaySceneIndex);
        }
    }

    // =========================================================================
    // Store Page — landscape two-column: characters LEFT / maps RIGHT
    // =========================================================================
    private void BuildStorePage(GameObject canvas)
    {
        _storePage = MakeNode("StorePage", canvas);
        var pageRT = _storePage.AddComponent<RectTransform>();
        pageRT.anchorMin = Vector2.zero; pageRT.anchorMax = Vector2.one;
        pageRT.offsetMin = Vector2.zero; pageRT.offsetMax = Vector2.zero;
        _storePage.AddComponent<Image>().color = new Color(0.020f, 0.012f, 0.008f, 0.97f);

        BuildPageBackButton(_storePage, HideStorePage);
        BuildPageTitle(_storePage, "STORE");

        // Full-width divider below title
        BuildPageDivider(_storePage, -112f, 1600f);

        // Column center-line divider
        var colDiv = MakeNode("ColDiv", _storePage);
        var colDivRT = colDiv.AddComponent<RectTransform>();
        colDivRT.anchorMin = new Vector2(0.5f, 0.5f); colDivRT.anchorMax = new Vector2(0.5f, 0.5f);
        colDivRT.pivot = new Vector2(0.5f, 0.5f);
        colDivRT.anchoredPosition = new Vector2(0f, -80f);
        colDivRT.sizeDelta = new Vector2(1f, 800f);
        colDiv.AddComponent<Image>().color = ColAccentDim;

        // ── Left column: Characters (anchored 0.02 → 0.49) ──────────────────
        var charCol   = MakeNode("CharColumn", _storePage);
        var charColRT = charCol.AddComponent<RectTransform>();
        charColRT.anchorMin = new Vector2(0.02f, 0f);
        charColRT.anchorMax = new Vector2(0.49f, 1f);
        charColRT.offsetMin = Vector2.zero;
        charColRT.offsetMax = Vector2.zero;

        var chHdr = MakeNode("CharHdr", charCol);
        var chTMP = AddTMP(chHdr, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(500f, 32f));
        chTMP.text = "— CHARACTERS —"; chTMP.fontSize = 18f;
        chTMP.alignment = TextAlignmentOptions.Center; chTMP.color = ColTextDim; chTMP.characterSpacing = 4f;

        string[] charIds    = { "arissa",        "eve"                        };
        string[] charNames  = { "ARISSA",         "EVE"                        };
        string[] charSubs   = { "Shadow Blade",   "Maze Runner"                };
        float[]  charCardX  = { -180f,             180f                         };
        Color[]  charAccent = { ColAccent,          new Color(0.2f, 0.7f, 1.0f) };

        for (int i = 0; i < 2; i++)
        {
            int ci = i;
            BuildCharCard(charCol, charNames[i], charSubs[i], charAccent[i],
                          charCardX[i], -340f,
                          out _charCardBgs[i], out _charCardBords[i], out _charBadges[i],
                          () => SelectChar(charIds[ci]));
        }

        // ── Right column: Maps (anchored 0.51 → 0.98) ───────────────────────
        var mapCol   = MakeNode("MapColumn", _storePage);
        var mapColRT = mapCol.AddComponent<RectTransform>();
        mapColRT.anchorMin = new Vector2(0.51f, 0f);
        mapColRT.anchorMax = new Vector2(0.98f, 1f);
        mapColRT.offsetMin = Vector2.zero;
        mapColRT.offsetMax = Vector2.zero;

        var mapHdr = MakeNode("MapHdr", mapCol);
        var mapTMP = AddTMP(mapHdr, new Vector2(0.5f, 1f), new Vector2(0f, -152f), new Vector2(500f, 32f));
        mapTMP.text = "— MAPS —"; mapTMP.fontSize = 18f;
        mapTMP.alignment = TextAlignmentOptions.Center; mapTMP.color = ColTextDim; mapTMP.characterSpacing = 4f;

        string[] mapIds    = { "dungeon",                     "desert"                   };
        string[] mapNames  = { "DUNGEON",                      "DESERT"                   };
        string[] mapSubs   = { "Dark  ·  Stone",               "Sand  ·  Heat"            };
        float[]  mapCardY  = { -278f,                          -430f                      };
        Color[]  mapAccent = { new Color(0.3f, 0.5f, 1.0f),    ColGold                    };

        for (int i = 0; i < 2; i++)
        {
            int mi = i;
            BuildMapCard(mapCol, mapNames[i], mapSubs[i], mapAccent[i], mapCardY[i],
                         out _mapCardBgs[i], out _mapCardBords[i], out _mapBadges[i],
                         () => SelectMap(mapIds[mi]));
        }

        RefreshStoreCards();
        _storePage.SetActive(false);
    }

    // Character card — square, avatar circle + name + sub + badge
    private static void BuildCharCard(GameObject parent,
                                       string label, string sub, Color accent,
                                       float posX, float posY,
                                       out Image bg, out Image border,
                                       out TextMeshProUGUI badge,
                                       System.Action onSelect)
    {
        var frame   = MakeNode("CharCard_" + label, parent);
        var frameRT = frame.AddComponent<RectTransform>();
        frameRT.anchorMin        = new Vector2(0.5f, 1f);
        frameRT.anchorMax        = new Vector2(0.5f, 1f);
        frameRT.pivot            = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(posX, posY);
        frameRT.sizeDelta        = new Vector2(320f, 270f);

        border       = frame.AddComponent<Image>();
        border.color = ColAccentDim;

        var btn = frame.AddComponent<Button>(); btn.targetGraphic = border;
        var cb  = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(0.80f, 0.80f, 0.80f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onSelect?.Invoke());

        // Inner background
        var inner   = MakeNode("Inner", frame);
        var innerRT = inner.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(2f, 2f); innerRT.offsetMax = new Vector2(-2f, -2f);
        bg = inner.AddComponent<Image>();
        bg.color = ColSurface;

        // Avatar circle placeholder (top area)
        var avGO = MakeNode("Avatar", frame);
        var avRT = avGO.AddComponent<RectTransform>();
        avRT.anchorMin = new Vector2(0.5f, 1f); avRT.anchorMax = new Vector2(0.5f, 1f);
        avRT.pivot = new Vector2(0.5f, 1f);
        avRT.anchoredPosition = new Vector2(0f, -22f);
        avRT.sizeDelta = new Vector2(62f, 62f);
        avGO.AddComponent<Image>().color = ColSurface2;

        // Name
        var nGO = MakeNode("Name", frame);
        var nRT = nGO.AddComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 0.38f); nRT.anchorMax = new Vector2(1f, 0.62f);
        nRT.offsetMin = Vector2.zero; nRT.offsetMax = Vector2.zero;
        var nTMP = nGO.AddComponent<TextMeshProUGUI>();
        nTMP.text = label; nTMP.fontSize = 36f; nTMP.fontStyle = FontStyles.Bold;
        nTMP.alignment = TextAlignmentOptions.Center; nTMP.color = Color.white;

        // Sub
        var sGO = MakeNode("Sub", frame);
        var sRT = sGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.24f); sRT.anchorMax = new Vector2(1f, 0.38f);
        sRT.offsetMin = Vector2.zero; sRT.offsetMax = Vector2.zero;
        var sTMP = sGO.AddComponent<TextMeshProUGUI>();
        sTMP.text = sub; sTMP.fontSize = 17f;
        sTMP.alignment = TextAlignmentOptions.Center; sTMP.color = ColTextDim;

        // Badge (bottom strip)
        var bGO = MakeNode("Badge", frame);
        var bRT = bGO.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0.06f, 0f); bRT.anchorMax = new Vector2(0.94f, 0.24f);
        bRT.offsetMin = new Vector2(0f, 10f); bRT.offsetMax = new Vector2(0f, -10f);
        badge           = bGO.AddComponent<TextMeshProUGUI>();
        badge.text      = "TAP TO SELECT";
        badge.fontSize  = 15f; badge.fontStyle = FontStyles.Bold;
        badge.alignment = TextAlignmentOptions.Center; badge.color = ColTextDim;
    }

    // Map card — reference horizontal style: icon | name+tags | badge/checkmark
    private static void BuildMapCard(GameObject parent,
                                      string label, string sub, Color accent,
                                      float posY,
                                      out Image bg, out Image border,
                                      out TextMeshProUGUI badge,
                                      System.Action onSelect)
    {
        var frame   = MakeNode("MapCard_" + label, parent);
        var frameRT = frame.AddComponent<RectTransform>();
        frameRT.anchorMin        = new Vector2(0.5f, 1f);
        frameRT.anchorMax        = new Vector2(0.5f, 1f);
        frameRT.pivot            = new Vector2(0.5f, 0.5f);
        frameRT.anchoredPosition = new Vector2(0f, posY);
        frameRT.sizeDelta        = new Vector2(850f, 118f);

        border       = frame.AddComponent<Image>();
        border.color = ColAccentDim;

        var btn = frame.AddComponent<Button>(); btn.targetGraphic = border;
        var cb  = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        cb.pressedColor = new Color(0.80f, 0.80f, 0.80f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onSelect?.Invoke());

        var inner   = MakeNode("Inner", frame);
        var innerRT = inner.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(2f, 2f); innerRT.offsetMax = new Vector2(-2f, -2f);
        bg = inner.AddComponent<Image>();
        bg.color = ColSurface;

        // Left icon square
        var iconGO = MakeNode("Icon", frame);
        var iconRT = iconGO.AddComponent<RectTransform>();
        iconRT.anchorMin = new Vector2(0f, 0.5f); iconRT.anchorMax = new Vector2(0f, 0.5f);
        iconRT.pivot = new Vector2(0f, 0.5f);
        iconRT.anchoredPosition = new Vector2(18f, 0f);
        iconRT.sizeDelta = new Vector2(60f, 60f);
        iconGO.AddComponent<Image>().color = ColSurface2;

        // Map name (left-aligned after icon)
        var nGO = MakeNode("Name", frame);
        var nRT = nGO.AddComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0f, 0.5f); nRT.anchorMax = new Vector2(0f, 0.5f);
        nRT.pivot = new Vector2(0f, 0.5f);
        nRT.anchoredPosition = new Vector2(96f, 14f);
        nRT.sizeDelta = new Vector2(440f, 48f);
        var nTMP = nGO.AddComponent<TextMeshProUGUI>();
        nTMP.text = label; nTMP.fontSize = 28f; nTMP.fontStyle = FontStyles.Bold;
        nTMP.alignment = TextAlignmentOptions.MidlineLeft; nTMP.color = Color.white;

        // Map sub/tags
        var sGO = MakeNode("Sub", frame);
        var sRT = sGO.AddComponent<RectTransform>();
        sRT.anchorMin = new Vector2(0f, 0.5f); sRT.anchorMax = new Vector2(0f, 0.5f);
        sRT.pivot = new Vector2(0f, 0.5f);
        sRT.anchoredPosition = new Vector2(96f, -16f);
        sRT.sizeDelta = new Vector2(440f, 34f);
        var sTMP = sGO.AddComponent<TextMeshProUGUI>();
        sTMP.text = sub; sTMP.fontSize = 17f;
        sTMP.alignment = TextAlignmentOptions.MidlineLeft; sTMP.color = ColTextDim;

        // Badge / checkmark (right-aligned)
        var bGO = MakeNode("Badge", frame);
        var bRT = bGO.AddComponent<RectTransform>();
        bRT.anchorMin = new Vector2(1f, 0.5f); bRT.anchorMax = new Vector2(1f, 0.5f);
        bRT.pivot = new Vector2(1f, 0.5f);
        bRT.anchoredPosition = new Vector2(-22f, 0f);
        bRT.sizeDelta = new Vector2(220f, 42f);
        badge           = bGO.AddComponent<TextMeshProUGUI>();
        badge.text      = "TAP TO SELECT";
        badge.fontSize  = 15f; badge.fontStyle = FontStyles.Bold;
        badge.alignment = TextAlignmentOptions.MidlineRight; badge.color = ColTextDim;
    }

    private void RefreshStoreCards()
    {
        string[] charIds    = { "arissa",   "eve"                        };
        Color[]  charAccents = { ColAccent,  new Color(0.2f, 0.7f, 1.0f) };

        for (int i = 0; i < 2; i++)
        {
            bool sel = _selectedChar == charIds[i];
            Color ac = charAccents[i];
            if (_charCardBgs[i]   != null)
                _charCardBgs[i].color   = sel ? new Color(ac.r*0.18f, ac.g*0.18f, ac.b*0.18f, 1f) : ColSurface;
            if (_charCardBords[i] != null)
                _charCardBords[i].color = sel ? ac : ColAccentDim;
            if (_charBadges[i]    != null)
            {
                _charBadges[i].text  = sel ? "SELECTED  ✓" : "TAP TO SELECT";
                _charBadges[i].color = sel ? ac : ColTextDim;
            }
        }

        string[] mapIds    = { "dungeon",                "desert"  };
        Color[]  mapAccents = { new Color(0.3f, 0.5f, 1.0f), ColGold };

        for (int i = 0; i < 2; i++)
        {
            bool sel = _selectedMap == mapIds[i];
            Color ac = mapAccents[i];
            if (_mapCardBgs[i]   != null)
                _mapCardBgs[i].color   = sel ? new Color(ac.r*0.18f, ac.g*0.18f, ac.b*0.18f, 1f) : ColSurface;
            if (_mapCardBords[i] != null)
                _mapCardBords[i].color = sel ? ac : ColAccentDim;
            if (_mapBadges[i]    != null)
            {
                _mapBadges[i].text  = sel ? "SELECTED  ✓" : "TAP TO SELECT";
                _mapBadges[i].color = sel ? ac : ColTextDim;
            }
        }
    }

    private void SelectChar(string id)
    {
        _selectedChar = id;
        PlayerPrefs.SetString("SelectedCharacter", id);
        PlayerPrefs.Save();
        RefreshStoreCards();
    }

    private void SelectMap(string id)
    {
        _selectedMap = id;
        PlayerPrefs.SetString("SelectedBiome", id);
        PlayerPrefs.Save();
        RefreshStoreCards();
    }

    private void ShowStorePage()
    {
        if (_storePage == null) return;
        _storePage.SetActive(true);
        var cg = _storePage.GetComponent<CanvasGroup>();
        if (cg == null) cg = _storePage.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.DOFade(1f, 0.22f).SetEase(Ease.OutQuad);
    }

    private void HideStorePage()
    {
        if (_storePage == null) return;
        var cg = _storePage.GetComponent<CanvasGroup>();
        if (cg == null) cg = _storePage.AddComponent<CanvasGroup>();
        cg.DOFade(0f, 0.18f).SetEase(Ease.InQuad)
          .OnComplete(() => _storePage.SetActive(false));
    }

    // =========================================================================
    // Records Page — landscape full-width rows: dot + name/sub | stars | time
    // =========================================================================
    private void BuildRecordsPage(GameObject canvas)
    {
        _recordsPage = MakeNode("RecordsPage", canvas);
        var pageRT = _recordsPage.AddComponent<RectTransform>();
        pageRT.anchorMin = Vector2.zero; pageRT.anchorMax = Vector2.one;
        pageRT.offsetMin = Vector2.zero; pageRT.offsetMax = Vector2.zero;
        _recordsPage.AddComponent<Image>().color = new Color(0.020f, 0.012f, 0.008f, 0.97f);

        BuildPageBackButton(_recordsPage, HideRecordsPage);
        BuildPageTitle(_recordsPage, "RECORDS");
        BuildPageDivider(_recordsPage, -112f, 1600f);

        // "Best Times" sub-label
        var subGO  = MakeNode("SubLabel", _recordsPage);
        var subTMP = AddTMP(subGO, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(600f, 28f));
        subTMP.text = "BEST TIMES"; subTMP.fontSize = 14f;
        subTMP.alignment = TextAlignmentOptions.Center;
        subTMP.color = ColTextDim; subTMP.characterSpacing = 6f;

        string[] diffNames = { "EASY",   "MEDIUM",   "HARD"    };
        string[] diffSubs  = { "15 × 15  ·  4:00 limit",
                                "20 × 20  ·  3:00 limit",
                                "25 × 25  ·  2:30 limit" };
        Color[]  diffCols  = { ColEasyFg, ColMedFg,   ColHardFg };
        float    startY    = -220f;

        for (int i = 0; i < 3; i++)
        {
            float rowY  = startY - i * 148f;
            float best  = PlayerPrefs.GetFloat("BestTime_" + i, float.MaxValue);
            int   stars = PlayerPrefs.GetInt("Stars_" + i, 0);
            string bestStr = best < float.MaxValue ? MenuFormatTime(best) : "—";

            // Row bg
            var rowGO = MakeNode("Row_" + i, _recordsPage);
            var rowRT = rowGO.AddComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0.5f, 1f); rowRT.anchorMax = new Vector2(0.5f, 1f);
            rowRT.pivot = new Vector2(0.5f, 0.5f);
            rowRT.anchoredPosition = new Vector2(0f, rowY);
            rowRT.sizeDelta = new Vector2(1560f, 118f);
            rowGO.AddComponent<Image>().color = ColSurface;

            // Colored left dot (reference: record-dot)
            var dotGO = MakeNode("Dot", rowGO);
            var dotRT = dotGO.AddComponent<RectTransform>();
            dotRT.anchorMin = new Vector2(0f, 0.5f); dotRT.anchorMax = new Vector2(0f, 0.5f);
            dotRT.pivot = new Vector2(0f, 0.5f);
            dotRT.anchoredPosition = new Vector2(28f, 0f);
            dotRT.sizeDelta = new Vector2(14f, 14f);
            dotGO.AddComponent<Image>().color = diffCols[i];

            // Difficulty name
            var dGO = MakeNode("D", rowGO);
            var dRT = dGO.AddComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0.5f); dRT.anchorMax = new Vector2(0f, 0.5f);
            dRT.pivot = new Vector2(0f, 0.5f);
            dRT.anchoredPosition = new Vector2(56f, 12f);
            dRT.sizeDelta = new Vector2(260f, 52f);
            var dTMP = dGO.AddComponent<TextMeshProUGUI>();
            dTMP.text = diffNames[i]; dTMP.fontSize = 32f; dTMP.fontStyle = FontStyles.Bold;
            dTMP.alignment = TextAlignmentOptions.MidlineLeft; dTMP.color = ColText;

            // Sub-text below name
            var sGO = MakeNode("S", rowGO);
            var sRT = sGO.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0.5f); sRT.anchorMax = new Vector2(0f, 0.5f);
            sRT.pivot = new Vector2(0f, 0.5f);
            sRT.anchoredPosition = new Vector2(56f, -18f);
            sRT.sizeDelta = new Vector2(400f, 32f);
            var sTMP = sGO.AddComponent<TextMeshProUGUI>();
            sTMP.text = diffSubs[i]; sTMP.fontSize = 16f;
            sTMP.alignment = TextAlignmentOptions.MidlineLeft; sTMP.color = ColTextDim;

            // Stars (right of center)
            Color starLit = diffCols[i];
            for (int s = 0; s < 3; s++)
            {
                bool lit = s < stars;
                var sgGO = MakeNode("Star" + s, rowGO);
                var sgRT = sgGO.AddComponent<RectTransform>();
                sgRT.anchorMin = new Vector2(1f, 0.5f); sgRT.anchorMax = new Vector2(1f, 0.5f);
                sgRT.pivot = new Vector2(1f, 0.5f);
                sgRT.anchoredPosition = new Vector2(-240f - (2 - s) * 46f, 0f);
                sgRT.sizeDelta = new Vector2(38f, 38f);
                var sgImg = sgGO.AddComponent<Image>();
                sgImg.sprite = ArtifactIcons.MakeStar(lit);
                sgImg.color  = lit ? starLit : ColTextDim;
            }

            // Best time — right side, orange (reference: record-time)
            var bGO = MakeNode("B", rowGO);
            var bRT = bGO.AddComponent<RectTransform>();
            bRT.anchorMin = new Vector2(1f, 0.5f); bRT.anchorMax = new Vector2(1f, 0.5f);
            bRT.pivot = new Vector2(1f, 0.5f);
            bRT.anchoredPosition = new Vector2(-28f, 0f);
            bRT.sizeDelta = new Vector2(200f, 64f);
            var bTMP = bGO.AddComponent<TextMeshProUGUI>();
            bTMP.text = bestStr; bTMP.fontSize = 44f; bTMP.fontStyle = FontStyles.Bold;
            bTMP.alignment = TextAlignmentOptions.MidlineRight;
            bTMP.color = best < float.MaxValue ? ColAccent : ColTextDim;
        }

        bool anyRecord = PlayerPrefs.GetFloat("BestTime_0", float.MaxValue) < float.MaxValue
                      || PlayerPrefs.GetFloat("BestTime_1", float.MaxValue) < float.MaxValue
                      || PlayerPrefs.GetFloat("BestTime_2", float.MaxValue) < float.MaxValue;
        if (!anyRecord)
        {
            var noteGO  = MakeNode("NoRec", _recordsPage);
            var noteTMP = AddTMP(noteGO, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(900f, 40f));
            noteTMP.text = "Complete a run to set your first record";
            noteTMP.fontSize = 22f; noteTMP.alignment = TextAlignmentOptions.Center;
            noteTMP.color = ColTextDim;
        }

        _recordsPage.SetActive(false);
    }

    private void ShowRecordsPage()
    {
        if (_recordsPage == null) return;
        _recordsPage.SetActive(true);
        var cg = _recordsPage.GetComponent<CanvasGroup>();
        if (cg == null) cg = _recordsPage.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.DOFade(1f, 0.22f).SetEase(Ease.OutQuad);
    }

    private void HideRecordsPage()
    {
        if (_recordsPage == null) return;
        var cg = _recordsPage.GetComponent<CanvasGroup>();
        if (cg == null) cg = _recordsPage.AddComponent<CanvasGroup>();
        cg.DOFade(0f, 0.18f).SetEase(Ease.InQuad)
          .OnComplete(() => _recordsPage.SetActive(false));
    }

    // =========================================================================
    // Shared page header helpers
    // =========================================================================

    // Reference-style back button: small square with surface bg + orange "‹"
    private static void BuildPageBackButton(GameObject page, System.Action onClick)
    {
        var frameGO = MakeNode("BackFrame", page);
        var frameRT = frameGO.AddComponent<RectTransform>();
        frameRT.anchorMin        = new Vector2(0f, 1f);
        frameRT.anchorMax        = new Vector2(0f, 1f);
        frameRT.pivot            = new Vector2(0f, 1f);
        frameRT.anchoredPosition = new Vector2(32f, -28f);
        frameRT.sizeDelta        = new Vector2(58f, 58f);
        frameGO.AddComponent<Image>().color = ColAccentDim;

        var innerGO = MakeNode("Inner", frameGO);
        var innerRT = innerGO.AddComponent<RectTransform>();
        innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
        innerRT.offsetMin = new Vector2(1f, 1f); innerRT.offsetMax = new Vector2(-1f, -1f);
        var innerImg = innerGO.AddComponent<Image>();
        innerImg.color = ColSurface;

        var btn = frameGO.AddComponent<Button>(); btn.targetGraphic = innerImg;
        var cb  = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => onClick?.Invoke());

        var lGO = MakeNode("Arrow", frameGO);
        var lRT = lGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;
        var lTMP = lGO.AddComponent<TextMeshProUGUI>();
        lTMP.text = "‹"; lTMP.fontSize = 44f; lTMP.fontStyle = FontStyles.Bold;
        lTMP.alignment = TextAlignmentOptions.Center; lTMP.color = ColAccent;
    }

    // Page title (orange, Bebas-style, top-center)
    private static void BuildPageTitle(GameObject page, string title)
    {
        var go  = MakeNode("PageTitle", page);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta        = new Vector2(700f, 80f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = title; tmp.fontSize = 62f; tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = ColAccent; tmp.characterSpacing = 2f;
    }

    // Thin horizontal divider (1px, orange-dim)
    private static void BuildPageDivider(GameObject page, float posY, float width)
    {
        var go = MakeNode("Divider", page);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, posY);
        rt.sizeDelta        = new Vector2(width, 1f);
        go.AddComponent<Image>().color = ColAccentDim;
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    private static TextMeshProUGUI AddTMP(GameObject go, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchor;
        rt.anchorMax        = anchor;
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return go.AddComponent<TextMeshProUGUI>();
    }

    private static TextMeshProUGUI AddStretchLabel(GameObject parent, string text,
                                                    float fontSize, FontStyles style,
                                                    Color color, float charSpacing)
    {
        var go  = MakeNode("Lbl", parent);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = fontSize;
        tmp.fontStyle        = style;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.color            = color;
        tmp.characterSpacing = charSpacing;
        return tmp;
    }

    private static GameObject MakeStretch(string name, GameObject parent)
    {
        var go  = MakeNode(name, parent);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return go;
    }

    private static GameObject MakeNode(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static string MenuFormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}

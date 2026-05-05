using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class SewerLevelManager : MonoBehaviour
{
    public static SewerLevelManager Instance { get; private set; }

    [Header("Settings")]
    public float levelTime = 200f;

    float _timeLeft;
    bool  _over;

    TextMeshProUGUI _timerLabel;
    TextMeshProUGUI _camModeLabel;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        Instance  = this;
        _timeLeft = levelTime;
        DOTween.Init();
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() => PlayerSpawner.OnPlayerSpawned += _ =>
    {
        BuildHUD();
        // Tight sewer corridors need lower camera pivot and shorter pull distance
        FollowCamera.Instance?.SetCameraSettings(
            newHeightOffset: 0.4f,
            newDistance:     2.2f,
            newPitch:        15f);
    };

    // ── Update ────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_over) return;
        _timeLeft -= Time.deltaTime;
        UpdateTimer();
        if (_timeLeft <= 0f) Lose("Время вышло");
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public float TimeLeft => _timeLeft;

    public void PlayerReachedExit()
    {
        if (_over) return;
        _over = true;
        ShowOverlay("ВЫБРАЛСЯ!", new Color(0.2f, 0.8f, 0.3f));
        DOVirtual.DelayedCall(2f, () => SceneManager.LoadScene("MainMenu"));
    }

    public void PlayerCaught()
    {
        if (_over) return;
        Lose("ПОЙМАН");
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    void BuildHUD()
    {
        // Canvas
        var canvasGO = new GameObject("SewerHUD");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Timer — top center
        var timerGO = new GameObject("Timer");
        timerGO.transform.SetParent(canvasGO.transform, false);
        _timerLabel = timerGO.AddComponent<TextMeshProUGUI>();
        _timerLabel.fontSize  = 38;
        _timerLabel.alignment = TextAlignmentOptions.Center;
        _timerLabel.color     = new Color(0.85f, 0.85f, 0.85f);
        var timerRT = timerGO.GetComponent<RectTransform>();
        timerRT.anchorMin = new Vector2(0.35f, 0.90f);
        timerRT.anchorMax = new Vector2(0.65f, 1.00f);
        timerRT.offsetMin = timerRT.offsetMax = Vector2.zero;
        UpdateTimer();

        // Camera toggle button — top right
        var btnGO  = new GameObject("CamBtn");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0.55f);
        var btn    = btnGO.AddComponent<Button>();
        var btnRT  = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.88f, 0.88f);
        btnRT.anchorMax = new Vector2(0.99f, 0.99f);
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        _camModeLabel = labelGO.AddComponent<TextMeshProUGUI>();
        _camModeLabel.text      = "3P";
        _camModeLabel.fontSize  = 22;
        _camModeLabel.alignment = TextAlignmentOptions.Center;
        _camModeLabel.color     = new Color(0.72f, 0.72f, 0.88f);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

        btn.onClick.AddListener(() => {
            FollowCamera.Instance?.ToggleCameraMode();
            if (_camModeLabel != null)
                _camModeLabel.text = (FollowCamera.Instance != null && FollowCamera.Instance.IsFirstPerson)
                    ? "1P" : "3P";
        });
    }

    void UpdateTimer()
    {
        if (_timerLabel == null) return;
        int m = Mathf.Max(0, (int)_timeLeft / 60);
        int s = Mathf.Max(0, (int)_timeLeft % 60);
        _timerLabel.text  = $"{m:00}:{s:00}";
        _timerLabel.color = _timeLeft < 30f ? new Color(1f, 0.25f, 0.15f) : new Color(0.85f, 0.85f, 0.85f);
    }

    void ShowOverlay(string text, Color color)
    {
        var go  = new GameObject("Overlay");
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 72;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.4f);
        rt.anchorMax = new Vector2(0.9f, 0.6f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    void Lose(string reason)
    {
        _over = true;
        ShowOverlay(reason, new Color(1f, 0.2f, 0.1f));
        DOVirtual.DelayedCall(2f, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
    }
}

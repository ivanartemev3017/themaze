using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Spawns patrol-based enemies after player spawns.
/// Easy: 1 slow enemy (intro difficulty). Medium: 2. Hard: 3.
/// Enemies spawn at a moderate distance so the player encounters them early.
/// Shows a brief "CREATURE IS HUNTING YOU" message at spawn.
/// Attach to PlayerSpawner GameObject — subscribes to OnPlayerSpawned.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    private void OnPlayerSpawned(GameObject player)
    {
        try
        {
            SpawnEnemies(player);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[EnemySpawner] {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SpawnEnemies(GameObject player)
    {
        var diff = GameManager.CurrentDifficulty;
        var maze = FindAnyObjectByType<MazeGenerator>();
        if (maze == null) { Debug.LogError("[EnemySpawner] MazeGenerator not found!"); return; }

        int   count;
        float moveSpeed, chaseSpeed, detectionRange;
        int   detectionPathLen;

        switch (diff)
        {
            case GameManager.Difficulty.Easy:
                count            = 1;
                moveSpeed        = 2.0f;
                chaseSpeed       = 2.8f;
                detectionRange   = 28f;
                detectionPathLen = 12;
                break;
            case GameManager.Difficulty.Medium:
                count            = 2;
                moveSpeed        = 2.8f;
                chaseSpeed       = 3.8f;
                detectionRange   = 36f;
                detectionPathLen = 16;
                break;
            default: // Hard
                count            = 3;
                moveSpeed        = 3.2f;
                chaseSpeed       = 4.5f;
                detectionRange   = 48f;
                detectionPathLen = 22;
                break;
        }

        // Spawn within 15-35% of maze diagonal so enemy is reachable but not on top of player
        float diag     = maze.cellSize * Mathf.Sqrt(maze.mazeWidth * maze.mazeWidth + maze.mazeHeight * maze.mazeHeight);
        float minDist  = diag * 0.12f;
        float maxDist  = diag * 0.40f;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPos = FindSpawnPosition(maze, player.transform.position, minDist, maxDist);
            var enemyGO = MazeEnemy.SpawnEnemy(spawnPos);
            var enemy   = enemyGO.AddComponent<MazeEnemy>();
            enemy.Setup(player.transform, maze, moveSpeed, chaseSpeed, detectionRange, detectionPathLen);
            spawned++;
            Debug.Log($"[EnemySpawner] Enemy #{spawned} spawned at {spawnPos} ({diff})");
        }

        // Brief HUD notification
        string msg = spawned == 1 ? "A CREATURE IS HUNTING YOU"
                                  : $"{spawned} CREATURES ARE HUNTING YOU";
        ShowHuntMessage(msg);

        Debug.Log($"[EnemySpawner] Done — {spawned} enemies spawned for {diff}.");
    }

    private static Vector3 FindSpawnPosition(MazeGenerator maze, Vector3 playerPos,
                                              float minDist, float maxDist)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int x = Random.Range(0, maze.mazeWidth);
            int y = Random.Range(0, maze.mazeHeight);
            Vector3 pos = maze.GetCellCentre(x, y);
            pos.y = 0.05f;

            float d = Vector3.Distance(pos, playerPos);
            if (d >= minDist && d <= maxDist)
                return pos;
        }

        // Fallback: anywhere far enough from player
        for (int attempt = 0; attempt < 30; attempt++)
        {
            int x = Random.Range(0, maze.mazeWidth);
            int y = Random.Range(0, maze.mazeHeight);
            Vector3 pos = maze.GetCellCentre(x, y);
            pos.y = 0.05f;
            if (Vector3.Distance(pos, playerPos) >= minDist)
                return pos;
        }

        Vector3 fallback = maze.ExitWorldPosition;
        fallback.y = 0.05f;
        return fallback;
    }

    // -------------------------------------------------------------------------

    private static void ShowHuntMessage(string text)
    {
        var canvasGO      = new GameObject("HuntMsgCanvas");
        var canvas        = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        canvasGO.AddComponent<CanvasScaler>();

        var go  = new GameObject("HuntMsg");
        go.transform.SetParent(canvasGO.transform, false);
        var rt  = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -60f);
        rt.sizeDelta        = new Vector2(900f, 60f);

        var tmp          = go.AddComponent<TextMeshProUGUI>();
        tmp.text         = text;
        tmp.fontSize     = 32f;
        tmp.fontStyle    = FontStyles.Bold;
        tmp.alignment    = TextAlignmentOptions.Center;
        tmp.color        = new Color(0.9f, 0.1f, 0.1f, 0f);

        // Fade in then out over 3 seconds
        var cg        = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha      = 0f;
        cg.interactable      = false;
        cg.blocksRaycasts    = false;

        StartFadeSequence(canvasGO, cg);
    }

    private static void StartFadeSequence(GameObject owner, CanvasGroup cg)
    {
        // Use a simple coroutine-less approach via a helper MonoBehaviour
        var runner = owner.AddComponent<FadeAndDestroy>();
        runner.Init(cg, fadeInTime: 0.5f, holdTime: 2.5f, fadeOutTime: 1.0f);
    }
}

/// <summary>Fades a CanvasGroup in, holds, fades out, then destroys the GameObject.</summary>
internal class FadeAndDestroy : MonoBehaviour
{
    private CanvasGroup _cg;
    private float _fadeIn, _hold, _fadeOut;
    private float _elapsed;
    private enum Stage { FadeIn, Hold, FadeOut, Done }
    private Stage _stage;

    public void Init(CanvasGroup cg, float fadeInTime, float holdTime, float fadeOutTime)
    {
        _cg = cg; _fadeIn = fadeInTime; _hold = holdTime; _fadeOut = fadeOutTime;
        _stage = Stage.FadeIn; _elapsed = 0f;
    }

    void Update()
    {
        _elapsed += Time.unscaledDeltaTime;
        switch (_stage)
        {
            case Stage.FadeIn:
                _cg.alpha = Mathf.Clamp01(_elapsed / _fadeIn);
                if (_elapsed >= _fadeIn) { _stage = Stage.Hold; _elapsed = 0f; }
                break;
            case Stage.Hold:
                if (_elapsed >= _hold)  { _stage = Stage.FadeOut; _elapsed = 0f; }
                break;
            case Stage.FadeOut:
                _cg.alpha = 1f - Mathf.Clamp01(_elapsed / _fadeOut);
                if (_elapsed >= _fadeOut) { _stage = Stage.Done; Destroy(gameObject); }
                break;
        }
    }
}

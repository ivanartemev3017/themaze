using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

// =============================================================================
// ArtifactType — global enum (shared by ArtifactInventory, MazeArtifact, GameManager)
// =============================================================================
public enum ArtifactType { TimeBonus = 0, EnemyFreeze = 1 }

// =============================================================================
// ArtifactInventory — persistent cross-session storage in PlayerPrefs
// =============================================================================
public static class ArtifactInventory
{
    private static string Key(ArtifactType t) => "Inv_" + (int)t;

    public static int GetCount(ArtifactType t)  => PlayerPrefs.GetInt(Key(t), 0);

    public static void Add(ArtifactType t)
    {
        PlayerPrefs.SetInt(Key(t), GetCount(t) + 1);
        PlayerPrefs.Save();
    }

    public static bool TryUse(ArtifactType t)
    {
        int n = GetCount(t);
        if (n <= 0) return false;
        PlayerPrefs.SetInt(Key(t), n - 1);
        PlayerPrefs.Save();
        return true;
    }

    public static int TotalCount =>
        GetCount(ArtifactType.TimeBonus) + GetCount(ArtifactType.EnemyFreeze);
}

// =============================================================================
// ArtifactIcons — procedural pixel-art icon sprites
// =============================================================================
public static class ArtifactIcons
{
    private const int S = 64;   // texture size

    public static Sprite Make(ArtifactType type) =>
        type == ArtifactType.TimeBonus ? MakeTimeIcon() : MakeFreezeIcon();

    // ── Ice / Freeze (dark-blue circle, white snowflake) ─────────────────────
    static Sprite MakeFreezeIcon()
    {
        var p = new Color[S * S];
        int cx = S / 2, cy = S / 2;

        FillCircle(p, cx, cy, S / 2 - 1, new Color(0.03f, 0.07f, 0.22f, 0.95f));
        DrawRing  (p, cx, cy, S / 2 - 1, 3, new Color(0.15f, 0.82f, 1.0f));
        DrawSnowflake(p, cx, cy, (int)(S * 0.37f), new Color(0.88f, 0.97f, 1.0f));

        return ToSprite(p);
    }

    // ── Time (dark-gold circle, gold clock face) ──────────────────────────────
    static Sprite MakeTimeIcon()
    {
        var p = new Color[S * S];
        int cx = S / 2, cy = S / 2;

        FillCircle(p, cx, cy, S / 2 - 1, new Color(0.20f, 0.12f, 0.0f, 0.95f));
        DrawRing  (p, cx, cy, S / 2 - 1, 3, new Color(1.0f, 0.78f, 0.08f));
        DrawClock (p, cx, cy, (int)(S * 0.35f), new Color(1.0f, 0.92f, 0.6f));

        return ToSprite(p);
    }

    // ── Primitives ─────────────────────────────────────────────────────────────

    static void FillCircle(Color[] p, int cx, int cy, int r, Color col)
    {
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            int dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy <= r * r && InB(x, y))
                p[y * S + x] = col;
        }
    }

    static void DrawRing(Color[] p, int cx, int cy, int r, int thick, Color col)
    {
        int r1sq = (r - thick) * (r - thick), r2sq = r * r;
        for (int y = cy - r; y <= cy + r; y++)
        for (int x = cx - r; x <= cx + r; x++)
        {
            int d2 = (x-cx)*(x-cx) + (y-cy)*(y-cy);
            if (d2 >= r1sq && d2 <= r2sq && InB(x, y))
                p[y * S + x] = col;
        }
    }

    // Bresenham with thickness
    static void DrawLine(Color[] p, int x0, int y0, int x1, int y1, Color col, int w = 1)
    {
        int dx = Mathf.Abs(x1-x0), dy = Mathf.Abs(y1-y0);
        int sx = x0<x1?1:-1, sy = y0<y1?1:-1, err = dx-dy;
        while (true)
        {
            for (int py = y0-w/2; py <= y0+w/2; py++)
            for (int px = x0-w/2; px <= x0+w/2; px++)
                if (InB(px, py)) p[py*S+px] = col;
            if (x0==x1 && y0==y1) break;
            int e2 = 2*err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 <  dx) { err += dx; y0 += sy; }
        }
    }

    static void DrawSnowflake(Color[] p, int cx, int cy, int armLen, Color col)
    {
        for (int arm = 0; arm < 6; arm++)
        {
            float a = arm * 60f * Mathf.Deg2Rad;
            int ex = cx + Mathf.RoundToInt(Mathf.Cos(a) * armLen);
            int ey = cy + Mathf.RoundToInt(Mathf.Sin(a) * armLen);
            DrawLine(p, cx, cy, ex, ey, col, 2);

            // 2 branch pairs per arm
            for (float frac = 0.32f; frac <= 0.62f; frac += 0.3f)
            {
                int bx = cx + Mathf.RoundToInt(Mathf.Cos(a) * armLen * frac);
                int by = cy + Mathf.RoundToInt(Mathf.Sin(a) * armLen * frac);
                float bLen = armLen * 0.30f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float ba = a + side * 60f * Mathf.Deg2Rad;
                    DrawLine(p, bx, by,
                        bx + Mathf.RoundToInt(Mathf.Cos(ba) * bLen),
                        by + Mathf.RoundToInt(Mathf.Sin(ba) * bLen),
                        col, 1);
                }
            }
        }
        FillCircle(p, cx, cy, 2, col);
    }

    static void DrawClock(Color[] p, int cx, int cy, int r, Color col)
    {
        DrawRing(p, cx, cy, r, 3, col);

        // 12 tick marks
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            DrawLine(p,
                cx + Mathf.RoundToInt(Mathf.Cos(a) * (r-3)),
                cy + Mathf.RoundToInt(Mathf.Sin(a) * (r-3)),
                cx + Mathf.RoundToInt(Mathf.Cos(a) * (r-7)),
                cy + Mathf.RoundToInt(Mathf.Sin(a) * (r-7)),
                col, 1);
        }

        // Hour hand → 10 o'clock  (120° from right = CCW)
        float hA = 120f * Mathf.Deg2Rad;
        DrawLine(p, cx, cy,
            cx + Mathf.RoundToInt(Mathf.Cos(hA) * r * 0.50f),
            cy + Mathf.RoundToInt(Mathf.Sin(hA) * r * 0.50f), col, 2);

        // Minute hand → 2 o'clock  (-60° from right)
        float mA = -60f * Mathf.Deg2Rad;
        DrawLine(p, cx, cy,
            cx + Mathf.RoundToInt(Mathf.Cos(mA) * r * 0.70f),
            cy + Mathf.RoundToInt(Mathf.Sin(mA) * r * 0.70f), col, 2);

        FillCircle(p, cx, cy, 2, col);
    }

    // ── Star (5-pointed, filled or dim) ──────────────────────────────────────
    public static Sprite MakeStar(bool filled)
    {
        var p = new Color[S * S];
        int cx = S / 2, cy = S / 2;
        int outerR = S / 2 - 3;
        int innerR = Mathf.RoundToInt(outerR * 0.40f);
        Color col = filled ? new Color(1f, 0.85f, 0.10f) : new Color(0.30f, 0.30f, 0.30f, 0.80f);

        // 5-pointed star: alternating outer/inner vertices
        var verts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float angle = (i * Mathf.PI / 5f) - Mathf.PI / 2f;
            float r = (i % 2 == 0) ? outerR : innerR;
            verts[i] = new Vector2(cx + Mathf.Cos(angle) * r, cy + Mathf.Sin(angle) * r);
        }

        // Scanline polygon fill
        for (int y = cy - outerR; y <= cy + outerR; y++)
        {
            var xs = new System.Collections.Generic.List<float>();
            for (int vi = 0; vi < verts.Length; vi++)
            {
                Vector2 v0 = verts[vi], v1 = verts[(vi + 1) % verts.Length];
                if ((v0.y <= y && v1.y > y) || (v1.y <= y && v0.y > y))
                    xs.Add(v0.x + (y - v0.y) / (v1.y - v0.y) * (v1.x - v0.x));
            }
            xs.Sort();
            for (int xi = 0; xi + 1 < xs.Count; xi += 2)
                for (int x = Mathf.RoundToInt(xs[xi]); x <= Mathf.RoundToInt(xs[xi + 1]); x++)
                    if (InB(x, y)) p[y * S + x] = col;
        }

        return ToSprite(p);
    }

    static bool InB(int x, int y) => x >= 0 && x < S && y >= 0 && y < S;

    static Sprite ToSprite(Color[] pixels)
    {
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear };
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, S, S), Vector2.one * 0.5f);
    }
}

// =============================================================================
// MazeArtifact — collectible in maze, adds to persistent inventory on pickup
// =============================================================================
/// <summary>
/// Collectible artifact spawned inside the maze.
/// Picking it up does NOT apply the effect immediately — it adds to the player's
/// persistent ArtifactInventory. The player spends inventory items by tapping
/// the HUD icons during gameplay.
/// </summary>
public class MazeArtifact : MonoBehaviour
{
    public ArtifactType Type { get; private set; }

    /// <summary>Total artifacts spawned this game (for 3★ tracking).</summary>
    public static int TotalSpawned   { get; set; }
    /// <summary>Total artifacts collected this game (for 3★ tracking).</summary>
    public static int TotalCollected { get; set; }

    private const float CollectRadius = 2.2f;
    private const float DotHeight     = 55f;

    private bool _collected;

    // Cached player transform for distance-based fallback collection
    private static Transform _playerTF;

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += CachePlayer;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= CachePlayer;
    static void CachePlayer(GameObject p) => _playerTF = p.transform;

    void Start()
    {
        // In case player already spawned before this artifact was created
        if (_playerTF == null)
        {
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) _playerTF = pm.transform;
        }
    }

    void Update()
    {
        // Fallback collection via distance — CharacterController.Move() does not
        // reliably send OnTriggerEnter on all Android/Unity versions.
        if (_collected || _playerTF == null) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;
        if (Vector3.Distance(transform.position, _playerTF.position) < CollectRadius)
            Collect();
    }

    // =========================================================================
    // Factory
    // =========================================================================

    public static GameObject Create(ArtifactType type, Vector3 position)
    {
        var go = new GameObject("Artifact_" + type);
        position.y = 1.2f;
        go.transform.position = position;

        var sphere       = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius    = CollectRadius;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;

        var artifact = go.AddComponent<MazeArtifact>();
        artifact.Type = type;

        Color color = type == ArtifactType.TimeBonus
            ? new Color(1.0f, 0.72f, 0.10f)   // gold
            : new Color(0.20f, 0.82f, 1.00f);  // cyan

        // Visual root — receives all animations
        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(go.transform, false);
        visualRoot.transform.localPosition = Vector3.zero;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color * 1.5f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 1.8f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        if (type == ArtifactType.TimeBonus)
        {
            string modelPath = "Artifacts/hourglass";
            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, visualRoot.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale    = Vector3.one * 0.25f;
                foreach (var col in model.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(col);
                // НЕ вызывать ApplyMaterialAllSlots — оставить родной материал FBX
            }
            else
            {
                BuildFallbackSphere(visualRoot, mat);
            }
        }
        else
        {
            string modelPath = "Artifacts/crystal";
            var prefab = Resources.Load<GameObject>(modelPath);
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, visualRoot.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localScale    = Vector3.one;
                foreach (var col in model.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(col);

                // Удалить мусорные объекты которые могли войти в FBX при экспорте
                foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || child.gameObject == model) continue;
                    string n = child.name;
                    if (n == "Plane" || n == "Camera" || n == "Point" ||
                        n == "Light" || n == "Lamp"   || n == "Armature")
                        Object.Destroy(child.gameObject);
                }

                AutoScaleModel(model, 0.7f);
                // НЕ вызывать ApplyMaterialAllSlots — оставить родной материал FBX
            }
            else
            {
                BuildProceduralCrystal(visualRoot, mat); // fallback если FBX не найден
            }
        }

        // Bob up/down
        visualRoot.transform.DOLocalMoveY(0.28f, 1.3f)
              .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        // Pulse scale
        visualRoot.transform.DOScale(Vector3.one * 1.08f, 0.9f)
              .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        // Slow Y spin — item виден с любой стороны коридора
        float spinDur = type == ArtifactType.TimeBonus ? 5f : 4f;
        visualRoot.transform.DOLocalRotate(
            new Vector3(0f, 360f, 0f), spinDur, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1);

        // Glow light
        var ltGO = new GameObject("ArtifactLight");
        ltGO.transform.SetParent(go.transform, false);
        var lt       = ltGO.AddComponent<Light>();
        lt.type      = LightType.Point;
        lt.color     = color;
        lt.intensity = 6f;
        lt.range     = 14f;
        lt.shadows   = LightShadows.None;

        // Minimap star marker — spins and pulses to stand out against enemy dots
        var starGO = new GameObject("ArtifactMinimapStar");
        starGO.transform.SetParent(go.transform, false);
        starGO.transform.localPosition = Vector3.up * DotHeight;
        starGO.transform.localScale    = Vector3.one * 2.4f;  // larger = easier to spot
        var starMF = starGO.AddComponent<MeshFilter>();
        var starMR = starGO.AddComponent<MeshRenderer>();
        starMF.sharedMesh = BuildStarMesh(0.9f, 0.38f, 5);
        starMR.sharedMaterial = new Material(mat);  // clone artifact material — always valid
        starMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        starMR.receiveShadows    = false;
        // Slow spin makes it look like a collectible pickup marker
        starGO.transform.DOLocalRotate(new Vector3(0f, 360f, 0f), 2.5f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear).SetLoops(-1);
        // Pulse between sizes to attract the eye
        starGO.transform.DOScale(Vector3.one * 1.9f, 0.55f)
            .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);

        TotalSpawned++;
        return go;
    }

    // =========================================================================

    void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (GameManager.Instance != null && GameManager.Instance.State != GameManager.GameState.Playing) return;
        if (other.gameObject.name != "Player" &&
            other.GetComponentInParent<PlayerMovement>() == null) return;
        Collect();
    }

    private void Collect()
    {
        if (_collected) return;
        _collected = true;
        TotalCollected++;

        ArtifactInventory.Add(Type);
        GameManager.Instance?.OnArtifactCollected(Type);

        string label = Type == ArtifactType.TimeBonus ? "+TIME" : "+FROST";
        Color  col   = Type == ArtifactType.TimeBonus
            ? new Color(1f, 0.78f, 0.1f)
            : new Color(0.2f, 0.85f, 1f);
        ShowPickupPopup(label, col);

        DOTween.Kill(transform, complete: false);
        Destroy(gameObject);
    }

    // =========================================================================
    // Minimap star mesh
    // =========================================================================

    // =========================================================================
    // Procedural crystal — 3 elongated diamond shards, visible from all sides
    // =========================================================================

    private static void BuildProceduralCrystal(GameObject root, Material mat)
    {
        // Shard config: (yOffset, scale, rotY)
        var shards = new (float yOff, float scaleY, float scaleXZ, float rotY)[]
        {
            (0.00f, 0.55f, 0.14f,   0f),
            (0.08f, 0.42f, 0.11f,  55f),
            (0.04f, 0.48f, 0.10f, -40f),
        };

        foreach (var s in shards)
        {
            var shard = new GameObject("Shard");
            shard.transform.SetParent(root.transform, false);
            shard.transform.localPosition = new Vector3(0f, s.yOff, 0f);
            shard.transform.localRotation = Quaternion.Euler(0f, s.rotY, 0f);
            shard.transform.localScale    = new Vector3(s.scaleXZ, s.scaleY, s.scaleXZ);

            var mf = shard.AddComponent<MeshFilter>();
            var mr = shard.AddComponent<MeshRenderer>();
            mf.sharedMesh      = BuildDiamondMesh();
            mr.sharedMaterial  = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
        }
    }

    /// <summary>
    /// Elongated diamond (octahedron-like): flat base at y=0, peak at y=1, bottom at y=-0.35.
    /// 6 sides — visible from any horizontal angle in the corridor.
    /// </summary>
    private static Mesh BuildDiamondMesh()
    {
        const int sides = 6;
        float topY  =  1.0f;
        float midY  =  0.3f;   // widest ring
        float botY  = -0.35f;
        float r     =  0.5f;

        int ringVerts = sides;
        // vertices: top(1) + ring(sides) + bottom(1)
        var verts = new Vector3[1 + ringVerts + 1];
        verts[0] = new Vector3(0f, topY, 0f);
        for (int i = 0; i < ringVerts; i++)
        {
            float a = i * Mathf.PI * 2f / ringVerts;
            verts[1 + i] = new Vector3(Mathf.Cos(a) * r, midY, Mathf.Sin(a) * r);
        }
        verts[1 + ringVerts] = new Vector3(0f, botY, 0f);

        var tris = new System.Collections.Generic.List<int>();
        // Top cap
        for (int i = 0; i < sides; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % sides;
            tris.Add(0); tris.Add(b); tris.Add(a);
        }
        // Bottom cap
        int bot = 1 + ringVerts;
        for (int i = 0; i < sides; i++)
        {
            int a = 1 + i;
            int b = 1 + (i + 1) % sides;
            tris.Add(bot); tris.Add(a); tris.Add(b);
        }

        var mesh = new Mesh { name = "DiamondShard" };
        mesh.vertices  = verts;
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void BuildFallbackSphere(GameObject root, Material mat)
    {
        var fb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fb.transform.SetParent(root.transform, false);
        fb.transform.localScale = Vector3.one * 0.4f;
        Object.Destroy(fb.GetComponent<Collider>());
        fb.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // Overrides ALL material slots on every renderer — sharedMaterial only sets slot 0.
    private static void ApplyMaterialAllSlots(GameObject model, Material mat)
    {
        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
        {
            var slots = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            mr.sharedMaterials = slots;
        }
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var slots = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
            smr.sharedMaterials = slots;
        }
    }

    // Scales model so its largest dimension equals targetSize world units.
    // Call immediately after Instantiate with localScale=Vector3.one.
    private static void AutoScaleModel(GameObject model, float targetSize)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { model.transform.localScale = Vector3.one * 0.3f; return; }
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim > 0.001f)
            model.transform.localScale = Vector3.one * (targetSize / maxDim);
    }

    // =========================================================================
    // Minimap star mesh
    // =========================================================================

    /// <summary>
    /// Procedural flat N-pointed star mesh in the XZ plane.
    /// Double-sided so the top-down minimap camera always sees it.
    /// </summary>
    private static Mesh BuildStarMesh(float outerR, float innerR, int points)
    {
        int total = points * 2;  // alternating outer/inner vertices
        var verts = new Vector3[total + 1];
        verts[0]  = Vector3.zero;  // center
        for (int i = 0; i < total; i++)
        {
            float angle = i * Mathf.PI / points;  // 180°/points per step
            float r = (i % 2 == 0) ? outerR : innerR;
            verts[i + 1] = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
        }

        // Double-sided: each sector triangle rendered from both sides
        var tris = new int[total * 6];
        for (int i = 0; i < total; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = (i + 1) % total + 1;
            tris[i * 6 + 0] = a; tris[i * 6 + 1] = b; tris[i * 6 + 2] = c;
            tris[i * 6 + 3] = a; tris[i * 6 + 4] = c; tris[i * 6 + 5] = b;
        }

        var mesh = new Mesh { name = "StarMesh" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // =========================================================================
    // Pickup popup — floats up and fades
    // =========================================================================

    static void ShowPickupPopup(string text, Color color)
    {
        var canvasGO   = new GameObject("ArtifactPickupPopup");
        var canvas     = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvasGO.AddComponent<CanvasScaler>();

        var cg = canvasGO.AddComponent<CanvasGroup>();
        cg.alpha          = 1f;
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        var txtGO = new GameObject("T");
        txtGO.transform.SetParent(canvasGO.transform, false);
        var rt          = txtGO.AddComponent<RectTransform>();
        rt.anchorMin    = new Vector2(0.15f, 0.4f);
        rt.anchorMax    = new Vector2(0.15f, 0.4f);
        rt.pivot        = new Vector2(0.5f,  0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta    = new Vector2(220f, 55f);

        var tmp       = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 36f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color;

        rt.DOAnchorPosY(85f, 1.8f).SetEase(Ease.OutCubic);
        DOTween.To(() => cg.alpha, a => cg.alpha = a, 0f, 1.8f)
               .SetEase(Ease.InCubic)
               .OnComplete(() => Object.Destroy(canvasGO));
    }

}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using DG.Tweening;

/// <summary>
/// Circular minimap — horror colour theme + exit direction arrow.
///
/// Arrow sits on the edge of the minimap circle and always points toward the exit.
/// It fades to 30% opacity when the exit is within the visible map area.
/// </summary>
public class MinimapSystem : MonoBehaviour
{
    private const float OrthoSize  = 18f;   // world units visible each side (~4 cells)
    private const float CamHeight  = 60f;
    private const int   TexSize    = 256;
    private const int   UISize     = 240;   // +33% larger minimap
    private const int   Margin     = 16;
    private const int   ArrowSize  = 26;    // px — size of the exit arrow

    // Horror palette
    private static readonly Color ColBackground = new Color(0.05f, 0.01f, 0.01f, 1f);  // near-black red
    private static readonly Color ColBorder     = new Color(0.35f, 0.04f, 0.04f, 0.9f); // dark blood-red
    private static readonly Color ColPlayerDot  = new Color(1.00f, 0.10f, 0.10f, 1f);  // bright red
    private static readonly Color ColArrow      = new Color(1.00f, 0.55f, 0.05f, 1f);  // amber

    private Camera      _minimapCam;
    private Transform   _player;
    private Vector3     _exitPos;
    private Color       _savedAmbient;
    private AmbientMode _savedAmbientMode;
    private bool        _savedFog;
    private Color       _savedFogColor;

    // Arrow UI
    private RectTransform _arrowRT;
    private Image         _arrowImg;

    // =========================================================================
    void OnEnable()
    {
        PlayerSpawner.OnPlayerSpawned             += OnPlayerSpawned;
        RenderPipelineManager.beginCameraRendering += OnBeginCamRender;
        RenderPipelineManager.endCameraRendering   += OnEndCamRender;
    }

    void OnDisable()
    {
        PlayerSpawner.OnPlayerSpawned             -= OnPlayerSpawned;
        RenderPipelineManager.beginCameraRendering -= OnBeginCamRender;
        RenderPipelineManager.endCameraRendering   -= OnEndCamRender;
    }

    void Start() => BuildMinimapUI();

    void OnPlayerSpawned(GameObject player)
    {
        // Wrapped in try-catch: an unhandled exception here blocks the entire
        // OnPlayerSpawned multicast chain (EnemySpawner, SoundManager never called).
        try
        {
            _player = player.transform;

            var mgr = FindAnyObjectByType<MazeManager>();
            if (mgr != null) _exitPos = mgr.ExitPosition;

            // Player direction arrow at Y=55 (above walls, seen only by minimap camera).
            // Flat mesh in XZ plane, parented to player so it rotates with them.
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");

            // Player direction arrow — flat mesh in XZ plane (seen from above).
            // Points toward player's +Z (forward). Rotates with the player transform.
            var arrowGO = new GameObject("MinimapPlayerArrow");
            arrowGO.transform.SetParent(player.transform, false);
            arrowGO.transform.localPosition = new Vector3(0f, 55f, 0f);
            arrowGO.transform.localScale    = Vector3.one * 1.8f;  // larger = easier to read on mobile

            var arrowMF = arrowGO.AddComponent<MeshFilter>();
            var arrowMR = arrowGO.AddComponent<MeshRenderer>();
            arrowMF.sharedMesh = BuildArrowMesh();
            // Bright white marker — high contrast on the dark minimap background
            if (shader != null)
                arrowMR.sharedMaterial = new Material(shader) { color = Color.white };
            arrowMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            arrowMR.receiveShadows    = false;

            // No scale pulse — keep shape readable so direction is always clear
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MinimapSystem] OnPlayerSpawned failed: " + e.Message);
        }
    }

    void LateUpdate()
    {
        if (_minimapCam == null || _player == null) return;

        // Follow player
        var p = _player.position;
        _minimapCam.transform.position = new Vector3(p.x, CamHeight, p.z);

        // Update exit arrow
        UpdateArrow();
    }

    // =========================================================================
    // Arrow
    // =========================================================================

    private void UpdateArrow()
    {
        if (_arrowRT == null || _exitPos == Vector3.zero) return;

        Vector3 toExit = _exitPos - _player.position;
        toExit.y = 0f;
        float dist = toExit.magnitude;

        if (dist < 2f)
        {
            _arrowRT.gameObject.SetActive(false);
            return;
        }
        _arrowRT.gameObject.SetActive(true);

        toExit.Normalize();

        // World XZ → UI XY
        float uiX = toExit.x;
        float uiY = toExit.z;

        // Place on edge of minimap circle
        float radius = UISize * 0.5f - ArrowSize * 0.5f - 4f;
        _arrowRT.anchoredPosition = new Vector2(uiX * radius, uiY * radius);

        // Rotate: sprite points up (0°) = +Y in UI = +Z in world
        _arrowRT.localEulerAngles = new Vector3(0f, 0f,
            -Mathf.Atan2(uiX, uiY) * Mathf.Rad2Deg);

        // Dim when exit is already visible on the minimap
        float alpha = dist > OrthoSize
            ? 1f
            : Mathf.Lerp(0.25f, 1f, dist / OrthoSize);
        _arrowImg.color = new Color(ColArrow.r, ColArrow.g, ColArrow.b, alpha);
    }

    // =========================================================================
    // Render hooks
    // =========================================================================

    private void OnBeginCamRender(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _minimapCam) return;
        _savedAmbient     = RenderSettings.ambientLight;
        _savedAmbientMode = RenderSettings.ambientMode;
        _savedFog         = RenderSettings.fog;
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 0.85f;
        RenderSettings.fog          = false;   // fog ruins top-down minimap view
    }

    private void OnEndCamRender(ScriptableRenderContext ctx, Camera cam)
    {
        if (cam != _minimapCam) return;
        RenderSettings.ambientMode  = _savedAmbientMode;
        RenderSettings.ambientLight = _savedAmbient;
        RenderSettings.fog          = _savedFog;
    }

    // =========================================================================
    // Build UI
    // =========================================================================

    private void BuildMinimapUI()
    {
        // Render texture
        var rt = new RenderTexture(TexSize, TexSize, 16) { name = "MinimapRT" };
        rt.Create();

        // Minimap camera
        var camGO = new GameObject("MinimapCamera");
        _minimapCam = camGO.AddComponent<Camera>();
        _minimapCam.orthographic     = true;
        _minimapCam.orthographicSize = OrthoSize;
        _minimapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        _minimapCam.transform.position = new Vector3(0f, CamHeight, 0f);
        _minimapCam.targetTexture    = rt;
        _minimapCam.depth            = -2;
        _minimapCam.clearFlags       = CameraClearFlags.SolidColor;
        _minimapCam.backgroundColor  = ColBackground;
        _minimapCam.cullingMask      = ~0;

        // Canvas
        var canvasGO = new GameObject("MinimapCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Border ring
        AddBorderRing(canvasGO.transform, UISize, Margin);

        // Circular mask
        var maskGO              = new GameObject("MinimapMask");
        maskGO.transform.SetParent(canvasGO.transform, false);
        var maskRect            = maskGO.AddComponent<RectTransform>();
        maskRect.anchorMin      = new Vector2(1f, 1f);
        maskRect.anchorMax      = new Vector2(1f, 1f);
        maskRect.pivot          = new Vector2(1f, 1f);
        maskRect.anchoredPosition = new Vector2(-Margin, -Margin);
        maskRect.sizeDelta      = new Vector2(UISize, UISize);
        var maskImg             = maskGO.AddComponent<Image>();
        maskImg.sprite          = CreateCircleSprite(128);
        maskGO.AddComponent<Mask>().showMaskGraphic = false;

        // Map image inside mask
        var rawGO       = new GameObject("MinimapImage");
        rawGO.transform.SetParent(maskGO.transform, false);
        var rawRect     = rawGO.AddComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = Vector2.zero;
        rawRect.offsetMax = Vector2.zero;
        rawGO.AddComponent<RawImage>().texture = rt;

        // Exit direction arrow (child of mask so it clips to circle)
        BuildArrow(maskGO.transform);
    }

    private void BuildArrow(Transform maskParent)
    {
        var go         = new GameObject("ExitArrow");
        go.transform.SetParent(maskParent, false);

        _arrowRT           = go.AddComponent<RectTransform>();
        _arrowRT.anchorMin = new Vector2(0.5f, 0.5f);
        _arrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        _arrowRT.pivot     = new Vector2(0.5f, 0.5f);
        _arrowRT.sizeDelta = new Vector2(ArrowSize, ArrowSize);

        _arrowImg        = go.AddComponent<Image>();
        _arrowImg.sprite = CreateArrowSprite(64);
        _arrowImg.color  = ColArrow;
        _arrowImg.raycastTarget = false;

        // Start hidden — shown once player spawns and _exitPos is known
        go.SetActive(false);
    }

    // =========================================================================
    // Sprite helpers
    // =========================================================================

    private static void AddBorderRing(Transform parent, int uiSize, int margin)
    {
        var go             = new GameObject("MinimapBorder");
        go.transform.SetParent(parent, false);
        go.transform.SetSiblingIndex(0);
        var r              = go.AddComponent<RectTransform>();
        r.anchorMin        = new Vector2(1f, 1f);
        r.anchorMax        = new Vector2(1f, 1f);
        r.pivot            = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-margin, -margin);
        r.sizeDelta        = new Vector2(uiSize + 6, uiSize + 6);
        var img            = go.AddComponent<Image>();
        img.sprite         = CreateCircleSprite(128);
        img.color          = ColBorder;
        img.raycastTarget  = false;
    }

    private static Sprite CreateCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size * 0.5f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            pixels[y * size + x] = dx * dx + dy * dy <= r * r ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    /// <summary>Filled triangle pointing upward — rotated by RectTransform to indicate direction.</summary>
    private static Sprite CreateArrowSprite(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // Triangle: tip at top-center, base at bottom
            float t         = (float)y / (size - 1);           // 0=bottom 1=top
            float halfWidth = (1f - t) * half;
            bool inside     = Mathf.Abs(x - half) < halfWidth;
            pixels[y * size + x] = inside ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    // =========================================================================
    // 3-D arrow mesh helpers
    // =========================================================================

    /// <summary>
    /// GPS/radar-style marker: rounded body (shows position) + forward nose (shows direction).
    /// Teardrop shape — clearly distinguishes front from back at a glance.
    /// Double-sided so the top-down minimap camera always sees it.
    /// +Z = player forward. Scale set in OnPlayerSpawned.
    /// </summary>
    private static Mesh BuildArrowMesh()
    {
        var mesh = new Mesh { name = "PlayerMarker" };

        // 9 outer vertices forming a teardrop/GPS-pin outline + center
        // Shape: pointed nose at +Z, rounded body widening then tapering to back
        var verts = new Vector3[]
        {
            new Vector3( 0.00f, 0f,  1.15f),  // 0  nose tip
            new Vector3( 0.42f, 0f,  0.55f),  // 1  right shoulder
            new Vector3( 0.62f, 0f,  0.00f),  // 2  right wide
            new Vector3( 0.48f, 0f, -0.52f),  // 3  right tail
            new Vector3( 0.00f, 0f, -0.80f),  // 4  back center
            new Vector3(-0.48f, 0f, -0.52f),  // 5  left tail
            new Vector3(-0.62f, 0f,  0.00f),  // 6  left wide
            new Vector3(-0.42f, 0f,  0.55f),  // 7  left shoulder
            new Vector3( 0.00f, 0f,  0.00f),  // 8  center (fan origin)
        };

        // Fan from center — 8 segments covering the full outline
        var frontTris = new int[]
        {
            8,0,1,  8,1,2,  8,2,3,  8,3,4,
            8,4,5,  8,5,6,  8,6,7,  8,7,0,
        };
        // Reversed winding for back face (double-sided)
        var backTris = new int[]
        {
            8,1,0,  8,2,1,  8,3,2,  8,4,3,
            8,5,4,  8,6,5,  8,7,6,  8,0,7,
        };

        var tris = new int[frontTris.Length + backTris.Length];
        frontTris.CopyTo(tris, 0);
        backTris.CopyTo(tris, frontTris.Length);

        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

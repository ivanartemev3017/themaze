using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the virtual joystick canvas at runtime — no manual Editor setup needed.
/// Left side: movement joystick (floating).
/// Right side: camera swipe handled by TouchCameraInput (separate component on Main Camera).
///
/// Joystick zone = left half of screen, bottom 60%.
/// </summary>
public class MobileControls : MonoBehaviour
{
    // Canvas reference resolution — matches rest of HUD
    private const float RefW  = 1080f;
    private const float RefH  = 1920f;

    // Joystick visual sizes (canvas px at reference resolution)
    private const float BgSize    = 280f;
    private const float ThumbSize = 130f;
    private const float Range     = 110f;

    // =========================================================================
    void Awake()
    {
        // Lock to landscape for this scene
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        EnsureEventSystem();
        BuildJoystick();
        EnsureTouchCameraInput();
    }

    // =========================================================================
    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static void EnsureTouchCameraInput()
    {
        // TouchCameraInput reads raw Input.touches on right half — no UI needed.
        // Just ensure the component exists on Main Camera.
        if (FindAnyObjectByType<TouchCameraInput>() != null) return;
        var cam = Camera.main;
        if (cam != null)
            cam.gameObject.AddComponent<TouchCameraInput>();
    }

    private static void BuildJoystick()
    {
        // ----- Canvas -----
        var canvasGO        = new GameObject("JoystickCanvas");
        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;   // below HUD (10) and minimap (10)

        var scaler                 = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ----- Input zone: left 50 %, bottom 70 % -----
        // FloatingJoystick component lives here; it reads its own RectTransform as
        // 'baseRect' so the zone doubles as the component root.
        var zoneGO       = new GameObject("FloatingJoystick");
        zoneGO.transform.SetParent(canvasGO.transform, false);
        var zoneRT       = zoneGO.AddComponent<RectTransform>();
        zoneRT.anchorMin = new Vector2(0f, 0f);
        zoneRT.anchorMax = new Vector2(0.5f, 0.42f);
        zoneRT.offsetMin = Vector2.zero;
        zoneRT.offsetMax = Vector2.zero;
        // Transparent image — required so pointer events hit this element
        var zoneImg      = zoneGO.AddComponent<Image>();
        zoneImg.color    = Color.clear;

        // ----- Background ring (shown on touch, repositioned to finger) -----
        var bgGO       = new GameObject("Background");
        bgGO.transform.SetParent(zoneGO.transform, false);
        var bgRT       = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot     = new Vector2(0.5f, 0.5f);
        bgRT.sizeDelta = new Vector2(BgSize, BgSize);
        var bgImg      = bgGO.AddComponent<Image>();
        bgImg.sprite   = MakeCircleSprite(128);
        bgImg.color    = new Color(1f, 1f, 1f, 0.22f);
        bgImg.raycastTarget = false;
        // Leave active=true here; FloatingJoystick.Start() will call SetActive(false)

        // ----- Handle (thumb dot, child of Background so it moves with it) -----
        // Joystick.cs sets handle.anchoredPosition relative to its parent (background),
        // so parenting here keeps the maths correct.
        var handleGO       = new GameObject("Handle");
        handleGO.transform.SetParent(bgGO.transform, false);
        var handleRT       = handleGO.AddComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0.5f, 0.5f);
        handleRT.anchorMax = new Vector2(0.5f, 0.5f);
        handleRT.pivot     = new Vector2(0.5f, 0.5f);
        handleRT.sizeDelta = new Vector2(ThumbSize, ThumbSize);
        var handleImg      = handleGO.AddComponent<Image>();
        handleImg.sprite   = MakeCircleSprite(64);
        handleImg.color    = new Color(1f, 1f, 1f, 0.45f);
        handleImg.raycastTarget = false;

        // ----- Wire FloatingJoystick — inject serialized fields via reflection -----
        // AddComponent calls Awake immediately (no fields accessed there), so we
        // can safely inject before Start() runs on the next frame.
        var joystick = zoneGO.AddComponent<FloatingJoystick>();

        var baseType = typeof(Joystick);
        var flags    = System.Reflection.BindingFlags.NonPublic |
                       System.Reflection.BindingFlags.Instance;
        baseType.GetField("background", flags)?.SetValue(joystick, bgRT);
        baseType.GetField("handle",     flags)?.SetValue(joystick, handleRT);

        // Dead zone is enforced here (Joystick Pack) AND in PlayerMovement (keyboard parity).
        joystick.DeadZone    = 0.15f;
        joystick.HandleRange = Range / (BgSize * 0.5f);  // normalise to 0-1 range
    }

    // =========================================================================
    private static Sprite MakeCircleSprite(int size)
    {
        var tex    = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        float r    = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            pixels[y * size + x] = (dx*dx + dy*dy) <= r*r ? Color.white : Color.clear;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}

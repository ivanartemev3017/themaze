using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MazeRunner
{
    /// <summary>Code-built uGUI: procedural sprites, consistent palette, small builder helpers.</summary>
    public static class UIKit
    {
        // ── palette ─────────────────────────────────────────────────────────────
        public static readonly Color Ink = new(0.96f, 0.94f, 0.89f);
        public static readonly Color Muted = new(0.62f, 0.64f, 0.68f);
        public static readonly Color PanelColor = new(0.055f, 0.06f, 0.075f, 0.94f);
        public static readonly Color PanelSoft = new(1f, 1f, 1f, 0.07f);
        public static readonly Color Amber = new(1f, 0.69f, 0.26f);
        public static readonly Color AmberDark = new(0.16f, 0.09f, 0.02f);
        public static readonly Color Teal = new(0.35f, 0.95f, 0.78f);
        public static readonly Color Danger = new(1f, 0.33f, 0.25f);
        public static readonly Color Gold = new(1f, 0.8f, 0.3f);
        public static readonly Color StarEmpty = new(1f, 1f, 1f, 0.16f);

        // ── sprites ─────────────────────────────────────────────────────────────
        static Sprite _rounded, _circle, _ring, _star, _lock, _pause, _gear, _diamond, _arrow, _key, _clock, _soft;
        public static Sprite Rounded => _rounded ??= MakeRounded(96, 30);
        public static Sprite CircleSprite => _circle ??= Make(128, (x, y) => Circle(x, y, 0.96f), 0);
        public static Sprite RingSprite => _ring ??= Make(128, (x, y) => Mathf.Min(Circle(x, y, 0.96f), 1f - Circle(x, y, 0.80f)), 0);
        public static Sprite Star => _star ??= Make(128, StarShape, 0);
        public static Sprite Diamond => _diamond ??= Make(96, (x, y) => Edge(1f - (Mathf.Abs(x) * 1.25f + Mathf.Abs(y))), 0);
        public static Sprite Arrow => _arrow ??= Make(96, ArrowShape, 0);
        public static Sprite Pause => _pause ??= Make(96, (x, y) => Mathf.Max(Box(x + 0.3f, y, 0.13f, 0.6f), Box(x - 0.3f, y, 0.13f, 0.6f)), 0);
        public static Sprite Lock => _lock ??= Make(96, LockShape, 0);
        public static Sprite Gear => _gear ??= Make(128, GearShape, 0);
        public static Sprite KeyIcon => _key ??= Make(96, KeyShape, 0);
        public static Sprite Clock => _clock ??= Make(96, ClockShape, 0);
        public static Sprite Soft => _soft ??= Make(128, (x, y) => Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(x * x + y * y)), 1.6f), 0);

        static float Edge(float d) => Mathf.Clamp01(d * 40f + 0.5f);
        static float Circle(float x, float y, float r) => Edge(r - Mathf.Sqrt(x * x + y * y));
        static float Box(float x, float y, float hw, float hh) => Mathf.Min(Edge(hw - Mathf.Abs(x)), Edge(hh - Mathf.Abs(y)));

        static float StarShape(float x, float y)
        {
            float a = Mathf.Atan2(x, y);
            float r = Mathf.Sqrt(x * x + y * y);
            float seg = Mathf.PI * 2f / 5f;
            float k = Mathf.Abs(Mathf.Repeat(a + seg * 0.5f, seg) - seg * 0.5f) / (seg * 0.5f);
            float rad = Mathf.Lerp(0.95f, 0.42f, k);
            return Edge(rad - r);
        }

        static float ArrowShape(float x, float y)
        {
            // Pointing up: tip at y=0.85, base at y=-0.6.
            float d = Mathf.Min(Mathf.Min(0.85f - y, y + 0.6f), (0.85f - y) * 0.62f - Mathf.Abs(x));
            return Edge(d);
        }

        static float LockShape(float x, float y)
        {
            float body = Box(x, y + 0.25f, 0.55f, 0.42f);
            float r = Mathf.Sqrt(x * x + (y - 0.2f) * (y - 0.2f));
            float shackle = y > 0.15f ? Mathf.Min(Edge(0.42f - r), Edge(r - 0.24f)) : 0f;
            float legs = Mathf.Max(Box(x - 0.33f, y + 0.05f, 0.09f, 0.25f), Box(x + 0.33f, y + 0.05f, 0.09f, 0.25f));
            float hole = Circle(x, y + 0.2f, 0.11f);
            return Mathf.Max(Mathf.Max(body, shackle), legs * (y > 0.15f ? 0 : 1)) * (1f - hole);
        }

        static float GearShape(float x, float y)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Mathf.Atan2(y, x);
            float teeth = Mathf.Cos(a * 8f) > 0.2f ? 0.93f : 0.72f;
            return Mathf.Min(Edge(teeth - r), Edge(r - 0.3f));
        }

        static float KeyShape(float x, float y)
        {
            float bow = Mathf.Min(Circle(x + 0.45f, y, 0.4f), 1f - Circle(x + 0.45f, y, 0.18f));
            float shaft = Box(x - 0.25f, y, 0.55f, 0.1f);
            float bit = Mathf.Max(Box(x - 0.55f, y - 0.18f, 0.08f, 0.18f), Box(x - 0.3f, y - 0.15f, 0.07f, 0.14f));
            return Mathf.Max(bow, Mathf.Max(shaft, bit));
        }

        static float ClockShape(float x, float y)
        {
            float ring = Mathf.Min(Circle(x, y, 0.92f), 1f - Circle(x, y, 0.74f));
            float hands = Mathf.Max(Box(x, y - 0.25f, 0.07f, 0.3f), Box(x + 0.2f, y, 0.25f, 0.07f));
            return Mathf.Max(ring, hands);
        }

        static Sprite Make(int size, Func<float, float, float> alpha, int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float x = (i + 0.5f) / size * 2f - 1f, y = (j + 0.5f) / size * 2f - 1f;
                px[j * size + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * border);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float cx = Mathf.Clamp(i + 0.5f, radius, size - radius), cy = Mathf.Clamp(j + 0.5f, radius, size - radius);
                float d = radius - Vector2.Distance(new Vector2(i + 0.5f, j + 0.5f), new Vector2(cx, cy));
                px[j * size + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(d + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * (radius + 2));
        }

        // ── builders ────────────────────────────────────────────────────────────
        public static RectTransform CreateCanvas(string name, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
                UnityEngine.Object.DontDestroyOnLoad(es);
            }
            return (RectTransform)go.transform;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.one * inset; rt.offsetMax = -Vector2.one * inset;
            return rt;
        }

        /// <summary>Anchor at a normalized point with a fixed size, offset in reference pixels.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset = default, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sprite.border != Vector4.zero) img.type = UnityEngine.UI.Image.Type.Sliced;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast = true)
            => Image(parent, name, Rounded, color, raycast);

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, bool title = false,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var rt = Node(parent, "Text");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var bank = AssetBank.I;
            var font = title ? bank.TitleFont : bank.Font;
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public enum Style { Primary, Secondary, Ghost }

        public static Button Button(Transform parent, string label, Action onClick, Style style = Style.Primary, float fontSize = 46)
        {
            var bg = Panel(parent, "Button " + label, style switch
            {
                Style.Primary => Amber,
                Style.Secondary => new Color(1f, 1f, 1f, 0.1f),
                _ => new Color(0, 0, 0, 0),
            });
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { Sfx.I?.Click(); onClick?.Invoke(); });
            bg.gameObject.AddComponent<PressFx>();
            if (!string.IsNullOrEmpty(label))
            {
                var t = Text(bg.transform, label, fontSize, style == Style.Primary ? AmberDark : Ink, title: true);
                t.rectTransform.Fill();
                t.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return btn;
        }

        public static Button IconButton(Transform parent, Sprite icon, Action onClick, float size = 120f)
        {
            var bg = Image(parent, "IconButton", CircleSprite, new Color(0f, 0f, 0f, 0.45f), true);
            bg.rectTransform.sizeDelta = Vector2.one * size;
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { Sfx.I?.Click(); onClick?.Invoke(); });
            bg.gameObject.AddComponent<PressFx>();
            var ic = Image(bg.transform, "Icon", icon, Ink);
            ic.rectTransform.Fill(size * 0.27f);
            return btn;
        }

        public static void Stars(Transform parent, int stars, float size, float spacing, Vector2 center)
        {
            for (int i = 0; i < 3; i++)
            {
                var s = Image(parent, "Star", Star, i < stars ? Gold : StarEmpty);
                s.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.one * size, center + new Vector2((i - 1) * spacing, i == 1 ? size * 0.12f : 0f));
            }
        }

        /// <summary>GetComponent-or-AddComponent. Never use ?? on Unity objects: destroyed/missing ones are "fake null".</summary>
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static CanvasGroup FadeIn(RectTransform rt, float duration = 0.25f)
        {
            var cg = rt.gameObject.GetOrAdd<CanvasGroup>();
            cg.alpha = 0f;
            cg.DOFade(1f, duration).SetUpdate(true);
            return cg;
        }
    }

    /// <summary>Squash on press — tactile feel for every button.</summary>
    public sealed class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData e) { transform.DOKill(); transform.DOScale(0.94f, 0.08f).SetUpdate(true); }
        public void OnPointerUp(PointerEventData e) { transform.DOKill(); transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true); }
        void OnDestroy() => transform.DOKill();
    }
}

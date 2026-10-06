using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static MazeRunner.UIKit;

namespace MazeRunner
{
    /// <summary>All menu-type screens. Each builds itself into the given root.</summary>
    public static class Screens
    {
        static readonly Vector2 Mid = new(0.5f, 0.5f);
        static App App => App.I;

        // ── main menu ───────────────────────────────────────────────────────────
        public static void Menu(RectTransform root)
        {
            SideShade(root, 1150f);

            var title = Text(root, Loc.Get("title"), 168, Ink, title: true, TextAlignmentOptions.Left);
            title.rectTransform.Place(new Vector2(0, 1), new Vector2(1100, 200), new Vector2(110, -120), new Vector2(0, 1));
            title.characterSpacing = 6;
            var tag = Text(root, Loc.Get("tagline"), 46, Amber, false, TextAlignmentOptions.Left);
            tag.rectTransform.Place(new Vector2(0, 1), new Vector2(1000, 70), new Vector2(118, -320), new Vector2(0, 1));

            int ch = Mathf.Clamp(Save.D.LastChapter, 0, LevelConfig.ChapterCount - 1);
            if (!Save.ChapterOpen(ch)) ch = 0;
            int lv = Save.NextLevel(ch);

            var play = Button(root, Loc.Get("play"), () => App.Play(LevelConfig.For(ch, lv)), Style.Primary, 64);
            play.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(620, 150), new Vector2(110, 430), new Vector2(0, 0));
            var sub = Text(play.transform, $"{Loc.Get("ch" + ch)} · {Loc.Get("level")} {lv + 1}", 30, new Color(0.16f, 0.09f, 0.02f, 0.7f));
            sub.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(600, 40), new Vector2(0, 14), new Vector2(0.5f, 0));
            var playLabel = play.GetComponentInChildren<TextMeshProUGUI>();
            playLabel.rectTransform.offsetMin = new Vector2(0, 30);
            play.transform.DOScale(1.035f, 0.9f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(play.gameObject);

            var levels = Button(root, Loc.Get("levels"), () => App.ShowLevels(ch), Style.Secondary, 44);
            levels.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(300, 110), new Vector2(110, 290), new Vector2(0, 0));

            var daily = Button(root, Loc.Get("daily"), () => App.Play(LevelConfig.Daily(System.DateTime.Now)), Style.Secondary, 38);
            daily.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(300, 110), new Vector2(430, 290), new Vector2(0, 0));

            string today = System.DateTime.Now.ToString("yyyy-MM-dd");
            string dailyInfo = Save.D.DailyDate == today ? Loc.F("daily_best", Loc.Time(Save.D.DailyBest)) : Loc.F("daily_sub", Save.D.DailyStreak);
            var di = Text(root, dailyInfo, 28, Muted, false, TextAlignmentOptions.Left);
            di.rectTransform.Place(new Vector2(0, 0), new Vector2(700, 40), new Vector2(118, 236), new Vector2(0, 0));

            int total = 0; for (int c = 0; c < LevelConfig.ChapterCount; c++) total += Save.ChapterStars(c);
            var stars = Image(root, "StarIcon", Star, Gold);
            stars.rectTransform.Place(new Vector2(0, 0), new Vector2(44, 44), new Vector2(118, 120), new Vector2(0, 0));
            var st = Text(root, $"{total} / {LevelConfig.ChapterCount * LevelConfig.LevelsPerChapter * 3}", 34, Ink, false, TextAlignmentOptions.Left);
            st.rectTransform.Place(new Vector2(0, 0), new Vector2(400, 44), new Vector2(176, 120), new Vector2(0, 0));

            var gear = IconButton(root, Gear, App.ShowSettings, 110);
            gear.GetComponent<RectTransform>().Place(new Vector2(1, 1), new Vector2(110, 110), new Vector2(-50, -50));
        }

        static void SideShade(RectTransform root, float width)
        {
            var shade = Image(root, "Shade", Gradient, new Color(0, 0, 0, 0.88f));
            shade.rectTransform.anchorMin = Vector2.zero; shade.rectTransform.anchorMax = new Vector2(0, 1);
            shade.rectTransform.pivot = new Vector2(0, 0.5f);
            shade.rectTransform.sizeDelta = new Vector2(width, 0);
        }

        static Sprite _gradient;
        static Sprite Gradient
        {
            get
            {
                if (_gradient != null) return _gradient;
                var tex = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int i = 0; i < 256; i++) { float t = i / 255f; tex.SetPixel(i, 0, new Color(1, 1, 1, Mathf.Pow(1f - t, 1.4f))); }
                tex.Apply();
                return _gradient = Sprite.Create(tex, new Rect(0, 0, 256, 1), new Vector2(0.5f, 0.5f));
            }
        }

        // ── level select ────────────────────────────────────────────────────────
        public static void Levels(RectTransform root, int chapter)
        {
            var bg = Image(root, "Bg", null, new Color(0.02f, 0.02f, 0.03f, 0.72f));
            bg.rectTransform.Fill();

            var back = IconButton(root, Arrow, App.ShowMenu, 110);
            back.GetComponent<RectTransform>().Place(new Vector2(0, 1), new Vector2(110, 110), new Vector2(50, -50));
            back.transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, 90);

            // Chapter tabs.
            for (int c = 0; c < LevelConfig.ChapterCount; c++)
            {
                int cc = c;
                bool sel = c == chapter;
                var tab = Button(root, "", () => App.ShowLevels(cc), sel ? Style.Primary : Style.Secondary);
                tab.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(470, 120), new Vector2((c - 1) * 500, -50), new Vector2(0.5f, 1));
                var name = Text(tab.transform, Loc.Get("ch" + c), 34, sel ? AmberDark : Ink, title: true);
                name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(460, 60), new Vector2(0, -12), new Vector2(0.5f, 1));
                bool open = Save.ChapterOpen(c);
                var infoColor = sel ? new Color(0.16f, 0.09f, 0.02f, 0.75f) : Muted;
                var info = Text(tab.transform, open ? $"{Save.ChapterStars(c)}/{LevelConfig.LevelsPerChapter * 3}" : Loc.Get("locked"), 28, infoColor);
                info.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(460, 40), new Vector2(open ? 18 : 0, 12), new Vector2(0.5f, 0));
                if (open)
                {
                    var si = Image(tab.transform, "StarIcon", Star, infoColor);
                    si.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(30, 30), new Vector2(-62, 17), new Vector2(0.5f, 0));
                }
            }

            var grid = Node(root, "Grid").Place(Mid, new Vector2(1640, 720), new Vector2(0, -70));
            bool chapterOpen = Save.ChapterOpen(chapter);
            int next = Save.NextLevel(chapter);
            const int cols = 10;
            for (int i = 0; i < LevelConfig.LevelsPerChapter && chapterOpen; i++)
            {
                int lv = i;
                int col = i % cols, row = i / cols;
                bool open = Save.LevelOpen(chapter, i);
                int stars = Save.StarsOf(chapter, i);
                var tile = Panel(grid, "L" + i, open ? (i == next ? new Color(1f, 0.69f, 0.26f, 0.22f) : PanelSoft) : new Color(1, 1, 1, 0.03f));
                tile.rectTransform.Place(new Vector2(0, 1), new Vector2(148, 200), new Vector2(col * 165f + 8, -row * 240f), new Vector2(0, 1));
                if (open)
                {
                    var btn = tile.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => { Sfx.I?.Click(); App.Play(LevelConfig.For(chapter, lv)); });
                    tile.gameObject.AddComponent<PressFx>();
                    var num = Text(tile.transform, (i + 1).ToString(), 64, i == next ? Amber : Ink, title: true);
                    num.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(140, 100), new Vector2(0, -30), new Vector2(0.5f, 1));
                    for (int s = 0; s < 3; s++)
                    {
                        var si = Image(tile.transform, "s", Star, s < stars ? Gold : StarEmpty);
                        si.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(38, 38), new Vector2((s - 1) * 40, 26), new Vector2(0.5f, 0));
                    }
                    if (i == next) tile.transform.DOScale(1.05f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(tile.gameObject);
                }
                else
                {
                    var l = Image(tile.transform, "Lock", Lock, new Color(1, 1, 1, 0.18f));
                    l.rectTransform.Place(Mid, new Vector2(64, 64));
                }
            }

            if (!chapterOpen)
            {
                var cover = Panel(root, "Cover", new Color(0.03f, 0.03f, 0.04f, 0.94f));
                cover.rectTransform.Place(Mid, new Vector2(1100, 560), new Vector2(0, -70));
                var l = Image(cover.transform, "Lock", Lock, Amber);
                l.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(110, 110), new Vector2(0, -50), new Vector2(0.5f, 1));
                var t = Text(cover.transform, Loc.Get("unlock_body"), 38, Ink);
                t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(860, 170), new Vector2(0, -180), new Vector2(0.5f, 1));
                var b = Button(cover.transform, UnlockLabel(), () => Store.I?.Buy(), Style.Primary, 48);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(620, 130), new Vector2(0, 50), new Vector2(0.5f, 0));
            }
        }

        static string UnlockLabel()
        {
            var price = Store.I != null ? Store.I.Price : "";
            return string.IsNullOrEmpty(price) ? Loc.Get("unlock") : $"{Loc.Get("unlock")} · {price}";
        }

        // ── overlays ────────────────────────────────────────────────────────────
        static RectTransform Card(RectTransform overlay, Vector2 size)
        {
            var card = Panel(overlay, "Card", PanelColor);
            card.rectTransform.Place(Mid, size);
            card.transform.localScale = Vector3.one * 0.9f;
            card.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(card.gameObject);
            return card.rectTransform;
        }

        static TextMeshProUGUI Heading(RectTransform card, string text, Color color, float y = -50)
        {
            var t = Text(card, text, 84, color, title: true);
            t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(card.sizeDelta.x - 60, 110), new Vector2(0, y), new Vector2(0.5f, 1));
            return t;
        }

        static void Stack(RectTransform card, float startY, params (string label, System.Action action, Style style)[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                var (label, action, style) = buttons[i];
                var b = Button(card, label, action, style, 46);
                b.GetComponent<RectTransform>().Place(new Vector2(0.5f, 1), new Vector2(520, 116), new Vector2(0, startY - i * 136), new Vector2(0.5f, 1));
            }
        }

        public static void Pause(RectTransform overlay)
        {
            var card = Card(overlay, new Vector2(680, 720));
            Heading(card, Loc.Get("paused"), Ink);
            Stack(card, -200,
                (Loc.Get("resume"), App.CloseOverlay, Style.Primary),
                (Loc.Get("retry"), App.Retry, Style.Secondary),
                (Loc.Get("settings"), App.ShowSettings, Style.Secondary),
                (Loc.Get("menu"), App.ShowMenu, Style.Ghost));
        }

        public static void Settings(RectTransform overlay)
        {
            var card = Card(overlay, new Vector2(760, 780));
            Heading(card, Loc.Get("settings"), Ink);
            Toggle(card, -200, Loc.Get("sound"), () => Save.D.Sound, v => Save.D.Sound = v);
            Toggle(card, -320, Loc.Get("music"), () => Save.D.Music, v => Save.D.Music = v);
            Toggle(card, -440, Loc.Get("vibration"), () => Save.D.Vibration, v => Save.D.Vibration = v);
            var restore = Button(card, Loc.Get("restore"), () => Store.I?.Restore(), Style.Ghost, 34);
            restore.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(400, 90), new Vector2(0, 150), new Vector2(0.5f, 0));
            var close = Button(card, Loc.Get("back"), App.CloseOverlay, Style.Secondary, 42);
            close.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(400, 110), new Vector2(0, 40), new Vector2(0.5f, 0));
        }

        static void Toggle(RectTransform card, float y, string label, System.Func<bool> get, System.Action<bool> set)
        {
            var row = Node(card, label).Place(new Vector2(0.5f, 1), new Vector2(620, 100), new Vector2(0, y), new Vector2(0.5f, 1));
            var t = Text(row, label, 44, Ink, false, TextAlignmentOptions.Left);
            t.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(380, 80), Vector2.zero, new Vector2(0, 0.5f));
            Button btn = null;
            btn = Button(row, get() ? Loc.Get("on") : Loc.Get("off"), () =>
            {
                set(!get());
                Save.Flush();
                Sfx.I?.Apply();
                btn.GetComponentInChildren<TextMeshProUGUI>().text = get() ? Loc.Get("on") : Loc.Get("off");
                btn.GetComponent<Image>().color = get() ? Amber : new Color(1, 1, 1, 0.1f);
                btn.GetComponentInChildren<TextMeshProUGUI>().color = get() ? AmberDark : Ink;
            }, get() ? Style.Primary : Style.Secondary, 38);
            btn.GetComponent<RectTransform>().Place(new Vector2(1, 0.5f), new Vector2(200, 90), Vector2.zero, new Vector2(1, 0.5f));
        }

        public static void Unlock(RectTransform overlay)
        {
            var card = Card(overlay, new Vector2(1000, 820));
            Heading(card, Loc.Get("unlock_title"), Amber);
            for (int c = 1; c < LevelConfig.ChapterCount; c++)
            {
                var chip = Panel(card, "Chip", PanelSoft, false);
                chip.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(420, 90), new Vector2((c - 1.5f) * 450, -190), new Vector2(0.5f, 1));
                var ct = Text(chip.transform, Loc.Get("ch" + c), 36, Ink, title: true);
                ct.rectTransform.Fill();
            }
            var body = Text(card, Loc.Get("unlock_body"), 38, Ink);
            body.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(860, 140), new Vector2(0, -300), new Vector2(0.5f, 1));
            var buy = Button(card, UnlockLabel(), () => Store.I?.Buy(), Style.Primary, 50);
            buy.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(640, 130), new Vector2(0, 160), new Vector2(0.5f, 0));
            var close = Button(card, Loc.Get("back"), App.CloseOverlay, Style.Ghost, 38);
            close.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(400, 100), new Vector2(0, 40), new Vector2(0.5f, 0));
        }

        public static void Result(RectTransform overlay, LevelResult r)
        {
            var card = Card(overlay, new Vector2(900, 900));
            if (r.Won)
            {
                Heading(card, Loc.Get("escaped"), Teal, -40);
                // Stars pop in one by one.
                bool[] got = { r.StarExit, r.StarPar, r.StarShards };
                for (int i = 0; i < 3; i++)
                {
                    var bgStar = Image(card, "StarBg", Star, StarEmpty);
                    var pos = new Vector2((i - 1) * 170, i == 1 ? -190 : -215);
                    bgStar.rectTransform.Place(new Vector2(0.5f, 1), Vector2.one * 150, pos, new Vector2(0.5f, 1));
                    if (!got[i]) continue;
                    var s = Image(card, "Star", Star, Gold);
                    s.rectTransform.Place(new Vector2(0.5f, 1), Vector2.one * 150, pos, new Vector2(0.5f, 1));
                    s.transform.localScale = Vector3.zero;
                    int idx = i;
                    s.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetDelay(0.35f + i * 0.28f).SetUpdate(true).SetLink(s.gameObject)
                        .OnStart(() => Sfx.I?.Shard());
                }

                string[] lines =
                {
                    Loc.Get("star_exit"),
                    Loc.F("star_par", Loc.Time(r.Cfg.ParTime)),
                    Loc.Get("star_shards"),
                };
                for (int i = 0; i < 3; i++)
                {
                    var icon = Image(card, "Bullet", Star, got[i] ? Gold : StarEmpty);
                    icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(34, 34), new Vector2(-290, -408 - i * 52), new Vector2(0.5f, 1));
                    var t = Text(card, lines[i], 34, got[i] ? Ink : Muted, false, TextAlignmentOptions.Left);
                    t.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(540, 50), new Vector2(20, -400 - i * 52), new Vector2(0.5f, 1));
                }

                string timeLine = $"{Loc.Get("time")}  {Loc.Time(r.Time)}";
                if (r.NewBest) timeLine += $"   <color=#FFB042>{Loc.Get("new_best")}</color>";
                var tl = Text(card, timeLine, 40, Ink, title: true);
                tl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(800, 60), new Vector2(0, -570), new Vector2(0.5f, 1));

                var next = Button(card, Loc.Get(r.Cfg.IsDaily ? "menu" : "next"), App.PlayNext, Style.Primary, 52);
                next.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(380, 130), new Vector2(215, 50), new Vector2(0.5f, 0));
                var retry = Button(card, Loc.Get("retry"), App.Retry, Style.Secondary, 44);
                retry.GetComponent<RectTransform>().Place(new Vector2(0.5f, 0), new Vector2(300, 130), new Vector2(-140, 50), new Vector2(0.5f, 0));
                var menu = IconButton(card, Arrow, App.ShowMenu, 100);
                menu.GetComponent<RectTransform>().Place(new Vector2(0, 0), new Vector2(100, 100), new Vector2(40, 65), new Vector2(0, 0));
                menu.transform.GetChild(0).localRotation = Quaternion.Euler(0, 0, 90);
            }
            else
            {
                bool caught = r.Reason == "caught";
                Heading(card, Loc.Get(caught ? "caught" : "timeout"), Danger, -80);
                var sub = Text(card, Loc.Get(caught ? "caught_sub" : "timeout_sub"), 40, Muted);
                sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(760, 120), new Vector2(0, -220), new Vector2(0.5f, 1));
                Stack(card, -420,
                    (Loc.Get("retry"), App.Retry, Style.Primary),
                    (Loc.Get("menu"), App.ShowMenu, Style.Secondary));
            }
        }

        // ── toast outside gameplay ──────────────────────────────────────────────
        public static void FloatingToast(RectTransform canvas, string text)
        {
            var p = Panel(canvas, "Toast", new Color(0, 0, 0, 0.8f), false);
            p.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(1100, 110), new Vector2(0, 80), new Vector2(0.5f, 0));
            var t = Text(p.transform, text, 36, Ink);
            t.rectTransform.Fill(16);
            var cg = p.gameObject.AddComponent<CanvasGroup>();
            DOTween.Sequence().SetUpdate(true).SetLink(p.gameObject)
                .Append(cg.DOFade(1, 0.2f).From(0))
                .AppendInterval(2.4f)
                .Append(cg.DOFade(0, 0.4f))
                .OnComplete(() => Object.Destroy(p.gameObject));
        }
    }
}

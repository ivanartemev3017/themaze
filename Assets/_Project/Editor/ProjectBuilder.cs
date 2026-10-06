using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace MazeRunner.EditorTools
{
    /// <summary>
    /// Creates every generated asset of v2 (materials, fonts, post profile, AssetBank, Main scene, build settings).
    /// Idempotent: safe to run again after changing anything here.
    /// Batch: Unity -batchmode -quit -executeMethod MazeRunner.EditorTools.ProjectBuilder.BuildAll
    /// </summary>
    public static class ProjectBuilder
    {
        const string Root = "Assets/_Project";
        const string Tex = Root + "/Art/Textures/";
        const string Mats = Root + "/Art/Materials/";
        const string Fonts = Root + "/Art/Fonts/";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string BankPath = Root + "/Resources/AssetBank.asset";

        [MenuItem("MazeRunner/Build All Generated Assets")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Mats);
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Art/Post");

            ConfigureTextures();
            ConfigureSizeBudget();
            var bank = AssetDatabase.LoadAssetAtPath<AssetBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<AssetBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            bank.Themes = new[]
            {
                new ThemeAssets
                {
                    Id = "stone",
                    WallSide = LitMat("Stone_WallSide", lit, "Bricks076C", new Color(0.78f, 0.80f, 0.74f), 0.15f),
                    WallTop  = LitMat("Stone_WallTop", lit, "Moss002", new Color(0.62f, 0.78f, 0.52f), 0.05f),
                    Floor    = LitMat("Stone_Floor", lit, "PavingStones138", new Color(0.82f, 0.80f, 0.74f), 0.2f),
                    Ground   = LitMat("Stone_Ground", lit, "Moss002", new Color(0.22f, 0.28f, 0.2f), 0.05f),
                    Ambient = new Color(0.16f, 0.18f, 0.22f),
                    Moon = new Color(0.55f, 0.65f, 0.95f), MoonIntensity = 0.6f,
                    Torch = new Color(1f, 0.64f, 0.32f),
                    Exit = new Color(0.4f, 1f, 0.75f),
                    EnemyTint = new Color(0.06f, 0.05f, 0.05f),
                    Background = new Color(0.02f, 0.025f, 0.03f),
                },
                new ThemeAssets
                {
                    Id = "sewer",
                    WallSide = LitMat("Sewer_WallSide", lit, "Bricks097", new Color(0.62f, 0.6f, 0.56f), 0.35f),
                    WallTop  = LitMat("Sewer_WallTop", lit, "Concrete042A", new Color(0.78f, 0.8f, 0.78f), 0.25f, 0.6f),
                    Floor    = LitMat("Sewer_Floor", lit, "Concrete031", new Color(0.42f, 0.5f, 0.56f), 0.8f),
                    Ground   = LitMat("Sewer_Ground", lit, "Concrete042A", new Color(0.12f, 0.15f, 0.16f), 0.4f),
                    Ambient = new Color(0.14f, 0.17f, 0.19f),
                    Moon = new Color(0.55f, 0.8f, 0.75f), MoonIntensity = 0.5f,
                    Torch = new Color(1f, 0.72f, 0.4f),
                    Exit = new Color(0.55f, 0.85f, 1f),
                    EnemyTint = new Color(0.08f, 0.1f, 0.06f),
                    Background = new Color(0.015f, 0.025f, 0.03f),
                },
                new ThemeAssets
                {
                    Id = "sands",
                    WallSide = LitMat("Sands_WallSide", lit, "Bricks084", new Color(0.95f, 0.82f, 0.62f), 0.1f),
                    WallTop  = LitMat("Sands_WallTop", lit, "Ground080", new Color(1f, 0.88f, 0.66f), 0.05f, 0.35f),
                    Floor    = LitMat("Sands_Floor", lit, "Tiles139", new Color(0.82f, 0.68f, 0.48f), 0.15f),
                    Ground   = LitMat("Sands_Ground", lit, "Ground080", new Color(0.4f, 0.3f, 0.2f), 0.05f, 0.35f),
                    Ambient = new Color(0.2f, 0.16f, 0.12f),
                    Moon = new Color(1f, 0.82f, 0.62f), MoonIntensity = 0.55f,
                    Torch = new Color(1f, 0.62f, 0.3f),
                    Exit = new Color(0.45f, 1f, 0.9f),
                    EnemyTint = new Color(0.32f, 0.2f, 0.1f),
                    Background = new Color(0.04f, 0.03f, 0.025f),
                },
            };

            bank.ShardMat = EmissiveMat("Shard", lit, new Color(0.35f, 0.85f, 1f), 2.2f);
            bank.KeyMat = EmissiveMat("Key", lit, new Color(1f, 0.75f, 0.25f), 2.0f, metallic: 0.8f);
            bank.HourglassMat = EmissiveMat("Hourglass", lit, new Color(1f, 0.82f, 0.45f), 1.2f, metallic: 0.5f);
            bank.CrystalMat = EmissiveMat("Crystal", lit, new Color(0.45f, 0.6f, 1f), 1.8f);
            bank.GateMat = Mat("Gate", lit, m => { m.SetColor("_BaseColor", new Color(0.18f, 0.17f, 0.16f)); m.SetFloat("_Metallic", 0.85f); m.SetFloat("_Smoothness", 0.45f); });

            bank.EnemyMat = Mat("Enemy", lit, m => { m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.55f); m.SetFloat("_Metallic", 0.1f); });

            var glowShader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Art/Shaders/Glow.shader");
            bank.Glow = Mat("Glow", glowShader, m => m.SetColor("_Color", Color.white));

            bank.Player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/ThirdPersonController/Prefabs/Player_Arissa.prefab");
            bank.Spider = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/External/Spiders/SandSpider.prefab");
            bank.SpiderWalk = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/External/Spiders/SpiderWalk.anim");
            bank.SpiderIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/External/Spiders/SpiderIdle.anim");
            bank.Hourglass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/External/Artifacts/hourglass.fbx");
            bank.Crystal = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/External/Artifacts/crystal.fbx");
            bank.Ambience = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/External/Audio/dungeon_ambience.wav");
            bank.Shift = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/External/Audio/stone_sliding.wav");
            bank.Growl = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/External/Audio/creature_growl.wav");
            bank.Tick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Art/External/Audio/metronome.wav");

            bank.Font = FontAsset("PTSans-Bold");
            bank.TitleFont = FontAsset("RussoOne-Regular");
            bank.Profile = PostProfile();

            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();

            BuildScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[MR] ProjectBuilder.BuildAll done");
        }

        // ── textures & materials ────────────────────────────────────────────────
        static void ConfigureTextures()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art/Textures" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_Normal.jpg");
                bool changed = false;
                if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
                if (imp.maxTextureSize != 1024) { imp.maxTextureSize = 1024; changed = true; }
                if (imp.anisoLevel != 4) { imp.anisoLevel = 4; changed = true; }
                if (changed) imp.SaveAndReimport();
            }
        }

        /// <summary>Keeps the APK small: compressed audio, tiny textures for the spider (its materials are replaced at runtime).</summary>
        static void ConfigureSizeBudget()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { Root + "/Art/External/Audio" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                var set = imp.defaultSampleSettings;
                bool loop = path.Contains("ambience");
                var want = new AudioImporterSampleSettings
                {
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.45f,
                    loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory,
                    sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                    sampleRateOverride = 22050,
                    preloadAudioData = !loop,
                };
                if (set.compressionFormat != want.compressionFormat || set.loadType != want.loadType || !imp.forceToMono || set.sampleRateOverride != 22050)
                {
                    imp.forceToMono = true;
                    imp.defaultSampleSettings = want;
                    imp.SaveAndReimport();
                }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Spiders/Textures" }))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (imp.maxTextureSize != 64) { imp.maxTextureSize = 64; imp.SaveAndReimport(); }
            }
        }

        static Material Mat(string name, Shader shader, System.Action<Material> setup)
        {
            string path = Mats + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            setup(m);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LitMat(string name, Shader lit, string tex, Color tint, float smooth, float bump = 1f) => Mat(name, lit, m =>
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + tex + "_Color.jpg"));
            m.SetColor("_BaseColor", tint);
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + tex + "_Normal.jpg");
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", bump);
            if (n != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        });

        static Material EmissiveMat(string name, Shader lit, Color color, float intensity, float metallic = 0f) => Mat(name, lit, m =>
        {
            m.SetColor("_BaseColor", color * 0.6f);
            m.SetColor("_EmissionColor", color * intensity);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", 0.7f);
        });

        // ── fonts ───────────────────────────────────────────────────────────────
        const string Charset =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~·—–«»…№" +
            "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюяІіЇїЄєҐґ";

        static TMP_FontAsset FontAsset(string file)
        {
            string path = Fonts + file + " SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(Fonts + file + ".ttf");
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = file + " SDF";
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTextures[0].name = file + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = file + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            fa.TryAddCharacters(Charset, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.Log($"[MR] font {file} missing glyphs: {missing}");
            // Pages created while pre-populating must be saved too.
            foreach (var t in fa.atlasTextures)
                if (t != null && !AssetDatabase.Contains(t)) { t.name = file + " Atlas+"; AssetDatabase.AddObjectToAsset(t, fa); }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        // ── post processing ─────────────────────────────────────────────────────
        static VolumeProfile PostProfile()
        {
            string path = Root + "/Art/Post/GameProfile.asset";
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(false);

            var tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = p.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.7f);
            color.contrast.Override(14f);
            color.saturation.Override(8f);

            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.32f);
            vig.smoothness.Override(0.45f);
            vig.color.Override(Color.black);

            foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
            EditorUtility.SetDirty(p);
            return p;
        }

        // ── scene & settings ────────────────────────────────────────────────────
        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("App").AddComponent<App>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.productName = "The Maze";
            // 32-bit too: many budget phones run 32-bit Android and reject arm64-only packages as "invalid".
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.bundleVersion = "1.0.0";
            // Google Play already has versionCode 20 (v0.2.1); every upload must be higher.
            if (PlayerSettings.Android.bundleVersionCode < 21) PlayerSettings.Android.bundleVersionCode = 21;

            const string iconPath = Root + "/Art/Icon.png";
            var imp = (TextureImporter)AssetImporter.GetAtPath(iconPath);
            if (imp != null && (imp.maxTextureSize != 1024 || imp.mipmapEnabled))
            {
                imp.maxTextureSize = 1024; imp.mipmapEnabled = false; imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null)
            {
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
                var sizes = PlayerSettings.GetIconSizes(UnityEditor.Build.NamedBuildTarget.Android, IconKind.Any);
                var arr = new Texture2D[sizes.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = icon;
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Android, arr, IconKind.Any);
            }
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace MazeRunner
{
    [Serializable]
    public sealed class ThemeAssets
    {
        public string Id;
        public Material WallSide, WallTop, Floor, Ground;
        public Color Ambient = new(0.08f, 0.09f, 0.12f);
        public Color Moon = new(0.45f, 0.55f, 0.85f);
        public float MoonIntensity = 0.35f;
        public Color Torch = new(1f, 0.62f, 0.3f);
        public Color Exit = new(0.35f, 1f, 0.75f);
        public Color Accent = new(0.95f, 0.75f, 0.35f);
        public Color EnemyTint = Color.black;
        public Color Background = new(0.02f, 0.02f, 0.03f);
    }

    /// <summary>
    /// Every asset the runtime needs, wired by the editor ProjectBuilder. Loading through one bank
    /// (instead of Shader.Find / scattered Resources paths) guarantees everything ships in the build.
    /// </summary>
    [CreateAssetMenu(menuName = "MazeRunner/Asset Bank")]
    public sealed class AssetBank : ScriptableObject
    {
        public ThemeAssets[] Themes;

        [Header("Characters")]
        public GameObject Player;
        public GameObject Spider;
        public AnimationClip SpiderWalk, SpiderIdle;
        public Material EnemyMat;

        [Header("Pickups")]
        public GameObject Hourglass;
        public GameObject Crystal;
        public Material ShardMat, KeyMat, HourglassMat, CrystalMat, GateMat;

        [Header("FX")]
        public Material Glow;
        public VolumeProfile Profile;

        [Header("Audio")]
        public AudioClip Ambience, Shift, Growl, Tick;

        [Header("UI")]
        public TMP_FontAsset Font;
        public TMP_FontAsset TitleFont;

        static AssetBank _instance;
        public static AssetBank I => _instance != null ? _instance : _instance = Resources.Load<AssetBank>("AssetBank");

        public ThemeAssets Theme(int index) => Themes[Mathf.Clamp(index, 0, Themes.Length - 1)];
    }
}

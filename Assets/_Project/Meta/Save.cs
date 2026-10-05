using System;
using UnityEngine;

namespace MazeRunner
{
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 2;
        public int[] Stars = new int[LevelConfig.ChapterCount * LevelConfig.LevelsPerChapter];
        public float[] Best = new float[LevelConfig.ChapterCount * LevelConfig.LevelsPerChapter];
        public bool Sound = true;
        public bool Music = true;
        public bool Vibration = true;
        public int LastChapter;
        public string DailyDate = "";
        public float DailyBest;
        public int DailyStreak;
        public string DailyStreakDate = "";
        public bool SeenIntro;
    }

    /// <summary>Single JSON blob in PlayerPrefs. Small, versioned, survives app updates.</summary>
    public static class Save
    {
        const string Key = "mr2_save";
        const string LegacyUnlockKey = "FullUnlocked"; // set by the v0.2 IAP flow — owned purchases stay owned

        public static SaveData D { get; private set; }

        public static void Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            try { D = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json); }
            catch (Exception e) { Debug.LogWarning("[Save] corrupt save, resetting: " + e.Message); D = new SaveData(); }

            int n = LevelConfig.ChapterCount * LevelConfig.LevelsPerChapter;
            if (D.Stars == null || D.Stars.Length != n) Array.Resize(ref D.Stars, n);
            if (D.Best == null || D.Best.Length != n) Array.Resize(ref D.Best, n);
        }

        public static void Flush()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
        }

        public static bool FullUnlocked
        {
            get => PlayerPrefs.GetInt(LegacyUnlockKey, 0) == 1;
            set { PlayerPrefs.SetInt(LegacyUnlockKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        static int Idx(int chapter, int level) => chapter * LevelConfig.LevelsPerChapter + level;

        public static int StarsOf(int chapter, int level) => D.Stars[Idx(chapter, level)];
        public static float BestOf(int chapter, int level) => D.Best[Idx(chapter, level)];

        public static int ChapterStars(int chapter)
        {
            int s = 0;
            for (int i = 0; i < LevelConfig.LevelsPerChapter; i++) s += StarsOf(chapter, i);
            return s;
        }

        public static bool ChapterOpen(int chapter) => chapter == 0 || FullUnlocked;

        public static bool LevelOpen(int chapter, int level)
        {
            if (!ChapterOpen(chapter)) return false;
            return level == 0 || StarsOf(chapter, level - 1) > 0;
        }

        /// <summary>First level of the chapter that is open but not yet beaten (or the last one).</summary>
        public static int NextLevel(int chapter)
        {
            for (int i = 0; i < LevelConfig.LevelsPerChapter; i++)
                if (StarsOf(chapter, i) == 0) return i;
            return LevelConfig.LevelsPerChapter - 1;
        }

        /// <summary>Records a result; returns true when it is a new best time.</summary>
        public static bool Record(LevelConfig cfg, int stars, float time)
        {
            if (cfg.IsDaily)
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                bool better = D.DailyDate != today || time < D.DailyBest;
                if (D.DailyStreakDate != today)
                {
                    string yesterday = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                    D.DailyStreak = D.DailyStreakDate == yesterday ? D.DailyStreak + 1 : 1;
                    D.DailyStreakDate = today;
                }
                if (better) { D.DailyDate = today; D.DailyBest = time; }
                Flush();
                return better;
            }

            int i = Idx(cfg.Chapter, cfg.Level);
            bool newBest = D.Best[i] <= 0f || time < D.Best[i];
            D.Stars[i] = Mathf.Max(D.Stars[i], stars);
            if (newBest) D.Best[i] = time;
            D.LastChapter = cfg.Chapter;
            Flush();
            return newBest;
        }
    }
}

using UnityEngine;

namespace MazeRunner
{
    /// <summary>Everything that defines one playable maze. Built deterministically from (chapter, level).</summary>
    public sealed class LevelConfig
    {
        public const int LevelsPerChapter = 30;
        public const int ChapterCount = 3;
        public const int DailyChapter = -1;

        public int Chapter;   // 0..ChapterCount-1, or DailyChapter
        public int Level;     // 0..LevelsPerChapter-1
        public int Seed;

        public int W, H;
        public float Braid;
        public float ShiftableFraction;
        public float ShiftInterval;      // seconds; 0 disables shifting
        public float FirstShiftDelay;
        public int SwapsPerShift;

        public int Enemies;
        public float EnemySpeed, EnemyChaseSpeed;
        public int EnemySight;           // cells of open corridor the enemy can "see" along

        public int Shards = 3;
        public bool NeedKey;
        public int Hourglasses, Crystals;
        public float LightRange;

        public float TimeLimit;          // filled in by LevelController from the actual maze
        public float ParTime;

        public int ThemeIndex => Chapter < 0 ? DailyTheme : Chapter;
        int DailyTheme;

        public static LevelConfig For(int chapter, int level)
        {
            float t = level / (float)(LevelsPerChapter - 1);
            var c = new LevelConfig { Chapter = chapter, Level = level, Seed = 7919 * (chapter + 1) + 104729 * (level + 1) };

            // Each chapter starts a little harder than the last one began.
            float start = chapter * 0.25f;
            float k = Mathf.Clamp01(start + t * (1f - start * 0.6f));

            int size = Mathf.RoundToInt(Mathf.Lerp(5, 16, k));
            c.W = size + size / 3;
            c.H = size;
            c.Braid = Mathf.Lerp(0.15f, 0.35f, k);
            c.ShiftableFraction = Mathf.Lerp(0.30f, 0.22f, k);
            c.ShiftInterval = Mathf.Lerp(12f, 7f, k);
            c.FirstShiftDelay = level == 0 && chapter == 0 ? 6f : Mathf.Min(8f, c.ShiftInterval);
            c.SwapsPerShift = 1 + size / 5;

            int lvl = level + chapter * 10;
            c.Enemies = lvl < 4 ? 0 : lvl < 12 ? 1 : lvl < 24 ? 2 : 3;
            c.EnemySpeed = Mathf.Lerp(2.0f, 2.8f, k);
            c.EnemyChaseSpeed = Mathf.Lerp(3.6f, 4.6f, k);
            c.EnemySight = Mathf.RoundToInt(Mathf.Lerp(3, 6, k));

            c.NeedKey = lvl >= 7 && level % 2 == 1;
            c.Hourglasses = level >= 2 ? 1 + size / 12 : 0;
            c.Crystals = c.Enemies > 0 ? 1 : 0;
            c.LightRange = Mathf.Lerp(11f, 8.5f, k);
            return c;
        }

        /// <summary>Daily maze — same for every player on a given date.</summary>
        public static LevelConfig Daily(System.DateTime date)
        {
            int seed = date.Year * 10000 + date.Month * 100 + date.Day;
            var rng = new System.Random(seed);
            var c = For(rng.Next(ChapterCount), 12 + rng.Next(14));
            c.DailyTheme = c.Chapter;
            c.Chapter = DailyChapter;
            c.Seed = seed;
            return c;
        }

        public bool IsDaily => Chapter == DailyChapter;
    }
}

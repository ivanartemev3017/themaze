using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>Tiny two-language table: Russian for ru/uk/be devices, English everywhere else.</summary>
    public static class Loc
    {
        public static readonly bool Ru = Application.systemLanguage is SystemLanguage.Russian or SystemLanguage.Ukrainian or SystemLanguage.Belarusian;

        static readonly Dictionary<string, (string en, string ru)> T = new()
        {
            ["title"]        = ("THE MAZE", "THE MAZE"),
            ["tagline"]      = ("The walls are moving.", "Стены двигаются."),
            ["play"]         = ("PLAY", "ИГРАТЬ"),
            ["levels"]       = ("LEVELS", "УРОВНИ"),
            ["daily"]        = ("DAILY MAZE", "ЛАБИРИНТ ДНЯ"),
            ["level"]        = ("LEVEL", "УРОВЕНЬ"),
            ["back"]         = ("BACK", "НАЗАД"),
            ["resume"]       = ("RESUME", "ПРОДОЛЖИТЬ"),
            ["retry"]        = ("RETRY", "ЕЩЁ РАЗ"),
            ["next"]         = ("NEXT", "ДАЛЬШЕ"),
            ["menu"]         = ("MENU", "МЕНЮ"),
            ["paused"]       = ("PAUSED", "ПАУЗА"),
            ["escaped"]      = ("ESCAPED!", "ВЫБРАЛСЯ!"),
            ["caught"]       = ("CAUGHT", "ПОЙМАН"),
            ["caught_sub"]   = ("The spider got you.", "Паук тебя настиг."),
            ["timeout"]      = ("NIGHT FELL", "НАСТУПИЛА НОЧЬ"),
            ["timeout_sub"]  = ("The maze closed before you got out.", "Лабиринт закрылся раньше, чем ты выбрался."),
            ["time"]         = ("TIME", "ВРЕМЯ"),
            ["best"]         = ("BEST", "РЕКОРД"),
            ["new_best"]     = ("NEW BEST!", "НОВЫЙ РЕКОРД!"),
            ["star_exit"]    = ("Escape the maze", "Выбраться из лабиринта"),
            ["star_par"]     = ("Escape in {0}", "Выбраться за {0}"),
            ["star_shards"]  = ("Collect all shards", "Собрать все осколки"),
            ["settings"]     = ("SETTINGS", "НАСТРОЙКИ"),
            ["sound"]        = ("Sound", "Звук"),
            ["music"]        = ("Ambience", "Атмосфера"),
            ["vibration"]    = ("Vibration", "Вибрация"),
            ["on"]           = ("ON", "ВКЛ"),
            ["off"]          = ("OFF", "ВЫКЛ"),
            ["locked"]       = ("LOCKED", "ЗАКРЫТО"),
            ["unlock"]       = ("UNLOCK ALL", "ОТКРЫТЬ ВСЁ"),
            ["unlock_title"] = ("FULL GAME", "ПОЛНАЯ ВЕРСИЯ"),
            ["unlock_body"]  = ("Open the Sewers and the Sands: 60 more levels and new enemies. One purchase, yours forever.",
                                "Откройте Канализацию и Пески: ещё 60 уровней и новые враги. Одна покупка — навсегда."),
            ["restore"]      = ("RESTORE", "ВОССТАНОВИТЬ"),
            ["store_wait"]   = ("Store is not ready yet. Try again in a moment.", "Магазин ещё не готов. Попробуйте чуть позже."),
            ["thanks"]       = ("Thank you! Everything is unlocked.", "Спасибо! Всё открыто."),
            ["ch0"]          = ("STONE MAZE", "КАМЕННЫЙ ЛАБИРИНТ"),
            ["ch1"]          = ("THE SEWERS", "КАНАЛИЗАЦИЯ"),
            ["ch2"]          = ("THE SANDS", "ПЕСКИ"),
            ["daily_sub"]    = ("A new maze every day. Streak: {0}", "Новый лабиринт каждый день. Серия: {0}"),
            ["daily_best"]   = ("Today's best: {0}", "Лучшее сегодня: {0}"),
            ["shift_soon"]   = ("The walls are about to move!", "Стены сейчас сдвинутся!"),
            ["need_key"]     = ("The gate is locked. Find the key!", "Ворота заперты. Найди ключ!"),
            ["got_key"]      = ("Key found! The gate is open.", "Ключ найден! Ворота открыты."),
            ["time_bonus"]   = ("+{0} seconds", "+{0} секунд"),
            ["freeze"]       = ("Spiders frozen!", "Пауки заморожены!"),
            ["shard"]        = ("Shard {0}/{1}", "Осколок {0}/{1}"),
            ["hint_move"]    = ("Drag anywhere to run. Find the glowing gate.", "Веди пальцем по экрану, чтобы бежать. Найди светящиеся ворота."),
            ["hint_shift"]   = ("Red floor = a wall will rise. Blue = a path opens.", "Красный пол — тут вырастет стена. Синий — откроется проход."),
            ["hint_enemy"]   = ("A spider! It sees along corridors — break line of sight.", "Паук! Он видит вдоль коридоров — уходи за угол."),
            ["hint_key"]     = ("This gate needs a key. Look for the golden light.", "Этим воротам нужен ключ. Ищи золотой свет."),
            ["hint_items"]   = ("Hourglass gives time. Crystal freezes spiders.", "Песочные часы дают время. Кристалл замораживает пауков."),
            ["chapter_done"] = ("Chapter complete!", "Глава пройдена!"),
        };

        public static string Get(string key)
        {
            if (!T.TryGetValue(key, out var v)) return key;
            return Ru ? v.ru : v.en;
        }

        public static string F(string key, params object[] args) => string.Format(Get(key), args);

        public static string Time(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int m = (int)(seconds / 60f), s = (int)(seconds % 60f);
            return $"{m}:{s:00}";
        }
    }
}

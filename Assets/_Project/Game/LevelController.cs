using System;
using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    public sealed class LevelResult
    {
        public LevelConfig Cfg;
        public bool Won;
        public string Reason;         // "caught" | "timeout" when lost
        public float Time;
        public bool StarExit, StarPar, StarShards;
        public int Stars => (StarExit ? 1 : 0) + (StarPar ? 1 : 0) + (StarShards ? 1 : 0);
        public bool NewBest;
    }

    /// <summary>Rules of one level: timer, wall shifts, pickups, enemies, win/lose.</summary>
    public sealed class LevelController : MonoBehaviour
    {
        public enum Phase { Intro, Playing, Won, Lost }

        public const float WarnTime = 2.0f;
        const float IntroTime = 2.4f;
        const float HourglassBonus = 15f;
        const float FreezeTime = 6f;

        public LevelConfig Cfg { get; private set; }
        public MazeData Maze { get; private set; }
        public MazeView View { get; private set; }
        public PlayerController Player { get; private set; }
        public ExitGate Gate { get; private set; }
        public Phase State { get; private set; }
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public int ShardsGot { get; private set; }
        public int ShardsTotal { get; private set; }
        public bool HasKey { get; private set; }
        public float ShiftIn => _shiftTimer;
        public Pickup Key { get; private set; }
        public bool Paused { get; set; }

        public event Action<string> Toast;
        public event Action<LevelResult> Finished;
        public event Action Changed;

        CameraRig _rig;
        readonly List<Enemy> _enemies = new();
        readonly List<Pickup> _pickups = new();
        System.Random _rng;
        float _shiftTimer, _introLeft;
        List<ShiftPlanner.Swap> _pending;
        bool _shiftHintShown;
        float _endTimer;

        public void Setup(LevelConfig cfg, MazeData maze, MazeView view, CameraRig rig, ThemeAssets theme)
        {
            Cfg = cfg; Maze = maze; View = view; _rig = rig;
            _rng = new System.Random(cfg.Seed ^ 0x5f3759df);

            Player = PlayerController.Spawn(transform, MazeView.CellCenter(maze.Start), theme, cfg.LightRange);
            Player.transform.rotation = Quaternion.LookRotation(DirTowardOpen(maze.Start), Vector3.up);
            Gate = ExitGate.Spawn(transform, maze, theme, cfg.NeedKey);

            PlacePickupsAndEnemies(theme);
            TimeLeft = cfg.TimeLimit;
            _shiftTimer = cfg.ShiftInterval > 0 ? cfg.FirstShiftDelay + IntroTime : float.PositiveInfinity;
            _introLeft = IntroTime;
            State = Phase.Intro;

            float span = Mathf.Max(maze.W, maze.H) * MazeView.Cell;
            rig.SetBounds(Vector3.zero, view.Size);
            rig.Intro(Player, view.Center, span, IntroTime);
        }

        Vector3 DirTowardOpen(Vector2Int c)
        {
            foreach (var d in DirExt.All)
                if (Maze.CanMove(c, d)) { var v = d.Delta(); return new Vector3(v.x, 0, v.y); }
            return Vector3.forward;
        }

        // ── placement ───────────────────────────────────────────────────────────
        void PlacePickupsAndEnemies(ThemeAssets theme)
        {
            var fromStart = MazePath.Distances(Maze, Maze.Start);
            var fromExit = MazePath.Distances(Maze, Maze.Exit);
            var taken = new HashSet<Vector2Int> { Maze.Start, Maze.Exit };
            int maxD = 0;
            foreach (var d in fromStart) maxD = Mathf.Max(maxD, d);

            int pathCells = fromStart[Maze.Exit.x, Maze.Exit.y];
            if (Cfg.NeedKey)
            {
                // Key far from both start and exit — forces a real detour.
                var keyCell = Best(c => Mathf.Min(fromStart[c.x, c.y], fromExit[c.x, c.y] * 1.2f) + (float)_rng.NextDouble(), taken);
                taken.Add(keyCell);
                Key = Pickup.Spawn(transform, PickupKind.Key, keyCell);
                _pickups.Add(Key);
                pathCells = fromStart[keyCell.x, keyCell.y] + fromExit[keyCell.x, keyCell.y];
            }

            ShardsTotal = Cfg.Shards;
            var shardCells = new List<Vector2Int>();
            for (int i = 0; i < Cfg.Shards; i++)
            {
                var cell = Best(c =>
                {
                    float score = Mathf.Min(fromStart[c.x, c.y], 8) + (Maze.OpenNeighbours(c) == 1 ? 4f : 0f);
                    foreach (var s in shardCells) score = Mathf.Min(score, Manhattan(c, s) * 1.5f);
                    return score + (float)_rng.NextDouble() * 3f;
                }, taken);
                taken.Add(cell); shardCells.Add(cell);
                _pickups.Add(Pickup.Spawn(transform, PickupKind.Shard, cell));
            }

            for (int i = 0; i < Cfg.Hourglasses; i++)
            {
                var cell = Best(c => fromStart[c.x, c.y] >= 4 ? (float)_rng.NextDouble() : -1f, taken);
                taken.Add(cell);
                _pickups.Add(Pickup.Spawn(transform, PickupKind.Hourglass, cell));
            }
            for (int i = 0; i < Cfg.Crystals; i++)
            {
                var cell = Best(c => fromStart[c.x, c.y] >= 3 && fromStart[c.x, c.y] <= maxD * 0.6f ? (float)_rng.NextDouble() : -1f, taken);
                taken.Add(cell);
                _pickups.Add(Pickup.Spawn(transform, PickupKind.Crystal, cell));
            }

            var enemyCells = new List<Vector2Int>();
            for (int i = 0; i < Cfg.Enemies; i++)
            {
                int minD = Mathf.Max(5, Mathf.RoundToInt(maxD * 0.4f));
                var cell = Best(c =>
                {
                    if (fromStart[c.x, c.y] < minD || fromExit[c.x, c.y] < 2) return -1f;
                    float score = 10f;
                    foreach (var e in enemyCells) score = Mathf.Min(score, Manhattan(c, e));
                    return score + (float)_rng.NextDouble() * 2f;
                }, new HashSet<Vector2Int> { Maze.Start, Maze.Exit });
                enemyCells.Add(cell);
                var enemy = Enemy.Spawn(transform, cell, Maze, Player, Cfg, theme, Cfg.Seed + i * 31);
                enemy.StartedChase += _ => { Sfx.I?.Growl(); };
                _enemies.Add(enemy);
            }

            // Generous time limit from the real route length; par rewards a confident run.
            Cfg.TimeLimit = Round5(22f + pathCells * 1.9f + (Cfg.Enemies > 0 ? 8f : 0f));
            Cfg.ParTime = Round5(8f + pathCells * 1.05f);
        }

        static float Round5(float v) => Mathf.Ceil(v / 5f) * 5f;
        static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        Vector2Int Best(Func<Vector2Int, float> score, HashSet<Vector2Int> exclude)
        {
            var best = new Vector2Int(Maze.W / 2, Maze.H / 2); float bestS = float.MinValue;
            for (int x = 0; x < Maze.W; x++)
            for (int y = 0; y < Maze.H; y++)
            {
                var c = new Vector2Int(x, y);
                if (exclude.Contains(c)) continue;
                float s = score(c);
                if (s > bestS) { bestS = s; best = c; }
            }
            return best;
        }

        // ── loop ────────────────────────────────────────────────────────────────
        void Update()
        {
            if (Paused) return;
            switch (State)
            {
                case Phase.Intro:
                    _introLeft -= Time.deltaTime;
                    if (_introLeft <= 0f)
                    {
                        State = Phase.Playing;
                        Player.InputEnabled = true;
                        ShowLevelHint();
                    }
                    TickShift();
                    break;
                case Phase.Playing:
                    Elapsed += Time.deltaTime;
                    TimeLeft -= Time.deltaTime;
                    TickShift();
                    CheckPickups();
                    CheckEnemies();
                    CheckExit();
                    if (TimeLeft <= 0f && State == Phase.Playing) Lose("timeout");
                    UpdateTension();
                    break;
                case Phase.Won:
                    // Walk out through the gate.
                    Player.transform.position += Gate.Outward * (3.5f * Time.deltaTime);
                    Player.transform.rotation = Quaternion.LookRotation(Gate.Outward);
                    Tick();
                    break;
                case Phase.Lost:
                    Tick();
                    break;
            }
        }

        void Tick()
        {
            Sfx.I?.SetTension(0f);
            if (_endTimer > 0f) { _endTimer -= Time.deltaTime; if (_endTimer <= 0f) Finished?.Invoke(_result); }
        }

        void UpdateTension()
        {
            float t = 0f;
            if (TimeLeft < 15f) t = Mathf.Max(t, 1f - TimeLeft / 15f);
            foreach (var e in _enemies) if (e.Chasing) t = Mathf.Max(t, 0.85f);
            Sfx.I?.SetTension(t);
        }

        void TickShift()
        {
            if (float.IsInfinity(_shiftTimer)) return;
            _shiftTimer -= Time.deltaTime;
            if (_pending == null && _shiftTimer <= WarnTime)
            {
                var protectedCells = new List<Vector2Int> { Player.Cell, Maze.Exit };
                foreach (var e in _enemies) { protectedCells.Add(e.Cell); protectedCells.Add(e.Next); }
                _pending = ShiftPlanner.Plan(Maze, _rng, Cfg.SwapsPerShift, protectedCells);
                if (_pending.Count > 0)
                {
                    View.ShowWarning(_pending, WarnTime);
                    Sfx.I?.Warn();
                    if (!_shiftHintShown && Cfg.Chapter == 0 && Cfg.Level <= 1) { _shiftHintShown = true; Toast?.Invoke(Loc.Get("hint_shift")); }
                }
            }
            if (_shiftTimer <= 0f)
            {
                ApplyShift();
                _shiftTimer = Cfg.ShiftInterval;
            }
        }

        void ApplyShift()
        {
            View.ClearWarnings();
            if (_pending == null) return;
            int applied = 0;
            foreach (var s in _pending)
            {
                if (Blocked(s.Close)) continue; // skipping a whole swap keeps the maze connected
                Maze.SetWall(s.Open, false);
                Maze.SetWall(s.Close, true);
                applied++;
            }
            _pending = null;
            if (applied > 0)
            {
                Sfx.I?.Shift();
                _rig.Shake(0.35f, 0.5f);
                Sfx.Vibrate();
                Changed?.Invoke();
            }
        }

        bool Blocked(Edge e)
        {
            var (min, max) = MazeView.WallBounds(e);
            var p = Player.transform.position;
            const float pad = 0.5f;
            if (p.x > min.x - pad && p.x < max.x + pad && p.z > min.z - pad && p.z < max.z + pad) return true;
            foreach (var en in _enemies) if (e.Touches(en.Cell) && e.Touches(en.Next)) return true;
            return false;
        }

        void CheckPickups()
        {
            var p = Player.transform.position;
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                var pk = _pickups[i];
                var d = pk.transform.position - p; d.y = 0;
                if (d.sqrMagnitude > 1.3f * 1.3f) continue;
                _pickups.RemoveAt(i);
                Collect(pk);
                Destroy(pk.gameObject);
            }
        }

        void Collect(Pickup pk)
        {
            switch (pk.Kind)
            {
                case PickupKind.Shard:
                    ShardsGot++;
                    Sfx.I?.Shard();
                    Toast?.Invoke(Loc.F("shard", ShardsGot, ShardsTotal));
                    break;
                case PickupKind.Key:
                    HasKey = true;
                    Gate.SetOpen(true);
                    Sfx.I?.Key();
                    Toast?.Invoke(Loc.Get("got_key"));
                    break;
                case PickupKind.Hourglass:
                    TimeLeft += HourglassBonus;
                    Sfx.I?.Pickup();
                    Toast?.Invoke(Loc.F("time_bonus", (int)HourglassBonus));
                    break;
                case PickupKind.Crystal:
                    foreach (var e in _enemies) e.Freeze(FreezeTime);
                    Sfx.I?.Freeze();
                    Toast?.Invoke(Loc.Get("freeze"));
                    break;
            }
            Changed?.Invoke();
        }

        void CheckEnemies()
        {
            var p = Player.transform.position;
            foreach (var e in _enemies)
            {
                if (e.Frozen) continue;
                var d = e.transform.position - p; d.y = 0;
                if (d.sqrMagnitude < 0.95f * 0.95f) { Lose("caught"); return; }
            }
        }

        bool _lockedToastShown;
        void CheckExit()
        {
            var d = Player.transform.position - Gate.Threshold; d.y = 0;
            if (d.magnitude > 1.9f) { _lockedToastShown = false; return; }
            if (!Gate.Open)
            {
                if (!_lockedToastShown) { _lockedToastShown = true; Toast?.Invoke(Loc.Get("need_key")); }
                return;
            }
            Win();
        }

        void ShowLevelHint()
        {
            if (Cfg.IsDaily) return;
            var prev = Cfg.Level > 0 ? LevelConfig.For(Cfg.Chapter, Cfg.Level - 1) : null;
            string key = null;
            if (Cfg.Chapter == 0 && Cfg.Level == 0) key = "hint_move";
            else if (Cfg.NeedKey && (prev == null || !AnyKeyBefore())) key = "hint_key";
            else if (Cfg.Enemies > 0 && (prev == null || prev.Enemies == 0) && Cfg.Chapter == 0) key = "hint_enemy";
            else if (Cfg.Hourglasses > 0 && prev != null && prev.Hourglasses == 0 && Cfg.Chapter == 0) key = "hint_items";
            if (key != null) Toast?.Invoke(Loc.Get(key));
        }

        bool AnyKeyBefore()
        {
            for (int i = 0; i < Cfg.Level; i++) if (LevelConfig.For(Cfg.Chapter, i).NeedKey) return true;
            return Cfg.Chapter > 0;
        }

        // ── end states ──────────────────────────────────────────────────────────
        LevelResult _result;

        void Win()
        {
            State = Phase.Won;
            Player.InputEnabled = false;
            Sfx.I?.Win();
            _result = new LevelResult
            {
                Cfg = Cfg, Won = true, Time = Elapsed,
                StarExit = true, StarPar = Elapsed <= Cfg.ParTime, StarShards = ShardsGot >= ShardsTotal,
            };
            _result.NewBest = Save.Record(Cfg, _result.Stars, Elapsed);
            _endTimer = 1.3f;
            Changed?.Invoke();
        }

        void Lose(string reason)
        {
            State = Phase.Lost;
            Player.InputEnabled = false;
            Sfx.I?.Lose();
            Sfx.Vibrate();
            _rig.Shake(0.5f, 0.6f);
            _result = new LevelResult { Cfg = Cfg, Won = false, Reason = reason, Time = Elapsed };
            _endTimer = 1.2f;
            Changed?.Invoke();
        }

        /// <summary>Where the HUD arrow should point: the key while the gate is locked, the gate otherwise.</summary>
        public Vector3 Objective => !Gate.Open && Key != null ? Key.transform.position : Gate.Threshold;
    }
}

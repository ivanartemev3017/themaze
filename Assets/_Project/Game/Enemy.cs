using System.Collections.Generic;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>
    /// Spider. Moves cell-to-cell on the maze graph (no physics). Sees along straight open corridors;
    /// chases what it sees, searches the last seen spot, otherwise patrols.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        enum State { Patrol, Chase, Search }

        public Vector2Int Cell { get; private set; }
        public Vector2Int Next { get; private set; }
        public bool Chasing => _state == State.Chase;
        public bool Frozen => _frozen > 0f;

        MazeData _m;
        PlayerController _player;
        LevelConfig _cfg;
        System.Random _rng;
        State _state;
        List<Vector2Int> _path;
        int _pathIdx;
        Vector2Int _goal, _lastSeen;
        float _lost, _frozen, _sightTimer;

        Animation _anim;
        Transform _model;
        Material _halo;
        Color _haloIdle = new(1f, 0.2f, 0.05f, 0.25f), _haloChase = new(1f, 0.1f, 0.02f, 1.1f), _haloFrozen = new(0.3f, 0.75f, 1f, 1f);

        public event System.Action<Enemy> StartedChase;

        public static Enemy Spawn(Transform parent, Vector2Int cell, MazeData m, PlayerController player, LevelConfig cfg, ThemeAssets theme, int seed)
        {
            var bank = AssetBank.I;
            var go = new GameObject("Spider");
            go.transform.SetParent(parent, false);
            go.transform.position = MazeView.CellCenter(cell);
            var e = go.AddComponent<Enemy>();
            e._m = m; e._player = player; e._cfg = cfg; e._rng = new System.Random(seed);
            e.Cell = e.Next = cell;

            if (bank.Spider != null)
            {
                var model = Instantiate(bank.Spider, go.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * 0.75f;
                foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var a in model.GetComponentsInChildren<Animator>()) Destroy(a);
                // The asset ships with built-in-pipeline materials (pink in URP) — replace with the bank's URP material.
                var body = new Material(bank.EnemyMat);
                body.SetColor("_BaseColor", theme.EnemyTint);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = body;
                    r.sharedMaterials = mats;
                }
                e._model = model.transform;
                e._anim = model.GetOrAdd<Animation>();
                if (bank.SpiderWalk != null) { e._anim.AddClip(bank.SpiderWalk, "Walk"); e._anim.wrapMode = WrapMode.Loop; e._anim.Play("Walk"); }
                if (bank.SpiderIdle != null) e._anim.AddClip(bank.SpiderIdle, "Idle");
            }

            // Floor halo — makes the spider readable from above and signals its mood.
            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = Vector3.up * 0.05f;
            halo.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatQuad(3.4f, 3.4f);
            var hr = halo.AddComponent<MeshRenderer>();
            e._halo = new Material(bank.Glow);
            e._halo.SetFloat("_Shape", 0);
            e._halo.SetColor("_Color", e._haloIdle);
            hr.sharedMaterial = e._halo;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            m.WallChanged += e.OnWallChanged;
            return e;
        }

        void OnDestroy() { if (_m != null) _m.WallChanged -= OnWallChanged; }

        public void Freeze(float seconds)
        {
            _frozen = seconds;
            if (_anim != null) _anim.Stop();
        }

        void OnWallChanged(Edge e, bool on)
        {
            _path = null;
            if (on && Cell != Next && e.Touches(Cell) && e.Touches(Next)) Next = Cell; // turn back
        }

        void Update()
        {
            if (_player == null) return;
            if (_frozen > 0f)
            {
                _frozen -= Time.deltaTime;
                _halo.SetColor("_Color", _haloFrozen);
                if (_frozen <= 0f && _anim != null && _anim.GetClip("Walk") != null) _anim.Play("Walk");
                return;
            }

            _sightTimer -= Time.deltaTime;
            if (_sightTimer <= 0f) { _sightTimer = 0.12f; Look(); }

            float speed = _state == State.Chase ? _cfg.EnemyChaseSpeed : _cfg.EnemySpeed;
            if (_anim != null && _anim.isPlaying) foreach (AnimationState s in _anim) s.speed = speed / 2.2f;

            var target = MazeView.CellCenter(Next);
            var pos = transform.position;
            var to = target - pos; to.y = 0;
            if (to.magnitude < 0.05f)
            {
                transform.position = target;
                Cell = Next;
                Next = Decide();
            }
            else
            {
                var step = to.normalized * Mathf.Min(to.magnitude, speed * Time.deltaTime);
                transform.position = pos + step;
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            var c = _halo.GetColor("_Color");
            _halo.SetColor("_Color", Color.Lerp(c, _state == State.Chase ? _haloChase : _haloIdle, Time.deltaTime * 4f));
        }

        void Look()
        {
            var pc = _player.Cell;
            bool sees = CanSee(pc);
            if (sees)
            {
                if (_state != State.Chase) StartedChase?.Invoke(this);
                _state = State.Chase;
                _lastSeen = pc;
                _lost = 0f;
                _path = null;
            }
            else if (_state == State.Chase)
            {
                _lost += 0.12f;
                if (_lost > 0.6f) { _state = State.Search; _path = null; }
            }
        }

        bool CanSee(Vector2Int pc)
        {
            if (pc == Cell || pc == Next) return true;
            foreach (var d in DirExt.All)
            {
                var c = Cell;
                for (int i = 0; i < _cfg.EnemySight; i++)
                {
                    if (!_m.CanMove(c, d)) break;
                    c += d.Delta();
                    if (c == pc) return true;
                }
            }
            return false;
        }

        Vector2Int Decide()
        {
            switch (_state)
            {
                case State.Chase:
                    return StepToward(_player.Cell, replan: true);
                case State.Search:
                    if (Cell == _lastSeen) { _state = State.Patrol; _path = null; break; }
                    return StepToward(_lastSeen, replan: false);
            }

            if (_path == null || _pathIdx >= _path.Count || Cell == _goal)
            {
                _goal = RandomGoal();
                _path = MazePath.Find(_m, Cell, _goal);
                _pathIdx = 1;
            }
            if (_path == null || _pathIdx >= _path.Count) return Cell;
            var n = _path[_pathIdx];
            if (!Adjacent(Cell, n)) { _path = null; return Cell; }
            _pathIdx++;
            return n;
        }

        Vector2Int StepToward(Vector2Int goal, bool replan)
        {
            if (replan || _path == null || _pathIdx >= _path.Count || _path[^1] != goal)
            {
                _path = MazePath.Find(_m, Cell, goal);
                _pathIdx = 1;
            }
            if (_path == null || _pathIdx >= _path.Count) return Cell;
            var n = _path[_pathIdx];
            if (!Adjacent(Cell, n)) { _path = null; return Cell; }
            _pathIdx++;
            return n;
        }

        bool Adjacent(Vector2Int a, Vector2Int b)
        {
            foreach (var d in DirExt.All) if (a + d.Delta() == b && _m.CanMove(a, d)) return true;
            return false;
        }

        Vector2Int RandomGoal()
        {
            for (int i = 0; i < 20; i++)
            {
                var c = new Vector2Int(_rng.Next(_m.W), _rng.Next(_m.H));
                int manhattan = Mathf.Abs(c.x - Cell.x) + Mathf.Abs(c.y - Cell.y);
                if (manhattan >= 4 && c != _m.Exit) return c;
            }
            return new Vector2Int(_rng.Next(_m.W), _rng.Next(_m.H));
        }
    }
}

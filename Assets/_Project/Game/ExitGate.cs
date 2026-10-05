using DG.Tweening;
using UnityEngine;

namespace MazeRunner
{
    /// <summary>The way out: an opening in the outer wall with bars, a light beam visible over the walls, and a glow.</summary>
    public sealed class ExitGate : MonoBehaviour
    {
        public bool Open { get; private set; }
        public Vector3 Threshold { get; private set; }  // centre of the opening
        public Vector3 Outward { get; private set; }

        Transform _bars;
        BoxCollider _barsCol;
        Material _beamMat, _haloMat;
        Light _light;
        Color _openColor, _lockedColor = new(1f, 0.55f, 0.15f);

        public static ExitGate Spawn(Transform parent, MazeData m, ThemeAssets theme, bool locked)
        {
            var bank = AssetBank.I;
            var edge = Edge.Between(m.Exit, m.ExitSide);
            var go = new GameObject("ExitGate");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<ExitGate>();
            g._openColor = theme.Exit;
            g.Threshold = MazeView.EdgeCenter(edge);
            var d = m.ExitSide.Delta();
            g.Outward = new Vector3(d.x, 0, d.y);
            go.transform.position = g.Threshold;
            go.transform.rotation = Quaternion.LookRotation(g.Outward, Vector3.up);

            // Bars across the opening (local X spans the gap).
            float gap = MazeView.Cell - MazeView.PillarT;
            g._bars = new GameObject("Bars").transform;
            g._bars.SetParent(go.transform, false);
            var kit = new MeshKit { SideUV = 1f, TopUV = 1f };
            int n = 7;
            for (int i = 0; i < n; i++)
            {
                float x = -gap * 0.5f + gap * (i + 0.5f) / n;
                kit.Box(new Vector3(x - 0.07f, 0, -0.07f), new Vector3(x + 0.07f, MazeView.WallH + 0.3f, 0.07f));
            }
            kit.Box(new Vector3(-gap * 0.5f, MazeView.WallH * 0.55f, -0.09f), new Vector3(gap * 0.5f, MazeView.WallH * 0.55f + 0.16f, 0.09f));
            MeshKit.Spawn("BarsMesh", g._bars, kit.Build("Bars"), bank.GateMat, bank.GateMat);
            g._barsCol = g._bars.gameObject.AddComponent<BoxCollider>();
            g._barsCol.center = new Vector3(0, MazeView.WallH * 0.5f, 0);
            g._barsCol.size = new Vector3(gap, MazeView.WallH, 0.3f);

            // Lintel above the opening.
            var lintel = new MeshKit();
            lintel.Box(new Vector3(-MazeView.Cell * 0.5f - 0.2f, MazeView.WallH + 0.35f, -0.5f), new Vector3(MazeView.Cell * 0.5f + 0.2f, MazeView.WallH + 0.85f, 0.5f));
            MeshKit.Spawn("Lintel", go.transform, lintel.Build("Lintel"), theme.WallSide, theme.WallTop);

            // Beam: two crossed upright quads, additive.
            g._beamMat = new Material(bank.Glow);
            g._beamMat.SetFloat("_Shape", 2);
            var beamMesh = MeshKit.UprightQuad(2.6f, 22f);
            for (int i = 0; i < 2; i++)
            {
                var b = new GameObject("Beam");
                b.transform.SetParent(go.transform, false);
                b.transform.localPosition = Vector3.forward * 1.2f;
                b.transform.localRotation = Quaternion.Euler(0, i * 90f, 0);
                b.AddComponent<MeshFilter>().sharedMesh = beamMesh;
                var r = b.AddComponent<MeshRenderer>();
                r.sharedMaterial = g._beamMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = new Vector3(0, 0.05f, 0.8f);
            halo.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatQuad(7f, 7f);
            var hr = halo.AddComponent<MeshRenderer>();
            g._haloMat = new Material(bank.Glow);
            g._haloMat.SetFloat("_Shape", 0);
            hr.sharedMaterial = g._haloMat;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            g._light = new GameObject("GateLight").AddComponent<Light>();
            g._light.transform.SetParent(go.transform, false);
            g._light.transform.localPosition = new Vector3(0, 2.5f, 1.5f);
            g._light.range = 10f; g._light.intensity = 6f; g._light.shadows = LightShadows.None;

            g.SetOpen(!locked, instant: true);
            return g;
        }

        public void SetOpen(bool open, bool instant = false)
        {
            Open = open;
            var c = open ? _openColor : _lockedColor;
            _beamMat.SetColor("_Color", new Color(c.r, c.g, c.b, open ? 0.9f : 0.35f));
            _haloMat.SetColor("_Color", new Color(c.r, c.g, c.b, open ? 1.1f : 0.5f));
            _light.color = c;
            _barsCol.enabled = !open;
            float y = open ? -(MazeView.WallH + 0.4f) : 0f;
            DOTween.Kill(_bars);
            if (instant) _bars.localPosition = new Vector3(0, y, 0);
            else _bars.DOLocalMoveY(y, 1.1f).SetEase(Ease.InOutCubic).SetTarget(_bars);
        }

        void Update()
        {
            float p = 0.85f + 0.15f * Mathf.Sin(Time.time * 2.4f);
            _light.intensity = 6f * p;
        }

        void OnDestroy() { if (_bars != null) DOTween.Kill(_bars); }
    }
}

using UnityEngine;

namespace MazeRunner
{
    public enum PickupKind { Shard, Key, Hourglass, Crystal }

    /// <summary>Floating, spinning collectible with a glow on the floor. Collected by distance (LevelController).</summary>
    public sealed class Pickup : MonoBehaviour
    {
        public PickupKind Kind { get; private set; }
        public Vector2Int Cell { get; private set; }
        Transform _visual;
        float _phase;

        public static Pickup Spawn(Transform parent, PickupKind kind, Vector2Int cell)
        {
            var bank = AssetBank.I;
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = MazeView.CellCenter(cell);
            var p = go.AddComponent<Pickup>();
            p.Kind = kind;
            p.Cell = cell;
            p._phase = (cell.x * 7 + cell.y * 13) * 0.37f;

            Color glow;
            GameObject visual;
            switch (kind)
            {
                case PickupKind.Shard:
                    visual = Gem(go.transform, bank.ShardMat, 0.35f, 0.9f);
                    glow = new Color(0.3f, 0.85f, 1f, 0.9f);
                    break;
                case PickupKind.Key:
                    visual = Gem(go.transform, bank.KeyMat, 0.5f, 1.2f);
                    glow = new Color(1f, 0.75f, 0.2f, 1.3f);
                    var l = new GameObject("KeyLight").AddComponent<Light>();
                    l.transform.SetParent(go.transform, false);
                    l.transform.localPosition = Vector3.up * 2f;
                    l.color = new Color(1f, 0.75f, 0.3f); l.range = 7f; l.intensity = 4f; l.shadows = LightShadows.None;
                    break;
                case PickupKind.Hourglass:
                    visual = Model(go.transform, bank.Hourglass, bank.HourglassMat, 1.1f);
                    glow = new Color(1f, 0.85f, 0.4f, 0.8f);
                    break;
                default:
                    visual = Model(go.transform, bank.Crystal, bank.CrystalMat, 1.1f);
                    glow = new Color(0.45f, 0.6f, 1f, 0.9f);
                    break;
            }
            p._visual = visual.transform;

            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = Vector3.up * 0.05f;
            halo.AddComponent<MeshFilter>().sharedMesh = MeshKit.FlatQuad(2.6f, 2.6f);
            var hr = halo.AddComponent<MeshRenderer>();
            var hm = new Material(bank.Glow);
            hm.SetFloat("_Shape", 0);
            hm.SetColor("_Color", glow);
            hr.sharedMaterial = hm;
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p;
        }

        static GameObject Gem(Transform parent, Material mat, float r, float h)
        {
            var go = new GameObject("Gem");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = MeshKit.Gem(r, h);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static GameObject Model(Transform parent, GameObject prefab, Material mat, float height)
        {
            if (prefab == null) return Gem(parent, mat, 0.4f, height);
            var holder = new GameObject("Model");
            holder.transform.SetParent(parent, false);
            var m = Instantiate(prefab, holder.transform);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            var renderers = m.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return holder;
            var b = renderers[0].bounds;
            foreach (var r in renderers) { b.Encapsulate(r.bounds); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            float s = height / Mathf.Max(0.01f, b.size.y);
            m.transform.localScale *= s;
            // Re-centre the scaled model on the holder so it spins around its middle.
            var nb = renderers[0].bounds; foreach (var r in renderers) nb.Encapsulate(r.bounds);
            m.transform.position += holder.transform.position - nb.center;
            return holder;
        }

        void Update()
        {
            float t = Time.time + _phase;
            _visual.localPosition = Vector3.up * (1.2f + Mathf.Sin(t * 2.2f) * 0.18f);
            _visual.localRotation = Quaternion.Euler(0, t * 90f, Kind == PickupKind.Shard || Kind == PickupKind.Key ? 0 : Mathf.Sin(t) * 10f);
        }
    }
}

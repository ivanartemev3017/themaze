using UnityEngine;

public class SewerTheme : MonoBehaviour
{
    [SerializeField] Material wallMaterial;
    [SerializeField] Material floorMaterial;
    [SerializeField] Material ceilingMaterial;

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += Apply;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= Apply;

    void Apply(GameObject _)
    {
        var maze = GameObject.Find("Maze");
        if (maze == null) return;

        foreach (var r in maze.GetComponentsInChildren<Renderer>())
        {
            var n = r.gameObject.name;
            if      (wallMaterial    != null && n.StartsWith("Wall"))    r.sharedMaterial = wallMaterial;
            else if (floorMaterial   != null && n.StartsWith("Floor"))   r.sharedMaterial = floorMaterial;
            else if (ceilingMaterial != null && n.StartsWith("Ceiling")) r.sharedMaterial = ceilingMaterial;

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}

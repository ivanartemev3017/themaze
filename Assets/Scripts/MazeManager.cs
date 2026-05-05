using UnityEngine;
using DG.Tweening;

public class MazeManager : MonoBehaviour
{
    [Tooltip("Reference to the MazeGenerator component (can be on the same or a separate GameObject).")]
    public MazeGenerator mazeGenerator;

    private void Awake()
    {
        // Load biome before anything else — AtmosphereSetup reads BiomeLibrary.Current in Start()
        string biomeId = PlayerPrefs.GetString("SelectedBiome", "dungeon");
        BiomeLibrary.Activate(biomeId);

        if (GetComponent<MazeShifter>() == null)
            gameObject.AddComponent<MazeShifter>();
    }

    private void Start()
    {
        DOTween.Init();

        BiomeSettings b = BiomeLibrary.Current;

        // Ambient
        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = b.ambientColor;

        // Fog
        RenderSettings.fog         = true;
        RenderSettings.fogMode     = FogMode.Exponential;
        RenderSettings.fogColor    = b.fogColor;
        RenderSettings.fogDensity  = b.fogDensity;

        // Directional light (sun)
        var sun = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (sun != null)
        {
            sun.color     = b.sunColor;
            sun.intensity = b.sunIntensity;
        }

        if (mazeGenerator == null)
        {
            Debug.LogError("[MazeManager] mazeGenerator is not assigned.");
            return;
        }

        // Override floor material from biome if specified
        if (!string.IsNullOrEmpty(b.floorMaterialPath))
        {
            var mat = Resources.Load<Material>(b.floorMaterialPath);
            if (mat != null)
                mazeGenerator.floorMaterial = mat;
            else
                Debug.LogWarning($"[MazeManager] Biome floor material not found: {b.floorMaterialPath}");
        }

        mazeGenerator.GenerateMaze();
    }

    public void RegenerateMaze() => mazeGenerator?.RegenerateMaze();

    public Vector3 StartPosition => mazeGenerator != null ? mazeGenerator.StartWorldPosition : Vector3.zero;
    public Vector3 ExitPosition  => mazeGenerator != null ? mazeGenerator.ExitWorldPosition  : Vector3.zero;
}

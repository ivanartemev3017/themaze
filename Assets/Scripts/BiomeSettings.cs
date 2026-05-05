using UnityEngine;

public class BiomeSettings
{
    public string id;
    public string floorMaterialPath;  // Resources path, null = use MazeGenerator's default
    public string enemyPrefabPath;   // Resources path to enemy prefab, null = procedural dementor

    [Header("Ambient & Fog")]
    public Color  ambientColor;
    public Color  fogColor;
    public float  fogDensity;

    [Header("Fill Lights")]
    public Color  fillLightColor;
    public float  fillLightIntensity;

    [Header("Directional Light (Sun)")]
    public Color  sunColor;
    public float  sunIntensity;

    [Header("Post Processing")]
    public float  vignetteIntensity;
    public float  chromaticAberration;
    public float  contrast;
    public float  saturation;
    public Color  colorFilter;
    public float  bloomThreshold;
    public float  bloomIntensity;
}

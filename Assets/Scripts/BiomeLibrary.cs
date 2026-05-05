using UnityEngine;

public static class BiomeLibrary
{
    public static BiomeSettings Current { get; private set; } = Dungeon();

    public static void Activate(string id)
    {
        Current = id == "desert" ? Desert() : Dungeon();
    }

    public static BiomeSettings Dungeon() => new BiomeSettings
    {
        id                  = "dungeon",
        floorMaterialPath   = null,
        ambientColor        = new Color(0.42f, 0.46f, 0.62f),
        fogColor            = new Color(0.04f, 0.05f, 0.08f),
        fogDensity          = 0.05f,
        fillLightColor      = new Color(0.55f, 0.65f, 1.0f),
        fillLightIntensity  = 7.0f,
        sunColor            = new Color(0.50f, 0.50f, 0.60f),
        sunIntensity        = 0.15f,
        vignetteIntensity   = 0.12f,
        chromaticAberration = 0.12f,
        contrast            = 0f,
        saturation          = -10f,
        colorFilter         = new Color(0.96f, 0.93f, 0.88f),
        bloomThreshold      = 0.75f,
        bloomIntensity      = 1.4f,
    };

    public static BiomeSettings Desert() => new BiomeSettings
    {
        id                  = "desert",
        floorMaterialPath   = "Materials/DesertFloor",
        enemyPrefabPath     = "Spiders/SandSpider",
        ambientColor        = new Color(0.85f, 0.78f, 0.55f),
        fogColor            = new Color(0.92f, 0.85f, 0.65f),
        fogDensity          = 0.006f,
        fillLightColor      = new Color(1.0f, 0.92f, 0.65f),
        fillLightIntensity  = 25.0f,
        sunColor            = new Color(1.0f, 0.95f, 0.75f),
        sunIntensity        = 1.8f,
        vignetteIntensity   = 0.08f,
        chromaticAberration = 0.0f,
        contrast            = 10f,
        saturation          = 20f,
        colorFilter         = new Color(1.0f, 0.95f, 0.80f),
        bloomThreshold      = 0.85f,
        bloomIntensity      = 0.6f,
    };
}

using UnityEngine;

/// <summary>
/// Creates a dynamic Point Light parented to the player to simulate a carried torch.
/// Includes organic flickering for atmosphere.
/// Subscribed to PlayerSpawner.OnPlayerSpawned — no manual Editor steps required.
/// </summary>
public class PlayerTorch : MonoBehaviour
{
    private Light _light;
    private float _baseIntensity = 38f;

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    private void OnPlayerSpawned(GameObject player)
    {
        var go = new GameObject("PlayerTorchLight");
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = new Vector3(0f, 1.5f, 0.6f);

        _light           = go.AddComponent<Light>();
        _light.type      = LightType.Point;
        _light.color     = new Color(1.0f, 0.62f, 0.22f);
        _light.intensity = _baseIntensity;
        _light.range     = 50f;
        _light.shadows   = LightShadows.None;
    }

    void Update()
    {
        if (_light == null) return;
        // Organic flicker: two sine waves at different frequencies + perlin noise
        float t = Time.time;
        float flicker = Mathf.Sin(t * 8.5f) * 0.08f
                      + Mathf.Sin(t * 13.2f) * 0.05f
                      + (Mathf.PerlinNoise(t * 4f, 0f) - 0.5f) * 0.12f;
        _light.intensity = _baseIntensity * (1f + flicker);
    }

    /// <summary>Used by blackout event to temporarily kill the torch.</summary>
    public void SetBaseIntensity(float intensity)
    {
        _baseIntensity = intensity;
    }
}

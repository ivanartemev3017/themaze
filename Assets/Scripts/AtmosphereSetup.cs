using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AtmosphereSetup : MonoBehaviour
{
    void Start()
    {
        BiomeSettings b = BiomeLibrary.Current;

        var go      = new GameObject("AtmosphereVolume");
        var volume  = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(b.vignetteIntensity);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.Override(b.chromaticAberration);

        var col = profile.Add<ColorAdjustments>(true);
        col.contrast.Override(b.contrast);
        col.saturation.Override(b.saturation);
        col.colorFilter.Override(b.colorFilter);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(b.bloomThreshold);
        bloom.intensity.Override(b.bloomIntensity);
        bloom.scatter.Override(0.65f);
    }
}

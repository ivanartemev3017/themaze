using UnityEngine;

namespace MazeRunner
{
    /// <summary>All game audio. Recorded clips from the bank plus tiny synthesized blips for UI and pickups.</summary>
    public sealed class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }

        AudioSource _ambience, _oneShot, _heart;
        AudioClip _click, _pickup, _shard, _key, _win, _lose, _warn, _heartbeat, _freeze;

        public static Sfx Create(Transform parent)
        {
            var go = new GameObject("Sfx");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Sfx>();
        }

        void Awake()
        {
            I = this;
            _ambience = gameObject.AddComponent<AudioSource>();
            _ambience.loop = true; _ambience.volume = 0.55f; _ambience.playOnAwake = false;
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _heart = gameObject.AddComponent<AudioSource>();
            _heart.loop = true; _heart.volume = 0f; _heart.playOnAwake = false;

            _click     = Synth("click", 0.05f, t => Tone(t, 900f, 0.05f) * 0.35f);
            _pickup    = Synth("pickup", 0.25f, t => (Tone(t, 660f + t * 1600f, 0.25f)) * 0.4f);
            _shard     = Synth("shard", 0.45f, t => (Tone(t, 1046f, 0.45f) + Tone(t, 1568f, 0.35f) * 0.6f) * 0.3f);
            _key       = Synth("key", 0.7f, t => (Tone(t, 523f, 0.7f) + Tone(t - 0.12f, 784f, 0.58f) + Tone(t - 0.24f, 1046f, 0.46f)) * 0.25f);
            _win       = Synth("win", 1.4f, t => (Tone(t, 392f, 1.4f) + Tone(t - 0.15f, 523f, 1.25f) + Tone(t - 0.3f, 659f, 1.1f) + Tone(t - 0.45f, 784f, 0.95f)) * 0.22f);
            _lose      = Synth("lose", 1.3f, t => (Tone(t, 220f - t * 60f, 1.3f) + Tone(t, 233f - t * 60f, 1.3f)) * 0.3f);
            _warn      = Synth("warn", 0.5f, t => Tone(t, 140f, 0.5f) * Mathf.Sin(t * 60f) * 0.5f);
            _freeze    = Synth("freeze", 0.9f, t => Noise(t) * Mathf.Exp(-t * 4f) * 0.25f + Tone(t, 1800f - t * 900f, 0.9f) * 0.15f);
            _heartbeat = Synth("heart", 0.8f, t => (Thump(t) + Thump(t - 0.22f) * 0.7f) * 0.8f);
            _heart.clip = _heartbeat;
            Apply();
        }

        public void Apply()
        {
            AudioListener.volume = Save.D.Sound ? 1f : 0f;
            var bank = AssetBank.I;
            if (Save.D.Music && bank.Ambience != null)
            {
                if (_ambience.clip != bank.Ambience) _ambience.clip = bank.Ambience;
                if (!_ambience.isPlaying) _ambience.Play();
            }
            else _ambience.Stop();
        }

        public void Click() => Play(_click, 1f);
        public void Pickup() => Play(_pickup, 1f);
        public void Shard() => Play(_shard, 1f);
        public void Key() => Play(_key, 1f);
        public void Win() => Play(_win, 1f);
        public void Lose() => Play(_lose, 1f);
        public void Warn() => Play(_warn, 0.8f);
        public void Freeze() => Play(_freeze, 1f);
        public void Shift() => Play(AssetBank.I.Shift, 0.9f);
        public void Growl() => Play(AssetBank.I.Growl, 0.8f);

        /// <summary>0 = silent, 1 = loud and fast — driven by danger (low time, spider chasing).</summary>
        public void SetTension(float t)
        {
            t = Mathf.Clamp01(t);
            if (t > 0.01f && !_heart.isPlaying) _heart.Play();
            _heart.volume = Mathf.MoveTowards(_heart.volume, t * 0.9f, Time.unscaledDeltaTime * 2f);
            _heart.pitch = Mathf.Lerp(0.9f, 1.5f, t);
            if (_heart.volume <= 0.001f && t <= 0.01f) _heart.Stop();
        }

        public static void Vibrate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Save.D.Vibration) Handheld.Vibrate();
#endif
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip != null) _oneShot.PlayOneShot(clip, volume);
        }

        // ── synthesis ───────────────────────────────────────────────────────────
        const int Rate = 22050;

        static AudioClip Synth(string name, float seconds, System.Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Tone(float t, float freq, float length)
        {
            if (t < 0f || t > length) return 0f;
            float env = Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 5f / length);
            return Mathf.Sin(2f * Mathf.PI * freq * t) * env;
        }

        static float Thump(float t)
        {
            if (t < 0f || t > 0.18f) return 0f;
            return Mathf.Sin(2f * Mathf.PI * (60f - t * 120f) * t) * Mathf.Exp(-t * 22f);
        }

        static uint _seed = 1;
        static float Noise(float _)
        {
            _seed = _seed * 1664525u + 1013904223u;
            return (_seed >> 9) / (float)(1 << 23) * 2f - 1f;
        }
    }
}

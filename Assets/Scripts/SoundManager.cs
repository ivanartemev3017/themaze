using UnityEngine;

/// <summary>
/// Central sound manager. Handles:
///   - Player footsteps (from StarterAssets WAVs)
///   - Ambient drone (procedural)
///   - Heartbeat when enemy is near (procedural)
///   - Wall shift rumble (procedural)
///   - Win/Lose stingers (procedural)
///
/// Attach to PlayerSpawner GO. Subscribes to OnPlayerSpawned.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ---- Audio sources ----
    private AudioSource _footstepSource;
    private AudioSource _ambientSource;
    private AudioSource _heartbeatSource;
    private AudioSource _stingerSource;

    // ---- Footstep clips (loaded from Resources path) ----
    private AudioClip[] _footstepClips;
    private float       _footstepTimer;
    private const float FootstepInterval = 0.42f;  // seconds between steps while moving

    // ---- Heartbeat state ----
    private AudioClip _heartbeatClip;
    private bool      _heartbeatPlaying;
    private float     _heartbeatTargetVol;

    // ---- Procedural clips ----
    private AudioClip _ambientClip;
    private AudioClip _rumbleClip;
    private AudioClip _winClip;
    private AudioClip _loseClip;

    // ---- Metronome ----
    private AudioSource _metronomeSource;
    private AudioClip   _metronomeClip;
    private float       _metronomeTimer;
    private bool        _metronomeIsLoop;  // true when loaded file is a full loop track

    // ---- Enemy wail (3D positional) ----
    private AudioClip _wailClip;
    private float     _wailTimer;
    private float     _nextWailIn = 6f;

    private Transform _player;

    // =========================================================================
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        GenerateProceduralClips();
    }

    void OnEnable()  => PlayerSpawner.OnPlayerSpawned += OnPlayerSpawned;
    void OnDisable() => PlayerSpawner.OnPlayerSpawned -= OnPlayerSpawned;

    private void OnPlayerSpawned(GameObject player)
    {
        try { SetupAudio(player); }
        catch (System.Exception e) { Debug.LogError("[SoundManager] Setup failed: " + e.Message + "\n" + e.StackTrace); }
    }

    private void SetupAudio(GameObject player)
    {
        _player = player.transform;

        // Ensure no double AudioListener (would mute everything in Unity)
        EnsureSingleAudioListener();

        // Footstep source on player
        _footstepSource = player.AddComponent<AudioSource>();
        _footstepSource.spatialBlend = 0f;
        _footstepSource.volume = 0.7f;
        _footstepSource.playOnAwake = false;

        // Load footstep clips
        LoadFootstepClips();

        // Ambient source
        var ambGO = new GameObject("AmbientAudio");
        ambGO.transform.SetParent(player.transform, false);
        _ambientSource = ambGO.AddComponent<AudioSource>();
        _ambientSource.clip = _ambientClip;
        _ambientSource.loop = true;
        _ambientSource.volume = 0.45f;
        _ambientSource.spatialBlend = 0f;
        _ambientSource.Play();

        // Heartbeat source
        var hbGO = new GameObject("HeartbeatAudio");
        hbGO.transform.SetParent(player.transform, false);
        _heartbeatSource = hbGO.AddComponent<AudioSource>();
        _heartbeatSource.clip = _heartbeatClip;
        _heartbeatSource.loop = true;
        _heartbeatSource.volume = 0f;
        _heartbeatSource.spatialBlend = 0f;
        _heartbeatSource.Play();

        // Stinger source (win/lose)
        _stingerSource = player.AddComponent<AudioSource>();
        _stingerSource.spatialBlend = 0f;
        _stingerSource.playOnAwake = false;
        _stingerSource.volume = 0.85f;

        // Metronome source (2D — always audible, Interstellar-style ticking)
        var metGO = new GameObject("MetronomeAudio");
        metGO.transform.SetParent(player.transform, false);
        _metronomeSource = metGO.AddComponent<AudioSource>();
        _metronomeSource.spatialBlend = 0f;
        _metronomeTimer = 0f;

        if (_metronomeIsLoop)
        {
            // Real file is a full looping track — play continuously, control volume/pitch
            _metronomeSource.clip = _metronomeClip;
            _metronomeSource.loop = true;
            _metronomeSource.volume = 0.08f;
            _metronomeSource.playOnAwake = false;
            _metronomeSource.Play();
        }
        else
        {
            _metronomeSource.playOnAwake = false;
        }
    }

    void Update()
    {
        if (_player == null) return;
        UpdateFootsteps();
        UpdateHeartbeat();
        UpdateMetronome();
        UpdateEnemyWail();
    }

    // =========================================================================
    // Footsteps
    // =========================================================================

    private void LoadFootstepClips()
    {
        string basePath = "Assets/StarterAssets/ThirdPersonController/Character/Sfx/";
        var clips = new System.Collections.Generic.List<AudioClip>();
        for (int i = 1; i <= 10; i++)
        {
            string path = basePath + $"Player_Footstep_{i:D2}";
            // Try loading — these are in Assets, not Resources, so we use a different approach
            // We'll use the concrete footsteps or generate procedural ones
        }

        // Since footstep WAVs are not in Resources folder, generate procedural footsteps
        _footstepClips = new AudioClip[4];
        for (int i = 0; i < 4; i++)
            _footstepClips[i] = GenerateFootstep(i);
    }

    private void UpdateFootsteps() { } // disabled — procedural footsteps were too harsh

    // =========================================================================
    // Heartbeat — louder when enemy is closer
    // =========================================================================

    private void UpdateHeartbeat()
    {
        if (_heartbeatSource == null) return;

        // Find closest enemy
        float closestDist = float.MaxValue;
        var enemies = FindObjectsByType<MazeEnemy>(FindObjectsInactive.Exclude);
        foreach (var e in enemies)
        {
            float d = Vector3.Distance(_player.position, e.transform.position);
            if (d < closestDist) closestDist = d;
        }

        // Heartbeat volume ramps up when enemy is within 25 units
        float targetVol = 0f;
        if (closestDist < 25f)
        {
            targetVol = Mathf.InverseLerp(25f, 5f, closestDist) * 0.6f;
            _heartbeatSource.pitch = Mathf.Lerp(0.8f, 1.4f, targetVol / 0.6f);
        }

        _heartbeatSource.volume = Mathf.MoveTowards(_heartbeatSource.volume, targetVol, Time.deltaTime * 0.5f);
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>Play wall shift rumble at position.</summary>
    public void PlayRumble(Vector3 position)
    {
        if (_rumbleClip == null) return;
        var go = new GameObject("Rumble");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = _rumbleClip;
        src.spatialBlend = 0.6f;
        src.volume = 1.0f;
        src.maxDistance = 40f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.pitch = Random.Range(0.8f, 1.1f);
        src.Play();
        Destroy(go, _rumbleClip.length + 0.5f);
    }

    /// <summary>Play win stinger.</summary>
    public void PlayWin()
    {
        if (_stingerSource != null && _winClip != null)
            _stingerSource.PlayOneShot(_winClip, 0.6f);
    }

    /// <summary>Play lose stinger.</summary>
    public void PlayLose()
    {
        if (_stingerSource != null && _loseClip != null)
            _stingerSource.PlayOneShot(_loseClip, 0.5f);
    }

    /// <summary>Boost heartbeat to max for final rush.</summary>
    public void SetHeartbeatMax()
    {
        if (_heartbeatSource != null)
        {
            _heartbeatSource.volume = 0.6f;
            _heartbeatSource.pitch  = 1.5f;
        }
    }

    // =========================================================================
    // Audio listener guard
    // =========================================================================

    private static void EnsureSingleAudioListener()
    {
        var all = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude);
        if (all.Length <= 1) return;

        // Keep the one on Main Camera, disable the rest
        Camera mainCam = Camera.main;
        foreach (var al in all)
        {
            bool isMainCam = mainCam != null && al.gameObject == mainCam.gameObject;
            if (!isMainCam) al.enabled = false;
        }
        Debug.LogWarning($"[SoundManager] Found {all.Length} AudioListeners — disabled extras to prevent audio mute.");
    }

    // =========================================================================
    // Procedural audio generation
    // =========================================================================

    private void GenerateProceduralClips()
    {
        // Load real audio files first, fall back to procedural if missing
        _ambientClip = Resources.Load<AudioClip>("Audio/dungeon_ambience") ?? GenerateAmbientDrone();

        var metClip = Resources.Load<AudioClip>("Audio/metronome");
        if (metClip != null)
        {
            _metronomeClip   = metClip;
            _metronomeIsLoop = metClip.length > 3f;  // long file = full loop track
        }
        else
        {
            _metronomeClip   = GenerateMetronomeTick();
            _metronomeIsLoop = false;
        }

        _wailClip  = Resources.Load<AudioClip>("Audio/creature_growl") ?? GenerateDementorWail();
        _rumbleClip = Resources.Load<AudioClip>("Audio/stone_sliding")  ?? GenerateRumble();

        // Procedural only (no real files for these)
        _heartbeatClip = GenerateHeartbeat();
        _winClip       = GenerateWinStinger();
        _loseClip      = GenerateLoseStinger();
    }

    private static AudioClip GenerateFootstep(int variant)
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.12f);
        var clip = AudioClip.Create($"Footstep_{variant}", samples, 1, sampleRate, false);
        var data = new float[samples];
        float freq = 60f + variant * 15f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 35f);  // sharp attack, fast decay
            float noise = (Random.value * 2f - 1f) * 0.6f;
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.4f;
            data[i] = (noise + tone) * env;
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenerateAmbientDrone()
    {
        int sampleRate = 22050;
        int length = sampleRate * 8;  // 8 second loop
        var clip = AudioClip.Create("AmbientDrone", length, 1, sampleRate, false);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            // Raised frequencies so they're audible on phone speakers (min ~100 Hz)
            float drone = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.28f;
            drone += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.14f;
            drone += Mathf.Sin(2f * Mathf.PI * 240f * t) * 0.08f;
            drone += Mathf.Sin(2f * Mathf.PI * 60f  * t) * 0.10f;  // sub-harmonic texture
            // Slow wobble
            float mod = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.15f * t);
            // Add filtered noise for texture
            float noise = (Mathf.PerlinNoise(t * 3f, 0f) - 0.5f) * 0.12f;
            data[i] = Mathf.Clamp((drone * mod + noise) * 0.65f, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenerateHeartbeat()
    {
        int sampleRate = 22050;
        int length = sampleRate * 1;  // 1 second = ~1 beat cycle
        var clip = AudioClip.Create("Heartbeat", length, 1, sampleRate, false);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / sampleRate;
            // Raised to ~90Hz so phone speakers can reproduce it
            float thud1 = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 14f);
            thud1 += Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 18f) * 0.4f;
            float t2 = t - 0.28f;
            float thud2 = t2 > 0f
                ? (Mathf.Sin(2f * Mathf.PI * 110f * t2) * Mathf.Exp(-t2 * 16f)
                +  Mathf.Sin(2f * Mathf.PI * 220f * t2) * Mathf.Exp(-t2 * 20f) * 0.35f) * 0.7f
                : 0f;
            data[i] = Mathf.Clamp((thud1 + thud2) * 0.9f, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenerateRumble()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 1.5f);  // 1.5 seconds
        var clip = AudioClip.Create("WallRumble", samples, 1, sampleRate, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Clamp01(t * 5f) * Mathf.Exp(-(t - 0.2f) * 2f);
            float rumble = Mathf.Sin(2f * Mathf.PI * 35f * t) * 0.5f;
            rumble += Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.3f;
            float noise = (Mathf.PerlinNoise(t * 20f, 1f) - 0.5f) * 0.5f;
            data[i] = Mathf.Clamp((rumble + noise) * env, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenerateWinStinger()
    {
        int sampleRate = 22050;
        int samples = sampleRate * 2;
        var clip = AudioClip.Create("WinStinger", samples, 1, sampleRate, false);
        var data = new float[samples];
        // Rising major chord
        float[] freqs = { 262f, 330f, 392f, 523f }; // C major arpeggio
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float noteStart = n * 0.15f;
                if (t >= noteStart)
                {
                    float nt = t - noteStart;
                    float env = Mathf.Exp(-nt * 1.5f);
                    val += Mathf.Sin(2f * Mathf.PI * freqs[n] * nt) * env * 0.2f;
                }
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip GenerateLoseStinger()
    {
        int sampleRate = 22050;
        int samples = sampleRate * 2;
        var clip = AudioClip.Create("LoseStinger", samples, 1, sampleRate, false);
        var data = new float[samples];
        // Descending minor — ominous
        float[] freqs = { 220f, 185f, 155f, 130f }; // A descending
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float noteStart = n * 0.2f;
                if (t >= noteStart)
                {
                    float nt = t - noteStart;
                    float env = Mathf.Exp(-nt * 1.2f);
                    val += Mathf.Sin(2f * Mathf.PI * freqs[n] * nt) * env * 0.25f;
                }
            }
            data[i] = Mathf.Clamp(val, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    // ── Metronome tick — dry wood-block click ────────────────────────────────
    private static AudioClip GenerateMetronomeTick()
    {
        int sampleRate = 22050;
        int samples = (int)(sampleRate * 0.055f);   // 55 ms — very short
        var clip = AudioClip.Create("MetronomeTick", samples, 1, sampleRate, false);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t   = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 90f);        // sharp attack, instant decay
            float tone = Mathf.Sin(2f * Mathf.PI * 1100f * t) * 0.60f
                       + Mathf.Sin(2f * Mathf.PI * 750f  * t) * 0.35f;
            // Deterministic pseudo-noise (no Random — audio generation is not main-thread safe to assume seed)
            float noise = Mathf.Sin(i * 173.1f) * Mathf.Sin(i * 311.7f) * 0.25f;
            data[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    // ── Dementor-style wail — frequency-sweep + tremolo ─────────────────────
    private static AudioClip GenerateDementorWail()
    {
        int sampleRate = 22050;
        int samples    = (int)(sampleRate * 1.7f);
        var clip = AudioClip.Create("DementorWail", samples, 1, sampleRate, false);
        var data = new float[samples];
        float invSR = 1f / sampleRate;
        float phase  = 0f;
        float phase2 = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t        = i * invSR;
            float progress = (float)i / samples;    // 0..1

            // Frequency sweeps 1700 → 230 Hz with sqrt curve (fast drop at start)
            float freq  = Mathf.Lerp(1700f, 230f, Mathf.Sqrt(progress));
            float freq2 = freq * 1.51f;             // imperfect fifth harmonic
            phase  += freq  * invSR;
            phase2 += freq2 * invSR;

            // Tremolo 10 Hz — quivering Dementor texture
            float tremolo = 0.70f + 0.30f * Mathf.Sin(2f * Mathf.PI * 10f * t);

            // Envelope: sharp ramp in, peak, long tail
            float env;
            if (progress < 0.07f)       env = progress / 0.07f;
            else if (progress < 0.45f)  env = 1.0f;
            else                         env = 1.0f - (progress - 0.45f) / 0.55f;

            float tone  = Mathf.Sin(2f * Mathf.PI * phase)  * 0.65f
                        + Mathf.Sin(2f * Mathf.PI * phase2) * 0.28f;

            // Breath noise — deterministic sin-based pseudo-noise
            float noise = Mathf.Sin(i * 127.3f) * Mathf.Sin(i * 317.9f) * 0.14f;

            data[i] = Mathf.Clamp((tone + noise) * env * tremolo * 0.72f, -1f, 1f);
        }
        clip.SetData(data, 0);
        return clip;
    }

    // =========================================================================
    // Metronome — Interstellar-style ticking that accelerates with urgency
    // =========================================================================

    private void UpdateMetronome()
    {
        if (_metronomeSource == null || _metronomeClip == null) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameManager.GameState.Playing) return;

        float timeLeft = GameManager.Instance.TimeRemaining;

        // BPM ramp: 38 BPM when plenty of time → 210 BPM in final seconds
        float bpm;
        if      (timeLeft > 120f) bpm = 38f;
        else if (timeLeft >  60f) bpm = Mathf.Lerp(80f,  38f, (timeLeft - 60f)  / 60f);
        else if (timeLeft >  30f) bpm = Mathf.Lerp(130f, 80f, (timeLeft - 30f)  / 30f);
        else if (timeLeft >  10f) bpm = Mathf.Lerp(175f, 130f,(timeLeft - 10f)  / 20f);
        else                      bpm = Mathf.Lerp(210f, 175f, timeLeft          / 10f);

        float interval = 60f / bpm;

        // Volume: nearly inaudible when time is ample, rises as clock runs down
        float urgency = Mathf.Clamp01(1f - timeLeft / 120f);  // 0 @ 120 s → 1 @ 0 s
        float vol     = Mathf.Lerp(0.08f, 0.42f, urgency);
        // Pitch slightly rises with urgency (Interstellar effect)
        float pitch   = Mathf.Lerp(0.90f, 1.25f, urgency);

        if (_metronomeIsLoop)
        {
            // Looping track — just adjust volume and pitch in real time
            _metronomeSource.volume = Mathf.MoveTowards(_metronomeSource.volume, vol,   Time.deltaTime * 0.5f);
            _metronomeSource.pitch  = Mathf.MoveTowards(_metronomeSource.pitch,  pitch, Time.deltaTime * 0.3f);
        }
        else
        {
            _metronomeTimer += Time.deltaTime;
            if (_metronomeTimer >= interval)
            {
                _metronomeTimer -= interval;
                _metronomeSource.pitch = pitch;
                _metronomeSource.PlayOneShot(_metronomeClip, vol);
            }
        }
    }

    // =========================================================================
    // Enemy wail — 3D positional screech when Dementor is nearby
    // =========================================================================

    private void UpdateEnemyWail()
    {
        if (_wailClip == null || _player == null) return;

        float closestDist = float.MaxValue;
        MazeEnemy closestEnemy = null;
        var enemies = FindObjectsByType<MazeEnemy>(FindObjectsInactive.Exclude);
        foreach (var e in enemies)
        {
            float d = Vector3.Distance(_player.position, e.transform.position);
            if (d < closestDist) { closestDist = d; closestEnemy = e; }
        }

        // Only wail when enemy is within 24 units
        if (closestDist > 24f || closestEnemy == null)
        {
            _wailTimer = Mathf.Max(0f, _wailTimer - Time.deltaTime * 0.5f); // slow bleed-out
            return;
        }

        _wailTimer += Time.deltaTime;
        if (_wailTimer >= _nextWailIn)
        {
            _wailTimer   = 0f;
            _nextWailIn  = Random.Range(4.5f, 9.0f);

            // Volume inversely proportional to distance — real growl, can be loud
            float vol = Mathf.Lerp(0.95f, 0.25f, closestDist / 24f);

            var srcGO = new GameObject("EnemyWail");
            srcGO.transform.position = closestEnemy.transform.position;
            var src = srcGO.AddComponent<AudioSource>();
            src.clip         = _wailClip;
            src.spatialBlend = 0.85f;       // mostly 3D — direction matters
            src.volume       = vol;
            src.maxDistance  = 32f;
            src.rolloffMode  = AudioRolloffMode.Linear;
            src.pitch        = Random.Range(0.80f, 1.20f);  // variation each time
            src.Play();
            Destroy(srcGO, _wailClip.length + 0.5f);
        }
    }
}

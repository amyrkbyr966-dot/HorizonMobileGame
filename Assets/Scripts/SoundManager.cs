using UnityEngine;

/// <summary>Procedural sound, no audio files (0 MB of audio on disk —
/// this is the size-budget win: a 3 GB game ships dozens of recorded
/// engine samples; we synthesise them). Every garage car has its own
/// engine voice from EngineVoiceData (A/B-tuned against real recordings),
/// built as a PULSE-TRAIN clip (v3.3 model): one exhaust pressure pulse per
/// cylinder firing, each pulse a decaying burst whose tone tracks the firing
/// rate (f, 2f, 3f, 4f with the tuned gains), plus a deterministic noise
/// grain, a per-pulse noise burst and intake roar. The clip is built at a
/// 3000 rpm reference and its loop is an exact number of firing periods, so
/// it loops cleanly; AudioSource.pitch (= rpm / 3000) moves it across the
/// rev range. The engine runs through the car's real gear count (from
/// EngineVoiceData, not a hardcoded 5) with a shift dip + clunk, the skid
/// is a tonal tyre squeal, and lift-off crackle pops fire on throttle lift
/// for the cars that crackle.
/// Extra layers (v3.3): the driver hears the engine through the cabin, so
/// the continuous tones run through a per-car low-pass from interiorDamp
/// (Ghost 0.85 = dark 1200 Hz cabin, Cobra 0.10 = raw ~17800 Hz); tunnel
/// reverb is a bypassed AudioReverbFilter on the tones, switched by
/// TunnelZone.SetTunnelMode (no tunnel geometry exists yet, so it is
/// dormant); the skid swaps to a duller, noisier wet clip when the weather
/// turns to rain; the shift dip is per-car from the gearbox (7+ gears =
/// fast 0.12 s / 0.85x, 6 or fewer = 0.25 s / 0.70x).
/// Volume is saved in PlayerPrefs ("SoundVolume").
/// Attach to a GameObject in the Main scene; it finds the player car itself.</summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Volume")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    public bool muted;

    // Pulse-train clip: built at this reference rpm; pitch = rpm / RefRpm.
    const int RefRpm = 3000;
    const int SampleRate = 44100;

    AudioSource engineSource;
    AudioSource turboSource;
    AudioSource skidSource;
    AudioSource sfxSource;
    AudioClip engineClip;
    AudioClip skidClip;       // dry asphalt
    AudioClip skidClipWet;    // wet asphalt (rain) — duller, noisier
    AudioClip chimeClip;
    AudioClip clickClip;
    AudioClip coinClip;
    AudioClip crashClip;
    AudioClip deliveryClip;
    AudioClip shiftClip;
    AudioClip crackleClip;
    CarController playerCar;
    int currentVoiceIndex = -1;
    int lastGear = -1;
    float shiftTimer;
    float shiftDipTime = 0.25f;   // per-car: fast gearbox (7+ gears) 0.12 s
    float shiftDipPitch = 0.70f;  // per-car: fast gearbox 0.85x (smaller drop)
    float prevThrottle;
    EngineVoiceData.EngineVoiceSpec currentSpec;
    // Cabin (interior) treatment: the driver hears the engine through the
    // cabin, so the continuous tones (engine + whistle) run through a
    // per-car low-pass set from spec.interiorDamp. The filters live on the
    // sources' own child GameObjects — a filter component colours every
    // AudioSource on its GameObject, so the engine and turbo need their own.
    AudioLowPassFilter cabinEngine;
    AudioLowPassFilter cabinTurbo;
    // Tunnel reverb: an AudioReverbFilter on each continuous tone, bypassed
    // until a TunnelZone (or any caller) sets tunnel mode. No tunnel
    // geometry exists in the world yet, so this is dormant in-game.
    AudioReverbFilter tunnelEngine;
    AudioReverbFilter tunnelTurbo;
    bool tunnelMode;
    WeatherSystem.WeatherType lastWeather = WeatherSystem.WeatherType.Clear;
    float stateLogTimer;   // throttled runtime state log (in-game verification)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        masterVolume = PlayerPrefs.GetFloat("SoundVolume", 0.8f);
        muted = PlayerPrefs.GetInt("SoundMuted", 0) == 1;
        // Engine + turbo each get their own child GameObject so their
        // cabin low-pass and tunnel reverb colour only that tone (a filter
        // component affects every AudioSource on its GameObject).
        var engineGo = new GameObject("EngineAudio");
        engineGo.transform.SetParent(transform, false);
        engineSource = engineGo.AddComponent<AudioSource>();
        cabinEngine = engineGo.AddComponent<AudioLowPassFilter>();
        tunnelEngine = engineGo.AddComponent<AudioReverbFilter>();
        SetupTunnelReverb(tunnelEngine);
        var turboGo = new GameObject("TurboAudio");
        turboGo.transform.SetParent(transform, false);
        turboSource = turboGo.AddComponent<AudioSource>();
        cabinTurbo = turboGo.AddComponent<AudioLowPassFilter>();
        tunnelTurbo = turboGo.AddComponent<AudioReverbFilter>();
        SetupTunnelReverb(tunnelTurbo);
        skidSource = gameObject.AddComponent<AudioSource>();
        sfxSource = gameObject.AddComponent<AudioSource>();
        engineSource.loop = true;
        turboSource.loop = true;
        skidSource.loop = true;
        skidClip = MakeSkidClip(950f, 0.25f, 0.20f);
        skidClipWet = MakeSkidClip(650f, 0.18f, 0.32f);
        chimeClip = MakeChimeClip();
        clickClip = MakeToneClip(1200f, 0.08f, 0.5f);
        coinClip = MakeToneClip(1568f, 0.12f, 0.5f);
        crashClip = MakeNoiseClip(0.3f, 0.9f);
        deliveryClip = MakeToneClip(1047f, 0.25f, 0.6f);
        shiftClip = MakeToneClip(85f, 0.09f, 0.8f);
        skidSource.clip = skidClip;
        ApplyVoice(CarSelection.SelectedIndex);
        ApplyVolume();
        engineSource.Play();
        turboSource.Play();
    }

    /// <summary>Tunnel reverb preset: a hard-walled tube — long-ish dark
    /// decay, strong early reflections. Bypassed (enabled = false) until
    /// SetTunnelMode(true) is called.</summary>
    static void SetupTunnelReverb(AudioReverbFilter f)
    {
        f.reverbPreset = AudioReverbPreset.User;
        f.dryLevel = 0f;
        f.room = 0f;
        f.roomHF = 0f;
        f.roomLF = 0f;
        f.decayTime = 1.8f;
        f.decayHFRatio = 0.5f;
        f.reflectionsLevel = -12f;
        f.reflectionsDelay = 0.02f;
        f.reverbLevel = -8f;
        f.reverbDelay = 0.03f;
        f.diffusion = 100f;
        f.density = 100f;
        f.hfReference = 5000f;
        f.lfReference = 250f;
        f.enabled = false;
    }

    /// <summary>Called by a TunnelZone when the player drives into / out of
    /// a tunnel (or any enclosed space). Toggles the tunnel reverb on the
    /// continuous engine + whistle tones.</summary>
    public void SetTunnelMode(bool inTunnel)
    {
        tunnelMode = inTunnel;
        if (tunnelEngine != null) tunnelEngine.enabled = inTunnel;
        if (tunnelTurbo != null) tunnelTurbo.enabled = inTunnel;
    }

    void ApplyVoice(int carIndex)
    {
        currentVoiceIndex = Mathf.Clamp(carIndex, 0, EngineVoiceData.Voices.Length - 1);
        currentSpec = EngineVoiceData.ForCar(currentVoiceIndex);
        bool isEV = currentSpec.aspiration == EngineVoiceData.Aspiration.Electric;
        engineClip = isEV ? MakeEVClip() : MakeEngineClip(currentSpec, currentVoiceIndex);
        engineSource.clip = engineClip;
        if (!engineSource.isPlaying) engineSource.Play();
        // Lift-off crackle one-shot for the cars that crackle (ref level > 0).
        crackleClip = currentSpec.crackleLevel > 0
            ? MakeCrackleClip(currentSpec.crackleLevel) : null;
        // Turbo whistle / supercharger whine loop, at the car's own frequency.
        float whistle = currentSpec.turboWhistleHz > 0f ? currentSpec.turboWhistleHz
            : currentSpec.superchargerWhineHz > 0f ? currentSpec.superchargerWhineHz : 0f;
        turboSource.clip = whistle > 0f ? MakeTurboClip(whistle, currentSpec.turboWhistleGain) : null;
        if (turboSource.clip != null && !turboSource.isPlaying) turboSource.Play();
        else if (turboSource.clip == null && turboSource.isPlaying) turboSource.Stop();
        // Cabin (interior) treatment: the driver hears the engine through
        // the cabin. Cutoff falls with the square of the insulation, floored
        // at 1200 Hz so the firing fundamental always stays audible — the
        // Ghost (damp 0.85) gets a dark 1200 Hz cabin, a Cobra (0.10) stays
        // raw at ~17800 Hz, a typical car (0.30) sits at ~10800 Hz. The
        // whistle gets the same cabin (it is the most cabin-sensitive layer:
        // a well-insulated car barely lets the whistle in). Skid + crackle
        // are exterior feedback one-shots and stay unfiltered.
        float cutoff = Mathf.Max(1200f, 22000f * (1f - currentSpec.interiorDamp)
            * (1f - currentSpec.interiorDamp));
        if (cabinEngine != null) cabinEngine.cutoffFrequency = cutoff;
        if (cabinTurbo != null) cabinTurbo.cutoffFrequency = cutoff;
        // Per-car gear-shift behaviour, from the gearbox the car carries:
        // 7+ gears = a modern fast gearbox (DCT/PDK/8-9-speed auto) — a
        // quick 0.12 s dip with a small 0.85x pitch drop; 6 or fewer = a
        // slower box — 0.25 s and a deeper 0.70x drop. (gearCount is the
        // game's own gearbox data: DCT cars were given 7-8 gears.)
        bool fastBox = currentSpec.gearCount >= 7;
        shiftDipTime = fastBox ? 0.12f : 0.25f;
        shiftDipPitch = fastBox ? 0.85f : 0.70f;
        Debug.Log("[SoundManager] engine voice: car " + currentVoiceIndex +
            " pulses=" + currentSpec.pulsesPerRev + " redline=" + currentSpec.redlineRpm +
            " gears=" + currentSpec.gearCount + " crackleLv=" + currentSpec.crackleLevel +
            " whistle=" + whistle + "Hz cabinCutoff=" + cutoff + "Hz");
    }

    void Update()
    {
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                playerCar = p.GetComponent<CarController>();
            }
            else return;
        }
        // Rebuild the voice if the player switched cars (garage / menu).
        if (CarSelection.SelectedIndex != currentVoiceIndex)
            ApplyVoice(CarSelection.SelectedIndex);

        float speedNorm = Mathf.Clamp01(playerCar.GetSpeedMs() / Mathf.Max(1f, playerCar.maxSpeed));
        float throttle = MobileControls.Instance != null ? MobileControls.Instance.Throttle : 0f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle = 1f;

        // --- Gears from EngineVoiceData (DCT cars have 7-8, not a hardcoded 5). ---
        // RPM climbs inside each gear, then drops on the shift (0.62x redline
        // post-shift, matching the sample template); first gear starts at idle.
        int gearCount = Mathf.Max(1, currentSpec.gearCount);
        int gear = Mathf.Min(gearCount - 1, (int)(speedNorm * gearCount));
        float gearFrac = speedNorm * gearCount - gear;
        float redline = Mathf.Max(1f, (float)currentSpec.redlineRpm);
        float idle = (float)currentSpec.idleRpm;
        float rpm;
        if (currentSpec.aspiration == EngineVoiceData.Aspiration.Electric)
        {
            rpm = 0f; // EV has no gears; the whine is driven by speed below.
        }
        else if (gear == 0)
        {
            rpm = idle + gearFrac * (redline - idle);
        }
        else
        {
            rpm = 0.62f * redline + gearFrac * (redline - 0.62f * redline);
        }
        if (gear != lastGear && speedNorm > 0.03f && lastGear >= 0)
        {
            shiftTimer = shiftDipTime;
            PlayOneShot(shiftClip, 0.7f);
        }
        lastGear = gear;
        if (shiftTimer > 0f) shiftTimer -= Time.deltaTime;

        // --- Pitch: the clip is built at 3000 rpm, so pitch = rpm / 3000. ---
        // The EV whine is 45 Hz at launch -> ~175 Hz at top speed (A/B vs the
        // real Plaid clip); its clip is built at a 100 Hz reference.
        float pitch;
        if (currentSpec.aspiration == EngineVoiceData.Aspiration.Electric)
        {
            float whine = 45f + speedNorm * 130f;
            pitch = whine / 100f;
        }
        else
        {
            pitch = rpm / RefRpm;
            if (shiftTimer > 0f) pitch *= shiftDipPitch;   // per-car shift dip
        }
        engineSource.pitch = Mathf.Clamp(pitch, 0.1f, 4f);
        engineSource.volume = muted ? 0f : masterVolume *
            (0.10f + 0.30f * Mathf.Max(speedNorm, throttle));

        // --- Lift-off crackle: throttle lifted while moving, car crackles. ---
        if (prevThrottle > 0.5f && throttle < 0.2f && speedNorm > 0.05f
            && crackleClip != null)
        {
            PlayOneShot(crackleClip, 0.8f);
            // Logged so lift-off crackle is verifiable from Player.log on a
            // VM with no audio hardware (see the state log below).
            Debug.Log("[SoundManager] crackle fired: car=" + currentVoiceIndex
                + " level=" + currentSpec.crackleLevel
                + " rpm=" + (int)rpm);
        }
        prevThrottle = throttle;

        // Turbo whistle / supercharger whine spools up with speed + throttle.
        if (turboSource.clip != null)
            turboSource.volume = muted ? 0f : masterVolume * 0.30f * speedNorm * Mathf.Max(0.25f, throttle);
        else
            turboSource.volume = 0f;

        bool skidding = playerCar.IsSkidding && playerCar.GetSpeedMs() > 8f;
        // Surface-dependent skid: rain wets the road, so the squeal goes
        // duller (650 Hz, more hiss) and quieter than on dry asphalt. The
        // swap happens on the weather change, not every frame; if the skid
        // is playing it restarts on the new clip. (No off-road surface
        // signal exists in the car/world yet, so there is no gravel variant
        // to trigger — that needs a surface tag from the world side.)
        bool wet = WeatherSystem.Instance != null
            && WeatherSystem.Instance.Current == WeatherSystem.WeatherType.Rain;
        if (wet != (lastWeather == WeatherSystem.WeatherType.Rain))
        {
            lastWeather = wet ? WeatherSystem.WeatherType.Rain
                : WeatherSystem.WeatherType.Clear;
            bool wasPlaying = skidSource.isPlaying;
            skidSource.clip = wet ? skidClipWet : skidClip;
            if (wasPlaying) skidSource.Play();
        }
        if (skidding && !skidSource.isPlaying) skidSource.Play();
        else if (!skidding && skidSource.isPlaying) skidSource.Stop();
        // Skid volume follows how hard the car is sliding, not a flat level;
        // wet asphalt squeals less, so the wet clip plays quieter.
        float slip = Mathf.Clamp01((playerCar.SlipAmount - 4f) / 8f);
        skidSource.volume = muted ? 0f : masterVolume * 0.30f * slip * (wet ? 0.7f : 1f);

        // Runtime state log (throttled, 1 line / 5 s): this VM has no audio
        // hardware, so in-game sound cannot be verified by ear here — the
        // log is the verification. It proves the pulse-train clip is
        // pitch-tracking the RPM, the gears shift, the whistle spools, the
        // skid + wet/dry state follow the car, and the crackle fires (the
        // crackle line below appears once per lift-off event, not here).
        stateLogTimer -= Time.deltaTime;
        if (stateLogTimer <= 0f)
        {
            stateLogTimer = 5f;
            Debug.Log("[SoundManager] state: car=" + currentVoiceIndex
                + " rpm=" + (int)rpm + " gear=" + (gear + 1) + "/" + gearCount
                + " pitch=" + engineSource.pitch.ToString("0.00")
                + " throttle=" + throttle.ToString("0.00")
                + " speedNorm=" + speedNorm.ToString("0.00")
                + " skid=" + (skidding ? "yes" : "no")
                + " wet=" + (wet ? "yes" : "no")
                + " tunnel=" + (tunnelMode ? "yes" : "no")
                + " whistleVol=" + turboSource.volume.ToString("0.00"));
        }
    }

    public void PlayCheckpoint() { PlayOneShot(chimeClip, 0.8f); }
    public void PlayClick() { PlayOneShot(clickClip, 0.6f); }
    public void PlayCoin() { PlayOneShot(coinClip, 0.7f); }
    public void PlayCrash() { PlayOneShot(crashClip, 0.9f); }
    public void PlayDelivery() { PlayOneShot(deliveryClip, 0.7f); }
    public void PlayRepair() { PlayOneShot(chimeClip, 0.5f); }

    public void ToggleMute()
    {
        muted = !muted;
        PlayerPrefs.SetInt("SoundMuted", muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    public void SetVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat("SoundVolume", masterVolume);
        PlayerPrefs.Save();
        ApplyVolume();
    }

    void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || sfxSource == null || muted) return;
        sfxSource.PlayOneShot(clip, masterVolume * volume);
    }

    void ApplyVolume()
    {
        if (engineSource != null) engineSource.volume = muted ? 0f : masterVolume * 0.2f;
        if (skidSource != null) skidSource.volume = muted ? 0f : masterVolume * 0.25f;
    }

    /// <summary>Pulse-train engine clip (v3.3 model), built at the 3000 rpm
    /// reference. One exhaust pressure pulse per firing; each pulse is a
    /// decaying burst whose tone TRACKS the firing rate (f, 2f, 3f, 4f with
    /// the A/B-tuned gains spec.h2/h3/h4 — the 3000-rpm blend stored in
    /// EngineVoiceData), plus a deterministic noise grain and a per-pulse
    /// noise burst, plus intake roar. The loop is an exact number of firing
    /// periods, so it loops cleanly; every component is periodic with the
    /// firing period, so the seam is inaudible.</summary>
    static AudioClip MakeEngineClip(EngineVoiceData.EngineVoiceSpec spec, int carIndex)
    {
        float pulses = spec.pulsesPerRev;
        float f0 = RefRpm / 60f * pulses;              // firing freq at 3000 rpm
        float red = Mathf.Max(1f, (float)spec.redlineRpm);
        int nPeriods = Mathf.Clamp(Mathf.RoundToInt(0.25f * f0), 20, 200);
        float loopDur = nPeriods / f0;
        int len = Mathf.RoundToInt(loopDur * SampleRate);
        float[] data = new float[len];
        var rng = new System.Random(12345 + carIndex * 7919); // deterministic per car

        float g1 = 1f, g2 = spec.h2, g3 = spec.h3, g4 = spec.h4;
        // Coherent-overlap compensation (tracking bursts sum in phase):
        // 1 / (1 + f * tau), tau = 6 ms.
        float overlap = 1f / (1f + f0 * 0.006f);
        float amp = (0.75f + 0.25f * (RefRpm / red)) * spec.exhaustAmp * overlap;

        // Fixed per-pulse noise burst (20 ms, decays over 2 ms) — the same
        // pattern every pulse, so the loop stays periodic. It ramps in over
        // 0.2 ms (1 - exp(-t/0.0002)): without the ramp the burst starts at a
        // random value, which is a click at every pulse onset (and a loud one
        // at the loop seam — measured 0.42 on the 911). The Python master
        // lowpass smooths this; here the ramp does the same job.
        int blen = (int)(0.020f * SampleRate);
        float[] noiseBurst = new float[blen];
        for (int i = 0; i < blen; i++)
        {
            float t = (float)i / SampleRate;
            noiseBurst[i] = (float)(rng.NextDouble() * 2.0 - 1.0)
                * Mathf.Exp(-t / 0.002f) * (1f - Mathf.Exp(-t / 0.0002f));
        }
        // Deterministic 1.5 ms noise grain, highpassed above 500 Hz (one-pole),
        // identical every pulse — its train is exactly periodic, so its
        // spectrum is a comb at the firing harmonics (the clear pitch).
        int glen = (int)(0.0015f * SampleRate);
        float[] grain = new float[glen];
        float hpA = Mathf.Exp(-2f * Mathf.PI * 500f / SampleRate);
        float hpPrev = 0f, hpPrevIn = 0f, gpeak = 0f;
        for (int i = 0; i < glen; i++)
        {
            float inp = (float)(rng.NextDouble() * 2.0 - 1.0);
            float y = hpA * (hpPrev + inp - hpPrevIn);
            hpPrev = y; hpPrevIn = inp;
            // Same 0.2 ms ramp-in as the noise burst (no click at pulse onset).
            grain[i] = y * (1f - Mathf.Exp(-((float)i / SampleRate) / 0.0002f));
            gpeak = Mathf.Max(gpeak, Mathf.Abs(grain[i]));
        }
        if (gpeak > 0f) for (int i = 0; i < glen; i++) grain[i] /= gpeak;

        // Intake roar: a sum of sines at multiples of the firing frequency
        // inside the intake band, random phases — periodic by construction,
        // so the loop stays clean. (A filtered-noise intake would click at
        // the seam; this is the same texture at a low gain.)
        int nComp = 0;
        float[] compFreq = null, compPhase = null;
        if (spec.intakeHiHz > spec.intakeLoHz && spec.intakeGain > 0f)
        {
            int k0 = Mathf.Max(1, Mathf.CeilToInt(spec.intakeLoHz / f0));
            int k1 = Mathf.FloorToInt(spec.intakeHiHz / f0);
            nComp = Mathf.Clamp(k1 - k0 + 1, 0, 24);
            compFreq = new float[nComp];
            compPhase = new float[nComp];
            for (int k = 0; k < nComp; k++)
            {
                compFreq[k] = (k0 + k) * f0;
                compPhase[k] = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
            }
        }
        float intakeScale = 2.0f * Mathf.Pow(RefRpm / red, 1.6f) * spec.intakeGain;

        // Build the loop. Each sample sums the bursts of the pulses in the
        // last 20 ms, wrapped modulo the loop (a pulse from the previous loop
        // turn still contributes at the start — that is what makes the seam
        // inaudible).
        float period = 1f / f0;
        int maxBack = Mathf.CeilToInt(0.020f / period);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            float s = 0f;
            int kNow = Mathf.FloorToInt(t * f0);
            for (int back = 0; back <= maxBack; back++)
            {
                int k = kNow - back;
                int kw = ((k % nPeriods) + nPeriods) % nPeriods;
                float tk = kw / f0;
                float dt = t - tk;
                if (dt < 0f) dt += loopDur;
                if (dt >= 0.020f) continue;
                float dec = Mathf.Exp(-dt / 0.006f);
                float ph = 2f * Mathf.PI * f0 * dt;
                s += 0.7f * dec * amp * (g1 * Mathf.Sin(ph)
                                       + g2 * Mathf.Sin(2f * ph)
                                       + g3 * Mathf.Sin(3f * ph)
                                       + g4 * Mathf.Sin(4f * ph));
                int ni = Mathf.Min(blen - 1, Mathf.RoundToInt(dt * SampleRate));
                s += noiseBurst[ni] * 0.35f * spec.exhaustAmp;
                if (dt < 0.0015f)
                {
                    // RoundToInt, not a truncating cast: dt is computed by a
                    // floating-point subtraction, so dt*rate can land a hair
                    // below an integer (1.9999999) and truncate to the wrong
                    // sample — that made the noise/grain non-periodic and the
                    // loop click at the seam (measured 0.42 on the 911).
                    int gi = Mathf.Min(glen - 1, Mathf.RoundToInt(dt * SampleRate));
                    s += grain[gi] * 0.2f * spec.exhaustAmp;
                }
            }
            if (nComp > 0)
            {
                float it = 0f;
                for (int k = 0; k < nComp; k++)
                    it += Mathf.Sin(2f * Mathf.PI * compFreq[k] * t + compPhase[k]) / nComp;
                s += it * intakeScale;
            }
            data[i] = s;
        }
        // Master: soft clip (tanh) + normalize to peak ~0.89 (as the Python master).
        float peak = 0f;
        for (int i = 0; i < len; i++)
        {
            data[i] = (float)System.Math.Tanh(data[i] * 1.1);
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        }
        if (peak > 0f)
        {
            float sc = 0.89f / peak;
            for (int i = 0; i < len; i++) data[i] *= sc;
        }
        var clip = AudioClip.Create("EngineLoop", len, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>EV motor whine (Teslo): built at a 100 Hz reference, the
    /// whine is 45 Hz at launch -&gt; ~175 Hz at top speed (A/B vs the real
    /// Plaid clip) — Update() sets pitch = whine / 100. Wind/road noise is
    /// the main loudness at speed, so the whine itself stays subtle.</summary>
    static AudioClip MakeEVClip()
    {
        int len = SampleRate / 4; // 0.25 s loop, whole cycles at 100 Hz
        float[] data = new float[len];
        var rng = new System.Random(4242);
        // Wind: bandpassed noise 100-600 Hz — approximated here by a sum of
        // sines at multiples of 100 Hz inside the band (periodic, clean loop).
        // (Was 200-900 Hz; the EV sample's wind band was lowered to 100-600 Hz
        // in round 2 so the high-speed centroid matches the real Plaid's ~350 Hz.)
        float[] windFreq = new float[6];
        float[] windPhase = new float[6];
        for (int k = 0; k < 6; k++)
        {
            windFreq[k] = 100f + k * 100f;
            windPhase[k] = (float)(rng.NextDouble() * 2.0 * Mathf.PI);
        }
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            float ph = 2f * Mathf.PI * 100f * t;
            float s = 0.20f * Mathf.Sin(ph) + 0.08f * Mathf.Sin(2f * ph) + 0.03f * Mathf.Sin(3f * ph);
            float w = 0f;
            for (int k = 0; k < 6; k++)
                w += Mathf.Sin(2f * Mathf.PI * windFreq[k] * t + windPhase[k]) / 6f;
            data[i] = s + w * 0.25f;
        }
        float peak = 0f;
        for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f) for (int i = 0; i < len; i++) data[i] *= 0.89f / peak;
        var clip = AudioClip.Create("EVLoop", len, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Lift-off crackle one-shot: a burst of exhaust pops (12 ms
    /// noise bursts, 3 ms decay) at random times over ~0.7 s. Level 1 = 8
    /// pops, level 2 = 14, level 3 = 20 (the strongest, e.g. AMG GT).</summary>
    static AudioClip MakeCrackleClip(int level)
    {
        int len = Mathf.RoundToInt(SampleRate * 0.7f);
        float[] data = new float[len];
        var rng = new System.Random(777 + level * 131);
        int pops = level >= 3 ? 20 : level == 2 ? 14 : 8;
        for (int p = 0; p < pops; p++)
        {
            int start = rng.Next(0, len - (int)(0.012f * SampleRate));
            int plen = (int)(0.012f * SampleRate);
            float a = 0.5f + (float)rng.NextDouble() * 0.5f;
            for (int i = 0; i < plen; i++)
            {
                float t = (float)i / SampleRate;
                data[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0)
                    * Mathf.Exp(-t / 0.003f) * a;
            }
        }
        float peak = 0f;
        for (int i = 0; i < len; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0f) for (int i = 0; i < len; i++) data[i] *= 0.85f / peak;
        var clip = AudioClip.Create("Crackle", len, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Turbo whistle / supercharger whine: a high sine with an
    /// 11 Hz spool wobble (11 whole cycles per second, loops cleanly).
    /// gain is the per-car whistle gain (spec.turboWhistleGain, 1.0 = the
    /// typical Python amp 0.06; the Ghost's 0.01 amp gives 0.17, so its
    /// whisper-quiet turbos stay quiet in-game instead of whistling as loud
    /// as every other car — the fixed-amplitude clip was the turbo gap.
    /// A gain of 0 silences the loop; the caller only builds it when a
    /// whistle frequency exists, so 0 is never reached in practice.</summary>
    static AudioClip MakeTurboClip(float freq, float gain)
    {
        int rate = 44100;
        int len = rate;
        float[] data = new float[len];
        float g = Mathf.Max(0f, gain);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float wob = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 11f * t);
            data[i] = g * (0.30f * wob * Mathf.Sin(2f * Mathf.PI * freq * t)
                    + 0.10f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t));
        }
        var clip = AudioClip.Create("TurboLoop", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Tyre squeal: a tone (the classic skid frequency) mixed
    /// with noise, 1 s loop, whole cycles only so it loops cleanly.
    /// Dry asphalt: 950 Hz, mostly tone. Wet asphalt: 650 Hz, duller and
    /// noisier (hiss/spray) — built as a second clip and swapped in by
    /// Update() when the weather turns to rain.</summary>
    static AudioClip MakeSkidClip(float toneHz, float toneAmp, float noiseAmp)
    {
        int rate = 44100;
        int len = rate;
        float[] data = new float[len];
        var rng = new System.Random(777);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float wob = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            data[i] = (toneAmp * wob * Mathf.Sin(2f * Mathf.PI * toneHz * t)
                     + noiseAmp * (float)(rng.NextDouble() * 2.0 - 1.0)) * 0.6f;
        }
        var clip = AudioClip.Create("SkidLoop", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeToneClip(float freq, float seconds, float amp)
    {
        int rate = 44100;
        int len = Mathf.RoundToInt(rate * seconds);
        float[] data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float env = 1f - (float)i / len; // decay so it does not click
            data[i] = amp * env * Mathf.Sin(2f * Mathf.PI * freq * t);
        }
        var clip = AudioClip.Create("Tone", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeChimeClip()
    {
        int rate = 44100;
        int len = Mathf.RoundToInt(rate * 0.7f);
        float[] data = new float[len];
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / rate;
            float env = 1f - (float)i / len;
            float freq = t < 0.3f ? 880f : 1318f;
            data[i] = 0.5f * env * Mathf.Sin(2f * Mathf.PI * freq * t);
        }
        var clip = AudioClip.Create("Chime", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeNoiseClip(float seconds, float amp)
    {
        int rate = 44100;
        int len = Mathf.RoundToInt(rate * seconds);
        float[] data = new float[len];
        var rng = new System.Random(12345);
        for (int i = 0; i < len; i++)
        {
            float env = 1f - (float)i / len;
            data[i] = amp * env * (float)(rng.NextDouble() * 2.0 - 1.0);
        }
        var clip = AudioClip.Create("Noise", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

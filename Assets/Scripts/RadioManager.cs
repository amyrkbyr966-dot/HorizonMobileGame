using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>RADIO — three stations of PROCEDURAL music while
/// free-roaming (and everywhere in the Main scene). No copyrighted
/// tracks, no audio files: every station loop is synthesised in code
/// at runtime in the same style as SoundManager's procedural clips
/// (oscillators + envelopes rendered into an AudioClip), so the
/// radio costs 0 MB of audio assets:
///
///   0  Horizon FM — J-Pop Drive : 128 BPM bright synth-pop,
///      square-wave arpeggio lead, pumping bass, 4-on-floor kick.
///   1  Midnight Lo-Fi : 72 BPM jazzy 7th-chord pads, soft bass,
///      vinyl crackle, late-night cruise.
///   2  Eurobeat Rush : 152 BPM driving eurobeat, saw bass,
///      16th-note riff, four-on-floor kick.
///
/// Public API: NextStation() / PreviousStation() / SetStation(int) /
/// SetVolume(float) / CurrentStationName / StationCount. Station and
/// volume persist in PlayerPrefs. Loops are generated lazily on first
/// select and cached. The radio respects SoundManager.muted.</summary>
public class RadioManager : MonoBehaviour
{
    public static RadioManager Instance { get; private set; }

    public enum Station { JPopDrive = 0, MidnightLoFi = 1, EurobeatRush = 2 }

    public const int StationCount = 3;
    const int SampleRate = 44100;
    const string PrefsStation = "Radio_Station";
    const string PrefsVolume = "Radio_Volume";

    public Station CurrentStation { get; private set; }
    public string CurrentStationName
    {
        get
        {
            switch (CurrentStation)
            {
                case Station.JPopDrive: return "Horizon FM - J-Pop Drive";
                case Station.MidnightLoFi: return "Midnight Lo-Fi";
                case Station.EurobeatRush: return "Eurobeat Rush";
                default: return "Horizon FM";
            }
        }
    }
    public float Volume { get; private set; } = 0.35f;
    public bool IsPlaying { get { return radioSource != null && radioSource.isPlaying; } }

    AudioSource radioSource;
    readonly AudioClip[] stationClips = new AudioClip[StationCount];
    readonly bool[] stationBuilt = new bool[StationCount];
    float muteCheckTimer;
    HUD hud;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CurrentStation = (Station)Mathf.Clamp(PlayerPrefs.GetInt(PrefsStation, 0), 0, StationCount - 1);
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsVolume, 0.35f));
        var go = new GameObject("RadioAudio");
        go.transform.SetParent(transform, false);
        radioSource = go.AddComponent<AudioSource>();
        radioSource.loop = true;
        radioSource.volume = Volume;
        radioSource.playOnAwake = false;
    }

    void Start()
    {
        if (SceneManager.GetActiveScene().name == "Main")
            PlayStation(CurrentStation);
    }

    void Update()
    {
        // Respect the master mute (SoundManager owns it).
        muteCheckTimer -= Time.deltaTime;
        if (muteCheckTimer <= 0f)
        {
            muteCheckTimer = 1f;
            bool muted = SoundManager.Instance != null && SoundManager.Instance.muted;
            if (radioSource != null)
                radioSource.volume = muted ? 0f : Volume;
        }
    }

    public void NextStation()
    {
        PlayStation((Station)(((int)CurrentStation + 1) % StationCount));
    }

    public void PreviousStation()
    {
        PlayStation((Station)(((int)CurrentStation + StationCount - 1) % StationCount));
    }

    public void SetStation(int index)
    {
        PlayStation((Station)Mathf.Clamp(index, 0, StationCount - 1));
    }

    public void PlayStation(Station station)
    {
        CurrentStation = station;
        PlayerPrefs.SetInt(PrefsStation, (int)station);
        PlayerPrefs.Save();
        if (!stationBuilt[(int)station])
        {
            stationClips[(int)station] = BuildStationLoop(station);
            stationBuilt[(int)station] = true;
        }
        if (radioSource == null) return;
        radioSource.clip = stationClips[(int)station];
        radioSource.Play();
        ShowMessage("Radio: " + CurrentStationName, 3f);
        Debug.Log("[Radio] now playing " + CurrentStationName);
    }

    public void StopRadio()
    {
        if (radioSource != null) radioSource.Stop();
    }

    public void SetVolume(float v)
    {
        Volume = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(PrefsVolume, Volume);
        PlayerPrefs.Save();
        if (radioSource != null && (SoundManager.Instance == null || !SoundManager.Instance.muted))
            radioSource.volume = Volume;
    }

    // ---- Procedural station synthesis ----

    enum Wave { Sine, Square, Saw, Triangle }

    struct NoteEvent
    {
        public float startSec, durSec, freq, gain;
        public Wave wave;
        public bool isKick, isHat, isPad, isCrackle;
        public NoteEvent(float start, float dur, float freq, Wave wave, float gain)
        {
            startSec = start; durSec = dur; this.freq = freq;
            this.wave = wave; this.gain = gain;
            isKick = isHat = isPad = isCrackle = false;
        }
    }

    AudioClip BuildStationLoop(Station station)
    {
        int bpm, bars;
        int[] roots;       // chord roots, MIDI, one per bar
        int[][] chordTones; // extra chord tones (semitone offsets from root)
        bool loFi = false;
        switch (station)
        {
            case Station.JPopDrive:
                bpm = 128; bars = 8;
                roots = new int[] { 57, 54, 50, 52, 57, 54, 50, 52 }; // A F#m D E
                chordTones = new int[][] { new int[] { 0, 4, 7 }, new int[] { 0, 3, 7 }, new int[] { 0, 4, 7 }, new int[] { 0, 4, 7, 10 } };
                break;
            case Station.MidnightLoFi:
                bpm = 72; bars = 4;
                roots = new int[] { 48, 45, 50, 43 }; // C Am Dm G
                chordTones = new int[][] { new int[] { 0, 4, 7, 11 }, new int[] { 0, 3, 7, 10 }, new int[] { 0, 3, 7, 10 }, new int[] { 0, 4, 7, 10 } };
                loFi = true;
                break;
            default: // EurobeatRush
                bpm = 152; bars = 8;
                roots = new int[] { 48, 43, 45, 41, 48, 43, 45, 41 }; // C G Am F
                chordTones = new int[][] { new int[] { 0, 4, 7 }, new int[] { 0, 4, 7 }, new int[] { 0, 3, 7 }, new int[] { 0, 4, 7 } };
                break;
        }
        float beat = 60f / bpm;
        float barLen = beat * 4f;
        float loopLen = barLen * bars;
        var notes = new List<NoteEvent>();

        for (int bar = 0; bar < bars; bar++)
        {
            float barStart = bar * barLen;
            int root = roots[bar % roots.Length];
            int[] tones = chordTones[bar % chordTones.Length];
            float rootFreq = MidiFreq(root);

            // Pad: whole-bar chord (sine stack, slow attack).
            foreach (int t in tones)
            {
                var pad = new NoteEvent(barStart, barLen, MidiFreq(root + t), Wave.Sine, loFi ? 0.055f : 0.04f);
                pad.isPad = true;
                notes.Add(pad);
            }
            // Bass: root, 8ths (J-pop/eurobeat) or whole notes (lo-fi).
            if (loFi)
            {
                notes.Add(new NoteEvent(barStart, barLen * 0.9f, MidiFreq(root - 12), Wave.Sine, 0.22f));
            }
            else
            {
                for (int e = 0; e < 8; e++)
                    notes.Add(new NoteEvent(barStart + e * beat * 0.5f, beat * 0.45f, MidiFreq(root - 12), Wave.Saw, 0.16f));
            }
            // Kick: four-on-floor (lo-fi: beats 1 and 3, soft).
            int kicks = loFi ? 2 : 4;
            for (int k = 0; k < kicks; k++)
            {
                var kick = new NoteEvent(barStart + k * (loFi ? beat * 2f : beat), 0.14f, 120f, Wave.Sine, loFi ? 0.25f : 0.5f);
                kick.isKick = true;
                notes.Add(kick);
            }
            // Hats: 8ths (J-pop/eurobeat) / offbeat 8ths (lo-fi).
            for (int h = 0; h < 8; h++)
            {
                if (loFi && h % 2 == 0) continue;
                var hat = new NoteEvent(barStart + h * beat * 0.5f, 0.05f, 8000f, Wave.Sine, loFi ? 0.05f : 0.08f);
                hat.isHat = true;
                notes.Add(hat);
            }
            // Lead: pentatonic arpeggio (J-pop: 16ths, eurobeat: 16th riff,
            // lo-fi: sparse melody on beats 1.5 / 3.5).
            int[] penta = { 0, 3, 5, 7, 10, 12, 15 };
            var rng = new System.Random(1000 + bar);
            if (loFi)
            {
                foreach (float off in new float[] { 1.5f, 3.5f })
                {
                    int step = penta[rng.Next(penta.Length)];
                    notes.Add(new NoteEvent(barStart + off * beat, beat * 0.9f, MidiFreq(root + 12 + step), Wave.Triangle, 0.10f));
                }
            }
            else
            {
                for (int s = 0; s < 16; s++)
                {
                    int step = penta[(s * 5 + bar) % penta.Length];
                    notes.Add(new NoteEvent(barStart + s * beat * 0.25f, beat * 0.22f, MidiFreq(root + 12 + step), Wave.Square, 0.07f));
                }
            }
            // Vinyl crackle (lo-fi only): random tiny ticks.
            if (loFi)
            {
                for (int c = 0; c < 120; c++)
                {
                    var tick = new NoteEvent(barStart + (float)rng.NextDouble() * barLen, 0.004f, 0f, Wave.Sine, 0.03f);
                    tick.isCrackle = true;
                    notes.Add(tick);
                }
            }
        }

        int total = Mathf.RoundToInt(loopLen * SampleRate);
        float[] data = new float[total];
        foreach (var n in notes) RenderNote(data, n, total);
        // Gentle normalise so the loop never clips.
        float peak = 0.001f;
        foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
        float norm = 0.85f / peak;
        for (int i = 0; i < total; i++) data[i] *= norm;

        var clip = AudioClip.Create("Radio_" + station, total, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void RenderNote(float[] data, NoteEvent n, int total)
    {
        int start = Mathf.RoundToInt(n.startSec * SampleRate);
        int len = Mathf.RoundToInt(n.durSec * SampleRate);
        if (start >= total) return;
        len = Mathf.Min(len, total - start);
        var rng = new System.Random(start + 7);
        double phase = 0.0;
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            float u = (float)i / Mathf.Max(1, len); // 0..1 through the note
            float env;
            float sample;
            if (n.isKick)
            {
                // Pitch-drop kick: 130 -> 45 Hz, fast decay.
                float f = Mathf.Lerp(130f, 45f, u);
                phase += (double)f / SampleRate;
                env = Mathf.Exp(-22f * t);
                sample = Mathf.Sin((float)(phase * 2.0 * System.Math.PI)) * env;
            }
            else if (n.isHat)
            {
                env = Mathf.Exp(-90f * t);
                sample = ((float)rng.NextDouble() * 2f - 1f) * env;
            }
            else if (n.isCrackle)
            {
                sample = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
            }
            else if (n.isPad)
            {
                phase += (double)n.freq / SampleRate;
                float attack = Mathf.Min(1f, u / 0.3f);
                float release = 1f - Mathf.SmoothStep(0.8f, 1f, u);
                env = attack * release;
                sample = Mathf.Sin((float)(phase * 2.0 * System.Math.PI)) * env;
            }
            else
            {
                phase += (double)n.freq / SampleRate;
                env = Mathf.Exp(-4f * u) * Mathf.Min(1f, t / 0.005f);
                sample = WaveSample(n.wave, (float)phase) * env;
            }
            data[start + i] += sample * n.gain;
        }
    }

    static float WaveSample(Wave wave, float phase)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square:
                return (Mathf.Sin(p * 2f * Mathf.PI) >= 0f ? 0.6f : -0.6f);
            case Wave.Saw:
                return 2f * p - 1f;
            case Wave.Triangle:
                return 2f * Mathf.Abs(2f * p - 1f) - 1f;
            default:
                return Mathf.Sin(p * 2f * Mathf.PI);
        }
    }

    static float MidiFreq(int midi)
    {
        return 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

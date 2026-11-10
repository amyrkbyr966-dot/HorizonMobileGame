using System.Collections.Generic;
using UnityEngine;

/// <summary>SEASONAL FESTIVALS — the world changes with the seasons.
/// Hooks SeasonManager ONLY through its public API (Current, IsSnow)
/// — SeasonManager.cs is never edited. All effects are particle
/// systems built in code (soft-dot sprite generated at runtime, no
/// texture or audio files, ~0 MB) plus real lights:
///
///   Spring — sakura petals falling over Ueno Park (Sakura Grove).
///   Summer — fireworks over Tokyo Bay, only at night (real bursts
///            + a flash light per burst, random colours).
///   Autumn — falling leaves (orange/red) over Ueno Park + Shibuya.
///   Winter — snow falling around the player + a snow festival at
///            Ueno Park: warm festival lights ringing the pond.
///            (Stalls arrive with the Phase B environment build;
///            no placeholder geometry is created here.)
///
/// The active festival is exposed as CurrentFestival / FestivalLabel
/// for the Phase B HUD line. Season changes are picked up by polling
/// SeasonManager.Current once a second (it has no change event).</summary>
public class FestivalManager : MonoBehaviour
{
    public static FestivalManager Instance { get; private set; }

    public enum Festival { None, Sakura, Fireworks, AutumnLeaves, SnowFestival }

    /// <summary>Festival running right now (None outside its season).</summary>
    public Festival CurrentFestival { get; private set; } = Festival.None;
    public string FestivalLabel
    {
        get
        {
            switch (CurrentFestival)
            {
                case Festival.Sakura: return "Sakura Festival - Ueno Park";
                case Festival.Fireworks: return "Summer Fireworks - Tokyo Bay";
                case Festival.AutumnLeaves: return "Autumn Leaves - Ueno Park";
                case Festival.SnowFestival: return "Snow Festival - Ueno Park";
                default: return "";
            }
        }
    }

    static readonly Vector3 UenoParkCentre = new Vector3(-1950f, 0f, 1750f);
    static readonly Vector3 TokyoBayCentre = new Vector3(0f, 0f, -2160f);

    ParticleSystem sakuraSystem;
    ParticleSystem leavesSystem;
    ParticleSystem snowSystem;
    ParticleSystem fireworksSystem;
    Light fireworkFlash;
    readonly List<GameObject> festivalLights = new List<GameObject>();
    Texture2D softDot;
    SeasonManager.Season lastSeason = (SeasonManager.Season)(-1);
    float pollTimer;
    float fireworkTimer;
    HUD hud;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        softDot = MakeSoftDotTexture();
    }

    void Start()
    {
        BuildSystems();
        BuildFestivalLights();
        ApplySeason(SeasonManager.Instance != null ? SeasonManager.Instance.Current : SeasonManager.Season.Spring);
    }

    void Update()
    {
        // Season change poll (1 Hz — SeasonManager has no event).
        pollTimer -= Time.deltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = 1f;
            if (SeasonManager.Instance != null && SeasonManager.Instance.Current != lastSeason)
                ApplySeason(SeasonManager.Instance.Current);
        }

        // Snow follows the player; fireworks burst on a timer.
        if (snowSystem != null && snowSystem.gameObject.activeSelf)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                snowSystem.transform.position = player.transform.position + new Vector3(0f, 25f, 0f);
        }
        if (CurrentFestival == Festival.Fireworks && fireworksSystem != null)
        {
            var cycle = FindFirstObjectByType<DayNightCycle>();
            bool night = cycle != null && cycle.IsNight;
            fireworksSystem.gameObject.SetActive(night);
            if (night)
            {
                fireworkTimer -= Time.deltaTime;
                if (fireworkTimer <= 0f)
                {
                    fireworkTimer = 1.4f + Random.value * 1.6f;
                    BurstFirework();
                }
            }
        }
        if (fireworkFlash != null && fireworkFlash.intensity > 0f)
            fireworkFlash.intensity = Mathf.Max(0f, fireworkFlash.intensity - Time.deltaTime * 25f);
    }

    void ApplySeason(SeasonManager.Season season)
    {
        lastSeason = season;
        SetSystem(sakuraSystem, false);
        SetSystem(leavesSystem, false);
        SetSystem(snowSystem, false);
        SetSystem(fireworksSystem, false);
        SetLights(false);

        switch (season)
        {
            case SeasonManager.Season.Spring:
                CurrentFestival = Festival.Sakura;
                SetSystem(sakuraSystem, true);
                break;
            case SeasonManager.Season.Summer:
                CurrentFestival = Festival.Fireworks;
                SetSystem(fireworksSystem, true);
                break;
            case SeasonManager.Season.Autumn:
                CurrentFestival = Festival.AutumnLeaves;
                SetSystem(leavesSystem, true);
                break;
            case SeasonManager.Season.Winter:
                CurrentFestival = Festival.SnowFestival;
                SetSystem(snowSystem, true);
                SetLights(true);
                break;
            default:
                CurrentFestival = Festival.None;
                break;
        }
        Debug.Log("[Festival] " + FestivalLabel + " (" + season + ")");
        ShowMessage(FestivalLabel, 4f);
    }

    void BurstFirework()
    {
        Vector3 centre = TokyoBayCentre + new Vector3(Random.Range(-80f, 80f), Random.Range(55f, 95f), Random.Range(-40f, 40f));
        Color colour = Color.HSVToRGB(Random.value, 0.85f, 1f);
        var ep = new ParticleSystem.EmitParams();
        ep.startLifetime = 1.6f;
        ep.startSize = 0.55f;
        ep.startColor = colour;
        for (int i = 0; i < 90; i++)
        {
            ep.position = centre;
            ep.velocity = Random.onUnitSphere * Random.Range(9f, 22f);
            fireworksSystem.Emit(ep, 1);
        }
        if (fireworkFlash != null)
        {
            fireworkFlash.transform.position = centre;
            fireworkFlash.color = colour;
            fireworkFlash.intensity = 30f;
        }
    }

    // ---- Construction (all in code, no assets) ----

    void BuildSystems()
    {
        // Sakura: pink petals drifting down over Ueno Park, gentle sway.
        sakuraSystem = MakeFallingSystem("SakuraPetals", UenoParkCentre + new Vector3(0f, 30f, 0f),
            new Vector3(600f, 1f, 600f), new Color(1f, 0.75f, 0.85f), 0.28f, 45f, 9f, 1.6f, 0.25f, 1.2f);
        // Autumn leaves: orange/red, slightly faster fall.
        leavesSystem = MakeFallingSystem("AutumnLeaves", UenoParkCentre + new Vector3(0f, 30f, 0f),
            new Vector3(600f, 1f, 600f), new Color(1f, 0.55f, 0.15f), 0.32f, 50f, 8f, 2.2f, 0.45f, 1.0f);
        // Snow: white flakes in a box above the player (repositioned in Update).
        snowSystem = MakeFallingSystem("Snowfall", Vector3.up * 25f,
            new Vector3(120f, 1f, 120f), Color.white, 0.16f, 220f, 7f, 2.8f, 0.55f, 0.8f);
        // Fireworks: manual bursts only (emission rate 0), additive glow.
        fireworksSystem = MakeBurstSystem("Fireworks", TokyoBayCentre);
        var flashGo = new GameObject("FireworkFlash");
        flashGo.transform.SetParent(transform, false);
        fireworkFlash = flashGo.AddComponent<Light>();
        fireworkFlash.type = LightType.Point;
        fireworkFlash.range = 120f;
        fireworkFlash.intensity = 0f;
    }

    void BuildFestivalLights()
    {
        // Snow-festival lantern ring around the Ueno Park pond:
        // 8 warm lights, 60 m ring. Created once, toggled by season.
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2f / 8f;
            Vector3 pos = UenoParkCentre + new Vector3(Mathf.Cos(a) * 60f, 4f, Mathf.Sin(a) * 60f);
            var go = new GameObject("SnowFestivalLight_" + i);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.4f);
            light.intensity = 20f;
            light.range = 25f;
            go.SetActive(false);
            festivalLights.Add(go);
        }
    }

    ParticleSystem MakeFallingSystem(string name, Vector3 pos, Vector3 boxSize,
        Color colour, float size, float rate, float lifetime, float fallSpeed,
        float gravity, float sway)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = colour;
        main.startSize = size;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.gravityModifier = gravity;
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = boxSize;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = sway;
        noise.frequency = 0.6f;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = -fallSpeed;
        velocity.space = ParticleSystemSimulationSpace.World;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.material = MakeParticleMaterial(false);
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        go.SetActive(false);
        return ps;
    }

    ParticleSystem MakeBurstSystem(string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = Color.white;
        main.startSize = 0.55f;
        main.startLifetime = 1.6f;
        main.startSpeed = 0f;
        main.gravityModifier = 0.35f;
        main.maxParticles = 800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.material = MakeParticleMaterial(true);
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        go.SetActive(false);
        return ps;
    }

    Material MakeParticleMaterial(bool additive)
    {
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetTexture("_BaseMap", softDot);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Surface", 1f); // transparent
        if (additive)
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        }
        else
        {
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        }
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
        return mat;
    }

    static Texture2D MakeSoftDotTexture()
    {
        int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x - s / 2f) / (s / 2f);
                float dy = (y - s / 2f) / (s / 2f);
                float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                float a = d * d; // soft falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    static void SetSystem(ParticleSystem ps, bool on)
    {
        if (ps != null) ps.gameObject.SetActive(on);
    }

    void SetLights(bool on)
    {
        foreach (var go in festivalLights)
            if (go != null) go.SetActive(on);
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null && !string.IsNullOrEmpty(msg)) hud.ShowMessage(msg, seconds);
    }
}

using UnityEngine;

/// <summary>Seasons (Phase A — plan §3, Phase 4 design, implemented as a
/// standalone manager now so free-roam can already cycle seasons).
/// Four seasons, one texture set: ApplySeason swaps colours, lighting
/// and fog only — no duplicated city textures (plan §3.1), so seasons
/// cost ~0 MB of extra city/car textures.
///
/// GRIP (plan §3.2): effective grip = gripFactor x weatherGrip x
/// seasonGrip, floored at 0.60. CarController has no season field yet
/// (that one-line edit belongs to the plan's Phase 4, when
/// CarController is touched anyway), so Phase A writes the combined
/// weather x season product into CarController.weatherGripMultiplier
/// in LateUpdate — AFTER WeatherSystem.Update has written the pure
/// weather value for the frame, so the product is what the car feels.
/// The field name is then "combined weather+season grip" until Phase 4
/// adds a dedicated seasonGripMultiplier field; the math is identical.
///
/// LIGHTING: DayNightCycle sets the sun and sky absolutely every
/// frame, so SeasonManager applies its multipliers in LateUpdate
/// (sun intensity/colour, sky tint, fog density) — the base is reset
/// every frame by DayNightCycle/WeatherSystem, so the multiply is
/// stable and never compounds. Fog base density mirrors the table in
/// WeatherSystem.ApplyWeather (Clear 0.0012 / Cloudy 0.002 / Rain
/// 0.008 / Fog 0.02) and must stay in sync with it.
///
/// FLOW (plan §3.3): auto-advance every 5 min of free-roam, or one
/// season per championship round (the championship hook is Phase 5 —
/// call AdvanceSeason() from there). Manual select in Settings stores
/// PlayerPrefs "Season_Current" and pauses auto-advance until the
/// player re-enables it. Transitions crossfade over 3 s.
///
/// SNOW: in winter, WeatherSystem rain IS snow for grip purposes
/// (same 0.75 value, different look) — IsSnow exposes that; the snow
/// particle + white ground overlay needs scene prefabs and is a
/// Phase B/4 item, not faked here.</summary>
public class SeasonManager : MonoBehaviour
{
    public static SeasonManager Instance { get; private set; }

    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>Auto-advance period in free-roam, seconds (plan: 5 min).</summary>
    public const float FreeRoamAdvanceSeconds = 300f;
    /// <summary>Crossfade length for a season change, seconds.</summary>
    public const float TransitionSeconds = 3f;
    /// <summary>Effective grip never drops below this (plan §3.2).</summary>
    public const float MinEffectiveGrip = 0.60f;

    const string PrefsSeason = "Season_Current";
    const string PrefsAutoAdvance = "Season_AutoAdvance";

    public Season Current { get; private set; } = Season.Spring;
    /// <summary>False after a manual select, until re-enabled.</summary>
    public bool AutoAdvanceEnabled { get; private set; } = true;

    /// <summary>Grip multiplier of the current season (1.00 / 1.00 / 0.95 / 0.85).</summary>
    public float GripMultiplier { get { return GripFor(Current); } }

    /// <summary>Weather x season grip, floored at 0.60 — the number
    /// the car actually feels. Weather comes from WeatherSystem.</summary>
    public float EffectiveGripMultiplier
    {
        get
        {
            float weather = WeatherSystem.Instance != null
                ? WeatherSystem.Instance.GripMultiplier : 1f;
            return Mathf.Max(MinEffectiveGrip, weather * GripMultiplier);
        }
    }

    /// <summary>True in winter while the weather is Rain: rain falls
    /// as snow (same grip value, different look — plan §3.2).</summary>
    public bool IsSnow
    {
        get
        {
            return Current == Season.Winter
                && WeatherSystem.Instance != null
                && WeatherSystem.Instance.Current == WeatherSystem.WeatherType.Rain;
        }
    }

    public string SeasonLabel
    {
        get
        {
            switch (Current)
            {
                case Season.Spring: return "Spring - sakura";
                case Season.Summer: return "Summer";
                case Season.Autumn: return "Autumn";
                case Season.Winter: return "Winter - snow";
                default: return "Spring";
            }
        }
    }

    // Blended (crossfading) parameters actually applied this frame.
    float blendedGrip = 1f;
    float blendedSunIntensity = 1f;
    Color blendedSunTint = Color.white;
    Color blendedSkyTint = Color.white;
    float blendedFogMultiplier = 1f;

    Season fromSeason = Season.Spring;
    float transitionTimer = -1f; // < 0 = no transition running
    float freeRoamTimer;
    CarController playerCar;
    Light sun;
    Renderer skyDomeRenderer;
    bool skyDomeSearched;
    float refindTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Current = (Season)Mathf.Clamp(PlayerPrefs.GetInt(PrefsSeason, (int)Season.Spring), 0, 3);
        AutoAdvanceEnabled = PlayerPrefs.GetInt(PrefsAutoAdvance, 1) == 1;
        fromSeason = Current;
        ApplyParams(Current, 1f); // snap: no crossfade on boot
        MapNature.SetSakuraSeason(Current); // sakura canopy colour for the boot season
    }

    void Update()
    {
        // Season transition crossfade.
        if (transitionTimer >= 0f)
        {
            transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(transitionTimer / TransitionSeconds);
            ApplyParams(fromSeason, Current, t);
            if (t >= 1f) transitionTimer = -1f;
        }

        // Auto-advance: only while actually free-roaming (not paused,
        // not game over, Main scene, FreeRoam mode).
        var gm = GameManager.Instance;
        if (AutoAdvanceEnabled && gm != null && !gm.IsPaused && !gm.IsGameOver
            && gm.Mode == GameManager.GameMode.FreeRoam)
        {
            freeRoamTimer += Time.deltaTime;
            if (freeRoamTimer >= FreeRoamAdvanceSeconds)
            {
                freeRoamTimer = 0f;
                AdvanceSeason();
            }
        }
    }

    void LateUpdate()
    {
        ApplyLighting();
        ApplyGrip();
    }

    /// <summary>Manual season select (Settings). Pauses auto-advance
    /// until SetAutoAdvance(true) is called. Persists across restarts.</summary>
    public void SelectSeason(Season season)
    {
        if (season == Current) return;
        fromSeason = Current;
        Current = season;
        transitionTimer = 0f;
        AutoAdvanceEnabled = false;
        PlayerPrefs.SetInt(PrefsSeason, (int)Current);
        PlayerPrefs.SetInt(PrefsAutoAdvance, 0);
        PlayerPrefs.Save();
        Announce();
    }

    /// <summary>Re-enable (or disable) auto-advance after a manual select.</summary>
    public void SetAutoAdvance(bool enabled)
    {
        AutoAdvanceEnabled = enabled;
        freeRoamTimer = 0f;
        PlayerPrefs.SetInt(PrefsAutoAdvance, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Advance one season (free-roam timer, or one
    /// championship round — the championship hook is Phase 5).</summary>
    public void AdvanceSeason()
    {
        Season next = (Season)(((int)Current + 1) % 4);
        fromSeason = Current;
        Current = next;
        transitionTimer = 0f;
        PlayerPrefs.SetInt(PrefsSeason, (int)Current);
        PlayerPrefs.Save();
        Announce();
    }

    void Announce()
    {
        Debug.Log("[SeasonManager] season is now " + Current
            + " (grip " + GripMultiplier.ToString("0.00") + ")");
        MapNature.SetSakuraSeason(Current); // sakura bloom / leaf-out
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Season: " + SeasonLabel, 4f);
    }

    // ---- Season parameter tables (plan §3.1) ----

    public static float GripFor(Season s)
    {
        switch (s)
        {
            case Season.Autumn: return 0.95f;
            case Season.Winter: return 0.85f;
            default: return 1f; // Spring, Summer
        }
    }

    static float SunIntensityFor(Season s)
    {
        switch (s)
        {
            case Season.Spring: return 1.05f; // warm, bright
            case Season.Summer: return 1.15f; // strong sun
            case Season.Autumn: return 0.85f; // dimmer
            case Season.Winter: return 0.75f; // cold, low
            default: return 1f;
        }
    }

    static Color SunTintFor(Season s)
    {
        switch (s)
        {
            case Season.Spring: return new Color(1f, 0.95f, 0.88f); // warm
            case Season.Summer: return new Color(1f, 0.98f, 0.92f); // white-hot
            case Season.Autumn: return new Color(1f, 0.88f, 0.72f); // amber
            case Season.Winter: return new Color(0.82f, 0.88f, 1f); // cold blue
            default: return Color.white;
        }
    }

    static Color SkyTintFor(Season s)
    {
        switch (s)
        {
            case Season.Spring: return new Color(1f, 0.96f, 0.97f); // soft pink haze
            case Season.Summer: return new Color(0.98f, 1f, 1f);
            case Season.Autumn: return new Color(0.96f, 0.94f, 0.90f); // grey-blue
            case Season.Winter: return new Color(0.88f, 0.92f, 1f); // cold
            default: return Color.white;
        }
    }

    /// <summary>Fog density multiplier per season. Winter fog is
    /// denser (x1.5 per plan §3.2); spring has a light haze.</summary>
    static float FogMultiplierFor(Season s)
    {
        switch (s)
        {
            case Season.Spring: return 1.2f;
            case Season.Autumn: return 1.25f;
            case Season.Winter: return 1.5f;
            default: return 1f;
        }
    }

    /// <summary>Base fog density for the current weather — mirrors
    /// WeatherSystem.ApplyWeather. Kept as a table (not read back from
    /// RenderSettings) so the season multiply is idempotent every
    /// frame and never compounds.</summary>
    static float BaseFogDensity()
    {
        if (WeatherSystem.Instance == null) return 0.0012f;
        switch (WeatherSystem.Instance.Current)
        {
            case WeatherSystem.WeatherType.Cloudy: return 0.002f;
            case WeatherSystem.WeatherType.Rain: return 0.008f;
            case WeatherSystem.WeatherType.Fog: return 0.02f;
            default: return 0.0012f;
        }
    }

    void ApplyParams(Season s, float t)
    {
        ApplyParams(s, s, t);
    }

    void ApplyParams(Season from, Season to, float t)
    {
        blendedGrip = Mathf.Lerp(GripFor(from), GripFor(to), t);
        blendedSunIntensity = Mathf.Lerp(SunIntensityFor(from), SunIntensityFor(to), t);
        blendedSunTint = Color.Lerp(SunTintFor(from), SunTintFor(to), t);
        blendedSkyTint = Color.Lerp(SkyTintFor(from), SkyTintFor(to), t);
        blendedFogMultiplier = Mathf.Lerp(FogMultiplierFor(from), FogMultiplierFor(to), t);
    }

    void ApplyLighting()
    {
        refindTimer -= Time.deltaTime;
        if (sun == null && refindTimer <= 0f)
        {
            refindTimer = 1f;
            var cycle = FindFirstObjectByType<DayNightCycle>();
            if (cycle != null && cycle.sun != null) sun = cycle.sun;
            else
            {
                var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light l in lights)
                    if (l.type == LightType.Directional) { sun = l; break; }
            }
        }
        // DayNightCycle sets sun intensity/colour absolutely in Update;
        // multiplying here in LateUpdate is stable (base resets hourly).
        if (sun != null)
        {
            sun.intensity *= blendedSunIntensity;
            sun.color = new Color(
                Mathf.Clamp01(sun.color.r * blendedSunTint.r),
                Mathf.Clamp01(sun.color.g * blendedSunTint.g),
                Mathf.Clamp01(sun.color.b * blendedSunTint.b));
        }

        if (!skyDomeSearched)
        {
            var dome = GameObject.Find("SkyDome");
            if (dome != null) skyDomeRenderer = dome.GetComponent<Renderer>();
            if (Time.timeSinceLevelLoad > 5f) skyDomeSearched = true;
            else if (skyDomeRenderer != null) skyDomeSearched = true;
        }
        if (skyDomeRenderer != null)
        {
            var mat = skyDomeRenderer.material;
            if (mat != null && mat.HasProperty("_BaseColor"))
            {
                Color c = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", new Color(
                    Mathf.Clamp01(c.r * blendedSkyTint.r),
                    Mathf.Clamp01(c.g * blendedSkyTint.g),
                    Mathf.Clamp01(c.b * blendedSkyTint.b)));
            }
        }

        if (RenderSettings.fog)
            RenderSettings.fogDensity = BaseFogDensity() * blendedFogMultiplier;
    }

    void ApplyGrip()
    {
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
            else return;
        }
        float weather = WeatherSystem.Instance != null
            ? WeatherSystem.Instance.GripMultiplier : 1f;
        // Combined product, floored at 0.60 (plan §3.2). Written AFTER
        // WeatherSystem.Update so this frame's value is the product,
        // not the pure weather value. Phase 4 moves this into a
        // dedicated CarController.seasonGripMultiplier field.
        playerCar.weatherGripMultiplier =
            Mathf.Max(MinEffectiveGrip, weather * blendedGrip);
    }
}

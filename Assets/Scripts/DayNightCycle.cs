using UnityEngine;

/// <summary>Day / night cycle: rotates the directional sun around the
/// world over cycleMinutes, dims it at night, and tints its colour from
/// warm noon to orange dusk to blue night. The HUD clock shows the
/// in-game time (06:00 start, a full cycle is one day). Attach to the
/// Directional Light; if no light is assigned it finds one itself.
/// Cheap: one transform rotation + light intensity per frame.</summary>
[RequireComponent(typeof(Light))]
public class DayNightCycle : MonoBehaviour
{
    public float cycleMinutes = 4f;
    public float startHour = 10f;
    public Light sun;

    public float CurrentHour { get; private set; }
    public string ClockText { get { return FormatHour(CurrentHour); } }
    /// <summary>Night is when the sun is at or below the horizon — the
    /// same elevation that drives the light — not an hour threshold, so
    /// "Day" can never be shown under a black sky (round-3 bug #6).</summary>
    public bool IsNight { get { return elevation < 0.02f; } }

    float dayProgress;
    Light[] headlights;
    bool headlightsSearched;
    Renderer skyDomeRenderer;
    bool skyDomeSearched;

    /// <summary>Sine of the sun angle: 1 at noon, 0 at the horizon,
    /// negative after sunset. Every day/night decision reads this, never
    /// the clock, so the label and the light can never disagree
    /// (round-3 bug #6: clock said "06:00 Day" under a black sky).</summary>
    float elevation;

    void Awake()
    {
        if (sun == null) sun = GetComponent<Light>();
        CurrentHour = startHour;
        // Initialise the elevation from the start hour so IsNight is
        // correct before the first Update runs.
        elevation = Mathf.Sin((startHour - 6f) / 12f * 180f * Mathf.Deg2Rad);
    }

    void Update()
    {
        float cycleSeconds = Mathf.Max(30f, cycleMinutes * 60f);
        dayProgress += Time.deltaTime / cycleSeconds;
        if (dayProgress >= 1f) dayProgress -= 1f;
        // startHour -> startHour next day (a full cycle is one day).
        CurrentHour = startHour + dayProgress * 24f;
        if (CurrentHour >= 24f) CurrentHour -= 24f;
        // Sun angle: 06:00 sunrise (east horizon), 12:00 overhead, 18:00 sunset.
        float sunAngle = (CurrentHour - 6f) / 12f * 180f;
        transform.rotation = Quaternion.Euler(sunAngle, -30f, 0f);
        elevation = Mathf.Sin(sunAngle * Mathf.Deg2Rad); // 1 at noon, 0 at horizon
        // Brightness floor: the old formula gave sun intensity ~0.09 and
        // ambient ~0.31 at the 06:00 start, so the car and road were
        // nearly black in the first minute of every run (and at night).
        // The game now starts at 10:00 with the sun high, and even at
        // night the moonlight floor keeps the car visible. The +0.25
        // dawn/dusk lift keeps sunrise/sunset from going pitch black.
        float daylight = Mathf.Clamp01(elevation + 0.25f);
        if (sun != null)
        {
            sun.intensity = elevation > -0.05f
                ? 0.65f + 0.60f * daylight
                : 0.35f; // moonlight floor after sunset
            if (elevation > 0.6f) sun.color = new Color(1f, 0.96f, 0.88f);
            else if (elevation > 0.05f) sun.color = new Color(1f, 0.62f, 0.35f);
            else sun.color = new Color(0.35f, 0.45f, 0.75f);
        }
        RenderSettings.ambientIntensity = 0.55f + 0.45f * daylight;
        UpdateHeadlights(daylight);
        UpdateSkyTint(daylight);
    }

    /// <summary>Tints the sky dome with the same daylight factor that
    /// drives the sun, so the sky colour always matches the clock: blue
    /// by day, dark at night, never a black sky under a "Day" label
    /// (round-3 bug #6). The dome material is Unlit (CityVisuals), so
    /// its _BaseColor is the sky colour.</summary>
    void UpdateSkyTint(float daylight)
    {
        if (!skyDomeSearched)
        {
            var dome = GameObject.Find("SkyDome");
            if (dome != null) skyDomeRenderer = dome.GetComponent<Renderer>();
            if (Time.timeSinceLevelLoad > 5f) skyDomeSearched = true;
            else if (skyDomeRenderer != null) skyDomeSearched = true;
        }
        if (skyDomeRenderer == null) return;
        Material mat = skyDomeRenderer.material;
        if (mat == null) return;
        Color nightSky = new Color(0.10f, 0.12f, 0.22f);
        mat.SetColor("_BaseColor", Color.Lerp(nightSky, Color.white, daylight));
    }

    /// <summary>Headlights (added by CarVisuals on the player car) get
    /// brighter as it gets dark, so the car stays visible at night.</summary>
    void UpdateHeadlights(float daylight)
    {
        if (!headlightsSearched)
        {
            var all = FindObjectsByType<Light>(FindObjectsSortMode.None);
            var list = new System.Collections.Generic.List<Light>();
            foreach (Light l in all)
                if (l.type == LightType.Spot && l.gameObject.name == "HeadlightBeam")
                    list.Add(l);
            if (list.Count > 0) { headlights = list.ToArray(); headlightsSearched = true; }
            else if (Time.timeSinceLevelLoad > 5f) headlightsSearched = true;
        }
        if (headlights == null) return;
        float beam = daylight > 0.7f ? 2.5f : 6f;
        foreach (Light l in headlights) if (l != null) l.intensity = beam;
    }

    public static string FormatHour(float hour)
    {
        int h = Mathf.FloorToInt(hour) % 24;
        int m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
        return string.Format("{0:00}:{1:00}", h, m);
    }
}

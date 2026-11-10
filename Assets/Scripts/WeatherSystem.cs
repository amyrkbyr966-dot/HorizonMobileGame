using UnityEngine;

/// <summary>Dynamic weather: Clear / Cloudy / Rain / Fog. Changes every
/// 90-180 s. Rain and fog cut road grip (CarController.weatherGripMultiplier)
/// and visibility (RenderSettings fog); cloudy only dims the sky a little.
/// Works alongside DayNightCycle: this script never touches the sun, so
/// the two never fight. HUD shows the current weather.</summary>
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public enum WeatherType { Clear, Cloudy, Rain, Fog }
    public WeatherType Current { get; private set; } = WeatherType.Clear;
    public float ChangeIntervalMin = 90f;
    public float ChangeIntervalMax = 180f;

    /// <summary>Road grip multiplier for the current weather.</summary>
    public float GripMultiplier
    {
        get
        {
            switch (Current)
            {
                case WeatherType.Rain: return 0.75f;
                case WeatherType.Fog: return 0.9f;
                case WeatherType.Cloudy: return 0.97f;
                default: return 1f;
            }
        }
    }

    public string WeatherLabel
    {
        get
        {
            switch (Current)
            {
                case WeatherType.Rain: return "Rain - slippery!";
                case WeatherType.Fog: return "Fog - low visibility";
                case WeatherType.Cloudy: return "Cloudy";
                default: return "Clear";
            }
        }
    }

    float timer;
    CarController playerCar;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // DIAGNOSTIC (wet-skid verification): log the raw pref value so
        // a missed hook is diagnosable from Player.log alone.
        string forced = PlayerPrefs.GetString("ForceWeather", "");
        Debug.Log("[WeatherSystem] Awake: ForceWeather pref = '" + forced + "'");
        // TEST HOOK (wet-skid verification, 2026-11-07): PlayerPrefs
        // "ForceWeather" = "Rain" pins the weather to Rain for the whole
        // session. Any other value (or unset) leaves the normal 90-180 s
        // cycle EXACTLY as before — this is not a gameplay change.
        if (forced == "Rain")
        {
            Current = WeatherType.Rain;
            timer = float.MaxValue;
            ApplyWeather();
            Debug.Log("[WeatherSystem] TEST HOOK: weather forced to Rain (ForceWeather pref)");
            return;
        }
        timer = Random.Range(ChangeIntervalMin, ChangeIntervalMax);
        ApplyWeather();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = Random.Range(ChangeIntervalMin, ChangeIntervalMax);
            // Rain and fog are rarer than clear/cloudy.
            int roll = Random.Range(0, 10);
            WeatherType next = roll < 5 ? WeatherType.Clear
                : roll < 7 ? WeatherType.Cloudy
                : roll < 9 ? WeatherType.Rain : WeatherType.Fog;
            if (next != Current)
            {
                Current = next;
                ApplyWeather();
                var hud = FindFirstObjectByType<HUD>();
                if (hud != null) hud.ShowMessage("Weather: " + WeatherLabel, 4f);
            }
        }
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
        }
        if (playerCar != null) playerCar.weatherGripMultiplier = GripMultiplier;
    }

    void ApplyWeather()
    {
        switch (Current)
        {
            case WeatherType.Clear:
                // Light distance haze stays on even when clear (visual
                // rebuild): the city reads far better with a little air.
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.0012f;
                RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
                break;
            case WeatherType.Cloudy:
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.002f;
                RenderSettings.fogColor = new Color(0.7f, 0.72f, 0.75f);
                break;
            case WeatherType.Rain:
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.008f;
                RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.65f);
                break;
            case WeatherType.Fog:
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.02f;
                RenderSettings.fogColor = new Color(0.8f, 0.82f, 0.85f);
                break;
        }
    }
}

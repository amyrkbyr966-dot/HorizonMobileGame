using System.Collections.Generic;
using UnityEngine;

/// <summary>Player settings: graphics quality, sound volume, steering
/// sensitivity, vibration, language (EN / FA). Static, PlayerPrefs-backed,
/// applied with ApplyAll() at boot (GameManager calls it). Menu labels
/// come from GetLabel() so the main menu can switch language.
///
/// Quality tiers (fixed 2026-11-07, TokyoExpansionPlan §5): 4 tiers,
/// 0 = Low, 1 = Medium, 2 = High, 3 = Ultra. Low/Medium render at 720p,
/// High/Ultra at 1080p. Default Medium (720p). Saved in PlayerPrefs
/// ("Settings_Quality") and in the SaveManager blob (v3).</summary>
public static class SettingsManager
{
    public static int Quality
    {
        get { return PlayerPrefs.GetInt("Settings_Quality", 1); }
        set { PlayerPrefs.SetInt("Settings_Quality", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
    }
    public static float SoundVolume
    {
        get { return PlayerPrefs.GetFloat("Settings_Volume", 0.8f); }
        set { PlayerPrefs.SetFloat("Settings_Volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }
    public static float SteeringSensitivity
    {
        get { return PlayerPrefs.GetFloat("Settings_Sensitivity", 1f); }
        set { PlayerPrefs.SetFloat("Settings_Sensitivity", Mathf.Clamp(value, 0.5f, 2f)); PlayerPrefs.Save(); }
    }
    /// <summary>0 = English, 1 = Persian (Farsi).</summary>
    public static int Language
    {
        get { return PlayerPrefs.GetInt("Settings_Language", 0); }
        set { PlayerPrefs.SetInt("Settings_Language", Mathf.Clamp(value, 0, 1)); PlayerPrefs.Save(); }
    }
    public static bool Vibration
    {
        get { return PlayerPrefs.GetInt("Settings_Vibration", 1) == 1; }
        set { PlayerPrefs.SetInt("Settings_Vibration", value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static string QualityName()
    {
        switch (Quality)
        {
            case 0: return "Low 720p";
            case 1: return "Medium 720p";
            case 2: return "High 1080p";
            default: return "Ultra 1080p";
        }
    }

    /// <summary>Unity QualitySettings level for a tier. The project list
    /// is Very Low 0 / Low 1 / Medium 2 / High 3 / Very High 4 / Ultra 5,
    /// so Low maps to level 1, Medium to 2, High to 3 and Ultra to 5
    /// (max shadows, 8x MSAA, 3 pixel lights).</summary>
    public static int TierQualityLevel(int tier)
    {
        switch (Mathf.Clamp(tier, 0, 3))
        {
            case 0: return 1;
            case 1: return 2;
            case 2: return 3;
            default: return 5;
        }
    }

    /// <summary>Render resolution of a tier: 720p for Low/Medium,
    /// 1080p for High/Ultra.</summary>
    public static void TierResolution(int tier, out int width, out int height)
    {
        if (Mathf.Clamp(tier, 0, 3) >= 2) { width = 1920; height = 1080; }
        else { width = 1280; height = 720; }
    }

    public static void CycleQuality()
    {
        Quality = (Quality + 1) % 4;
        ApplyAll();
    }

    public static void ToggleLanguage()
    {
        Language = Language == 0 ? 1 : 0;
    }

    public static void ApplyAll()
    {
        int tier = Quality;
        QualitySettings.SetQualityLevel(TierQualityLevel(tier), true);
        Application.targetFrameRate = tier == 0 ? 30 : 60;
        // Resolution follows the tier: 720p for Low/Medium, 1080p for
        // High/Ultra. Skipped in the editor (no game view in batch mode).
#if !UNITY_EDITOR
        int w, h;
        TierResolution(tier, out w, out h);
        Screen.SetResolution(w, h, FullScreenMode.Windowed);
#endif
        // URP render scale: Low renders at 0.8x to save fill-rate on old
        // phones; the other tiers render at full resolution.
        var urp = QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null) urp.renderScale = tier == 0 ? 0.8f : 1f;
        if (SoundManager.Instance != null) SoundManager.Instance.SetVolume(SoundVolume);
        var controls = MobileControls.Instance;
        if (controls == null) controls = Object.FindFirstObjectByType<MobileControls>();
        if (controls != null) controls.steeringSensitivity = SteeringSensitivity;
    }

    public static void Vibrate()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (Vibration && Application.isMobilePlatform) Handheld.Vibrate();
#endif
    }

    static readonly Dictionary<string, string[]> Labels = new Dictionary<string, string[]>
    {
        { "Race", new string[] { "Race", "مسابقه" } },
        { "FreeRoam", new string[] { "Free Roam", "گشت آزاد" } },
        { "TimeTrial", new string[] { "Time Trial", "تایم تریل" } },
        { "Coins", new string[] { "Coin Hunt", "شکار سکه" } },
        { "StreetRace", new string[] { "Street Race", "مسابقه خیابانی" } },
        { "Delivery", new string[] { "Delivery", "تحویل کالا" } },
        { "Championship", new string[] { "Championship", "قهرمانی" } },
        { "Garage", new string[] { "Garage", "گاراژ" } },
        { "Settings", new string[] { "Settings", "تنظیمات" } },
        { "Quit", new string[] { "Quit", "خروج" } },
        { "Play", new string[] { "Play", "بازی" } },
        { "Money", new string[] { "Money", "پول" } },
    };

    public static string GetLabel(string key)
    {
        string[] pair;
        if (!Labels.TryGetValue(key, out pair)) return key;
        return pair[Language];
    }
}

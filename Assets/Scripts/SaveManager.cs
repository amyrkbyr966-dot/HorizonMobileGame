using UnityEngine;

/// <summary>Whole-profile save / load as one versioned JSON blob in
/// PlayerPrefs ("SaveData_v3"), plus syncing the legacy per-key values the
/// older systems read directly. Call SaveManager.Save() after any big
/// reward, and Load() once at boot (GameManager does both).
/// v3 (2026-11-07): quality tiers widened 0-2 to 0-3 (Low/Medium/High/
/// Ultra). A v2 blob is migrated in place: its quality (0-2) is kept
/// as-is — a stored 2 stays High, it does NOT become Ultra.</summary>
public static class SaveManager
{
    public const int SaveVersion = 3;
    public const string SaveKey = "SaveData_v3";
    public const string LegacySaveKey = "SaveData_v2";

    [System.Serializable]
    public class SaveData
    {
        public int version = SaveVersion;
        public int money;
        public int selectedCar;
        public string unlockedCars = "1";
        public string upgrades = "0,0,0,0";
        public int xp;
        public int reputation;
        public int racesWon;
        public int racesTotal;
        public int totalDrift;
        public float bestTimeTrial;
        public float bestLap;
        public int quality;
        public float volume;
        public float sensitivity;
        public int language;
        public int vibration;
        public string achievements = "";
    }

    public static void Save()
    {
        var d = new SaveData();
        d.money = GameManager.Instance != null ? GameManager.Instance.Money
            : PlayerPrefs.GetInt("Money", 1000);
        d.selectedCar = CarSelection.SelectedIndex;
        var unlocked = new System.Text.StringBuilder();
        for (int i = 0; i < CarSelection.Cars.Length; i++)
        {
            if (i > 0) unlocked.Append(",");
            unlocked.Append(CarSelection.IsUnlocked(i) ? "1" : "0");
        }
        d.unlockedCars = unlocked.ToString();
        d.upgrades = UpgradeSystem.LevelsCsv();
        d.xp = PlayerProfile.XP;
        d.reputation = PlayerProfile.Reputation;
        d.racesWon = PlayerProfile.RacesWon;
        d.racesTotal = PlayerProfile.RacesTotal;
        d.totalDrift = PlayerProfile.DriftTotal;
        d.bestTimeTrial = PlayerPrefs.GetFloat("BestTimeTrial", 0f);
        d.bestLap = PlayerPrefs.GetFloat("BestLap", 0f);
        d.quality = SettingsManager.Quality;
        d.volume = SettingsManager.SoundVolume;
        d.sensitivity = SettingsManager.SteeringSensitivity;
        d.language = SettingsManager.Language;
        d.vibration = SettingsManager.Vibration ? 1 : 0;
        d.achievements = Achievements.UnlockedCsv();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    /// <summary>Load the JSON blob back into the legacy keys. Returns false
    /// when there is no save yet (first run). A v2 blob is migrated to v3
    /// in place (quality kept as-is, 0-2 stays 0-2).</summary>
    public static bool Load()
    {
        string key = SaveKey;
        bool migrating = false;
        if (!PlayerPrefs.HasKey(key))
        {
            if (!PlayerPrefs.HasKey(LegacySaveKey)) return false;
            key = LegacySaveKey;
            migrating = true;
        }
        SaveData d;
        try { d = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(key)); }
        catch (System.Exception) { return false; }
        if (d == null || d.version < 1) return false;
        PlayerPrefs.SetInt("Money", d.money);
        // Selected car (2026-11-10): the menu writes PlayerPrefs
        // "SelectedCar" synchronously on every select/buy, but this
        // blob is only rewritten on Save() (rewards). A selection made
        // after the last Save() is NEWER than the blob — restoring the
        // blob's selectedCar over it loses the player's pick (seen on
        // disk: legacy "SelectedCar"=38, blob selectedCar=5, so every
        // boot reset the car to 5). Keep the legacy key when it exists
        // and heal the blob's copy; restore from the blob only when
        // there is no legacy key yet (first run / fresh prefs).
        // (Do NOT call Save() here to heal: GameManager.Money is not
        // loaded yet at this point, so a full Save would write 0.)
        if (PlayerPrefs.HasKey("SelectedCar"))
        {
            d.selectedCar = CarSelection.SelectedIndex;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
        }
        else
        {
            CarSelection.SelectedIndex = Mathf.Clamp(d.selectedCar, 0, CarSelection.Cars.Length - 1);
        }
        if (!string.IsNullOrEmpty(d.unlockedCars))
        {
            string[] parts = d.unlockedCars.Split(',');
            for (int i = 0; i < parts.Length && i < CarSelection.Cars.Length; i++)
                PlayerPrefs.SetInt("CarUnlocked_" + i, parts[i] == "1" ? 1 : 0);
        }
        UpgradeSystem.ApplyLevelsCsv(d.upgrades);
        PlayerPrefs.SetInt("Profile_XP", d.xp);
        PlayerPrefs.SetInt("Profile_Reputation", d.reputation);
        PlayerPrefs.SetInt("Profile_RacesWon", d.racesWon);
        PlayerPrefs.SetInt("Profile_RacesTotal", d.racesTotal);
        PlayerPrefs.SetInt("Profile_DriftTotal", d.totalDrift);
        PlayerPrefs.SetFloat("BestTimeTrial", d.bestTimeTrial);
        PlayerPrefs.SetFloat("BestLap", d.bestLap);
        SettingsManager.Quality = Mathf.Clamp(d.quality, 0, 3);
        SettingsManager.SoundVolume = Mathf.Clamp01(d.volume);
        SettingsManager.SteeringSensitivity = Mathf.Clamp(d.sensitivity, 0.5f, 2f);
        SettingsManager.Language = Mathf.Clamp(d.language, 0, 1);
        SettingsManager.Vibration = d.vibration == 1;
        Achievements.ApplyUnlockedCsv(d.achievements);
        PlayerPrefs.Save();
        if (migrating)
        {
            // Migrate the blob itself to v3, losslessly: same data, new
            // version, old key removed. A stored quality 2 stays High.
            d.version = SaveVersion;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(d));
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }
        return true;
    }

    public static void ResetSave()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
    }
}

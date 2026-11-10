using UnityEngine;

/// <summary>11 achievements with a HUD toast on unlock. Checked once a
/// second; unlocked ids are saved in PlayerPrefs ("Ach_<id>").</summary>
public class Achievements : MonoBehaviour
{
    public static Achievements Instance { get; private set; }

    [System.Serializable]
    public class AchievementDef
    {
        public string id;
        public string title;
        public string description;
        public AchievementDef(string id, string title, string description)
        {
            this.id = id; this.title = title; this.description = description;
        }
    }

    public static readonly AchievementDef[] Defs = new AchievementDef[]
    {
        new AchievementDef("first_win", "First Win", "Win your first race"),
        new AchievementDef("speed_200", "200 Club", "Reach 200 km/h"),
        new AchievementDef("speed_250", "250 Club", "Reach 250 km/h"),
        new AchievementDef("drift_10000", "Drift King", "Bank 10,000 drift points"),
        new AchievementDef("coins_100", "Coin Collector", "Collect 100 coins"),
        new AchievementDef("rich_10000", "Rich Racer", "Hold $10,000"),
        new AchievementDef("wins_5", "Five-Time Winner", "Win 5 races"),
        new AchievementDef("champion", "Champion", "Win a championship season"),
        new AchievementDef("distance_50", "Road Tripper", "Drive 50 km in total"),
        new AchievementDef("night_owl", "Night Owl", "Drive at night for 60 seconds"),
        new AchievementDef("collector", "Completionist", "Collect every coin in one hunt"),
    };

    float checkTimer;
    float nightTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 1f;
        var dayNight = FindFirstObjectByType<DayNightCycle>();
        if (dayNight != null && dayNight.IsNight) nightTimer += 1f;
        if (PlayerProfile.RacesWon >= 1) Unlock("first_win");
        if (PlayerProfile.TopSpeedKmh >= 200f) Unlock("speed_200");
        if (PlayerProfile.TopSpeedKmh >= 250f) Unlock("speed_250");
        if (PlayerProfile.DriftTotal >= 10000) Unlock("drift_10000");
        if (PlayerProfile.CoinsCollected >= 100) Unlock("coins_100");
        if (GameManager.Instance != null && GameManager.Instance.Money >= 10000) Unlock("rich_10000");
        if (PlayerProfile.RacesWon >= 5) Unlock("wins_5");
        if (Championship.Instance != null && Championship.Instance.PlayerIsChampion) Unlock("champion");
        if (PlayerProfile.DistanceKm >= 50f) Unlock("distance_50");
        if (nightTimer >= 60f) Unlock("night_owl");
        var coins = FindFirstObjectByType<CollectibleManager>();
        if (coins != null && coins.HuntFinished) Unlock("collector");
    }

    public static bool IsUnlocked(string id)
    {
        return PlayerPrefs.GetInt("Ach_" + id, 0) == 1;
    }

    public static int UnlockedCount()
    {
        int n = 0;
        foreach (var d in Defs) if (IsUnlocked(d.id)) n++;
        return n;
    }

    public static string UnlockedCsv()
    {
        var parts = new System.Collections.Generic.List<string>();
        foreach (var d in Defs) if (IsUnlocked(d.id)) parts.Add(d.id);
        return string.Join(",", parts.ToArray());
    }

    public static void ApplyUnlockedCsv(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return;
        foreach (string id in csv.Split(','))
            if (!string.IsNullOrEmpty(id)) PlayerPrefs.SetInt("Ach_" + id, 1);
        PlayerPrefs.Save();
    }

    void Unlock(string id)
    {
        if (IsUnlocked(id)) return;
        PlayerPrefs.SetInt("Ach_" + id, 1);
        PlayerPrefs.Save();
        string title = id;
        foreach (var d in Defs) if (d.id == id) { title = d.title; break; }
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowAchievement("Achievement: " + title);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCoin();
        PlayerProfile.AddXP(100);
    }
}

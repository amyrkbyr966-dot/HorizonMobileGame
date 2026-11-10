using UnityEngine;

/// <summary>Career profile: XP / level / reputation and lifetime driving
/// stats. Static, PlayerPrefs-backed, so any system can read or update it
/// without wiring. Level = XP / 1000 + 1. Saved immediately on change.</summary>
public static class PlayerProfile
{
    public static int XP { get { return PlayerPrefs.GetInt("Profile_XP", 0); } }
    public static int Level { get { return XP / 1000 + 1; } }
    public static int Reputation { get { return PlayerPrefs.GetInt("Profile_Reputation", 0); } }
    public static int RacesWon { get { return PlayerPrefs.GetInt("Profile_RacesWon", 0); } }
    public static int RacesTotal { get { return PlayerPrefs.GetInt("Profile_RacesTotal", 0); } }
    public static float TopSpeedKmh { get { return PlayerPrefs.GetFloat("Profile_TopSpeed", 0f); } }
    public static float DistanceKm { get { return PlayerPrefs.GetFloat("Profile_DistanceKm", 0f); } }
    public static int DriftTotal { get { return PlayerPrefs.GetInt("Profile_DriftTotal", 0); } }
    public static int CoinsCollected { get { return PlayerPrefs.GetInt("Profile_Coins", 0); } }

    /// <summary>Add XP. Returns true when the player levelled up.</summary>
    public static bool AddXP(int amount)
    {
        if (amount <= 0) return false;
        int before = Level;
        PlayerPrefs.SetInt("Profile_XP", XP + amount);
        PlayerPrefs.SetInt("Profile_Reputation", Reputation + amount / 10);
        PlayerPrefs.Save();
        return Level > before;
    }

    public static void RecordRace(bool won, int xpReward)
    {
        PlayerPrefs.SetInt("Profile_RacesTotal", RacesTotal + 1);
        if (won) PlayerPrefs.SetInt("Profile_RacesWon", RacesWon + 1);
        PlayerPrefs.Save();
        if (xpReward > 0) AddXP(xpReward);
    }

    public static void RecordTopSpeed(float kmh)
    {
        if (kmh > TopSpeedKmh)
        {
            PlayerPrefs.SetFloat("Profile_TopSpeed", kmh);
            PlayerPrefs.Save();
        }
    }

    public static void RecordDistance(float km)
    {
        if (km <= 0f) return;
        PlayerPrefs.SetFloat("Profile_DistanceKm", DistanceKm + km);
        PlayerPrefs.Save();
    }

    public static void RecordDrift(int points)
    {
        if (points <= 0) return;
        PlayerPrefs.SetInt("Profile_DriftTotal", DriftTotal + points);
        PlayerPrefs.Save();
    }

    public static void RecordCoin()
    {
        PlayerPrefs.SetInt("Profile_Coins", CoinsCollected + 1);
        PlayerPrefs.Save();
    }

    public static string Summary()
    {
        return "Lv " + Level + "  XP " + XP + "  Rep " + Reputation
            + "  Wins " + RacesWon + "/" + RacesTotal
            + "  Top " + Mathf.RoundToInt(TopSpeedKmh) + " km/h"
            + "  " + DistanceKm.ToString("0.0") + " km";
    }
}

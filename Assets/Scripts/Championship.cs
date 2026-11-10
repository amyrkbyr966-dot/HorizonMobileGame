using UnityEngine;

/// <summary>Season championship: 5 rounds, points per finish
/// (25/18/15/12/10/8/6/4/2/1), standings vs 3 named AI rivals, season
/// reward. Attach to a GameObject in the Main scene; it finds the
/// RaceManager itself. Progress is saved in PlayerPrefs.</summary>
public class Championship : MonoBehaviour
{
    public static Championship Instance { get; private set; }

    public static readonly string[] RoundNames = new string[]
    {
        "City Sprint", "Downtown GP", "Highway Rush", "Desert Dash", "Horizon Finale"
    };
    public static readonly string[] RivalNames = new string[] { "Vex", "Mara", "Kian" };
    public static readonly int[] PointsTable = new int[] { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };

    public int CurrentRound { get; private set; }
    public int PlayerPoints { get; private set; }
    public int[] RivalPoints { get; private set; }
    public bool SeasonFinished { get; private set; }
    public bool PlayerIsChampion { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        RivalPoints = new int[RivalNames.Length];
        CurrentRound = PlayerPrefs.GetInt("Champ_Round", 0);
        PlayerPoints = PlayerPrefs.GetInt("Champ_PlayerPts", 0);
        for (int i = 0; i < RivalNames.Length; i++)
            RivalPoints[i] = PlayerPrefs.GetInt("Champ_RivalPts_" + i, 0);
        SeasonFinished = PlayerPrefs.GetInt("Champ_Finished", 0) == 1;
    }

    void Save()
    {
        PlayerPrefs.SetInt("Champ_Round", CurrentRound);
        PlayerPrefs.SetInt("Champ_PlayerPts", PlayerPoints);
        for (int i = 0; i < RivalNames.Length; i++)
            PlayerPrefs.SetInt("Champ_RivalPts_" + i, RivalPoints[i]);
        PlayerPrefs.SetInt("Champ_Finished", SeasonFinished ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Call when a championship race ends. playerPosition is 1-based
    /// (1 = won). Rivals take the remaining points in a shuffled order.</summary>
    public void RecordRaceFinish(int playerPosition)
    {
        if (SeasonFinished) return;
        int pos = Mathf.Clamp(playerPosition, 1, PointsTable.Length);
        PlayerPoints += PointsTable[pos - 1];
        // Rivals fill positions 2..4 in random order.
        int[] taken = new int[] { pos };
        for (int r = 0; r < RivalNames.Length; r++)
        {
            int rp = 2 + r;
            while (System.Array.IndexOf(taken, rp) >= 0) rp++;
            if (rp > PointsTable.Length) rp = PointsTable.Length;
            System.Array.Resize(ref taken, taken.Length + 1);
            taken[taken.Length - 1] = rp;
            RivalPoints[r] += PointsTable[rp - 1];
        }
        CurrentRound++;
        bool wonRace = playerPosition == 1;
        PlayerProfile.RecordRace(wonRace, wonRace ? 300 : 100);
        if (CurrentRound >= RoundNames.Length)
        {
            SeasonFinished = true;
            PlayerIsChampion = true;
            for (int i = 0; i < RivalNames.Length; i++)
                if (RivalPoints[i] > PlayerPoints) PlayerIsChampion = false;
            int reward = 5000 + (PlayerIsChampion ? 3000 : 0);
            if (GameManager.Instance != null) GameManager.Instance.AddMoney(reward);
            PlayerProfile.AddXP(PlayerIsChampion ? 1000 : 400);
            var hud = FindFirstObjectByType<HUD>();
            if (hud != null)
                hud.ShowMessage(PlayerIsChampion
                    ? "CHAMPION! Season won  +$" + reward
                    : "Season over  +$" + reward, 5f);
            SaveManager.Save();
        }
        Save();
    }

    public string CurrentRoundName()
    {
        if (SeasonFinished) return "Season complete";
        return "Round " + (CurrentRound + 1) + "/5: " + RoundNames[CurrentRound];
    }

    public string StandingsText()
    {
        string s = "YOU " + PlayerPoints + " pts";
        for (int i = 0; i < RivalNames.Length; i++)
            s += "  |  " + RivalNames[i] + " " + RivalPoints[i] + " pts";
        return s;
    }

    public void ResetSeason()
    {
        CurrentRound = 0;
        PlayerPoints = 0;
        for (int i = 0; i < RivalNames.Length; i++) RivalPoints[i] = 0;
        SeasonFinished = false;
        PlayerIsChampion = false;
        Save();
    }
}

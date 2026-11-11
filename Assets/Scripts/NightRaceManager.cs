using UnityEngine;

/// <summary>DYNAMIC NIGHT STREET RACES — while free-roaming at night,
/// rival drivers appear on the street and challenge you. No race is
/// scheduled: offers spawn dynamically (every 60-120 s, only while
/// you are actually driving, > 40 km/h), a magenta beacon light marks
/// the waiting rival, and you have 15 s to accept (E key, or the
/// public AcceptOffer() for the touch UI) or decline.
/// Accepting starts a street race ONLY through the existing public
/// entry point GameManager.StartStreetRace() — RaceManager.cs is
/// never edited. The rival is the roster car closest in top speed to
/// your car, so the race is always fair. Finishing the race pays
/// $1500 + 300 XP (polled from the public RaceManager.RaceFinished
/// after the scene reload, keyed by the PlayerPrefs source flag so
/// the car-meet manager never double-pays).</summary>
public class NightRaceManager : MonoBehaviour
{
    public static NightRaceManager Instance { get; private set; }

    // Economy rebalance (2026-11-11, semi-final): $1,200 -> $1,500.
    public const int NightRaceRewardMoney = 1500;
    public const int NightRaceRewardXP = 300;
    public const float OfferWindowSeconds = 15f;

    const string PrefsSource = "StreetRace_Source";
    const string PrefsRival = "NightRace_RivalIndex";

    /// <summary>True while an offer is waiting for accept/decline.</summary>
    public bool OfferActive { get; private set; }
    /// <summary>Roster index of the rival in the current offer.</summary>
    public int OfferRivalIndex { get; private set; } = -1;
    /// <summary>Seconds left to answer the current offer.</summary>
    public float OfferTimeLeft { get; private set; }

    float nextOfferIn = 45f;
    float cooldown;
    bool rewardPending;
    GameObject beacon;
    HUD hud;
    CarController playerCar;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        rewardPending = PlayerPrefs.GetString(PrefsSource, "") == "night";
    }

    void Update()
    {
        // Reward for a night race that just finished (scene reloaded
        // by StartStreetRace; the source flag survives in PlayerPrefs).
        if (rewardPending)
        {
            var rm = FindFirstObjectByType<RaceManager>();
            if (rm != null && rm.RaceFinished) PayReward();
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;
        if (gm.Mode != GameManager.GameMode.FreeRoam) { ClearOffer(); return; }
        var cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle == null || !cycle.IsNight) { ClearOffer(); return; }

        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
            else return;
        }

        if (OfferActive)
        {
            OfferTimeLeft -= Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.E)) AcceptOffer();
            else if (Input.GetKeyDown(KeyCode.N) || OfferTimeLeft <= 0f) DeclineOffer();
            return;
        }

        cooldown -= Time.deltaTime;
        if (cooldown > 0f) return;
        nextOfferIn -= Time.deltaTime;
        if (nextOfferIn <= 0f && playerCar.GetSpeedKmh() > 40f)
            SpawnOffer();
    }

    void SpawnOffer()
    {
        nextOfferIn = Random.Range(60f, 120f);
        OfferRivalIndex = PickFairRival();
        OfferActive = true;
        OfferTimeLeft = OfferWindowSeconds;
        var def = CarSelection.Cars[OfferRivalIndex];

        // Magenta beacon light where the rival waits (40 m ahead).
        if (beacon == null)
        {
            beacon = new GameObject("NightRaceBeacon");
            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Color.magenta;
            light.intensity = 25f;
            light.range = 30f;
        }
        beacon.transform.position = playerCar.transform.position
            + playerCar.transform.forward * 40f + Vector3.up * 3f;
        beacon.SetActive(true);

        ShowMessage("Night race! " + def.carName + " (" + Mathf.RoundToInt(def.topSpeedKmh)
            + " km/h) wants to run.  [E] Accept  [N] Decline  (" + Mathf.RoundToInt(OfferTimeLeft) + "s)", 6f);
        Debug.Log("[NightRace] offer: " + def.carName + " (index " + OfferRivalIndex + ")");
    }

    /// <summary>Rival with top speed closest to the player's car
    /// (fair race), never the player's own car.</summary>
    int PickFairRival()
    {
        int mine = CarSelection.SelectedIndex;
        float myTop = CarSelection.Cars[mine].topSpeedKmh;
        int best = -1;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < CarSelection.Cars.Length; i++)
        {
            if (i == mine) continue;
            float d = Mathf.Abs(CarSelection.Cars[i].topSpeedKmh - myTop);
            if (d < bestDiff) { bestDiff = d; best = i; }
        }
        return best >= 0 ? best : (mine + 1) % CarSelection.Cars.Length;
    }

    /// <summary>Accept the waiting offer and start the street race.</summary>
    public void AcceptOffer()
    {
        if (!OfferActive) return;
        int rival = OfferRivalIndex;
        var def = CarSelection.Cars[rival];
        ClearOffer();
        Debug.Log("[NightRace] accepted vs " + def.carName);
        ShowMessage("Night race vs " + def.carName + " - good luck!", 4f);
        PlayerPrefs.SetString(PrefsSource, "night");
        PlayerPrefs.SetInt(PrefsRival, rival);
        PlayerPrefs.Save();
        rewardPending = true;
        var gm = GameManager.Instance;
        if (gm != null) gm.StartStreetRace();
    }

    /// <summary>Decline the waiting offer (90 s before the next).</summary>
    public void DeclineOffer()
    {
        if (!OfferActive) return;
        ClearOffer();
        cooldown = 90f;
        ShowMessage("Night race declined - the rival drives off", 3f);
    }

    void ClearOffer()
    {
        OfferActive = false;
        OfferRivalIndex = -1;
        if (beacon != null) beacon.SetActive(false);
    }

    void PayReward()
    {
        rewardPending = false;
        PlayerPrefs.DeleteKey(PrefsSource);
        PlayerPrefs.DeleteKey(PrefsRival);
        PlayerPrefs.Save();
        var gm = GameManager.Instance;
        if (gm != null) gm.AddMoney(NightRaceRewardMoney);
        PlayerProfile.AddXP(NightRaceRewardXP);
        PlayerProfile.RecordRace(true, NightRaceRewardXP);
        ShowMessage("Night race WON!  +$" + NightRaceRewardMoney + "  +" + NightRaceRewardXP + " XP", 6f);
        Debug.Log("[NightRace] reward paid");
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

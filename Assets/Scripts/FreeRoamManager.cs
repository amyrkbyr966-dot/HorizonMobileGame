using System.Collections.Generic;
using UnityEngine;

/// <summary>Free-roam mode (Phase A): the player spawns and drives
/// freely with NO race running — no race timer, no checkpoints, no
/// laps. The core of the game per the 2026-11-07 user direction:
/// free-roam driving and exploring Tokyo's 7 districts, with
/// discovery (landmarks, gas station, garage, coins, viewpoints),
/// traffic, day/night and weather. Races are ONE mode among others.
///
/// Works with the existing Main scene and CarController as-is:
/// - Active only when GameManager.Mode == FreeRoam (entered via
///   GameManager.StartFreeRoam). In any other mode it stays dormant
///   and never touches the race — RaceManager / Checkpoint / the
///   race-fix work are completely untouched by this script.
/// - The player car, traffic, day/night and weather already run in
///   the Main scene; this manager only adds discovery + district
///   awareness on top of them.
///
/// HUD NOTE: HUD.cs drives lapText/timeText from RaceManager and
/// modeText from GameManager.Mode every frame, and this phase must
/// not edit HUD.cs. So the district name + discovered-count line is
/// exposed here as public properties (CurrentDistrictName,
/// DiscoveredCount, TotalPlaces) for the Phase B HUD edit to read,
/// and discoveries are announced immediately with HUD.ShowMessage
/// toasts, which need no HUD change. The HUD line itself is a
/// Phase B item — see TokyoExpansionPlan.md §Phase A.
///
/// Discovery persists in PlayerPrefs "FreeRoam_Discovered" as a
/// comma-separated list of place ids (DistrictData.Places).</summary>
public class FreeRoamManager : MonoBehaviour
{
    public static FreeRoamManager Instance { get; private set; }

    const string PrefsDiscovered = "FreeRoam_Discovered";

    /// <summary>True while free-roam is the active mode.</summary>
    public bool IsActive { get; private set; }
    /// <summary>District the player is driving in right now.</summary>
    public string CurrentDistrictName { get; private set; } = "Shibuya";
    /// <summary>Places discovered (this save), across all districts.</summary>
    public int DiscoveredCount { get; private set; }
    /// <summary>Total discoverable places in the game.</summary>
    public int TotalPlaces { get { return DistrictData.TotalPlaces; } }
    /// <summary>Coins collected in this session (from CollectibleManager).</summary>
    public int CoinsCollected { get; private set; }
    /// <summary>Distance driven in free-roam this session, km.</summary>
    public float DistanceDrivenKm { get; private set; }
    /// <summary>Time spent in free-roam this session, seconds.</summary>
    public float FreeRoamTime { get; private set; }

    readonly HashSet<string> discovered = new HashSet<string>();
    readonly HashSet<string> announcedDistricts = new HashSet<string>();

    Transform player;
    Vector3 lastPos;
    bool hasLastPos;
    float checkTimer;
    HUD hud;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadDiscovered();
    }

    void Start()
    {
        hud = FindFirstObjectByType<HUD>();
        RefreshActive();
        if (IsActive)
            ShowMessage("Free Roam - explore Tokyo, discover places", 5f);
    }

    void Update()
    {
        RefreshActive();
        if (!IsActive) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;

        FreeRoamTime += Time.deltaTime;

        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                lastPos = player.position;
                hasLastPos = true;
            }
            else return;
        }

        // Distance driven (same 200 m/step sanity cap as GameManager).
        if (hasLastPos)
        {
            float moved = Vector3.Distance(player.position, lastPos);
            if (moved > 0f && moved < 200f) DistanceDrivenKm += moved / 1000f;
        }
        lastPos = player.position;
        hasLastPos = true;

        // District + discovery checks, twice a second (cheap).
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.5f;

        UpdateDistrict();
        CheckDiscovery();
        var cm = CollectibleManager.Instance;
        if (cm != null) CoinsCollected = cm.CollectedCount;
    }

    /// <summary>Re-reads the mode every frame so entering / leaving
    /// free-roam (menu, restart, mode switch) is picked up without a
    /// scene reload. Dormant in race modes: IsActive false, no work.</summary>
    void RefreshActive()
    {
        var gm = GameManager.Instance;
        bool active = gm != null && gm.Mode == GameManager.GameMode.FreeRoam;
        if (active && !IsActive)
        {
            IsActive = true;
            announcedDistricts.Clear();
            ShowMessage("Free Roam - explore Tokyo, discover places", 5f);
        }
        else if (!active && IsActive)
        {
            IsActive = false;
        }
    }

    void UpdateDistrict()
    {
        if (player == null) return;
        string name;
        if (DistrictStreaming.Instance != null)
            name = DistrictStreaming.Instance.CurrentDistrict.displayName;
        else
            name = DistrictData.DistrictAt(player.position).displayName;

        if (name != CurrentDistrictName)
        {
            CurrentDistrictName = name;
            if (!announcedDistricts.Contains(name))
            {
                announcedDistricts.Add(name);
                var info = DistrictData.DistrictAt(player.position);
                ShowMessage(name + " - " + info.character, 5f);
            }
        }
        else CurrentDistrictName = name;
    }

    void CheckDiscovery()
    {
        if (player == null) return;
        Vector3 pos = player.position;
        foreach (DistrictData.DiscoverablePlace place in DistrictData.Places)
        {
            if (discovered.Contains(place.id)) continue;
            Vector3 flat = new Vector3(pos.x, 0f, pos.z);
            Vector3 placeFlat = new Vector3(place.position.x, 0f, place.position.z);
            if (Vector3.Distance(flat, placeFlat) <= place.discoverRadius)
                Discover(place);
        }
    }

    void Discover(DistrictData.DiscoverablePlace place)
    {
        discovered.Add(place.id);
        DiscoveredCount = discovered.Count;
        SaveDiscovered();
        string label = place.kind == DistrictData.PlaceKind.GasStation
            || place.kind == DistrictData.PlaceKind.Garage
            ? place.name + " discovered"
            : "Discovered: " + place.name;
        Debug.Log("[FreeRoam] " + label + " (" + DiscoveredCount + "/" + TotalPlaces + ")");
        ShowMessage(label + "  (" + DiscoveredCount + "/" + TotalPlaces + ")", 4f);
    }

    /// <summary>True when a place was already discovered (saved).</summary>
    public bool IsDiscovered(string placeId)
    {
        return discovered.Contains(placeId);
    }

    /// <summary>One-line HUD status for the Phase B HUD edit to show:
    /// "Shibuya  -  Discovered 3/16  -  Coins 5".</summary>
    public string StatusLine()
    {
        return CurrentDistrictName + "  -  Discovered " + DiscoveredCount
            + "/" + TotalPlaces + "  -  Coins " + CoinsCollected;
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }

    void LoadDiscovered()
    {
        discovered.Clear();
        string saved = PlayerPrefs.GetString(PrefsDiscovered, "");
        if (string.IsNullOrEmpty(saved)) { DiscoveredCount = 0; return; }
        foreach (string id in saved.Split(','))
        {
            string clean = id.Trim();
            if (!string.IsNullOrEmpty(clean)) discovered.Add(clean);
        }
        DiscoveredCount = discovered.Count;
    }

    void SaveDiscovered()
    {
        PlayerPrefs.SetString(PrefsDiscovered, string.Join(",", discovered));
        PlayerPrefs.Save();
    }
}

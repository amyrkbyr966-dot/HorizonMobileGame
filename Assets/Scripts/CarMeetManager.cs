using System.Collections.Generic;
using UnityEngine;

/// <summary>DAIKOKU-FUTO-STYLE CAR MEET — a night parking-area meet at
/// Tokyo Bay where tuned cars gather under neon light. The player
/// drives in at night, walks the rows of parked cars from the 55-car
/// roster, and can challenge a driver to a street race.
///
/// LOCATION: Tokyo Bay district (DistrictData centre 0, -2160) —
/// meet plaza at (120, -2260), inside the district, next to the Bay
/// Bridge viewpoint. NIGHT ONLY: the meet only exists while
/// DayNightCycle.IsNight (the real Daikoku meet is a night meet).
/// PARKED CARS: 12 slots, roster cars built with the existing
/// CarVisuals.Rebuild (public API) in each car's real roster paint;
/// when the model-integration agent lands a real Kenney car-kit
/// prefab for a roster car at Resources/Models/Cars/<name>, that
/// prefab is used instead. Neon = real PointLights (underglow per
/// car + plaza flood lights); no placeholder geometry is created.
/// CHALLENGE: drive within 12 m of a parked car and press E (or
/// call ChallengeDriver(slot)) — the driver answers with a line and
/// the game starts a street race through GameManager.StartStreetRace
/// (public entry point; RaceManager itself is untouched). Win the
/// race and the meet pays $1500 + 400 XP (polled from the public
/// RaceManager.RaceFinished after the scene reload, keyed by the
/// PlayerPrefs source flag so the night-race manager never
/// double-pays).</summary>
public class CarMeetManager : MonoBehaviour
{
    public static CarMeetManager Instance { get; private set; }

    /// <summary>Meet plaza centre (Tokyo Bay district).</summary>
    public static readonly Vector3 MeetCentre = new Vector3(120f, 0f, -2260f);
    public const float MeetRadius = 120f;
    public const float ChallengeRadius = 12f;
    public const int ChallengeRewardMoney = 1500;
    public const int ChallengeRewardXP = 400;

    const string PrefsSource = "StreetRace_Source";
    const string PrefsRival = "Meet_RivalIndex";

    /// <summary>Roster indices parked at the meet (tuned cars only).
    /// Indices 0-7 never change; these are all fan favourites.</summary>
    public static readonly int[] ParkedRoster = new int[]
    {
        38, // MSAIN R34 GT
        39, // Toyata Supra
        41, // Mazdza RX-7
        43, // Lexuss LFA
        33, // Bpcati Chiron
        27, // Ferrarri LaFerrarri
        20, // Forde Mustang GT
        14, // Porscha 911 Carrera
        29, // Lamborghinni Countach
        17, // McLarren P2
        49, // Bpcati Tourbillon
        24, // Teslo Model S Plaid
    };

    /// <summary>What each driver says when you walk up.</summary>
    public static readonly string[] DriverLines = new string[]
    {
        "R34: 'Godzilla is hungry. You sure about this?'",
        "Supra: '2JZ, full boost. Let's see what you've got.'",
        "RX-7: 'Rotary screams at 9,000 rpm. Keep up if you can.'",
        "LFA: 'Hear that V10? That's the sound of losing.'",
        "Chiron: '1,500 horses. Don't embarrass yourself.'",
        "LaFerrarri: 'A V12 hybrid. This won't take long.'",
        "Mustang: 'American V8, straight pipes. Bring it.'",
        "911: 'Rear-engine, perfect balance. Your move.'",
        "Countach: 'A legend doesn't lose to a stranger.'",
        "P2: 'McLarren's fastest. Good luck catching me.'",
        "Tourbillon: 'The fastest car here. Prove me wrong.'",
        "Model S: 'No gears. No mercy. Just instant torque.'",
    };

    /// <summary>True while the meet is live (night + free-roam).</summary>
    public bool IsMeetLive { get; private set; }
    /// <summary>Slot index of the nearest parked car (-1 = none near).</summary>
    public int NearestSlot { get; private set; } = -1;

    GameObject meetRoot;
    readonly List<GameObject> parkedCars = new List<GameObject>();
    bool rewardPending;
    HUD hud;
    float checkTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        rewardPending = PlayerPrefs.GetString(PrefsSource, "") == "meet";
    }

    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.5f;

        // Challenge reward: a street race started from the meet just
        // finished (RaceManager is public-read; never edited here).
        if (rewardPending)
        {
            var rm = FindFirstObjectByType<RaceManager>();
            if (rm != null && rm.RaceFinished) PayChallengeReward();
            return;
        }

        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;
        var cycle = FindFirstObjectByType<DayNightCycle>();
        bool night = cycle != null && cycle.IsNight;
        bool freeRoam = gm.Mode == GameManager.GameMode.FreeRoam;
        bool live = night && freeRoam;

        if (live && !IsMeetLive) OpenMeet();
        else if (!live && IsMeetLive) CloseMeet();
        if (!IsMeetLive) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        float distToMeet = Vector3.Distance(
            new Vector3(player.transform.position.x, 0f, player.transform.position.z),
            new Vector3(MeetCentre.x, 0f, MeetCentre.z));
        if (distToMeet > MeetRadius + 300f)
        {
            if (parkedCars.Count > 0) DespawnParkedCars();
            NearestSlot = -1;
            return;
        }
        if (parkedCars.Count == 0) SpawnParkedCars();
        UpdateNearestSlot(player.transform.position);
        if (NearestSlot >= 0 && Input.GetKeyDown(KeyCode.E))
            ChallengeDriver(NearestSlot);
    }

    void OpenMeet()
    {
        IsMeetLive = true;
        Debug.Log("[CarMeet] Daikoku-style meet is live at Tokyo Bay (night)");
        ShowMessage("Car meet at Tokyo Bay - drive in, press E near a car to challenge", 5f);
    }

    void CloseMeet()
    {
        IsMeetLive = false;
        NearestSlot = -1;
        DespawnParkedCars();
        if (meetRoot != null) Destroy(meetRoot);
        meetRoot = null;
    }

    void SpawnParkedCars()
    {
        meetRoot = new GameObject("CarMeet");
        meetRoot.transform.position = MeetCentre;

        // Plaza flood lights (real lights; poles arrive with the
        // Phase B environment build — the light itself is final).
        Color[] floodColours = { new Color(0.2f, 0.6f, 1f), new Color(1f, 0.2f, 0.6f),
            new Color(0.2f, 1f, 0.7f), new Color(1f, 0.6f, 0.1f) };
        for (int i = 0; i < 4; i++)
        {
            var lightGo = new GameObject("MeetFlood_" + i);
            lightGo.transform.SetParent(meetRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(-45f + i * 30f, 8f, -20f + (i % 2) * 40f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = floodColours[i];
            light.intensity = 60f;
            light.range = 45f;
        }

        // Two rows of 6 parked cars, nose-in, 8 m apart.
        for (int slot = 0; slot < ParkedRoster.Length; slot++)
        {
            int row = slot / 6, col = slot % 6;
            Vector3 pos = new Vector3(-20f + col * 8f, 0f, row == 0 ? -8f : 8f);
            float yaw = row == 0 ? 0f : 180f;
            GameObject car = SpawnParkedCar(ParkedRoster[slot], pos, yaw);
            parkedCars.Add(car);
        }
        Debug.Log("[CarMeet] spawned " + parkedCars.Count + " parked cars");
    }

    GameObject SpawnParkedCar(int carIndex, Vector3 localPos, float yaw)
    {
        var def = CarSelection.Cars[carIndex];
        GameObject root = new GameObject("MeetCar_" + def.carName.Replace(" ", ""));
        root.transform.SetParent(meetRoot.transform, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Prefer a real imported model when the integration agent has
        // landed one for this car; otherwise the detailed procedural
        // visual (CarVisuals) — the same visuals the game already uses.
        string prefabPath = "Models/Cars/" + def.carName.Replace(" ", "");
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            Instantiate(prefab, root.transform, false);
        }
        else
        {
            // CarVisuals.Rebuild reads the Body child's material colour
            // as the paint, so seed it with the roster colour first.
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            var mf = body.AddComponent<MeshFilter>();
            var mr = body.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", def.bodyColor);
            mat.SetColor("_Color", def.bodyColor);
            mr.sharedMaterial = mat;
            CarVisuals.Rebuild(root.transform, false);
        }

        // Neon underglow in the car's own paint colour.
        var glowGo = new GameObject("Underglow");
        glowGo.transform.SetParent(root.transform, false);
        glowGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        var glow = glowGo.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = def.bodyColor;
        glow.intensity = 8f;
        glow.range = 7f;
        return root;
    }

    void DespawnParkedCars()
    {
        foreach (var car in parkedCars)
            if (car != null) Destroy(car);
        parkedCars.Clear();
    }

    void UpdateNearestSlot(Vector3 playerPos)
    {
        int best = -1;
        float bestD = ChallengeRadius;
        for (int i = 0; i < parkedCars.Count; i++)
        {
            if (parkedCars[i] == null) continue;
            float d = Vector3.Distance(playerPos, parkedCars[i].transform.position);
            if (d < bestD) { bestD = d; best = i; }
        }
        if (best != NearestSlot)
        {
            NearestSlot = best;
            if (best >= 0)
            {
                var def = CarSelection.Cars[ParkedRoster[best]];
                ShowMessage(DriverLines[best] + "  [E] Challenge  -  " + def.carName, 4f);
            }
        }
    }

    /// <summary>Challenge the driver of a parked car to a street race.
    /// Public so the (Phase B) touch UI can call it too.</summary>
    public void ChallengeDriver(int slot)
    {
        if (slot < 0 || slot >= ParkedRoster.Length) return;
        int carIndex = ParkedRoster[slot];
        var def = CarSelection.Cars[carIndex];
        Debug.Log("[CarMeet] challenged " + def.carName + " (slot " + slot + ")");
        ShowMessage("Race accepted! " + def.carName + " is waiting on the street...", 4f);
        PlayerPrefs.SetString(PrefsSource, "meet");
        PlayerPrefs.SetInt(PrefsRival, carIndex);
        PlayerPrefs.Save();
        rewardPending = true;
        var gm = GameManager.Instance;
        if (gm != null) gm.StartStreetRace();
    }

    void PayChallengeReward()
    {
        rewardPending = false;
        PlayerPrefs.DeleteKey(PrefsSource);
        PlayerPrefs.DeleteKey(PrefsRival);
        PlayerPrefs.Save();
        var gm = GameManager.Instance;
        if (gm != null) gm.AddMoney(ChallengeRewardMoney);
        PlayerProfile.AddXP(ChallengeRewardXP);
        PlayerProfile.RecordRace(true, ChallengeRewardXP);
        ShowMessage("Meet challenge WON!  +$" + ChallengeRewardMoney + "  +" + ChallengeRewardXP + " XP", 6f);
        Debug.Log("[CarMeet] challenge reward paid");
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

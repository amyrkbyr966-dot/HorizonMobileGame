using UnityEngine;

/// <summary>WANGAN MIDNIGHT TOP-SPEED RUN (my own idea #1) — the
/// Shutoko-style midnight highway run. At night in free-roam, four
/// speed traps sit on the Shibuya ring highway (the same ring the
/// DistrictData "Ring Highway" viewpoint marks). Pass a trap and
/// your speed through it is recorded; hit all four traps inside one
/// 10-minute window and the run is complete. Score = sum of the four
/// trap speeds. Beat 200 km/h at every trap for the $2000 Wangan
/// bonus; any completed run pays $500. Best score is kept per car
/// (PlayerPrefs "Wangan_Best_<carIndex>") so every car in the
/// 55-car roster has its own leaderboard line to chase. Reads only
/// the public CarController.GetSpeedKmh() — no gameplay code edited.
/// The run is night-only: the Wangan is a midnight run by definition.</summary>
public class WanganRunManager : MonoBehaviour
{
    public static WanganRunManager Instance { get; private set; }

    public const int TrapCount = 4;
    public const float RunWindowSeconds = 600f;
    public const float TrapRadius = 25f;
    public const float BonusSpeedKmh = 200f;
    public const int BonusMoney = 2000;
    public const int CompleteMoney = 500;

    /// <summary>Speed-trap gates on the ring highway (world XZ).</summary>
    public static readonly Vector3[] TrapPositions = new Vector3[]
    {
        new Vector3(0f, 0f, 450f),    // north straight (Ring Highway viewpoint)
        new Vector3(450f, 0f, 0f),    // east straight
        new Vector3(0f, 0f, -450f),   // south straight
        new Vector3(-450f, 0f, 0f),   // west straight
    };

    /// <summary>True while a run window is open.</summary>
    public bool RunActive { get; private set; }
    /// <summary>Trap speeds recorded this run (0 = not hit yet).</summary>
    public float[] TrapSpeeds { get; private set; } = new float[TrapCount];
    /// <summary>Sum of the four trap speeds this run.</summary>
    public float RunScore
    {
        get
        {
            float s = 0f;
            foreach (float v in TrapSpeeds) s += v;
            return s;
        }
    }
    /// <summary>Best run score with the currently selected car.</summary>
    public float BestScoreForSelectedCar { get { return BestScore(CarSelection.SelectedIndex); } }

    float runTimer;
    CarController playerCar;
    HUD hud;
    float checkTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.25f;

        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;
        if (gm.Mode != GameManager.GameMode.FreeRoam) { ResetRun(); return; }
        var cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle == null || !cycle.IsNight) { ResetRun(); return; }
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
            else return;
        }

        if (RunActive)
        {
            runTimer -= Time.deltaTime;
            if (runTimer <= 0f)
            {
                ShowMessage("Wangan run expired - hit all 4 traps within 10 minutes", 4f);
                ResetRun();
                return;
            }
        }

        Vector3 flat = new Vector3(playerCar.transform.position.x, 0f, playerCar.transform.position.z);
        for (int i = 0; i < TrapCount; i++)
        {
            if (TrapSpeeds[i] > 0f) continue;
            Vector3 trap = new Vector3(TrapPositions[i].x, 0f, TrapPositions[i].z);
            if (Vector3.Distance(flat, trap) <= TrapRadius)
                HitTrap(i);
        }
    }

    void HitTrap(int index)
    {
        float kmh = playerCar.GetSpeedKmh();
        TrapSpeeds[index] = kmh;
        if (!RunActive)
        {
            RunActive = true;
            runTimer = RunWindowSeconds;
            ShowMessage("Wangan run started! Trap 1/4 at " + Mathf.RoundToInt(kmh)
                + " km/h - hit all 4 traps in 10 min", 5f);
        }
        else
        {
            int hit = 0;
            foreach (float v in TrapSpeeds) if (v > 0f) hit++;
            ShowMessage("Speed trap " + hit + "/4: " + Mathf.RoundToInt(kmh)
                + " km/h  (total " + Mathf.RoundToInt(RunScore) + ")", 4f);
        }
        Debug.Log("[Wangan] trap " + (index + 1) + " at " + kmh.ToString("0") + " km/h");
        int done = 0;
        foreach (float v in TrapSpeeds) if (v > 0f) done++;
        if (done >= TrapCount) FinishRun();
    }

    void FinishRun()
    {
        RunActive = false;
        int carIndex = CarSelection.SelectedIndex;
        float score = RunScore;
        bool allFast = true;
        foreach (float v in TrapSpeeds) if (v < BonusSpeedKmh) allFast = false;
        int reward = allFast ? BonusMoney : CompleteMoney;
        var gm = GameManager.Instance;
        if (gm != null) gm.AddMoney(reward);
        bool newBest = score > BestScore(carIndex);
        if (newBest)
        {
            PlayerPrefs.SetFloat("Wangan_Best_" + carIndex, score);
            PlayerPrefs.Save();
        }
        var def = CarSelection.Cars[carIndex];
        ShowMessage(string.Format("Wangan run complete! {0} total in the {1}  +${2}{3}",
            Mathf.RoundToInt(score), def.carName, reward, newBest ? "  NEW BEST!" : ""), 6f);
        PlayerProfile.AddXP(200);
        Debug.Log("[Wangan] run finished: " + score.ToString("0") + " total, reward $" + reward);
        for (int i = 0; i < TrapCount; i++) TrapSpeeds[i] = 0f;
    }

    void ResetRun()
    {
        RunActive = false;
        for (int i = 0; i < TrapCount; i++) TrapSpeeds[i] = 0f;
    }

    public float BestScore(int carIndex)
    {
        return PlayerPrefs.GetFloat("Wangan_Best_" + carIndex, 0f);
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

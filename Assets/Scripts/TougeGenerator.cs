using System.Collections.Generic;
using UnityEngine;

/// <summary>TOUGE GENERATOR — builds the Haruna Touge road from REAL
/// Kenney road-kit pieces (kenney-city-kit-roads), never boxes.
/// Pieces used (all present in ~/workspace/models-downloads/
/// kenney-city-kit-roads/Models/FBX format/):
///   road-straight, road-curve, road-bend,
///   road-straight-barrier, road-curve-barrier, road-bend-barrier
/// (guardrails on the cliff side), and nature-kit pines
/// (tree_pineDefaultA, tree_oak, tree_default_fall) for the forest.
/// The generator loads prefabs from Resources at the paths below —
/// the model-integration agent imports the kits into Assets/Models;
/// until then Build() validates the data and logs exactly which
/// prefabs are missing instead of faking geometry with boxes.
/// Also runs the pass: timing, drift scoring (reads DriftScore's
/// public TotalDriftPoints), night-run x1.5 bonus, best-time
/// persistence via TougePass.RecordRun.</summary>
public class TougeGenerator : MonoBehaviour
{
    public static TougeGenerator Instance { get; private set; }

    /// <summary>Resources folder the model-integration agent imports
    /// the Kenney kits into (e.g. Assets/Resources/Models/KenneyRoads/
    /// road-straight.prefab). Change here, not in code paths.</summary>
    public string kitPrefabRoot = "Models/KenneyRoads/";
    public string naturePrefabRoot = "Models/KenneyNature/";

    /// <summary>Root object of the built pass (null until Build runs).</summary>
    public GameObject PassRoot { get; private set; }
    public bool IsBuilt { get { return PassRoot != null; } }
    public bool IsRunning { get; private set; }
    public bool IsNightRun { get; private set; }

    float runTimer;
    int runStartDrift;
    DriftScore driftScore;
    HUD hud;
    List<Vector3> cachedSpline;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        cachedSpline = TougePass.SampleSpline();
    }

    void Update()
    {
        if (!IsRunning) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;
        runTimer += Time.deltaTime;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        int idx = TougePass.NearestSampleIndex(cachedSpline, player.transform.position);
        // Finished: reached the last 2% of the spline.
        if (idx >= cachedSpline.Count - 3) FinishRun();
    }

    /// <summary>Validate the pass data, then assemble the road from
    /// kit pieces. Returns the root GameObject, or null when the kit
    /// prefabs are not imported yet (data stays valid; the build is
    /// retried by the Phase B map pass). No placeholder geometry is
    /// ever created.</summary>
    public GameObject Build()
    {
        string report;
        if (!TougePass.Validate(out report))
        {
            Debug.LogWarning("[TougeGenerator] " + report);
            return null;
        }
        Debug.Log(report);

        if (PassRoot != null) return PassRoot;
        GameObject root = new GameObject("TougePass");
        root.transform.position = Vector3.zero;

        var spline = TougePass.SampleSpline();
        int placed = 0, missing = 0;
        for (int i = 0; i < spline.Count - 1; i++)
        {
            Vector3 a = spline[i], b = spline[i + 1];
            Vector3 dir = (b - a).normalized;
            if (dir.sqrMagnitude < 0.0001f) continue;
            float turn = TurnAngle(spline, i);
            string piece = PieceForTurn(turn);
            GameObject prefab = LoadKit(piece);
            if (prefab == null) { missing++; continue; }
            GameObject go = Instantiate(prefab, a, Quaternion.LookRotation(dir), root.transform);
            go.name = piece + "_" + i;
            // Guardrail on the downhill (outer) side of every curve.
            if (Mathf.Abs(turn) > 12f)
            {
                GameObject rail = LoadKit(piece + "-barrier");
                if (rail != null)
                {
                    Vector3 side = Vector3.Cross(dir, Vector3.up) * (turn > 0f ? 1f : -1f);
                    GameObject r = Instantiate(rail, a + side * 4.2f, Quaternion.LookRotation(dir), root.transform);
                    r.name = piece + "-barrier_" + i;
                }
            }
            placed++;
        }

        // Forest: pines along the pass, every ~60 m, offset 12-20 m.
        GameObject treeA = LoadNature("tree_pineDefaultA");
        GameObject treeB = LoadNature("tree_oak");
        if (treeA != null || treeB != null)
        {
            var rng = new System.Random(1711);
            for (int i = 0; i < spline.Count; i += 6)
            {
                GameObject t = rng.Next(2) == 0 ? treeA : treeB;
                if (t == null) continue;
                Vector3 side = Vector3.Cross(Vector3.forward, Vector3.up);
                float off = (rng.Next(2) == 0 ? -1f : 1f) * (12f + (float)rng.NextDouble() * 8f);
                Vector3 pos = spline[i] + side * off;
                pos.y = spline[i].y;
                GameObject tree = Instantiate(t, pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), root.transform);
                tree.name = "TougePine_" + i;
            }
        }

        PassRoot = root;
        Debug.Log(string.Format("[TougeGenerator] built {0}: {1} road pieces placed, {2} samples missing a prefab (kit not imported yet)",
            TougePass.PassName, placed, missing));
        return root;
    }

    /// <summary>Start a timed drift run from the touge gate. Night
    /// runs (DayNightCycle.IsNight) pay x1.5 drift money.</summary>
    public void StartRun()
    {
        var cycle = FindFirstObjectByType<DayNightCycle>();
        IsNightRun = cycle != null && cycle.IsNight;
        driftScore = FindFirstObjectByType<DriftScore>();
        runStartDrift = driftScore != null ? driftScore.TotalDriftPoints : 0;
        runTimer = 0f;
        IsRunning = true;
        ShowMessage("Touge run started - " + TougePass.PassName
            + (IsNightRun ? "  NIGHT RUN x1.5" : "") + "  Best: " + TougePass.BestTimeText(), 5f);
        Debug.Log("[TougeGenerator] run started (night=" + IsNightRun + ")");
    }

    void FinishRun()
    {
        IsRunning = false;
        int drift = (driftScore != null ? driftScore.TotalDriftPoints : 0) - runStartDrift;
        if (IsNightRun) drift = Mathf.RoundToInt(drift * TougePass.NightRunMultiplier);
        TougePass.RecordRun(runTimer, drift);
        int reward = IsNightRun ? TougePass.NightRunBonusMoney : TougePass.DayRunRewardMoney;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(reward);
        if (driftScore != null) PlayerProfile.RecordDrift(drift);
        int m = Mathf.FloorToInt(runTimer / 60f);
        int s = Mathf.FloorToInt(runTimer % 60f);
        ShowMessage(string.Format("Touge finished {0:00}:{1:00}  Drift {2} pts  +${3}",
            m, s, drift, reward), 6f);
        Debug.Log(string.Format("[TougeGenerator] run finished {0:0.0}s drift {1} (best {2})",
            runTimer, drift, TougePass.BestTimeText()));
    }

    /// <summary>True when the player car is on (within 25 m of) the pass.</summary>
    public bool PlayerOnPass()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;
        var spline = cachedSpline ?? TougePass.SampleSpline();
        int idx = TougePass.NearestSampleIndex(spline, player.transform.position);
        return Vector3.Distance(spline[idx], player.transform.position) < 25f;
    }

    // ---- Kit piece selection ----

    float TurnAngle(List<Vector3> spline, int i)
    {
        if (i <= 0 || i + 1 >= spline.Count) return 0f;
        Vector3 d0 = (spline[i] - spline[i - 1]).normalized;
        Vector3 d1 = (spline[i + 1] - spline[i]).normalized;
        float angle = Vector3.SignedAngle(d0, d1, Vector3.up);
        return angle;
    }

    static string PieceForTurn(float turnDeg)
    {
        float a = Mathf.Abs(turnDeg);
        if (a < 8f) return "road-straight";
        if (a < 25f) return "road-curve";
        return "road-bend";
    }

    GameObject LoadKit(string pieceName)
    {
        return Resources.Load<GameObject>(kitPrefabRoot + pieceName);
    }

    GameObject LoadNature(string pieceName)
    {
        return Resources.Load<GameObject>(naturePrefabRoot + pieceName);
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

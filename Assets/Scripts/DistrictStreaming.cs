using System.Collections.Generic;
using UnityEngine;

/// <summary>District streaming skeleton (Phase A — plan §1.3, Phase 1).
/// Every 0.5 s it computes the player's 500 m cell, diffs the wanted
/// 3x3 set (9 cells) against the loaded set, and activates / deactivates
/// the pre-built cell-root GameObjects ("Cell_xx_yy"). Cells outside a
/// 750 m radius are unloaded. LOD tiers: LOD0 &lt;150 m, LOD1 150-400 m,
/// LOD2 400-750 m (see DistrictData.LodTierForDistance).
///
/// PHASE B (2026-11-09): PlateauMapLoader creates Cell_xx_yy roots
/// for every cell that holds PLATEAU tiles and registers them here
/// (RegisterCell), so the streamer leaves LEGACY mode and toggles cells
/// by the 3x3 rule. Roots are created ACTIVE; the first UpdateStreaming
/// sweep seeds the loaded set with every known root and then unloads
/// the unwanted ones (see firstSweep). If the loader fails entirely,
/// no roots exist and the streamer falls back to LEGACY mode exactly
/// as in Phase A. Roads, ground plane and sky are always loaded by
/// design and are never toggled by this script.
///
/// Traffic cars are pooled via ObjectPool (plan §1.5); this script only
/// counts / caps them — spawning stays with the existing traffic code.
/// Do NOT edit the scene from here.</summary>
public class DistrictStreaming : MonoBehaviour
{
    public static DistrictStreaming Instance { get; private set; }

    [Header("Streaming")]
    [Tooltip("How often (seconds) the wanted-cell set is recomputed.")]
    public float updateInterval = 0.5f;
    [Tooltip("Cells farther than this from the player are unloaded (metres).")]
    public float unloadRadius = DistrictData.UnloadRadius;

    /// <summary>Player's current cell on the 16x16 grid.</summary>
    public Vector2Int PlayerCell { get; private set; } = new Vector2Int(7, 7);
    /// <summary>District the player is driving in right now.</summary>
    public DistrictData.DistrictInfo CurrentDistrict { get; private set; }
    /// <summary>True once at least one Cell_xx_yy root was found in the
    /// scene. False in Phase A (legacy Shibuya scene, no cell roots).</summary>
    public bool StreamingActive { get; private set; }
    /// <summary>How many cell roots are currently loaded (active).</summary>
    public int LoadedCellCount { get; private set; }
    /// <summary>How many cell roots exist in the scene at all.</summary>
    public int KnownCellCount { get; private set; }
    /// <summary>Active traffic cars counted in the last sweep (cap 8).</summary>
    public int ActiveTrafficCount { get; private set; }

    float timer;
    bool legacyModeLogged;
    bool firstSweep = true;
    Transform player;
    readonly Dictionary<Vector2Int, GameObject> cellRoots =
        new Dictionary<Vector2Int, GameObject>();
    readonly HashSet<Vector2Int> loadedCells = new HashSet<Vector2Int>();

    /// <summary>PLATEAU district roots registered by PlateauMapLoader
    /// (district -&gt; chunk root). District-level streaming: the loader
    /// activates a root while the player is near that district; this
    /// table lets IsLoaded() and the budget report account for them.</summary>
    readonly Dictionary<DistrictData.DistrictId, GameObject> plateauRoots =
        new Dictionary<DistrictData.DistrictId, GameObject>();
    /// <summary>How many PLATEAU district chunks are registered.</summary>
    public int PlateauDistrictCount { get { return plateauRoots.Count; } }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        CurrentDistrict = DistrictData.Get(DistrictData.DistrictId.Shibuya);
        ScanForCellRoots();
    }

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = updateInterval;
        UpdateStreaming();
    }

    /// <summary>Find every "Cell_xx_yy" root already in the scene.
    /// Phase A finds none (legacy scene); Phase B's generated city
    /// registers them here automatically. Roots are found by name so
    /// no scene edit or inspector wiring is needed.</summary>
    void ScanForCellRoots()
    {
        cellRoots.Clear();
        for (int cx = 0; cx < DistrictData.CellsPerSide; cx++)
        {
            for (int cz = 0; cz < DistrictData.CellsPerSide; cz++)
            {
                var cell = new Vector2Int(cx, cz);
                var go = GameObject.Find(DistrictData.CellRootName(cell));
                if (go != null) cellRoots[cell] = go;
            }
        }
        KnownCellCount = cellRoots.Count;
        StreamingActive = KnownCellCount > 0;
    }

    /// <summary>Phase B / editor tooling hook: register a cell root that
    /// was created after Awake (e.g. a generated district). Safe to call
    /// repeatedly; re-registering the same cell replaces the entry.</summary>
    public void RegisterCell(Vector2Int cell, GameObject root)
    {
        if (root == null) return;
        cellRoots[cell] = root;
        KnownCellCount = cellRoots.Count;
        StreamingActive = true;
    }

    /// <summary>PLATEAU hook: register a converted-district marker
    /// (Plateau_&lt;name&gt;) created by PlateauMapLoader. Safe to call
    /// repeatedly. The marker is always active — it only records that
    /// the district's data is in the scene (budget report,
    /// PlateauDistrictCount). It does NOT make the district count as
    /// loaded in IsLoaded(): the tiles live under Cell_xx_yy roots and
    /// the cell check governs once streaming is active.</summary>
    public void RegisterPlateauDistrict(DistrictData.DistrictId id, GameObject root)
    {
        if (root == null) return;
        plateauRoots[id] = root;
    }

    void UpdateStreaming()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
            else return;
        }

        Vector3 pos = player.position;
        PlayerCell = DistrictData.CellForPosition(pos);
        CurrentDistrict = DistrictData.DistrictAt(pos);

        if (!StreamingActive)
        {
            // Phase A legacy mode: the whole 1x1 km Shibuya scene is
            // always loaded. Nothing to toggle — just keep the
            // district/cell tracking (and the traffic cap) honest.
            if (!legacyModeLogged)
            {
                legacyModeLogged = true;
                Debug.Log("[DistrictStreaming] legacy mode: no Cell_xx_yy roots " +
                    "in scene (Phase A, Shibuya 1x1 km undivided). " +
                    "Tracking player cell/district only; Phase B creates the 256 cell roots.");
            }
            LoadedCellCount = 0;
            CountTraffic();
            return;
        }

        // First sweep: cell roots are created ACTIVE (so ScanForCellRoots
        // can find them by name), and loadedCells starts empty — without
        // seeding, the unload rule below would never switch off a cell
        // that was never added to the set, so nothing would ever stream.
        // Seed with every known root; the unload rule then switches off
        // the unwanted ones, and the load rule switches on the wanted 3x3.
        if (firstSweep)
        {
            firstSweep = false;
            foreach (Vector2Int cell in cellRoots.Keys)
                loadedCells.Add(cell);
        }

        var wanted = DistrictData.WantedCells(PlayerCell);

        // Unload: loaded cells that are no longer wanted AND farther
        // than the unload radius from the player (hysteresis).
        var toUnload = new List<Vector2Int>();
        foreach (Vector2Int cell in loadedCells)
        {
            if (wanted.Contains(cell)) continue;
            float d = Vector3.Distance(DistrictData.CellCentre(cell), pos);
            if (d > unloadRadius) toUnload.Add(cell);
        }
        foreach (Vector2Int cell in toUnload)
        {
            loadedCells.Remove(cell);
            SetCellActive(cell, false);
        }

        // Load: wanted cells not yet loaded.
        foreach (Vector2Int cell in wanted)
        {
            if (loadedCells.Contains(cell)) continue;
            loadedCells.Add(cell);
            SetCellActive(cell, true);
        }

        LoadedCellCount = loadedCells.Count;
        CountTraffic();
    }

    void SetCellActive(Vector2Int cell, bool active)
    {
        GameObject root;
        if (!cellRoots.TryGetValue(cell, out root) || root == null) return;
        if (root.activeSelf != active) root.SetActive(active);
    }

    void CountTraffic()
    {
        var traffic = GameObject.FindGameObjectsWithTag("Traffic");
        ActiveTrafficCount = traffic != null ? traffic.Length : 0;
        if (ActiveTrafficCount > DistrictData.MaxTrafficCars)
        {
            Debug.LogWarning("[DistrictStreaming] traffic over budget: "
                + ActiveTrafficCount + " active, cap is "
                + DistrictData.MaxTrafficCars + " (plan §1.5)");
        }
    }

    /// <summary>LOD tier for an object at a world position, measured
    /// from the player (camera proxy — the player car is always near
    /// the camera). 0 full / 1 simplified / 2 silhouette / 3 culled.</summary>
    public int LodTierFor(Vector3 worldPos)
    {
        if (player == null) return 0;
        return DistrictData.LodTierForDistance(
            Vector3.Distance(worldPos, player.position));
    }

    /// <summary>True when a world position is inside the currently
    /// loaded 3x3 area. Phase B: PLATEAU tiles live under Cell_xx_yy
    /// roots, so when streaming is active the CELL check governs — a
    /// district marker root is always active (it only records that the
    /// district's data is in the scene) and must not make an unloaded
    /// cell count as loaded. In legacy mode (no cell roots, e.g. the
    /// loader failed) the whole Shibuya scene is loaded, as before.</summary>
    public bool IsLoaded(Vector3 worldPos)
    {
        if (StreamingActive)
            return loadedCells.Contains(DistrictData.CellForPosition(worldPos));
        DistrictData.DistrictInfo d = DistrictData.DistrictAt(worldPos);
        GameObject plateauRoot;
        if (plateauRoots.TryGetValue(d.id, out plateauRoot)
            && plateauRoot != null && plateauRoot.activeSelf) return true;
        return true;
    }

    /// <summary>Budget report for the HUD / logs: draw-call budget is
    /// measured in the Linux build (Phase 3), not here — this reports
    /// what the streamer itself can count.</summary>
    public string BudgetReport()
    {
        return string.Format(
            "Streaming {0} | cells {1}/{2} loaded | traffic {3}/{4} | district {5} | PLATEAU districts {6}",
            StreamingActive ? "ON" : "LEGACY",
            LoadedCellCount, DistrictData.CellsPerSide * DistrictData.CellsPerSide,
            ActiveTrafficCount, DistrictData.MaxTrafficCars,
            CurrentDistrict.displayName,
            PlateauDistrictCount);
    }
}

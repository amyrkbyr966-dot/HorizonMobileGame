using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Loads the converted Project PLATEAU Tokyo map at runtime.
///
/// Created by VisualBootstrap for the Main scene (no scene edit needed).
/// For each district it Resources.Loads the converted FBX files listed
/// in PlateauMap, instantiates them, and wires them into the streaming
/// system (Phase B):
///
///  1. ANCHOR SHIFT — the converted geometry keeps real Tokyo elevations
///     (Shibuya dem Y 6..41 m, Shinjuku 20..45 m, Tokyo Bay -11..18 m),
///     but the game spawns the player at y ~= 0.6 on a flat plane.
///     Without a shift the whole PLATEAU city floats 6..45 m above the
///     drivable plane (seen in the 2026-11-09 texture-fix photos: real
///     facades hanging in the sky over flat green ground, the car driving
///     underneath them). Each district root is therefore shifted down by
///     the ground height sampled at its anchor (district centre), so the
///     terrain at every district centre sits at y = 0. The shift is
///     sampled at runtime from the district's own dem/tran colliders (no
///     hard-coded heights) and logged per district.
///  2. CELL ROOTS — every tile is reparented (world position preserved)
///     under the Cell_xx_yy root (500 m grid, DistrictData) that contains
///     the tile's bounds centre, and the root is registered with
///     DistrictStreaming.RegisterCell. DistrictStreaming then leaves its
///     Phase A legacy mode and toggles cells by the 3x3 rule; this loader
///     no longer toggles district roots at runtime (the old per-district
///     activation is gone).
///  3. LEGACY HIDE — when the Shibuya PLATEAU terrain actually loads,
///     the baked procedural city visuals in Main.unity (Ground, roads,
///     box buildings, trees, dunes) are deactivated at runtime. The
///     procedural code (CityVisuals.Apply) was already skipped in that
///     case, but the scene file itself ships the procedural city, so
///     skipping Apply() alone left both cities overlapping. The hide is
///     runtime-only and only happens when the Shibuya dem loaded, so the
///     procedural city stays the fallback whenever PLATEAU fails.
///  4. WORLD SNAP — the player, the gas station / garage structures and
///     the race checkpoints are shifted onto the real terrain height at
///     their XZ (the old y ~= 0 poses would be buried or floating on the
///     shifted terrain). Coins / repairs / traffic keep their own logic
///     (follow-up: terrain-aware Collectible / TrafficCar — see report).
///  5. GAP TERRAIN — ConnectiveTerrain.Build fills the voids between the
///     district ground squares (see that file for the measured rects),
///     raises the mountain ring (MapTerrain), and builds the full road
///     network (ring highway + spokes + street grids, with markings).
///  6. NATURE — MapNature.Build plants the forests + sakura from
///     nature_placement.json (Kenney NatureKit trees + procedural
///     sakura), combined per zone per material, under the streaming
///     cell roots.
///  7. TOUGE — TougeGenerator.Build assembles the hidden Haruna Touge
///     mountain pass (Kenney road-kit pieces along the TougePass
///     spline) in the NW mountain ring, connected by the touge access
///     road from Ueno. The pass root stays always loaded (the 1.6 km
///     pass spans many cells; cell-parenting it would vanish the far
///     end while driving it).
///
/// FALLBACK: if a district has no PLATEAU files in the project (Ginza:
/// 0 converted tiles), it is left unwired — no geometry is invented for
/// it — and the procedural map stays the fallback for Shibuya only when
/// the Shibuya chunk itself fails to load (VisualBootstrap decides that
/// from PlateauShibuyaActive, set here at load time, not from the
/// player position).
///
/// Known gaps (not hidden): PLATEAU has no street props, traffic
/// lights or interiors; building meshes get no colliders yet (cars can
/// pass through buildings until per-building colliders are added);
/// Asakusa and Ueno Park ship LOD1 massing only (no LOD2 in the
/// package); Shinjuku / Asakusa / Akihabara / Ueno have no tran (road)
/// meshes converted yet — their street surfaces are the ConnectiveTerrain
/// ribbons (step 5), not bare dem. Trees are MapNature (step 6), not
/// PLATEAU.</summary>
public class PlateauMapLoader : MonoBehaviour
{
    public static PlateauMapLoader Instance { get; private set; }

    /// <summary>Kept for compatibility: district toggling is now owned by
    /// DistrictStreaming (cell roots). This loader only builds the world
    /// once, at scene load; it has no per-frame work anymore.</summary>
    public float updateInterval = 0.5f;
    /// <summary>Kept for compatibility (see updateInterval).</summary>
    public float keepLoadedMargin = DistrictData.UnloadRadius;

    /// <summary>Districts whose PLATEAU chunk is loaded in the scene.</summary>
    public List<DistrictData.DistrictId> LoadedDistricts { get; private set; }
    /// <summary>True when at least the Shibuya PLATEAU chunk loaded
    /// (the procedural city was skipped for Shibuya). Set at load time
    /// (not from the player position) so VisualBootstrap's decision is
    /// correct even before the player exists.</summary>
    public bool PlateauShibuyaActive { get; private set; }
    /// <summary>Total triangles instantiated across loaded districts
    /// (counted from MeshFilters, for the budget report).</summary>
    public long LoadedTriangles { get; private set; }

    /// <summary>Ground colliders (PLATEAU dem + tran, plus the connective
    /// terrain) for height sampling: anchor shift, gap fill, world snap
    /// and CarController's reset grounding. Buildings have no colliders
    /// by design, so a height query never answers a rooftop.</summary>
    public List<Collider> GroundColliders { get; private set; }
    /// <summary>World-space bounds of each district's dem (terrain) mesh,
    /// after the anchor shift. Used by ConnectiveTerrain to validate its
    /// gap rectangles against the converted data.</summary>
    public Dictionary<DistrictData.DistrictId, Bounds> DemBounds { get; private set; }
    /// <summary>Per-district vertical shift applied so the terrain at the
    /// district anchor sits at y = 0 (game ground level). Positive value
    /// = the district was lowered by that many metres.</summary>
    public Dictionary<DistrictData.DistrictId, float> AnchorShift { get; private set; }

    readonly Dictionary<Vector2Int, GameObject> cellRoots =
        new Dictionary<Vector2Int, GameObject>();
    readonly Dictionary<DistrictData.DistrictId, GameObject> districtMarkers =
        new Dictionary<DistrictData.DistrictId, GameObject>();
    Transform cellsRoot;
    bool shibuyaDemLoaded;

    /// <summary>One district's load result (temporary; the district root
    /// is destroyed again once its tiles move into cell roots).</summary>
    struct DistrictLoadResult
    {
        public GameObject root;
        public List<GameObject> tiles;
        public List<Collider> groundColliders;
        public List<Collider> demColliders;
        public bool demLoaded;
        public long tris;
    }

    /// <summary>Create the loader (once) and load every district that
    /// has PLATEAU files. Returns true when the Shibuya PLATEAU chunk
    /// loaded — the caller (VisualBootstrap) then skips the procedural
    /// CityVisuals.Apply() for Shibuya.</summary>
    public static bool Ensure()
    {
        if (Instance != null) return Instance.PlateauShibuyaActive;
        if (!PlateauMap.UsePlateauMap) return false;
        var go = new GameObject("PlateauMapRoot");
        Instance = go.AddComponent<PlateauMapLoader>();
        Instance.LoadAllDistricts();
        return Instance.PlateauShibuyaActive;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadedDistricts = new List<DistrictData.DistrictId>();
        GroundColliders = new List<Collider>();
        DemBounds = new Dictionary<DistrictData.DistrictId, Bounds>();
        AnchorShift = new Dictionary<DistrictData.DistrictId, float>();
    }

    /// <summary>Highest ground height at a world XZ (PLATEAU dem/tran or
    /// connective terrain), or false when no ground exists there (void /
    /// unwired district / cell not loaded). Used by the gap builder, the
    /// world snap and CarController's reset grounding. A reset only ever
    /// needs ground near the player, and the player's own 3x3 cells are
    /// always loaded, so unloaded far cells answering nothing is correct.
    /// Ray origin y = 500 m is above every ground surface (dem max
    /// 44.5 m raw); buildings have no colliders, so they can never be
    /// the answer.</summary>
    public static bool TryGetGroundHeight(float x, float z, out float y)
    {
        y = 0f;
        var inst = Instance;
        if (inst == null || inst.GroundColliders == null || inst.GroundColliders.Count == 0)
            return false;
        return TrySampleGround(inst.GroundColliders, x, z, out y);
    }

    /// <summary>Register a ground collider created after LoadAllDistricts
    /// (connective terrain). Safe to call repeatedly.</summary>
    public void RegisterGroundCollider(Collider col)
    {
        if (col == null) return;
        if (GroundColliders == null) GroundColliders = new List<Collider>();
        if (!GroundColliders.Contains(col)) GroundColliders.Add(col);
    }

    /// <summary>Highest hit of a downward ray against a given collider
    /// set (dem/tran only — never buildings). Direct Collider.Raycast,
    /// so it works at load time before the first physics step.</summary>
    static bool TrySampleGround(List<Collider> colliders, float x, float z, out float y)
    {
        y = 0f;
        if (colliders == null || colliders.Count == 0) return false;
        Ray ray = new Ray(new Vector3(x, 500f, z), Vector3.down);
        float best = float.NegativeInfinity;
        bool found = false;
        RaycastHit hit;
        foreach (var col in colliders)
        {
            if (col == null) continue;
            if (col.Raycast(ray, out hit, 1000f) && hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }
        if (!found) return false;
        y = best;
        return true;
    }

    void LoadAllDistricts()
    {
        if (LoadedDistricts == null) LoadedDistricts = new List<DistrictData.DistrictId>();
        if (GroundColliders == null) GroundColliders = new List<Collider>();
        if (DemBounds == null) DemBounds = new Dictionary<DistrictData.DistrictId, Bounds>();
        if (AnchorShift == null) AnchorShift = new Dictionary<DistrictData.DistrictId, float>();

        cellsRoot = new GameObject("PlateauCells").transform;
        cellsRoot.SetParent(transform, false);

        // 1. Load every district's files under a temporary district root.
        var loaded = new Dictionary<DistrictData.DistrictId, GameObject>();
        var districtTiles = new Dictionary<DistrictData.DistrictId, List<GameObject>>();
        var districtGround = new Dictionary<DistrictData.DistrictId, List<Collider>>();
        var districtDem = new Dictionary<DistrictData.DistrictId, List<Collider>>();
        foreach (DistrictData.DistrictId id in System.Enum.GetValues(typeof(DistrictData.DistrictId)))
        {
            PlateauMap.MapFile[] files = PlateauMap.FilesFor(id);
            if (files == null || files.Length == 0) continue;
            DistrictLoadResult r = LoadDistrict(id, files);
            if (r.root == null)
            {
                // 0 files present on disk (Ginza today): left unwired,
                // no geometry invented for it.
                Debug.Log("[PlateauMapLoader] district " + DistrictData.Get(id).displayName
                    + ": 0 files in the project (not converted yet) — left unwired");
                continue;
            }
            loaded[id] = r.root;
            districtTiles[id] = r.tiles;
            districtGround[id] = r.groundColliders;
            districtDem[id] = r.demColliders;
            LoadedDistricts.Add(id);
            if (id == DistrictData.DistrictId.Shibuya) shibuyaDemLoaded = r.demLoaded;
        }

        // 2. Anchor shift: lower each district so the terrain at its
        // anchor (district centre) sits at y = 0. Sampled from the
        // district's OWN ground colliders only — a neighbour's clipped
        // dem can never be the answer at the centre.
        foreach (KeyValuePair<DistrictData.DistrictId, GameObject> kv in loaded)
        {
            DistrictData.DistrictId id = kv.Key;
            DistrictData.DistrictInfo info = DistrictData.Get(id);
            float h = SampleAnchorHeight(id, info.centre, districtGround[id]);
            AnchorShift[id] = h;
            Vector3 pos = kv.Value.transform.position;
            kv.Value.transform.position = new Vector3(pos.x, -h, pos.z);
            // Dem bounds (world space, post-shift) for the gap builder.
            List<Collider> dems = districtDem[id];
            if (dems != null && dems.Count > 0)
            {
                Bounds b = dems[0].bounds;
                for (int i = 1; i < dems.Count; i++) b.Encapsulate(dems[i].bounds);
                DemBounds[id] = b;
            }
            Debug.Log("[PlateauMapLoader] district " + info.displayName
                + ": anchor ground y=" + h.ToString("0.0")
                + " -> shift " + (-h).ToString("0.0") + " m");
        }

        // 3. Hide the baked procedural city visuals (only when the real
        // Shibuya terrain is in — otherwise the fallback stays).
        if (shibuyaDemLoaded) HideLegacyCityVisuals();

        // 4. Snap the player and the static world dressing onto the
        // real terrain (old y ~= 0 poses would be buried / floating).
        SnapWorldObjectsToGround();

        // 5. Reparent every tile into its streaming cell root, then drop
        // the temporary district roots (a marker per district stays for
        // the budget report / IsLoaded).
        foreach (KeyValuePair<DistrictData.DistrictId, List<GameObject>> kv in districtTiles)
            foreach (GameObject tile in kv.Value)
                AssignTileToCell(tile, kv.Key);
        foreach (KeyValuePair<DistrictData.DistrictId, GameObject> kv in loaded)
        {
            Destroy(kv.Value);
            var marker = new GameObject("Plateau_" + DistrictData.Get(kv.Key).name);
            marker.transform.SetParent(transform, false);
            districtMarkers[kv.Key] = marker;
            if (DistrictStreaming.Instance != null)
                DistrictStreaming.Instance.RegisterPlateauDistrict(kv.Key, marker);
        }

        // 6. Fill the voids between the district ground squares,
        //    raise the mountain ring and build the road network.
        ConnectiveTerrain.Build(this);

        // 7. Plant the forests + sakura (needs the ground colliders
        //    from step 6 for the live Y sampling).
        MapNature.Build(this);

        // 8. Assemble the hidden touge pass in the NW mountain ring
        //    (returns null and logs when the Kenney kit is not imported
        //    yet — the map still loads; the pass is retried next load).
        //    TougeGenerator is attached by CreativeFeaturesBootstrap on
        //    scene load; the bootstrap runs before this loader, but
        //    guard anyway (either order of Awake is covered).
        var touge = TougeGenerator.Instance;
        if (touge == null) touge = FindFirstObjectByType<TougeGenerator>();
        if (touge != null)
        {
            GameObject pass = touge.Build();
            if (pass != null)
                Debug.Log("[PlateauMapLoader] touge pass built: " + TougePass.PassName
                    + " (" + TougePass.PassLengthKm.ToString("0.0") + " km, gate "
                    + TougePass.ControlPoints[0].position.ToString("0") + ")");
        }
        else
        {
            Debug.LogWarning("[PlateauMapLoader] no TougeGenerator in the scene — touge pass not built");
        }

        PlateauShibuyaActive = loaded.ContainsKey(DistrictData.DistrictId.Shibuya);
        Debug.Log("[PlateauMapLoader] " + PlateauMap.Attribution
            + " | districts loaded: " + LoadedDistricts.Count
            + " | tris: " + LoadedTriangles
            + " | cells: " + cellRoots.Count);
    }

    DistrictLoadResult LoadDistrict(DistrictData.DistrictId id, PlateauMap.MapFile[] files)
    {
        DistrictData.DistrictInfo info = DistrictData.Get(id);
        var root = new GameObject("Plateau_" + info.name);
        root.transform.SetParent(transform, false);
        root.transform.position = PlateauMap.PlateauOffset(id);
        var result = new DistrictLoadResult
        {
            root = root,
            tiles = new List<GameObject>(),
            groundColliders = new List<Collider>(),
            demColliders = new List<Collider>(),
            demLoaded = false,
            tris = 0,
        };
        int loaded = 0;
        long tris = 0;
        foreach (PlateauMap.MapFile f in files)
        {
            GameObject prefab = Resources.Load<GameObject>(f.ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[PlateauMapLoader] missing PLATEAU file (skipped): " + f.ResourcePath);
                continue;
            }
            GameObject go = Instantiate(prefab, root.transform, false);
            go.name = f.kind + "_" + f.file;
            // Static for batching — recursive: the FBX children are
            // separate GameObjects under the prefab root.
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.isStatic = true;
            if (f.NeedsCollider)
            {
                foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    var col = mf.gameObject.AddComponent<MeshCollider>();
                    col.sharedMesh = mf.sharedMesh;
                    col.convex = false;
                    result.groundColliders.Add(col);
                    if (f.kind == "dem")
                    {
                        result.demColliders.Add(col);
                        result.demLoaded = true;
                    }
                }
            }
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) tris += mf.sharedMesh.GetIndexCount(0) / 3;
            result.tiles.Add(go);
            loaded++;
        }
        if (loaded == 0)
        {
            Destroy(root);
            result.root = null;
            return result;
        }
        result.tris = tris;
        LoadedTriangles += tris;
        if (GroundColliders == null) GroundColliders = new List<Collider>();
        GroundColliders.AddRange(result.groundColliders);
        Debug.Log("[PlateauMapLoader] district " + info.displayName
            + ": " + loaded + "/" + files.Length + " files, "
            + tris + " tris, offset " + root.transform.position);
        return result;
    }

    /// <summary>Ground height at the district anchor (centre). Falls back
    /// to the median of a ring of samples if the centre itself is not on
    /// any ground (hole in the dem), and to 0 with a warning if nothing
    /// answers at all.</summary>
    float SampleAnchorHeight(DistrictData.DistrictId id, Vector2 centre, List<Collider> ground)
    {
        if (TrySampleGround(ground, centre.x, centre.y, out float h)) return h;
        var hits = new List<float>();
        float[] offs = { -150f, -75f, 75f, 150f };
        foreach (float dx in offs)
            foreach (float dz in offs)
                if (TrySampleGround(ground, centre.x + dx, centre.y + dz, out float hh))
                    hits.Add(hh);
        if (hits.Count > 0)
        {
            hits.Sort();
            Debug.LogWarning("[PlateauMapLoader] district " + DistrictData.Get(id).displayName
                + ": no ground under the anchor, using median of " + hits.Count + " ring samples");
            return hits[hits.Count / 2];
        }
        Debug.LogWarning("[PlateauMapLoader] district " + DistrictData.Get(id).displayName
            + ": no ground found anywhere near the anchor — shift 0, terrain will float");
        return 0f;
    }

    /// <summary>Cell root for a 500 m cell, created on demand under
    /// PlateauCells and registered with DistrictStreaming (either order
    /// of Awake is covered: if the streamer is not up yet, its
    /// ScanForCellRoots finds the root by name when it awakes).</summary>
    GameObject GetOrCreateCellRoot(Vector2Int cell)
    {
        GameObject root;
        if (!cellRoots.TryGetValue(cell, out root) || root == null)
        {
            root = new GameObject(DistrictData.CellRootName(cell));
            root.transform.SetParent(cellsRoot, false);
            root.isStatic = true;
            cellRoots[cell] = root;
            if (DistrictStreaming.Instance != null)
                DistrictStreaming.Instance.RegisterCell(cell, root);
        }
        return root;
    }

    /// <summary>Public cell-root access for the map builders that run
    /// after the districts load (MapNature parents its combined tree
    /// meshes under the cell root of their zone centre, so the trees
    /// stream with the district they stand in).</summary>
    public GameObject GetCellRoot(Vector2Int cell)
    {
        return GetOrCreateCellRoot(cell);
    }

    /// <summary>Parent a tile under the cell root containing its bounds
    /// centre (world position preserved, so the anchor shift stays
    /// baked in). Tiles without renderers fall back to their district
    /// centre cell. A 1 km tile spans up to 2x2 cells and is assigned by
    /// its centre only — its far edge can sit in an unloaded neighbour
    /// cell, so a big tile may pop in at ~250-500 m; the 3x3 wanted set
    /// bounds that, it does not hide nearby geometry.</summary>
    void AssignTileToCell(GameObject tile, DistrictData.DistrictId id)
    {
        Vector3 centre = DistrictData.Get(id).centre;
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = new Bounds();
            bool has = false;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                Bounds rb = r.bounds;
                if (rb.size.sqrMagnitude <= 0f) continue;
                if (!has) { b = rb; has = true; }
                else b.Encapsulate(rb);
            }
            if (has) centre = b.center;
        }
        Vector2Int cell = DistrictData.CellForPosition(centre);
        GameObject root = GetOrCreateCellRoot(cell);
        tile.transform.SetParent(root.transform, true);
    }

    /// <summary>Baked procedural city visuals in Main.unity (the scene
    /// file ships them; skipping CityVisuals.Apply() never removed them).
    /// Gameplay objects are NOT hidden: checkpoints, coins, waypoints,
    /// traffic, rivals, managers, cameras and lights stay. Gas station /
    /// garage structures are not hidden either — they are snapped onto
    /// the terrain instead (see SnapWorldObjectsToGround).</summary>
    static readonly string[] LegacyVisualPrefixes = new string[]
    {
        "Ground", "Road", "Dash", "Building", "Tower_", "DowntownTower",
        "HorizonTower", "Tree_", "Dune_", "DesertSandPatch",
    };

    static bool IsLegacyVisual(string name)
    {
        foreach (string p in LegacyVisualPrefixes)
            if (name.StartsWith(p)) return true;
        return false;
    }

    void HideLegacyCityVisuals()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        int hidden = 0;
        foreach (var go in roots)
        {
            if (go == null || !go.activeSelf) continue;
            if (go.transform == transform) continue;
            if (!IsLegacyVisual(go.name)) continue;
            go.SetActive(false);
            hidden++;
        }
        Debug.Log("[PlateauMapLoader] legacy procedural city visuals hidden ("
            + hidden + " root objects) — PLATEAU Shibuya is the city now; "
            + "the procedural code stays as the fallback if PLATEAU fails to load");
    }

    /// <summary>Shift the player and the static world dressing onto the
    /// real terrain. Runs at load time (Awake), before the first physics
    /// step, so setting transforms directly is safe.</summary>
    void SnapWorldObjectsToGround()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Vector3 p = player.transform.position;
            if (TryGetGroundHeight(p.x, p.z, out float gy))
            {
                player.transform.position = new Vector3(p.x, gy + 0.6f, p.z);
                Debug.Log("[PlateauMapLoader] player snapped to terrain y="
                    + (gy + 0.6f).ToString("0.0"));
            }
            else
            {
                Debug.LogWarning("[PlateauMapLoader] no ground under the player spawn ("
                    + p.x.ToString("0") + ", " + p.z.ToString("0")
                    + ") — left at y=" + p.y.ToString("0.0"));
            }
        }
        else
        {
            Debug.LogWarning("[PlateauMapLoader] no Player found to snap to the terrain");
        }

        // Gas station / garage: static dressing, shifted as a group by
        // the ground height at the group centre (keeps the structure
        // intact relative to the terrain).
        SnapGroupToGround(new string[]
            { "GasStationCanopy", "GasPost_0", "GasPost_1", "GasPost_2", "GasPost_3",
              "GasPump_0", "GasPump_1", "GasSign" }, -75f, -425f);
        SnapGroupToGround(new string[] { "GarageBuilding", "GarageDoor" }, 75f, -480f);

        // Race checkpoints: each gate sits on the terrain at its own XZ
        // (trigger is 16x4x16 around the object — on flat y = 0 it would
        // be buried or floating on the shifted terrain).
        for (int i = 0; i < 6; i++)
        {
            var cp = GameObject.Find("Checkpoint_0" + i);
            if (cp == null) continue;
            Vector3 p = cp.transform.position;
            if (TryGetGroundHeight(p.x, p.z, out float cy))
                cp.transform.position = new Vector3(p.x, cy, p.z);
        }
    }

    void SnapGroupToGround(string[] names, float cx, float cz)
    {
        if (!TryGetGroundHeight(cx, cz, out float gy))
        {
            Debug.LogWarning("[PlateauMapLoader] no ground under group at ("
                + cx.ToString("0") + ", " + cz.ToString("0") + ") — group left at y=0 level");
            return;
        }
        foreach (string n in names)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            Vector3 p = go.transform.position;
            go.transform.position = new Vector3(p.x, p.y + gy, p.z);
        }
    }

    /// <summary>Status line for logs / HUD debugging.</summary>
    public string StatusReport()
    {
        return string.Format("PLATEAU {0} | districts {1} | tris {2}",
            PlateauShibuyaActive ? "ON" : "fallback",
            LoadedDistricts != null ? LoadedDistricts.Count : 0,
            LoadedTriangles);
    }
}

using UnityEngine;
using UnityEngine.Rendering;

/// <summary>City visual rebuild (runtime, so the huge static Main.unity
/// file is never hand-edited): textured PBR facades with lit windows,
/// glass towers, rooftops + antennas, neon signs, asphalt roads with
/// sidewalks/curbs and streetlights, textured grass, layered trees,
/// a reflection probe for the car paint, a panoramic sky and drifting
/// clouds. Target look: Forza Horizon 1 (2012) at modern resolution.
/// No gameplay logic, positions or colliders are changed.</summary>
public static class CityVisuals
{
    static Transform root;
    static Texture2D facadeA, facadeB, facadeC, glassTower;
    static Texture2D facadeAEm, facadeBEm, facadeCEm, glassTowerEm;
    static Texture2D asphaltTex, grassTex, sidewalkTex, cloudTex, skyTex;
    static Texture2D neonTokyo, neonHorizon, neonRamen, neonSakura;
    static bool texturesLoaded;

    static void LoadTextures()
    {
        if (texturesLoaded) return;
        texturesLoaded = true;
        facadeA = Res("facadeA"); facadeB = Res("facadeB"); facadeC = Res("facadeC");
        glassTower = Res("glassTower");
        facadeAEm = Res("facadeA_emit"); facadeBEm = Res("facadeB_emit");
        facadeCEm = Res("facadeC_emit"); glassTowerEm = Res("glassTower_emit");
        asphaltTex = Res("asphalt"); grassTex = Res("grass"); sidewalkTex = Res("sidewalk");
        cloudTex = Res("cloud"); skyTex = Res("skyPanorama");
        neonTokyo = Res("neonTokyo"); neonHorizon = Res("neonHorizon");
        neonRamen = Res("neonRamen"); neonSakura = Res("neonSakura");
    }

    static Texture2D Res(string name)
    {
        Texture2D t = Resources.Load<Texture2D>("Textures/" + name);
        if (t == null) Debug.LogWarning("[CityVisuals] missing texture: " + name);
        return t;
    }

    public static void Apply()
    {
        LoadTextures();
        if (root != null) Object.Destroy(root.gameObject);
        root = new GameObject("CityVisualsRoot").transform;

        // First: push buildings off the roads. The scene file is never
        // edited — this runs at runtime, before ApplyBuildings reads
        // positions, so roof slabs, balconies and neon signs follow the
        // moved building.
        ClearRoadEdges();
        ApplySky();
        ApplyGroundAndRoads();
        ApplyBuildings();
        ApplyTrees();
        ApplyStreetlights();
        ApplyGasStation();
        ApplyClouds();
        ApplyReflectionProbe();
    }

    // ---------- Road-edge clearance ----------
    /// <summary>Is this GameObject a solid structure that can block a
    /// car? Covered by (a) building-like name or tag, or (b) any
    /// BoxCollider on a static object that is not gameplay (cars,
    /// checkpoints, coins, repairs, rivals) or roadside dressing
    /// (roads, ground, trees, waypoints, dashes, checkpoint posts and
    /// bars — none of which have colliders today, but they stay
    /// excluded if one ever gains one). The (b) fallback is what keeps
    /// a future structure covered whatever it is called: the garage
    /// was missed because (a) alone only knew "Building*"/"Tower".</summary>
    static bool IsBuildingLike(GameObject go)
    {
        string n = go.name;
        // Roads (including the Garage/Gas link driveways) and the
        // ground are never buildings — check this FIRST:
        // "RoadGarageLink" contains "Garage" and "RoadGasLink"
        // contains "Gas", so the name rules below would otherwise
        // claim them and the pass would move the road itself.
        if (n.StartsWith("Road") || n == "Ground") return false;
        if (n.StartsWith("Building") || n.Contains("Tower")
            || n.Contains("Garage") || n.Contains("Gas"))
            return true;
        if (go.tag == "Garage" || go.tag == "GasStation")
            return true;
        if (go.GetComponent<BoxCollider>() == null) return false;
        if (n.StartsWith("PlayerCar") || n.StartsWith("TrafficCar")
            || n.StartsWith("Rival_") || n.StartsWith("Checkpoint")
            || n.StartsWith("Coin_") || n.StartsWith("Repair_")
            || n.StartsWith("Tree_") || n.StartsWith("Waypoint")
            || n.StartsWith("Dash") || n == "Post" || n == "Bar")
            return false;
        return true;
    }

    static int Find(int[] parent, int i)
    {
        while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
        return i;
    }

    static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb) parent[ra] = rb;
    }

    /// <summary>Every building footprint must clear every road edge by
    /// 10 m. Measured from the scene: the four corner towers
    /// (Tower_0..3) OVERLAPPED the ring roads by 7 m, 20 mid-rise
    /// buildings sat 5 m from a road edge, and the grid buildings 8 m
    /// — at race speed a car that misses a turn crosses 5 m of grass
    /// in ~0.2 s, so every ring-corner turn threw the car into a wall
    /// (HP 100% -&gt; 0% across launches). Buildings are pushed away
    /// from the road, never deleted; structures sharing one footprint
    /// (Tower_0 + HorizonTower + its sign) move as one unit.
    /// Runs before ApplyBuildings so roofs/balconies/neon follow.
    /// Coverage (2026-11-08): the old candidate filter
    /// (StartsWith("Building") || Contains("Tower")) MISSED
    /// GarageBuilding — it starts with "Garage" — so the garage sat
    /// ON the z=-450 ring road while this pass reported "min gap
    /// 10.0 m, 0 violations". Candidates are now matched by
    /// building-like name/tag AND by solid BoxCollider (IsBuildingLike),
    /// and link/driveway roads no longer count as through-roads.</summary>
    static void ClearRoadEdges()
    {
        const float clearance = 10f;
        var roadPos = new System.Collections.Generic.List<Vector3>();
        var roadHalf = new System.Collections.Generic.List<Vector3>();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            // Link roads are driveways (RoadGasLink / RoadGarageLink):
            // they intentionally run up to — and through — their
            // building's forecourt, so they must not push that building
            // away. Only through-roads (Ring / NS / EW) clear buildings.
            if (!go.name.StartsWith("Road") || go.name.Contains("Link")) continue;
            Vector3 s = go.transform.localScale, p = go.transform.position;
            roadPos.Add(p);
            roadHalf.Add(new Vector3(Mathf.Abs(s.x) * 0.5f, 0f, Mathf.Abs(s.z) * 0.5f));
        }
        if (roadPos.Count == 0) return;

        // Every building-like object, not just names starting with
        // "Building" — see the method summary for the garage miss.
        var candidates = new System.Collections.Generic.List<GameObject>();
        var candHalf = new System.Collections.Generic.List<Vector3>();
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!IsBuildingLike(go)) continue;
            candidates.Add(go);
            Vector3 s = go.transform.localScale;
            candHalf.Add(new Vector3(Mathf.Abs(s.x) * 0.5f, 0f, Mathf.Abs(s.z) * 0.5f));
        }

        // Group structures that belong together so they move as one
        // unit: (a) same footprint centre (Tower_0 + HorizonTower +
        // its sign), (b) one footprint fully inside another (a canopy
        // and its posts/pumps/sign), (c) a thin slab (door, sign)
        // touching a face. Large boxes that merely overlap stay
        // separate: Building_00 and Tower_3 intersect at the corner
        // but are independent — grouping them would drag a building
        // that needs no push. Each group's box is the UNION of its
        // members' footprints, so a door slab sticking out of its
        // building's face is measured too.
        int n = candidates.Count;
        int[] parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                Vector3 pi = candidates[i].transform.position;
                Vector3 pj = candidates[j].transform.position;
                Vector3 hi = candHalf[i], hj = candHalf[j];
                bool sameCentre = Mathf.Abs(pi.x - pj.x) < 1f && Mathf.Abs(pi.z - pj.z) < 1f;
                bool contains = (pi.x - hi.x <= pj.x - hj.x + 0.5f && pi.x + hi.x >= pj.x + hj.x - 0.5f
                        && pi.z - hi.z <= pj.z - hj.z + 0.5f && pi.z + hi.z >= pj.z + hj.z - 0.5f)
                    || (pj.x - hj.x <= pi.x - hi.x + 0.5f && pj.x + hj.x >= pi.x + hi.x - 0.5f
                        && pj.z - hj.z <= pi.z - hi.z + 0.5f && pj.z + hj.z >= pi.z + hi.z - 0.5f);
                float gapX = Mathf.Abs(pi.x - pj.x) - (hi.x + hj.x);
                float gapZ = Mathf.Abs(pi.z - pj.z) - (hi.z + hj.z);
                bool thinTouch = (Mathf.Min(hi.x, hi.z) <= 1.5f || Mathf.Min(hj.x, hj.z) <= 1.5f)
                    && gapX <= 0f && gapZ <= 0f;
                if (sameCentre || contains || thinTouch) Union(parent, i, j);
            }
        var groups = new System.Collections.Generic.List<System.Collections.Generic.List<GameObject>>();
        var groupCentre = new System.Collections.Generic.List<Vector3>();
        var groupHalf = new System.Collections.Generic.List<Vector3>();
        var groupMin = new System.Collections.Generic.List<Vector3>();
        var groupMax = new System.Collections.Generic.List<Vector3>();
        var rootToGroup = new System.Collections.Generic.Dictionary<int, int>();
        for (int i = 0; i < n; i++)
        {
            int r = Find(parent, i);
            Vector3 p = candidates[i].transform.position;
            Vector3 h = candHalf[i];
            Vector3 mn = new Vector3(p.x - h.x, 0f, p.z - h.z);
            Vector3 mx = new Vector3(p.x + h.x, 0f, p.z + h.z);
            int g;
            if (!rootToGroup.TryGetValue(r, out g))
            {
                g = groups.Count;
                rootToGroup.Add(r, g);
                groups.Add(new System.Collections.Generic.List<GameObject>());
                groupMin.Add(mn); groupMax.Add(mx);
                groupCentre.Add(Vector3.zero); groupHalf.Add(Vector3.zero);
            }
            groups[g].Add(candidates[i]);
            Vector3 omin = groupMin[g], omax = groupMax[g];
            mn = new Vector3(Mathf.Min(omin.x, mn.x), 0f, Mathf.Min(omin.z, mn.z));
            mx = new Vector3(Mathf.Max(omax.x, mx.x), 0f, Mathf.Max(omax.z, mx.z));
            groupMin[g] = mn; groupMax[g] = mx;
            groupCentre[g] = new Vector3((mn.x + mx.x) * 0.5f, 0f, (mn.z + mx.z) * 0.5f);
            groupHalf[g] = new Vector3((mx.x - mn.x) * 0.5f, 0f, (mx.z - mn.z) * 0.5f);
        }

        int movedGroups = 0, movedObjects = 0;
        var groupDelta = new System.Collections.Generic.List<Vector3>();
        for (int gi = 0; gi < groups.Count; gi++) groupDelta.Add(Vector3.zero);
        for (int gi = 0; gi < groups.Count; gi++)
        {
            Vector3 centre = groupCentre[gi];
            Vector3 half = groupHalf[gi];
            Vector3 delta = Vector3.zero;
            // 3 passes: a corner structure is close to two roads at once
            // and needs a push on both axes; each push only increases
            // the separation from that road, so this converges.
            for (int pass = 0; pass < 3; pass++)
            {
                bool any = false;
                for (int ri = 0; ri < roadPos.Count; ri++)
                {
                    Vector3 rp = roadPos[ri], rh = roadHalf[ri];
                    float dx = (centre.x + delta.x) - rp.x;
                    float dz = (centre.z + delta.z) - rp.z;
                    float sepX = Mathf.Abs(dx) - (half.x + rh.x);
                    float sepZ = Mathf.Abs(dz) - (half.z + rh.z);
                    if (sepX >= clearance || sepZ >= clearance) continue;
                    float pushX = 0f, pushZ = 0f;
                    if (sepX > 0f && sepZ > 0f)
                    {
                        // Diagonal: push along the axis needing the smaller move.
                        float needX = Mathf.Sqrt(clearance * clearance - sepZ * sepZ) - sepX;
                        float needZ = Mathf.Sqrt(clearance * clearance - sepX * sepX) - sepZ;
                        if (needX <= needZ) pushX = needX; else pushZ = needZ;
                    }
                    else if (sepX > 0f) pushX = clearance - sepX;
                    else if (sepZ > 0f) pushZ = clearance - sepZ;
                    else if (-sepX <= -sepZ) pushX = clearance - sepX;
                    else pushZ = clearance - sepZ;
                    if (pushX > 0f) { delta.x += (dx >= 0f ? 1f : -1f) * pushX; any = true; }
                    if (pushZ > 0f) { delta.z += (dz >= 0f ? 1f : -1f) * pushZ; any = true; }
                }
                if (!any) break;
            }
            if (delta.sqrMagnitude > 0.0001f)
            {
                foreach (GameObject go in groups[gi])
                    go.transform.position += delta;
                groupDelta[gi] = delta;
                movedGroups++;
                movedObjects += groups[gi].Count;
                if (movedGroups <= 12)
                    Debug.Log($"[CityVisuals] road clearance: moved {groups[gi][0].name} ({centre.x:F0},{centre.z:F0}) by ({delta.x:F1},{delta.z:F1})");
            }
        }
        // Verify: re-measure the minimum gap of every group to every road.
        float minGap = float.MaxValue;
        int violations = 0;
        for (int gi = 0; gi < groups.Count; gi++)
        {
            Vector3 c = groupCentre[gi] + groupDelta[gi];
            Vector3 half = groupHalf[gi];
            for (int ri = 0; ri < roadPos.Count; ri++)
            {
                float sepX = Mathf.Abs(c.x - roadPos[ri].x) - (half.x + roadHalf[ri].x);
                float sepZ = Mathf.Abs(c.z - roadPos[ri].z) - (half.z + roadHalf[ri].z);
                float gap = (sepX > 0f && sepZ > 0f) ? Mathf.Sqrt(sepX * sepX + sepZ * sepZ)
                    : (sepX > 0f || sepZ > 0f) ? Mathf.Max(sepX, sepZ)
                    : -Mathf.Min(-sepX, -sepZ);
                if (gap < minGap) minGap = gap;
                if (gap < clearance - 0.01f) violations++;
            }
        }
        Debug.Log($"[CityVisuals] road-edge clearance: {movedGroups} structure groups ({movedObjects} objects) moved, target {clearance} m; min gap now {minGap:F1} m, violations: {violations}; checked {n} building-like objects in {groups.Count} groups");
    }

    // ---------- Sky / atmosphere ----------
    // The panorama is shown on a camera-following dome sphere. It MUST
    // use a real Unlit material asset (Assets/Resources/Materials/
    // SkyPanorama.mat, created by the FixMenu720p editor step): the old
    // code built a URP Lit material at runtime with emission, but the
    // _EMISSION shader variant is stripped from the player build (no
    // asset references it), so the dome rendered black and hid the
    // skybox — the black sky in every round-3 screenshot, even at noon.
    // An asset material keeps its shader variant in the build. If the
    // asset is ever missing, no dome is created at all, so the scene's
    // default skybox shows instead of a black dome (never black sky).
    public static void ApplySky()
    {
        LoadTextures();
        Material skyMat = Resources.Load<Material>("Materials/SkyPanorama");
        if (skyMat != null && skyMat.GetTexture("_BaseMap") == null && skyTex != null)
            skyMat.SetTexture("_BaseMap", skyTex);
        GameObject old = GameObject.Find("SkyDome");
        if (skyMat != null)
        {
            if (old == null)
            {
                GameObject dome = MeshFactory.Part("SkyDome", "Sphere.fbx", null,
                    Vector3.zero, new Vector3(700f, 700f, 700f), skyMat);
                dome.AddComponent<SkyDome>();
                MeshRenderer dr = dome.GetComponent<MeshRenderer>();
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dr.receiveShadows = false;
            }
            else
            {
                MeshRenderer mr = old.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = skyMat;
            }
        }
        else if (old != null)
        {
            Object.Destroy(old);
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.68f, 0.90f);
        RenderSettings.ambientEquatorColor = new Color(0.45f, 0.48f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.30f, 0.28f, 0.25f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0012f;
        RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.86f);
        Light sun = FindSun();
        if (sun != null) sun.intensity = 1.35f;
    }

    static Light FindSun()
    {
        GameObject go = GameObject.Find("Directional Light");
        return go != null ? go.GetComponent<Light>() : null;
    }

    // ---------- Ground + roads ----------
    static void ApplyGroundAndRoads()
    {
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.name == "Ground")
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null && grassTex != null)
                {
                    Material m = MeshFactory.Lit("GrassTex", Color.white, 0f, 0.15f);
                    m.SetTexture("_BaseMap", grassTex);
                    m.mainTextureScale = new Vector2(80f, 80f);
                    r.sharedMaterial = m;
                }
            }
            else if (go.name.StartsWith("Road"))
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r == null || asphaltTex == null) continue;
                Vector3 s = go.transform.localScale;
                float len = Mathf.Max(s.x, s.z), wid = Mathf.Min(s.x, s.z);
                Material m = MeshFactory.Lit("AsphaltTex", Color.white, 0.12f, 0.4f);
                m.SetTexture("_BaseMap", asphaltTex);
                m.mainTextureScale = new Vector2(Mathf.Max(1f, wid / 7f), Mathf.Max(1f, len / 7f));
                r.sharedMaterial = m;
                AddCurbs(go, len, wid);
            }
        }
    }

    static void AddCurbs(GameObject road, float len, float wid)
    {
        if (sidewalkTex == null) return;
        bool ns = road.transform.localScale.z >= road.transform.localScale.x;
        Vector3 p = road.transform.position;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 pos, scale;
            if (ns)
            {
                pos = new Vector3(p.x + side * (wid / 2f + 1.1f), 0.09f, p.z);
                scale = new Vector3(1.8f, 0.18f, len);
            }
            else
            {
                pos = new Vector3(p.x, 0.09f, p.z + side * (wid / 2f + 1.1f));
                scale = new Vector3(len, 0.18f, 1.8f);
            }
            // Rounded curb slab, not a sharp box edge.
            GameObject curb = MeshFactory.PartRounded("Sidewalk", root, pos, scale, 0.05f,
                SidewalkMaterial(len));
            curb.transform.localPosition = pos;
        }
    }

    static Material sidewalkMat;
    static Material SidewalkMaterial(float len)
    {
        if (sidewalkMat == null && sidewalkTex != null)
        {
            sidewalkMat = MeshFactory.Lit("SidewalkTex", Color.white, 0f, 0.3f);
            sidewalkMat.SetTexture("_BaseMap", sidewalkTex);
        }
        if (sidewalkMat != null) sidewalkMat.mainTextureScale = new Vector2(1f, Mathf.Max(1f, len / 7f));
        return sidewalkMat;
    }

    // ---------- Buildings ----------
    static void ApplyBuildings()
    {
        int modelCount = 0, segmentCount = 0, neonCount = 0;
        Texture2D[] neons = { neonTokyo, neonHorizon, neonRamen, neonSakura };
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("Building") && !go.name.Contains("Tower")) continue;
            Renderer r = go.GetComponent<Renderer>();
            if (r == null) continue;
            Vector3 s = go.transform.localScale;
            Vector3 p = go.transform.position;
            float h = s.y, w = s.x, d = s.z;
            bool tower = go.name.Contains("Tower") || h > 40f;
            int hash = Mathf.Abs(go.GetInstanceID());
            // Hide the box; its collider stays for gameplay.
            r.enabled = false;
            string kit, model;
            if (tower) { kit = "CityCommercial"; model = "building-skyscraper-" + (char)('a' + hash % 5); }
            else if (h >= 18f) { kit = "CityCommercial"; model = "building-" + (char)('a' + hash % 14); }
            else if (h >= 10f)
            {
                if (hash % 2 == 0) { kit = "CityCommercial"; model = "low-detail-building-" + (char)('a' + hash % 14); }
                else { kit = "CityIndustrial"; model = "building-" + (char)('a' + hash % 10); }
            }
            else { kit = "CitySuburban"; model = "building-type-" + (char)('a' + hash % 10); }
            segmentCount += PlaceBuildingModel(kit, model, p, w, h, d);
            modelCount++;
            // Neon sign on tall buildings near the centre (max 12).
            if (!tower && h >= 18f && neonCount < 12 &&
                Mathf.Abs(p.x) < 380f && Mathf.Abs(p.z) < 380f)
            {
                Texture2D neon = neons[neonCount % neons.Length];
                if (neon != null)
                {
                    Vector3 dir = (Vector3.zero - new Vector3(p.x, 0f, p.z)).normalized;
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                    Vector3 signPos = new Vector3(p.x, p.y + h * 0.15f, p.z)
                        + dir * (Mathf.Max(w, d) / 2f + 0.4f);
                    GameObject sign = MeshFactory.Part("NeonSign", "Quad.fbx", root,
                        signPos, new Vector3(11f, 3.4f, 1f), NeonMaterial(neon));
                    sign.transform.LookAt(new Vector3(0f, signPos.y, 0f));
                    neonCount++;
                }
            }
        }
        Debug.Log("[CityVisuals] Kenney buildings placed: " + modelCount + " footprints, "
            + segmentCount + " segments, neon signs: " + neonCount);
    }

    static int PlaceBuildingModel(string kit, string model, Vector3 p, float w, float h, float d)
    {
        GameObject prefab = KenneyModels.LoadPrefab("Models/Kenney/" + kit + "/" + model);
        if (prefab == null) return 0;
        // Natural size: instantiate once at scale 1 and measure.
        GameObject probe = Object.Instantiate(prefab, root, false);
        probe.transform.position = p;
        Bounds nb = KenneyModels.Measure(probe);
        Object.DestroyImmediate(probe);
        if (nb.size.y < 0.5f || nb.size.x < 0.5f || nb.size.z < 0.5f) return 0;
        int k = Mathf.Clamp(Mathf.RoundToInt(h / nb.size.y), 1, 10);
        float segH = h / k;
        float baseY = p.y - h / 2f;
        Material mat = KenneyModels.AtlasMaterial(kit, 0.05f, 0.3f);
        for (int i = 0; i < k; i++)
        {
            GameObject inst = Object.Instantiate(prefab, root, false);
            inst.name = "KenneyBuilding_" + model + "_" + i;
            inst.transform.position = new Vector3(p.x, baseY + i * segH, p.z);
            inst.transform.localScale = new Vector3(w / nb.size.x, segH / nb.size.y, d / nb.size.z);
            KenneyModels.ApplyMaterial(inst, mat);
            // Re-centre: the model origin may not be at its base centre.
            Bounds b = KenneyModels.Measure(inst);
            inst.transform.position += new Vector3(p.x - b.center.x, (baseY + i * segH) - b.min.y, p.z - b.center.z);
        }
        return k;
    }


    static Material NeonMaterial(Texture2D tex)
    {
        Material m = MeshFactory.Lit("Neon", Color.black, 0f, 0.4f);
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_EmissionMap", tex);
        m.SetColor("_EmissionColor", Color.white * 2.4f);
        m.EnableKeyword("_EMISSION");
        return m;
    }

    // ---------- Trees ----------
    static void ApplyTrees()
    {
        string[] models = { "tree_default", "tree_oak", "tree_pineDefaultA", "tree_cone",
            "tree_palm", "tree_detailed", "tree_fat", "tree_simple" };
        int placed = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("Tree_")) continue;
            int hash = Mathf.Abs(go.GetInstanceID());
            Transform trunk = go.transform.Find("Trunk");
            if (trunk != null) trunk.gameObject.SetActive(false);
            Transform leaves = go.transform.Find("Leaves");
            if (leaves != null) leaves.gameObject.SetActive(false);
            string model = models[hash % models.Length];
            // Seasonal variants exist for most trees; fall back to the
            // base model when a variant was not imported.
            string variant = model;
            if (hash % 9 == 0) variant = model + "_fall";
            else if (hash % 11 == 0) variant = model + "_dark";
            GameObject prefab = KenneyModels.LoadPrefab("Models/Kenney/NatureKit/" + variant);
            if (prefab == null) prefab = KenneyModels.LoadPrefab("Models/Kenney/NatureKit/" + model);
            if (prefab == null) continue;
            GameObject inst = Object.Instantiate(prefab, go.transform, false);
            inst.name = "KenneyTree";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, hash % 360, 0f);
            // Target height ~5 m times the tree object's own scale.
            float target = 5f * Mathf.Max(0.5f, go.transform.localScale.y);
            Bounds b = KenneyModels.Measure(inst);
            if (b.size.y > 0.01f)
            {
                float s = target / b.size.y;
                inst.transform.localScale = Vector3.one * s;
                // Ground the tree: shift so its base sits at the object's
                // ground plane (world y of the tree object).
                Bounds b2 = KenneyModels.Measure(inst);
                float dy = (go.transform.position.y - b2.min.y) / Mathf.Max(0.01f, go.transform.lossyScale.y);
                inst.transform.localPosition += new Vector3(0f, dy, 0f);
            }
            KenneyModels.ConvertColorMaterials(inst, 0f, 0.25f);
            placed++;
        }
        Debug.Log("[CityVisuals] Kenney trees placed: " + placed);
    }


    // ---------- Streetlights + road signs ----------
    static void ApplyStreetlights()
    {
        Material roadAtlas = KenneyModels.AtlasMaterial("CityRoads", 0.1f, 0.4f);
        Material poleAtlas = KenneyModels.AtlasMaterial("CityRoads", 0.4f, 0.5f);
        int placed = 0, signCount = 0, lightCount = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("Road")) continue;
            Vector3 s = go.transform.localScale, p = go.transform.position;
            float len = Mathf.Max(s.x, s.z), wid = Mathf.Min(s.x, s.z);
            if (len < 200f) continue;
            bool ns = s.z >= s.x;
            int side = (Mathf.Abs(go.GetInstanceID()) % 2 == 0) ? 1 : -1;
            for (float d = -len / 2f + 60f; d < len / 2f - 40f; d += 120f)
            {
                Vector3 basePos = ns
                    ? new Vector3(p.x + side * (wid / 2f + 1.6f), 0f, p.z + d)
                    : new Vector3(p.x + d, 0f, p.z + side * (wid / 2f + 1.6f));
                // Real Kenney street light. The racing-kit lightPostModern
                // is 7.8 m tall naturally (the city-roads light-curved is
                // a 0.67 m miniature); its origin is offset, so the model
                // is centred on its bounds (pole lands within ~1 m of the
                // sidewalk spot, arm over the road).
                GameObject lamp = PlaceModel("Models/Kenney/RacingKit/lightPostModern",
                    basePos, Quaternion.Euler(0f, ns ? (side > 0 ? 90f : -90f) : (side > 0 ? 0f : 180f), 0f));
                if (lamp != null)
                {
                    KenneyModels.ScaleToHeight(lamp, 7f);
                    KenneyModels.PlaceOnGround(lamp, basePos.x, 0f, basePos.z);
                    KenneyModels.ConvertColorMaterials(lamp, 0.4f, 0.5f);
                    placed++;
                }
                if (placed % 3 == 0 && signCount < 24)
                {
                    float yaw = ns ? (side > 0 ? -90f : 90f) : (side > 0 ? 0f : 180f);
                    GameObject sign = PlaceModel("Models/Kenney/CityRoads/road-sign-street",
                        basePos, Quaternion.Euler(0f, yaw, 0f));
                    if (sign != null)
                    {
                        KenneyModels.ScaleToHeight(sign, 2.6f);
                        KenneyModels.GroundY(sign, 0f);
                        KenneyModels.ApplyMaterial(sign, roadAtlas);
                        signCount++;
                    }
                }
            }
            // Traffic lights at both ends of each long road.
            for (int e = 0; e < 2; e++)
            {
                float d = (e == 0 ? -len / 2f + 8f : len / 2f - 8f);
                Vector3 tp = ns ? new Vector3(p.x + wid / 2f + 1.6f, 0f, p.z + d)
                    : new Vector3(p.x + d, 0f, p.z + wid / 2f + 1.6f);
                GameObject tl = PlaceModel("Models/Kenney/CityRoads/traffic-light",
                    tp, Quaternion.Euler(0f, ns ? 90f : 0f, 0f));
                if (tl != null)
                {
                    KenneyModels.ScaleToHeight(tl, 5f);
                    KenneyModels.GroundY(tl, 0f);
                    KenneyModels.ApplyMaterial(tl, roadAtlas);
                    lightCount++;
                }
            }
        }
        Debug.Log("[CityVisuals] Kenney streetlights: " + placed + ", road signs: "
            + signCount + ", traffic lights: " + lightCount);
    }

    static void ApplyGasStation()
    {
        foreach (string n in new string[] { "GasStationCanopy", "GasPost_0", "GasPost_1",
            "GasPost_2", "GasPost_3", "GasPump_0", "GasPump_1", "GasSign" })
        {
            GameObject go = GameObject.Find(n);
            if (go == null) continue;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }
        // NOTE: every racing-kit model is offset ~11 m in +Z from its
        // origin (Kenney's FBX root), so each one is centred on its
        // bounds with PlaceOnGround after scaling — plain GroundY would
        // leave the garage sitting on the pumps.
        // Canopy: tent over the pump area (-75, -425), 30 x 20 m, ~6 m high.
        GameObject canopy = PlaceModel("Models/Kenney/RacingKit/tent",
            new Vector3(-75f, 0f, -425f), Quaternion.identity);
        if (canopy != null)
        {
            Bounds b = KenneyModels.Measure(canopy);
            canopy.transform.localScale = new Vector3(30f / b.size.x, 6f / b.size.y, 20f / b.size.z);
            KenneyModels.PlaceOnGround(canopy, -75f, 0f, -425f);
            KenneyModels.ConvertColorMaterials(canopy, 0.1f, 0.4f);
        }
        // Station building + shop behind the canopy.
        GameObject garage = PlaceModel("Models/Kenney/RacingKit/pitsGarage",
            new Vector3(-75f, 0f, -447f), Quaternion.identity);
        if (garage != null)
        {
            KenneyModels.ScaleToHeight(garage, 7f);
            KenneyModels.PlaceOnGround(garage, -75f, 0f, -447f);
            KenneyModels.ConvertColorMaterials(garage, 0.1f, 0.4f);
        }
        GameObject office = PlaceModel("Models/Kenney/RacingKit/pitsOffice",
            new Vector3(-55f, 0f, -445f), Quaternion.Euler(0f, -90f, 0f));
        if (office != null)
        {
            KenneyModels.ScaleToHeight(office, 6f);
            KenneyModels.PlaceOnGround(office, -55f, 0f, -445f);
            KenneyModels.ConvertColorMaterials(office, 0.1f, 0.4f);
        }
        // Road sign at the north edge of the forecourt.
        GameObject sign = PlaceModel("Models/Kenney/RacingKit/billboard",
            new Vector3(-75f, 0f, -406f), Quaternion.identity);
        if (sign != null)
        {
            KenneyModels.ScaleToHeight(sign, 8f);
            KenneyModels.PlaceOnGround(sign, -75f, 0f, -406f);
            KenneyModels.ConvertColorMaterials(sign, 0.1f, 0.4f);
        }
        // Fuel tank + pump islands where the dispensers would be.
        GameObject tank = PlaceModel("Models/Kenney/CityIndustrial/detail-tank-large",
            new Vector3(-88f, 0f, -418f), Quaternion.identity);
        if (tank != null)
        {
            KenneyModels.ScaleToHeight(tank, 4f);
            KenneyModels.PlaceOnGround(tank, -88f, 0f, -418f);
            KenneyModels.ApplyMaterial(tank, KenneyModels.AtlasMaterial("CityIndustrial", 0.4f, 0.5f));
        }
        foreach (float px in new float[] { -80f, -70f })
        {
            GameObject island = PlaceModel("Models/Kenney/RacingKit/barrierWhite",
                new Vector3(px, 0f, -425f), Quaternion.identity);
            if (island != null)
            {
                KenneyModels.ScaleToHeight(island, 1.2f);
                KenneyModels.PlaceOnGround(island, px, 0f, -425f);
                KenneyModels.ConvertColorMaterials(island, 0.1f, 0.4f);
            }
        }
        Debug.Log("[CityVisuals] gas station assembled from kit parts (tent canopy + pitsGarage + pitsOffice + billboard + tank; no dispenser model exists)");
    }

    static GameObject PlaceModel(string resourcePath, Vector3 pos, Quaternion rot)
    {
        GameObject prefab = KenneyModels.LoadPrefab(resourcePath);
        if (prefab == null) return null;
        GameObject inst = Object.Instantiate(prefab, root, false);
        inst.transform.position = pos;
        inst.transform.rotation = rot;
        return inst;
    }


    // ---------- Clouds ----------
    // Lit transparent (Unlit is stripped from the player build — see sky).
    static void ApplyClouds()
    {
        if (cloudTex == null) return;
        for (int i = 0; i < 8; i++)
        {
            Material m = MeshFactory.Lit("Cloud", new Color(1f, 1f, 1f, 0.8f), 0f, 0f);
            m.SetTexture("_BaseMap", cloudTex);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 1f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetInt("_Cull", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
            GameObject c = MeshFactory.Part("Cloud", "Plane.fbx", root,
                new Vector3(-500f + i * 140f, 130f + (i % 3) * 28f, -260f + (i % 4) * 170f),
                new Vector3(28f, 1f, 14f), m, new Vector3(-90f, 0f, 0f));
            CloudDrift drift = c.AddComponent<CloudDrift>();
            drift.speed = 1.5f + (i % 3);
        }
    }

    // ---------- Reflection probe (car paint needs an environment) ----------
    static void ApplyReflectionProbe()
    {
        GameObject go = new GameObject("CityReflectionProbe");
        go.transform.SetParent(root, false);
        go.transform.position = new Vector3(0f, 30f, -40f);
        ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.resolution = 128;
        probe.size = new Vector3(600f, 150f, 600f);
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        try { probe.RenderProbe(); }
        catch (System.Exception e) { Debug.LogWarning("[CityVisuals] RenderProbe failed: " + e.Message); }
    }
}

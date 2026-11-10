using System.Collections.Generic;
using UnityEngine;

/// <summary>Connective terrain for the 60 sq km map (2026-11-09, 60 sq km
/// dense layout + 22:59 roads/forests/mountains/sakura direction).
///
/// WHAT IT BUILDS (all at load time, always loaded — roads + ground are
/// the cheap shared skeleton, never streamed):
///   1. GAP GROUND — 18 rectangles that tile exactly the world minus the
///      six dem squares + the Ginza fill (full coverage inside +/-3850 m,
///      no void anywhere the player can drive). Measured dem squares in
///      game coords (from plateau-out/*/*/dem/*.report.json bounds_unity
///      + the PlateauMap offsets; re-measure after any re-conversion):
///        Shibuya    x [-1016, 1016]  z [-1014,  797]
///        Shinjuku   x [ 1016, 3042]  z [  815, 2845]
///        Asakusa    x [ -917,  923]  z [  845, 2661]
///        Akihabara  x [ 1137, 2959]  z [-2865, -1031]
///        Tokyo Bay  x [-1117, 1113]  z [-3273, -1041]
///        Ueno       x [-2862, -1042]  z [  842, 2672]
///        Ginza FILL x [ 1000, 3000]  z [-1000, 1000]  (no dem yet)
///      When the real Ginza dem arrives, Build() subtracts the real
///      DemBounds rects from every gap rect (runtime rect subtraction),
///      so the Ginza fill cells shrink to exclude it — no void, no
///      double ground, no code change needed.
///   2. MOUNTAINS — fill vertices rise to MapTerrain.MountainHeight
///      (ring: NW touge 220 m, SE 170 m, S 120 m, SW 100 m, west
///      foothills 0-40 m; NO NE mountain — Shinjuku tiles cover the NE
///      to the world edge). The touge spline ridge makes the terrain
///      hug the pass road; vertices within 15 m of the spline snap to
///      the exact spline height. Grid is 25 m (not 50 m) so the ridge
///      reads as a mountain, not a smooth hill.
///   3. ROADS — a complete street network, not just gap ribbons:
///      the RING HIGHWAY (24 m, 8 segments looping the district
///      centres: Shibuya-Shinjuku-Asakusa-Ueno-Residential-TokyoBay-
///      Akihabara-Ginza-Shibuya), the 16 m spokes, the Ginza street
///      grid and the Residential street grid, plus the 12 m touge
///      access road (Ueno -> touge gate). Every ribbon carries painted
///      markings: white centre dashes on all roads, solid edge lines
///      on the ring. Ribbons run the FULL spoke (centre to centre),
///      following the real ground (TryGetGroundHeight, so they drape
///      over dems and mountains) — EXCEPT across the Shibuya and
///      Tokyo Bay dems, where the real PLATEAU tran road meshes exist
///      and the ribbon is clipped out (no double road surface).
///      The other four districts have NO tran meshes at all (never
///      converted — see PlateauMap.cs), so the ribbons ARE their
///      street network.
///
/// HOW (heights): ONE shared heightfield, so adjacent rectangles agree
/// exactly on shared edges (no steps between connectors) and every
/// seam meets the dem at the dem's own height. The heightfield is built
/// from boundary samples: the ground height sampled every 50 m along
/// every dem square's edges (1 m inside the square, highest dem/tran
/// hit = the driving surface), then inverse-distance weighting (exact
/// at a sample, smooth blend between edges). Samples are bucketed in
/// 1 km cells so a query only scans its 3x3 neighbourhood.
///
/// LIMITS (not hidden): the dem edge undulates between the 50 m
/// samples, so a seam can still carry a lip of a few tens of cm where
/// the terrain is rough — drivable, not a wall. Ring/spoke ribbons
/// cross building-tile BOUNDS on the Ginza boulevard (Shinjuku tiles
/// 53394516/17); the ribbon is a ground-level skin and the real street
/// runs between the buildings inside those tiles, but a ribbon wider
/// than the real street can clip a building base that fronts the
/// street — check the boulevard in the verification photos; if it
/// clips, narrow Road_East to 12 m on the tile-crossing stretch.</summary>
public static class ConnectiveTerrain
{
    /// <summary>One gap rectangle, game XZ, edges on dem-square bounds.</summary>
    struct GapRect
    {
        public string name;
        public float x0, z0, x1, z1;
        public GapRect(string name, float x0, float z0, float x1, float z1)
        {
            this.name = name; this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1;
        }
    }

    /// <summary>18 rects, world minus the six dems + Ginza fill
    /// (from compute_new_layout.py, 60 sq km layout). Area 36.50 km2.</summary>
    static readonly GapRect[] Rects = new GapRect[]
    {
        new GapRect("WestBand", -3850f, -3850f, -2862.2f, 3850f),
        new GapRect("SouthWest", -2862.2f, -3850f, -1117.5f, 842.2f),
        new GapRect("SouthStrip", -1117.5f, -3850f, 3850f, -3273.1f),
        new GapRect("TokyoBay_Akihabara_Slit", 1112.5f, -3273.1f, 1137f, 814.6f),
        new GapRect("SouthEast", 1137f, -3273.1f, 3850f, -2865.2f),
        new GapRect("EastBand", 2959f, -2865.2f, 3850f, 814.6f),
        new GapRect("Shibuya_TokyoBay_WestSlit", -1117.5f, -1041.1f, -1016.2f, 842.2f),
        new GapRect("Shibuya_TokyoBay_SouthSlit", -1016.2f, -1041.1f, 1112.5f, -1014.4f),
        new GapRect("Ginza_Akihabara_Fill", 1137f, -1031.2f, 2959f, 814.6f),
        new GapRect("Shibuya_Ginza_Slit", 1016.2f, -1014.4f, 1112.5f, 814.6f),
        new GapRect("Shibuya_Asakusa_WestSlit", -1016.2f, 796.5f, -917.5f, 3850f),
        new GapRect("Shibuya_Asakusa_Seam", -917.5f, 796.5f, 1015.8f, 845.1f),
        new GapRect("Shinjuku_EastFill", 3042.2f, 814.6f, 3850f, 2844.6f),
        new GapRect("Ueno_WestSlit", -1042.2f, 842.2f, -1016.2f, 3850f),
        new GapRect("Asakusa_Shinjuku_Slit", 922.5f, 845.1f, 1015.8f, 3850f),
        new GapRect("NorthStrip", -917.5f, 2661.1f, 922.5f, 3850f),
        new GapRect("Ueno_NorthFill", -2862.2f, 2672.2f, -1042.2f, 3850f),
        new GapRect("NorthEast", 1016.2f, 2844.6f, 3850f, 3850f),
    };

    /// <summary>Road ribbon: full spoke/grid street, game XZ.</summary>
    struct Ribbon
    {
        public string name;
        public Vector2 from, to;
        public float width;
        public bool ring;
        public Ribbon(string name, Vector2 from, Vector2 to, float width, bool ring)
        {
            this.name = name; this.from = from; this.to = to; this.width = width; this.ring = ring;
        }
    }

    /// <summary>24 ribbons, ~52.1 km total (from compute_new_layout.py).
    /// Ring = the 8-segment loop through the district centres (24 m).</summary>
    static readonly Ribbon[] Ribbons = new Ribbon[]
    {
        new Ribbon("Road_North", new Vector2(0f, 0f), new Vector2(0f, 1750f), 16f, false),
        new Ribbon("Road_South", new Vector2(0f, 0f), new Vector2(0f, -2160f), 16f, false),
        new Ribbon("Road_East", new Vector2(0f, 0f), new Vector2(2000f, 0f), 24f, true),
        new Ribbon("Road_West", new Vector2(0f, 0f), new Vector2(-1950f, 1750f), 16f, false),
        new Ribbon("Road_NE", new Vector2(0f, 0f), new Vector2(2030f, 1830f), 24f, true),
        new Ribbon("Road_SE", new Vector2(0f, 0f), new Vector2(2050f, -1950f), 16f, false),
        new Ribbon("Road_Asakusa_Shinjuku", new Vector2(0f, 1750f), new Vector2(2030f, 1830f), 24f, true),
        new Ribbon("Road_Asakusa_Ueno", new Vector2(0f, 1750f), new Vector2(-1950f, 1750f), 24f, true),
        new Ribbon("Road_Akihabara_TokyoBay", new Vector2(2050f, -1950f), new Vector2(0f, -2160f), 24f, true),
        new Ribbon("Road_Ginza_Shinjuku", new Vector2(2000f, 0f), new Vector2(2030f, 1830f), 16f, false),
        new Ribbon("Road_Ginza_Akihabara", new Vector2(2000f, 0f), new Vector2(2050f, -1950f), 24f, true),
        new Ribbon("Road_Ueno_Residential", new Vector2(-1950f, 1750f), new Vector2(-2400f, -2400f), 24f, true),
        new Ribbon("Road_Residential_TokyoBay", new Vector2(-2400f, -2400f), new Vector2(0f, -2160f), 24f, true),
        new Ribbon("Road_Touge_Access", new Vector2(-1950f, 1750f), new Vector2(-2900f, 2400f), 12f, false),
        new Ribbon("Road_Ginza_NS1", new Vector2(1500f, -900f), new Vector2(1500f, 900f), 16f, false),
        new Ribbon("Road_Ginza_NS2", new Vector2(2500f, -900f), new Vector2(2500f, 900f), 16f, false),
        new Ribbon("Road_Ginza_EW1", new Vector2(1100f, -500f), new Vector2(2900f, -500f), 16f, false),
        new Ribbon("Road_Ginza_EW2", new Vector2(1100f, 500f), new Vector2(2900f, 500f), 16f, false),
        new Ribbon("Road_Res_NS1", new Vector2(-1800f, -3500f), new Vector2(-1800f, -1300f), 12f, false),
        new Ribbon("Road_Res_NS2", new Vector2(-2400f, -3500f), new Vector2(-2400f, -1300f), 12f, false),
        new Ribbon("Road_Res_NS3", new Vector2(-3000f, -3500f), new Vector2(-3000f, -1300f), 12f, false),
        new Ribbon("Road_Res_EW1", new Vector2(-3500f, -1800f), new Vector2(-1300f, -1800f), 12f, false),
        new Ribbon("Road_Res_EW2", new Vector2(-3500f, -2400f), new Vector2(-1300f, -2400f), 12f, false),
        new Ribbon("Road_Res_EW3", new Vector2(-3500f, -3000f), new Vector2(-1300f, -3000f), 12f, false),
    };

    const float RibbonLift = 0.06f;
    const float MarkingLift = 0.02f; // markings sit 2 cm above the ribbon
    const float GridSpacing = 25f;   // fill grid (mountains need 25 m, not 50 m)
    const float SampleSpacing = 50f; // dem-edge boundary samples
    const float MinPiece = 1f;       // rect-subtraction drops pieces < 1 m

    static Material groundMat;
    static Material roadMat;
    static Material markingMat;

    /// <summary>Build all gap ground (with mountains) + the road network
    /// under a "ConnectiveTerrain" root (always active, never streamed).</summary>
    public static void Build(PlateauMapLoader loader)
    {
        if (loader == null) return;
        if (loader.GroundColliders == null || loader.GroundColliders.Count == 0)
        {
            Debug.LogWarning("[ConnectiveTerrain] no ground colliders — gap terrain skipped, voids stay");
            return;
        }

        var hf = BuildHeightfield(loader);
        if (hf == null || hf.SampleCount == 0)
        {
            Debug.LogWarning("[ConnectiveTerrain] no boundary samples — gap terrain skipped, voids stay");
            return;
        }

        var parent = new GameObject("ConnectiveTerrain").transform;
        parent.SetParent(loader.transform, false);
        parent.gameObject.isStatic = true;

        // Dem rects the fill must NOT cover (real Ginza dem, when it
        // arrives, shrinks the Ginza fill cells automatically).
        var demRects = new List<Rect>();
        if (loader.DemBounds != null)
            foreach (KeyValuePair<DistrictData.DistrictId, Bounds> kv in loader.DemBounds)
                demRects.Add(new Rect(kv.Value.min.x, kv.Value.min.z,
                    kv.Value.size.x, kv.Value.size.z));

        int tris = 0;
        int rectsBuilt = 0;
        foreach (GapRect r in Rects)
        {
            foreach (GapRect piece in SubtractDem(r, demRects))
            {
                GameObject go = BuildRectMesh(piece, hf, GroundMaterial(), parent);
                if (go == null) continue;
                rectsBuilt++;
                var col = go.GetComponent<MeshCollider>();
                if (col != null) loader.RegisterGroundCollider(col);
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            }
        }
        int ribbonsBuilt = 0;
        foreach (Ribbon rb in Ribbons)
        {
            foreach (Ribbon sub in ClipByTranDems(rb, loader))
            {
                GameObject go = BuildRibbonMesh(sub, hf, loader, RoadMaterial(), parent);
                if (go == null) continue;
                ribbonsBuilt++;
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                GameObject mk = BuildMarkingMesh(sub, hf, loader, MarkingMaterial(), parent);
                if (mk != null)
                {
                    var mmf = mk.GetComponent<MeshFilter>();
                    if (mmf != null && mmf.sharedMesh != null) tris += mmf.sharedMesh.triangles.Length / 3;
                }
            }
        }
        Debug.Log("[ConnectiveTerrain] 60 sq km layout: " + rectsBuilt
            + " ground pieces (18 rects, Ginza fill included, dem-subtracted) + "
            + ribbonsBuilt + " road segments (" + Ribbons.Length + " ribbons, ring + spokes + grids) + markings, "
            + tris + " tris, " + hf.SampleCount + " boundary samples");
    }

    /// <summary>Subtract every dem rect from a gap rect (axis-aligned
    /// split; pieces under MinPiece in either dimension are dropped).</summary>
    static List<GapRect> SubtractDem(GapRect r, List<Rect> dems)
    {
        var pieces = new List<GapRect>();
        pieces.Add(r);
        foreach (Rect d in dems)
        {
            var next = new List<GapRect>();
            foreach (GapRect p in pieces)
            {
                // No overlap: keep whole.
                if (d.xMax <= p.x0 || d.xMin >= p.x1 || d.yMax <= p.z0 || d.yMin >= p.z1)
                {
                    next.Add(p);
                    continue;
                }
                // Left / right strips.
                if (d.xMin > p.x0) next.Add(new GapRect(p.name, p.x0, p.z0, d.xMin, p.z1));
                if (d.xMax < p.x1) next.Add(new GapRect(p.name, d.xMax, p.z0, p.x1, p.z1));
                // Top / bottom strips inside the dem's x-range.
                float cx0 = Mathf.Max(p.x0, d.xMin);
                float cx1 = Mathf.Min(p.x1, d.xMax);
                if (d.yMin > p.z0) next.Add(new GapRect(p.name, cx0, p.z0, cx1, d.yMin));
                if (d.yMax < p.z1) next.Add(new GapRect(p.name, cx0, d.yMax, cx1, p.z1));
            }
            pieces = next;
        }
        var kept = new List<GapRect>();
        foreach (GapRect p in pieces)
            if (p.x1 - p.x0 >= MinPiece && p.z1 - p.z0 >= MinPiece) kept.Add(p);
        return kept;
    }

    /// <summary>Clip a ribbon by the Shibuya + Tokyo Bay dems (the only
    /// districts with real tran road meshes). The ribbon is split at
    /// the dem boundaries (10 m sampling, runs >= 25 m kept); the
    /// clipped-out part is the tran road itself.</summary>
    static List<Ribbon> ClipByTranDems(Ribbon rb, PlateauMapLoader loader)
    {
        var clip = new List<Rect>();
        if (loader.DemBounds != null)
        {
            Bounds b;
            if (loader.DemBounds.TryGetValue(DistrictData.DistrictId.Shibuya, out b))
                clip.Add(new Rect(b.min.x, b.min.z, b.size.x, b.size.z));
            if (loader.DemBounds.TryGetValue(DistrictData.DistrictId.TokyoBay, out b))
                clip.Add(new Rect(b.min.x, b.min.z, b.size.x, b.size.z));
        }
        if (clip.Count == 0)
        {
            var only = new List<Ribbon>();
            only.Add(rb);
            return only;
        }
        float len = Vector2.Distance(rb.from, rb.to);
        var result = new List<Ribbon>();
        Vector2? runStart = null;
        Vector2 prev = rb.from;
        int steps = Mathf.Max(1, Mathf.RoundToInt(len / 10f));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 p = Vector2.Lerp(rb.from, rb.to, t);
            bool inside = false;
            foreach (Rect d in clip)
                if (p.x >= d.xMin && p.x <= d.xMax && p.y >= d.yMin && p.y <= d.yMax) { inside = true; break; }
            if (!inside && runStart == null) runStart = p;
            if (inside && runStart != null)
            {
                if (Vector2.Distance(runStart.Value, prev) >= 25f)
                    result.Add(new Ribbon(rb.name, runStart.Value, prev, rb.width, rb.ring));
                runStart = null;
            }
            prev = p;
        }
        if (runStart != null && Vector2.Distance(runStart.Value, rb.to) >= 25f)
            result.Add(new Ribbon(rb.name, runStart.Value, rb.to, rb.width, rb.ring));
        return result;
    }

    /// <summary>Ground height at (x, z): the real built ground when it
    /// exists (dem / tran / already-built fill), else the heightfield.</summary>
    static float GroundHeightAt(PlateauMapLoader loader, Heightfield hf, float x, float z)
    {
        if (PlateauMapLoader.TryGetGroundHeight(x, z, out float h)) return h;
        return hf.Sample(x, z);
    }

    /// <summary>Heightfield from boundary samples along the dem edges,
    /// bucketed in 1 km cells (a query scans its 3x3 neighbourhood).</summary>
    class Heightfield
    {
        readonly List<Vector3> samples = new List<Vector3>(); // (x, h, z)
        readonly Dictionary<Vector2Int, List<int>> buckets = new Dictionary<Vector2Int, List<int>>();
        public int SampleCount { get { return samples.Count; } }

        public void Add(float x, float h, float z)
        {
            int idx = samples.Count;
            samples.Add(new Vector3(x, h, z));
            var key = new Vector2Int(Mathf.FloorToInt(x / 1000f), Mathf.FloorToInt(z / 1000f));
            List<int> list;
            if (!buckets.TryGetValue(key, out list)) { list = new List<int>(); buckets[key] = list; }
            list.Add(idx);
        }

        /// <summary>Ground height at (x, z): the nearest sample's height
        /// within 2 m (exact on a seam), otherwise inverse-distance
        /// weighting over samples within 2 km (smooth blend between the
        /// two edges of a gap). Returns 0 when there are no samples.</summary>
        public float Sample(float x, float z)
        {
            if (samples.Count == 0) return 0f;
            int bx = Mathf.FloorToInt(x / 1000f);
            int bz = Mathf.FloorToInt(z / 1000f);
            float bestD2 = float.MaxValue;
            float bestH = 0f;
            float wSum = 0f;
            float hSum = 0f;
            bool any = false;
            for (int cx = bx - 1; cx <= bx + 1; cx++)
            {
                for (int cz = bz - 1; cz <= bz + 1; cz++)
                {
                    List<int> list;
                    if (!buckets.TryGetValue(new Vector2Int(cx, cz), out list)) continue;
                    foreach (int i in list)
                    {
                        Vector3 s = samples[i];
                        float dx = x - s.x;
                        float dz = z - s.z;
                        float d2 = dx * dx + dz * dz;
                        any = true;
                        if (d2 < bestD2) { bestD2 = d2; bestH = s.y; }
                        if (d2 > 4000000f) continue; // > 2 km away: negligible weight
                        float w = 1f / (d2 + 25f);
                        wSum += w;
                        hSum += w * s.y;
                    }
                }
            }
            if (!any)
            {
                // Far from every dem edge (should not happen inside the
                // world): fall back to the global nearest sample.
                foreach (Vector3 s in samples)
                {
                    float d2 = (x - s.x) * (x - s.x) + (z - s.z) * (z - s.z);
                    if (d2 < bestD2) { bestD2 = d2; bestH = s.y; }
                }
                return bestH;
            }
            if (bestD2 < 4f) return bestH;
            if (wSum <= 0f) return bestH;
            return hSum / wSum;
        }
    }

    static Heightfield BuildHeightfield(PlateauMapLoader loader)
    {
        var hf = new Heightfield();
        if (loader.DemBounds == null || loader.DemBounds.Count == 0)
        {
            Debug.LogWarning("[ConnectiveTerrain] no dem bounds — cannot sample boundary heights");
            return hf;
        }
        int missed = 0;
        int sampled = 0;
        foreach (KeyValuePair<DistrictData.DistrictId, Bounds> kv in loader.DemBounds)
        {
            Bounds b = kv.Value;
            // All four edges, 1 m inside the square, every SampleSpacing.
            for (float x = b.min.x + 1f; x <= b.max.x - 1f; x += SampleSpacing)
            {
                SampleEdge(loader, hf, x, b.min.z + 1f, ref sampled, ref missed);
                SampleEdge(loader, hf, x, b.max.z - 1f, ref sampled, ref missed);
            }
            for (float z = b.min.z + 1f; z <= b.max.z - 1f; z += SampleSpacing)
            {
                SampleEdge(loader, hf, b.min.x + 1f, z, ref sampled, ref missed);
                SampleEdge(loader, hf, b.max.x - 1f, z, ref sampled, ref missed);
            }
        }
        if (missed > 0)
            Debug.Log("[ConnectiveTerrain] boundary sampling: " + sampled
                + " samples, " + missed + " points with no ground under them (mesh does not reach its bounds there)");
        return hf;
    }

    static void SampleEdge(PlateauMapLoader loader, Heightfield hf, float x, float z, ref int sampled, ref int missed)
    {
        if (PlateauMapLoader.TryGetGroundHeight(x, z, out float h))
        {
            hf.Add(x, h, z);
            sampled++;
        }
        else missed++;
    }

    static GameObject BuildRectMesh(GapRect r, Heightfield hf, Material mat, Transform parent)
    {
        int nx = Mathf.Clamp(Mathf.RoundToInt((r.x1 - r.x0) / GridSpacing), 1, 320);
        int nz = Mathf.Clamp(Mathf.RoundToInt((r.z1 - r.z0) / GridSpacing), 1, 320);
        int vx = nx + 1;
        int vz = nz + 1;
        Vector3[] verts = new Vector3[vx * vz];
        Vector2[] uvs = new Vector2[vx * vz];
        for (int j = 0; j < vz; j++)
        {
            for (int i = 0; i < vx; i++)
            {
                float x = r.x0 + (r.x1 - r.x0) * i / nx;
                float z = r.z0 + (r.z1 - r.z0) * j / nz;
                float h = Mathf.Max(hf.Sample(x, z), MapTerrain.MountainHeight(x, z));
                // Snap to the touge centreline height right beside the
                // pass so the road sits on the terrain, not above it.
                float sy, sd;
                if (MapTerrain.SplineHeight(x, z, out sy, out sd) && sd < MapTerrain.SplineSnapDistance)
                    h = sy;
                verts[j * vx + i] = new Vector3(x, h, z);
                uvs[j * vx + i] = new Vector2(x / GridSpacing, z / GridSpacing);
            }
        }
        int[] tris = new int[nx * nz * 6];
        int t = 0;
        for (int j = 0; j < nz; j++)
        {
            for (int i = 0; i < nx; i++)
            {
                int a = j * vx + i;
                int b = a + 1;
                int c = a + vx;
                int d = c + 1;
                // Up-facing winding (checked: (c-a)x(b-a) has +Y).
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        }
        var mesh = new Mesh();
        mesh.name = "GapGround_" + r.name;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("GapGround_" + r.name);
        go.transform.SetParent(parent, false);
        go.isStatic = true;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        var col = go.AddComponent<MeshCollider>();
        col.sharedMesh = mesh;
        col.convex = false;
        return go;
    }

    static GameObject BuildRibbonMesh(Ribbon rb, Heightfield hf, PlateauMapLoader loader, Material mat, Transform parent)
    {
        Vector3 axis = new Vector3(rb.to.x - rb.from.x, 0f, rb.to.y - rb.from.y);
        float len = axis.magnitude;
        if (len <= 0f) return null;
        axis /= len;
        Vector3 side = new Vector3(-axis.z, 0f, axis.x) * (rb.width * 0.5f);
        int n = Mathf.Clamp(Mathf.RoundToInt(len / GridSpacing), 1, 320);
        Vector3[] verts = new Vector3[(n + 1) * 2];
        Vector2[] uvs = new Vector2[(n + 1) * 2];
        for (int i = 0; i <= n; i++)
        {
            float t = (float)i / n;
            float cx = rb.from.x + (rb.to.x - rb.from.x) * t;
            float cz = rb.from.y + (rb.to.y - rb.from.y) * t;
            float h = GroundHeightAt(loader, hf, cx, cz) + RibbonLift;
            verts[i * 2] = new Vector3(cx - side.x, h, cz - side.z);
            verts[i * 2 + 1] = new Vector3(cx + side.x, h, cz + side.z);
            uvs[i * 2] = new Vector2(0f, t * len / GridSpacing);
            uvs[i * 2 + 1] = new Vector2(1f, t * len / GridSpacing);
        }
        int[] tris = new int[n * 6];
        int t2 = 0;
        for (int i = 0; i < n; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;
            // Up-facing winding: (b-a)x(c-a) has +Y (side x axis).
            // (The old ribbon code used (a,c,b)/(b,c,d), which faces
            // DOWN — never caught because the old layout was never
            // photo-verified. Do not "restore" the old order.)
            tris[t2++] = a; tris[t2++] = b; tris[t2++] = c;
            tris[t2++] = b; tris[t2++] = d; tris[t2++] = c;
        }
        var mesh = new Mesh();
        mesh.name = "GapRoad_" + rb.name;
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("GapRoad_" + rb.name);
        go.transform.SetParent(parent, false);
        go.isStatic = true;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        // No collider on purpose: the ribbon is a 6 cm visual skin over
        // the ground mesh, which already carries the collider. A collider
        // here would add a 6 cm step at the ribbon's edge.
        return go;
    }

    /// <summary>Painted markings for one ribbon: white centre dashes
    /// (3 m on, 6 m off) on every road, plus solid edge lines on the
    /// ring highway. One mesh, one material, 2 cm above the ribbon.</summary>
    static GameObject BuildMarkingMesh(Ribbon rb, Heightfield hf, PlateauMapLoader loader, Material mat, Transform parent)
    {
        Vector3 axis = new Vector3(rb.to.x - rb.from.x, 0f, rb.to.y - rb.from.y);
        float len = axis.magnitude;
        if (len <= 0f) return null;
        axis /= len;
        Vector3 side = new Vector3(-axis.z, 0f, axis.x);
        var verts = new List<Vector3>();
        var tris = new List<int>();
        // Walk in 3 m steps: dash pattern is 3 m on / 6 m off.
        int steps = Mathf.Max(1, Mathf.RoundToInt(len / 3f));
        for (int i = 0; i < steps; i++)
        {
            float t0 = (float)i / steps;
            float t1 = (float)(i + 1) / steps;
            float s0 = t0 * len;
            float s1 = t1 * len;
            Vector3 c0 = new Vector3(rb.from.x + axis.x * s0, 0f, rb.from.y + axis.z * s0);
            Vector3 c1 = new Vector3(rb.from.x + axis.x * s1, 0f, rb.from.y + axis.z * s1);
            float h0 = GroundHeightAt(loader, hf, c0.x, c0.z) + RibbonLift + MarkingLift;
            float h1 = GroundHeightAt(loader, hf, c1.x, c1.z) + RibbonLift + MarkingLift;
            c0.y = h0;
            c1.y = h1;
            // Centre dash on every third 3 m step (3 on, 6 off).
            if (i % 3 == 0)
                AddQuad(verts, tris, c0 - side * 0.175f, c0 + side * 0.175f,
                    c1 - side * 0.175f, c1 + side * 0.175f);
            if (rb.ring)
            {
                float edge = rb.width * 0.5f - 0.6f;
                AddQuad(verts, tris, c0 - side * (edge + 0.075f), c0 - side * (edge - 0.075f),
                    c1 - side * (edge + 0.075f), c1 - side * (edge - 0.075f));
                AddQuad(verts, tris, c0 + side * (edge - 0.075f), c0 + side * (edge + 0.075f),
                    c1 + side * (edge - 0.075f), c1 + side * (edge + 0.075f));
            }
        }
        if (verts.Count == 0) return null;
        var mesh = new Mesh();
        mesh.name = "GapMarking_" + rb.name;
        mesh.vertices = verts.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject("GapMarking_" + rb.name);
        go.transform.SetParent(parent, false);
        go.isStatic = true;
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return go;
    }

    static void AddQuad(List<Vector3> verts, List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int baseIdx = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        verts.Add(d);
        // Up-facing winding (same as the ribbon: (b-a)x(c-a) has +Y).
        tris.Add(baseIdx);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 3);
        tris.Add(baseIdx + 2);
    }

    static Material GroundMaterial()
    {
        if (groundMat == null)
        {
            groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMat.color = new Color(0.45f, 0.48f, 0.43f);
            groundMat.SetFloat("_Metallic", 0f);
            groundMat.SetFloat("_Smoothness", 0.3f);
        }
        return groundMat;
    }

    static Material RoadMaterial()
    {
        if (roadMat == null)
        {
            roadMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            roadMat.color = new Color(0.16f, 0.16f, 0.17f);
            roadMat.SetFloat("_Metallic", 0f);
            roadMat.SetFloat("_Smoothness", 0.2f);
        }
        return roadMat;
    }

    static Material MarkingMaterial()
    {
        if (markingMat == null)
        {
            markingMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            markingMat.color = new Color(0.92f, 0.92f, 0.88f);
            markingMat.SetFloat("_Metallic", 0f);
            markingMat.SetFloat("_Smoothness", 0.1f);
        }
        return markingMat;
    }
}

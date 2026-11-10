using System.Collections.Generic;
using UnityEngine;

/// <summary>MAP TERRAIN — the mountain ring + touge ridge height profile
/// for the 60 sq km map (2026-11-09 22:59 direction: the map is NOT only
/// city — mountains ring the city edge and the hidden touge pass climbs
/// the north-west peak).
///
/// MOUNTAINS (peak heights, measured from the layout script
/// compute_new_layout.py — same constants, same smoothstep, so the
/// Python placement pass and this runtime profile agree):
///   NW_Touge  x [-3850, 2672]..[-2931, 3850]  peak 220 m — the touge
///             mountain; its height comes from the SPLINE RIDGE rule
///             (below), not from the edge ramp.
///   SE        x [2959, -3850]..[3850, -2865]  peak 170 m
///   S         x [-1117, -3850]..[3850, -3273]  peak 120 m
///   SW        x [-3850, -3850]..[-2862, -3273]  peak 100 m
///   West foothills  x [-3850, -3273]..[-2862, 842]  rolling 0-40 m.
/// The NE corner has NO mountain on purpose: Shinjuku's LOD1 tiles
/// (53394527/37/47/57) cover the whole NE (x 2784..4002, z -430..4373)
/// to the world edge — the east side is built-up sprawl, and raising
/// terrain under those tiles would bury them. Tokyo's real mountains
/// are to the west (Okutama / Takao) anyway.
///
/// SPLINE RIDGE (NW only): the terrain hugs the touge centreline —
/// height = spline height minus 0.35 m per metre of horizontal distance
/// from the spline (a V-shaped pass valley), so the Kenney road pieces
/// sit ON the terrain instead of floating above it or being buried.
/// The summit (-3300, 3300) is the highest spline point (220 m), so
/// the ridge peaks there. ConnectiveTerrain additionally snaps fill
/// vertices within 15 m of the spline to the exact spline height.
///
/// Used by ConnectiveTerrain (fill vertex heights) and MapNature (tree
/// ground fallback). Pure data — no GameObjects, no Unity runs.</summary>
public static class MapTerrain
{
    /// <summary>Mountain zone: edge-ramp (height rises from the
    /// city-facing edge into the zone) or spline-ridge (NW).</summary>
    struct Zone
    {
        public string name;
        public float x0, z0, x1, z1, peak;
        public bool splineRidge;
        public Zone(string name, float x0, float z0, float x1, float z1, float peak, bool splineRidge)
        {
            this.name = name; this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1;
            this.peak = peak; this.splineRidge = splineRidge;
        }
    }

    static readonly Zone[] Zones = new Zone[]
    {
        new Zone("NW_Touge", -3850f, 2672f, -2931f, 3850f, 220f, true),
        new Zone("SE", 2959f, -3850f, 3850f, -2865f, 170f, false),
        new Zone("S", -1117f, -3850f, 3850f, -3273f, 120f, false),
        new Zone("SW", -3850f, -3850f, -2862f, -3273f, 100f, false),
    };

    /// <summary>West foothills rect + peak (rolling 0-40 m, not a peak).</summary>
    const float FoothillX0 = -3850f, FoothillZ0 = -3273f, FoothillX1 = -2862f, FoothillZ1 = 842f;
    const float FoothillPeak = 40f;

    /// <summary>Spline-ridge falloff: terrain drops this many metres
    /// per metre of horizontal distance from the touge centreline.</summary>
    public const float SplineRidgeSlope = 0.35f;
    /// <summary>Fill vertices closer than this to the spline snap to
    /// the exact spline height (ConnectiveTerrain).</summary>
    public const float SplineSnapDistance = 15f;
    /// <summary>Spline ridge only matters within this distance.</summary>
    const float SplineRidgeRange = 500f;

    static List<Vector3> cachedSpline;

    /// <summary>Mountain height at (x, z) — 0 outside the mountain
    /// zones / foothills / spline ridge. Matches the Python replica in
    /// compute_new_layout.py (same constants).</summary>
    public static float MountainHeight(float x, float z)
    {
        float h = 0f;
        foreach (Zone zn in Zones)
        {
            if (x < zn.x0 || x > zn.x1 || z < zn.z0 || z > zn.z1) continue;
            if (zn.splineRidge)
            {
                // Rolling base under the ridge (60 m at the far corner).
                float tx = (-2931f - x) / 919f;
                float tz = (z - 2672f) / 1178f;
                float t = Mathf.Max(tx, tz);
                h = Mathf.Max(h, 60f * Smooth01(t));
            }
            else
            {
                float tx = 0f, tz = 0f;
                if (zn.name == "SE") tx = (x - zn.x0) / (zn.x1 - zn.x0);
                if (zn.name == "SW") tx = (zn.x1 - x) / (zn.x1 - zn.x0);
                if (zn.name == "SE" || zn.name == "S" || zn.name == "SW")
                    tz = (zn.z1 - z) / (zn.z1 - zn.z0);
                float t = Mathf.Max(tx, tz);
                float ridge = 0.75f + 0.25f * Mathf.Sin(x * 0.021f + 1.7f)
                    * Mathf.Sin(z * 0.017f + 0.6f);
                h = Mathf.Max(h, zn.peak * Mathf.Pow(Smooth01(t), 1.5f) * ridge);
            }
        }
        // West foothills (rolling, overlaps the NW/SW zone edges — max wins).
        if (x >= FoothillX0 && x <= FoothillX1 && z >= FoothillZ0 && z <= FoothillZ1)
        {
            float rolling = 0.5f + 0.5f * Mathf.Sin(x * 0.013f + 0.4f)
                * Mathf.Sin(z * 0.011f + 2.1f);
            h = Mathf.Max(h, FoothillPeak * rolling
                * Smooth01((x - FoothillX0) / (FoothillX1 - FoothillX0)));
        }
        // Spline ridge (global — the pass descends out of its zone to the gate).
        float sy, sd;
        if (SplineHeight(x, z, out sy, out sd) && sd < SplineRidgeRange)
            h = Mathf.Max(h, sy - SplineRidgeSlope * sd);
        return h;
    }

    /// <summary>Nearest touge-spline sample to (x, z): its height and
    /// horizontal distance. False when the spline cannot be sampled
    /// (TougePass data missing — never in this project).</summary>
    public static bool SplineHeight(float x, float z, out float splineY, out float dist)
    {
        splineY = 0f;
        dist = float.MaxValue;
        if (cachedSpline == null) cachedSpline = TougePass.SampleSpline();
        if (cachedSpline == null || cachedSpline.Count == 0) return false;
        // Cheap gate: the spline lives in x [-3300, -2900], z [2400, 3330].
        if (x < -3850f || x > -2400f || z < 1900f || z > 3830f) return false;
        float bestD2 = float.MaxValue;
        float bestY = 0f;
        foreach (Vector3 s in cachedSpline)
        {
            float dx = x - s.x;
            float dz = z - s.z;
            float d2 = dx * dx + dz * dz;
            if (d2 < bestD2) { bestD2 = d2; bestY = s.y; }
        }
        dist = Mathf.Sqrt(bestD2);
        splineY = bestY;
        return true;
    }

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

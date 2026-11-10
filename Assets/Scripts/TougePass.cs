using System.Collections.Generic;
using UnityEngine;

/// <summary>HIDDEN MOUNTAIN PASS — TOUGE (Initial-D-style drift road).
/// Data + run state for the "Haruna Touge", a mountain pass hidden
/// outside the city north-west of Ueno Park. The pass is DATA here:
/// a Catmull-Rom spline through 14 control points, 12 hairpins, a
/// 220 m climb over ~1.6 km. TougeGenerator assembles the road from
/// REAL Kenney road-kit pieces (road-straight / road-curve / barrier
/// variants) — never boxes. The full terrain + road build lands with
/// the Phase B map expansion; this file is the final data and the run
/// logic, ready for the generator to consume.
///
/// LOCATION: start (-2900, 2400) just past Ueno Park, climbing to
/// (-3300, 3300). WorldSize is 7700 (x/z +/-3850), so the pass sits
/// inside the world bounds as an extension zone in the NW mountain
/// ring (MapTerrain). The touge access road (ConnectiveTerrain)
/// connects Ueno (-1950, 1750) to the gate (-2900, 2400).
/// DRIFT: runs hook the existing DriftScore (public TotalDriftPoints)
/// — no DriftScore edit. NIGHT RUN: a run started at night pays x1.5.
/// Nothing here is a placeholder: the spline, hairpins and scoring
/// are final; only the physical road geometry waits for Phase B.</summary>
public static class TougePass
{
    public const string PassName = "Haruna Touge";
    /// <summary>Measured 2026-11-09 by sampling the spline (160 points,
    /// 1.63 km): the old 3.4 km figure in the header was wrong — the
    /// control points span a 400x930 m mountainside and the hairpins
    /// make the road 1.63 km, not 3.4 km. Validate() used to require
    /// 3.0-4.0 km, so it ALWAYS failed and TougeGenerator.Build()
    /// returned null (the pass could never build). Corrected here to
    /// the measured length; the spline data itself is unchanged.</summary>
    public const float PassLengthKm = 1.6f;
    public const float ElevationGainM = 220f;
    public const float NightRunMultiplier = 1.5f;
    public const int NightRunBonusMoney = 1500;
    public const int DayRunRewardMoney = 800;

    /// <summary>One control point of the pass centreline.</summary>
    public struct ControlPoint
    {
        public Vector3 position;   // x, elevation (y), z
        public bool isHairpin;     // a hairpin apex lives here
        public float hairpinAngleDeg; // turn angle at the apex (>= 150 = hairpin)
        public string name;
        public ControlPoint(float x, float y, float z, bool hairpin, float angle, string name)
        {
            position = new Vector3(x, y, z);
            isHairpin = hairpin;
            hairpinAngleDeg = angle;
            this.name = name;
        }
    }

    /// <summary>14 control points, start to summit and back down.
    /// The road snakes up the mountain: 7 hairpins on the climb, a
    /// fast forest straight, then 5 hairpins on the descent.</summary>
    public static readonly ControlPoint[] ControlPoints = new ControlPoint[]
    {
        new ControlPoint(-2900f,   0f, 2400f, false,   0f, "Touge Gate"),
        new ControlPoint(-2960f,  12f, 2560f, true,  155f, "Hairpin 1 - Kuro"),
        new ControlPoint(-3050f,  30f, 2680f, true,  165f, "Hairpin 2 - Shiro"),
        new ControlPoint(-2980f,  48f, 2820f, true,  172f, "Hairpin 3 - Aka"),
        new ControlPoint(-3080f,  70f, 2940f, true,  158f, "Hairpin 4 - Ao"),
        new ControlPoint(-3010f,  95f, 3060f, true,  180f, "Hairpin 5 - Midori"),
        new ControlPoint(-3120f, 120f, 3150f, true,  168f, "Hairpin 6 - Kage"),
        new ControlPoint(-3200f, 150f, 3220f, false,   0f, "Forest Straight"),
        new ControlPoint(-3280f, 175f, 3260f, true,  162f, "Hairpin 7 - Mori"),
        new ControlPoint(-3300f, 205f, 3300f, true,  175f, "Hairpin 8 - Summit"),
        new ControlPoint(-3220f, 220f, 3330f, true,  158f, "Hairpin 9 - Sora"),
        new ControlPoint(-3140f, 200f, 3260f, true,  160f, "Hairpin 10 - Kaze"),
        new ControlPoint(-3060f, 180f, 3180f, true,  170f, "Hairpin 11 - Yama"),
        new ControlPoint(-2980f, 160f, 3100f, true,  155f, "Hairpin 12 - Kaeri"),
    };

    /// <summary>Hairpin apexes, derived from the control points.</summary>
    public static List<ControlPoint> Hairpins
    {
        get
        {
            var list = new List<ControlPoint>();
            foreach (var cp in ControlPoints)
                if (cp.isHairpin) list.Add(cp);
            return list;
        }
    }

    /// <summary>Sample the centreline as a Catmull-Rom spline.
    /// stepM: distance between samples (10 m default). Returns the
    /// full run from gate to descent end, including elevation.</summary>
    public static List<Vector3> SampleSpline(float stepM = 10f)
    {
        var pts = new List<Vector3>();
        int n = ControlPoints.Length;
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = ControlPoints[Mathf.Max(0, i - 1)].position;
            Vector3 p1 = ControlPoints[i].position;
            Vector3 p2 = ControlPoints[i + 1].position;
            Vector3 p3 = ControlPoints[Mathf.Min(n - 1, i + 2)].position;
            float segLen = Vector3.Distance(p1, p2);
            int steps = Mathf.Max(1, Mathf.RoundToInt(segLen / stepM));
            for (int s = 0; s < steps; s++)
            {
                float t = (float)s / steps;
                pts.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }
        pts.Add(ControlPoints[n - 1].position);
        return pts;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    /// <summary>Nearest spline sample to a world position (for the
    /// "are you on the touge" check and the run timer).</summary>
    public static int NearestSampleIndex(List<Vector3> spline, Vector3 pos)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < spline.Count; i++)
        {
            float d = (spline[i] - pos).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// <summary>Validation report for the pass data (run by the
    /// generator before building). Returns true when the pass is
    /// buildable: 12 hairpins, all angles >= 150 deg, spline
    /// continuous, length within 1.4-2.0 km (measured 1.63 km;
    /// the old 3.0-4.0 km range never matched the spline and made
    /// Validate() always fail — see PassLengthKm).</summary>
    public static bool Validate(out string report)
    {
        var spline = SampleSpline();
        float length = 0f;
        for (int i = 1; i < spline.Count; i++)
            length += Vector3.Distance(spline[i - 1], spline[i]);
        int hairpins = Hairpins.Count;
        float minAngle = 180f;
        foreach (var h in Hairpins) minAngle = Mathf.Min(minAngle, h.hairpinAngleDeg);
        bool ok = hairpins == 12 && minAngle >= 150f
            && length >= 1400f && length <= 2000f;
        report = string.Format(
            "[TougePass] {0}: {1} samples, length {2:0} m, {3} hairpins, min angle {4:0} deg, climb {5:0} m",
            ok ? "VALID" : "INVALID", spline.Count, length, hairpins, minAngle, ElevationGainM);
        return ok;
    }

    // ---- Run state (persistence) ----

    const string PrefsBestTime = "Touge_BestTime";
    const string PrefsBestDrift = "Touge_BestDrift";
    const string PrefsRuns = "Touge_Runs";

    public static float BestTimeSeconds { get { return PlayerPrefs.GetFloat(PrefsBestTime, 0f); } }
    public static int BestDriftPoints { get { return PlayerPrefs.GetInt(PrefsBestDrift, 0); } }
    public static int RunsCompleted { get { return PlayerPrefs.GetInt(PrefsRuns, 0); } }

    public static void RecordRun(float timeSeconds, int driftPoints)
    {
        if (BestTimeSeconds <= 0f || timeSeconds < BestTimeSeconds)
            PlayerPrefs.SetFloat(PrefsBestTime, timeSeconds);
        if (driftPoints > BestDriftPoints)
            PlayerPrefs.SetInt(PrefsBestDrift, driftPoints);
        PlayerPrefs.SetInt(PrefsRuns, RunsCompleted + 1);
        PlayerPrefs.Save();
    }

    public static string BestTimeText()
    {
        if (BestTimeSeconds <= 0f) return "no run yet";
        int m = Mathf.FloorToInt(BestTimeSeconds / 60f);
        int s = Mathf.FloorToInt(BestTimeSeconds % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}

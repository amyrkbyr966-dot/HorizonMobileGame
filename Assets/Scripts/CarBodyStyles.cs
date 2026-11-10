using UnityEngine;

/// <summary>Nine distinct body silhouettes, lofted procedurally in code
/// (no FBX files — this is also what keeps the build small: the whole car
/// range is a few kilobytes of profile data). Two APIs, one data set:
///  - StyleForCar / StyleForTraffic return the BodyStyleDef directly
///    (used by CarVisuals).
///  - ClassForCar / ClassForTraffic / ForClass use the integer class
///    system of MeshFactory.BuildCarBody(int) (0 city, 1 GT, 2 supercar,
///    3 SUV, 4 muscle, 5 sedan, 6 roadster, 7 hypercar, 8 coupe).
/// Each class has its own body loft, cabin loft, wing, rim, grille and
/// details, so a City Sprint hatchback cannot be mistaken for a Horizon X
/// hypercar. The Tokyo 50-car roster (later phase) reuses these 9 classes.</summary>
public enum WingType { None, Lip, Wing, BigWing }
public enum RimType { Silver, Black, Chrome }

public class BodyStyleDef
{
    public string name;
    public float[] z, w, top;          // body loft stations (+Z forward)
    public float bodyY;                // body origin height offset
    public float[] cz, cw, ctop;       // cabin glass loft stations
    public float cabinY, cabinZ;       // cabin origin
    public WingType wing;
    public int spokes;
    public RimType rim;
    public float grilleW, grilleH;
    public bool scoop;                 // hood scoop
    public bool drl;                   // LED daytime-running-light strip
    public bool openTop;               // roadster: windshield-only cabin, no roof panel
}

public static class CarBodyStyles
{
    static readonly float[] Z = { -2.10f, -1.95f, -1.60f, -1.10f, -0.55f, 0.00f, 0.55f, 1.05f, 1.45f, 1.80f, 2.02f, 2.12f };
    static readonly float[] CZ = { -1.05f, -0.75f, -0.30f, 0.15f, 0.60f, 0.95f, 1.08f };

    static BodyStyleDef Make(string name, float[] w, float[] top, float bodyY,
        float[] cw, float[] ctop, float cabinY, float cabinZ,
        WingType wing, int spokes, RimType rim, float grilleW, float grilleH,
        bool scoop, bool drl, float[] cz = null, bool openTop = false)
    {
        return new BodyStyleDef
        {
            name = name, z = Z, w = w, top = top, bodyY = bodyY,
            cz = cz ?? CZ, cw = cw, ctop = ctop, cabinY = cabinY, cabinZ = cabinZ,
            wing = wing, spokes = spokes, rim = rim,
            grilleW = grilleW, grilleH = grilleH, scoop = scoop, drl = drl,
            openTop = openTop
        };
    }

    // All[] order: 0 Hatchback, 1 Coupe, 2 SUV, 3 Muscle, 4 Supercar,
    // 5 GT, 6 Roadster, 7 Hypercar, 8 Sedan.
    public static readonly BodyStyleDef[] All = new BodyStyleDef[]
    {
        // 0 Hatchback — tall, upright city car (City Sprint).
        Make("Hatchback",
            new float[]{0.82f,0.88f,0.92f,0.94f,0.95f,0.95f,0.94f,0.92f,0.88f,0.82f,0.74f,0.58f},
            new float[]{0.34f,0.36f,0.37f,0.37f,0.36f,0.35f,0.33f,0.30f,0.26f,0.20f,0.13f,0.06f}, 0.02f,
            new float[]{0.66f,0.78f,0.82f,0.82f,0.78f,0.68f,0.52f},
            new float[]{0.14f,0.30f,0.34f,0.34f,0.30f,0.18f,0.04f}, 1.06f, -0.22f,
            WingType.Lip, 5, RimType.Silver, 0.9f, 0.14f, false, false),
        // 1 Coupe — low, sleek two-door (Turbo Falcon).
        Make("Coupe",
            new float[]{0.80f,0.88f,0.92f,0.94f,0.95f,0.95f,0.94f,0.92f,0.88f,0.82f,0.74f,0.58f},
            new float[]{0.24f,0.28f,0.29f,0.29f,0.28f,0.27f,0.25f,0.22f,0.18f,0.12f,0.07f,0.02f}, 0f,
            new float[]{0.62f,0.74f,0.80f,0.80f,0.76f,0.66f,0.50f},
            new float[]{0.10f,0.24f,0.28f,0.28f,0.24f,0.14f,0.02f}, 1.02f, -0.18f,
            WingType.Wing, 5, RimType.Silver, 1.1f, 0.16f, true, true),
        // 2 SUV — tall, wide, high stance (Desert Fox).
        Make("SUV",
            new float[]{0.86f,0.92f,0.96f,0.98f,0.99f,0.99f,0.98f,0.96f,0.92f,0.86f,0.76f,0.60f},
            new float[]{0.38f,0.42f,0.43f,0.43f,0.42f,0.41f,0.40f,0.38f,0.34f,0.28f,0.20f,0.10f}, 0.10f,
            new float[]{0.72f,0.82f,0.86f,0.86f,0.82f,0.74f,0.58f},
            new float[]{0.16f,0.34f,0.38f,0.38f,0.34f,0.22f,0.06f}, 1.14f, -0.25f,
            WingType.Lip, 6, RimType.Black, 1.3f, 0.22f, false, false),
        // 3 Muscle — long hood, cabin set back, ducktail (Night Hawk).
        Make("Muscle",
            new float[]{0.84f,0.90f,0.94f,0.96f,0.97f,0.97f,0.96f,0.94f,0.90f,0.84f,0.75f,0.60f},
            new float[]{0.26f,0.30f,0.31f,0.31f,0.30f,0.29f,0.28f,0.26f,0.23f,0.18f,0.11f,0.04f}, 0f,
            new float[]{0.58f,0.70f,0.76f,0.76f,0.72f,0.62f,0.46f},
            new float[]{0.08f,0.22f,0.26f,0.26f,0.22f,0.12f,0.02f}, 1.00f, -0.35f,
            WingType.Lip, 5, RimType.Chrome, 1.2f, 0.20f, true, false),
        // 4 Supercar — very low, very wide, fastback (Horizon R).
        Make("Supercar",
            new float[]{0.86f,0.94f,0.98f,1.00f,1.00f,0.99f,0.97f,0.94f,0.89f,0.82f,0.72f,0.55f},
            new float[]{0.18f,0.22f,0.23f,0.23f,0.22f,0.21f,0.19f,0.16f,0.12f,0.08f,0.04f,0.00f}, -0.03f,
            new float[]{0.60f,0.72f,0.78f,0.78f,0.74f,0.64f,0.48f},
            new float[]{0.08f,0.20f,0.24f,0.24f,0.20f,0.12f,0.02f}, 0.96f, -0.15f,
            WingType.Wing, 7, RimType.Black, 1.4f, 0.12f, false, true),
        // 5 GT — long, low, elegant grand tourer (Street GT).
        Make("GT",
            new float[]{0.82f,0.89f,0.93f,0.95f,0.96f,0.96f,0.95f,0.93f,0.89f,0.83f,0.74f,0.58f},
            new float[]{0.22f,0.26f,0.27f,0.27f,0.26f,0.25f,0.23f,0.20f,0.17f,0.12f,0.07f,0.02f}, -0.01f,
            new float[]{0.60f,0.72f,0.78f,0.78f,0.74f,0.64f,0.48f},
            new float[]{0.08f,0.22f,0.26f,0.26f,0.22f,0.12f,0.02f}, 0.99f, -0.20f,
            WingType.Wing, 7, RimType.Chrome, 1.2f, 0.14f, true, true),
        // 6 Roadster — low with a short windshield-only cabin (Apex GT).
        // The cabin is a real open-top windshield (5 stations, z 0.15 to
        // 1.08 in cabin space): the seats sit exposed behind it, with
        // roll hoops (CarVisuals case 6) instead of a glass roof. The
        // old 7-station full canopy made the roadster a closed coupe.
        Make("Roadster",
            new float[]{0.80f,0.88f,0.92f,0.94f,0.95f,0.95f,0.94f,0.92f,0.88f,0.82f,0.74f,0.58f},
            new float[]{0.20f,0.24f,0.25f,0.25f,0.24f,0.23f,0.21f,0.18f,0.14f,0.09f,0.05f,0.01f}, -0.02f,
            new float[]{0.72f,0.74f,0.70f,0.60f,0.44f},
            new float[]{0.10f,0.16f,0.18f,0.12f,0.02f}, 0.94f, -0.05f,
            WingType.Lip, 5, RimType.Silver, 1.1f, 0.13f, false, true,
            new float[]{0.15f,0.45f,0.75f,0.95f,1.08f}, true),
        // 7 Hypercar — extreme: lowest, widest, biggest wing (Horizon X).
        Make("Hypercar",
            new float[]{0.88f,0.96f,1.02f,1.03f,1.03f,1.02f,1.00f,0.96f,0.90f,0.82f,0.70f,0.52f},
            new float[]{0.15f,0.19f,0.20f,0.20f,0.19f,0.18f,0.16f,0.13f,0.10f,0.06f,0.03f,0.00f}, -0.04f,
            new float[]{0.58f,0.70f,0.76f,0.76f,0.72f,0.62f,0.46f},
            new float[]{0.06f,0.18f,0.22f,0.22f,0.18f,0.10f,0.01f}, 0.94f, -0.12f,
            WingType.BigWing, 10, RimType.Black, 1.5f, 0.10f, false, true),
        // 8 Sedan — three-box family saloon (traffic + later roster).
        Make("Sedan",
            new float[]{0.82f,0.88f,0.92f,0.94f,0.95f,0.95f,0.94f,0.92f,0.88f,0.82f,0.74f,0.58f},
            new float[]{0.28f,0.31f,0.32f,0.32f,0.31f,0.30f,0.29f,0.27f,0.24f,0.19f,0.12f,0.05f}, 0.01f,
            new float[]{0.62f,0.74f,0.80f,0.80f,0.76f,0.66f,0.50f},
            new float[]{0.10f,0.24f,0.28f,0.28f,0.24f,0.14f,0.02f}, 1.03f, -0.28f,
            WingType.Lip, 6, RimType.Silver, 1.0f, 0.15f, false, false),
    };

    // Integer class -> All[] index. Class system (MeshFactory):
    // 0 city, 1 GT, 2 supercar, 3 SUV, 4 muscle, 5 sedan,
    // 6 roadster, 7 hypercar, 8 coupe.
    static readonly int[] ClassToAll = { 0, 5, 4, 2, 3, 8, 6, 7, 1 };

    public static BodyStyleDef ForClass(int carClass)
    {
        return All[ClassToAll[Mathf.Clamp(carClass, 0, ClassToAll.Length - 1)]];
    }

    /// <summary>Garage car index (CarSelection) -> integer class.
    /// 0 City Sprint=city, 1 Street GT=GT, 2 Desert Fox=SUV,
    /// 3 Night Hawk=muscle, 4 Horizon R=supercar, 5 Turbo Falcon=coupe,
    /// 6 Apex GT=roadster, 7 Horizon X=hypercar, 8-54 the Tokyo roster
    /// (Germany 8-14, England 15-19, America 20-25, Italy 26-32,
    /// France 33-37, Japan 38-45, 2026 models 46-54). All Chinese cars
    /// and the non-sporty American SUV were removed 2026-11-07.
    /// NOTE: map values are CLASS numbers (2=supercar, 8=coupe), NOT
    /// All[] indices (All[2]=SUV, All[8]=Sedan) — the two index spaces
    /// differ, and confusing them makes Horizon R look like an SUV
    /// and Turbo Falcon like a sedan. ClassToAll above is the bridge.</summary>
    public static int ClassForCar(int carIndex)
    {
        int[] map =
        {
            0, 1, 3, 4, 2, 8, 6, 7,           // 0-7 original cars
            8, 1, 2, 2, 3, 2, 8,              // 8-14 Germany
            1, 5, 2, 7, 1,                    // 15-19 England
            4, 2, 2, 4, 5, 6,                 // 20-25 America (sporty only)
            2, 7, 2, 7, 2, 5, 7,              // 26-32 Italy
            7, 7, 5, 0, 8,                    // 33-37 France
            8, 8, 2, 8, 5, 2, 8, 8,           // 38-45 Japan
            2, 4, 2, 7, 7, 8, 2, 5, 8,        // 46-54 2026 models (9)
        };
        return map[Mathf.Clamp(carIndex, 0, map.Length - 1)];
    }

    /// <summary>Deterministic varied integer class for a traffic car.</summary>
    public static int ClassForTraffic(int instanceId)
    {
        int[] pool = { 0, 1, 2, 3, 4, 5 };
        return pool[Mathf.Abs(instanceId) % pool.Length];
    }

    /// <summary>Garage car index -> body style definition. Hero cars get
    /// a CLONED def with their own silhouette tweaks (a Chiron is longer
    /// and lower than a 911, an R34 has a taller greenhouse than a
    /// LaFerrarri, a Countach is a wedge) — same wheelbase/length class
    /// so physics and colliders are untouched; only the loft stations
    /// move a few centimetres. Every other car keeps its class def.</summary>
    public static BodyStyleDef StyleForCar(int carIndex)
    {
        BodyStyleDef baseDef = ForClass(ClassForCar(carIndex));
        switch (carIndex)
        {
            case 14: return Tweaked(baseDef, 0.00f, 0.02f, 0.02f);   // 911: rounder roof
            case 20: return Tweaked(baseDef, 0.01f, 0.00f, -0.02f);  // Mustang: long hood
            case 27: return Tweaked(baseDef, 0.02f, -0.03f, 0.00f);  // LaFerrarri: wider, lower
            case 29: return Tweaked(baseDef, 0.00f, -0.05f, -0.03f); // Countach: wedge, flat deck
            case 33: return Tweaked(baseDef, 0.03f, -0.02f, 0.01f);  // Chiron: widest, long nose
            case 38: return Tweaked(baseDef, -0.01f, 0.05f, 0.04f);  // R34: taller greenhouse
            case 39: return Tweaked(baseDef, 0.00f, 0.01f, 0.01f);   // Supra: coupe, high tail
            case 43: return Tweaked(baseDef, 0.01f, -0.01f, 0.00f);  // LFA: sharp supercar
            case 49: return Tweaked(baseDef, 0.03f, -0.04f, 0.00f);  // Tourbillon: widest, lowest
            default: return baseDef;
        }
    }

    /// <summary>Clone a def and shift width / roof height / cabin height
    /// by the given deltas (metres). Roof delta tapers toward the nose
    /// and tail so the silhouette stays smooth.</summary>
    static BodyStyleDef Tweaked(BodyStyleDef src, float dw, float dTop, float dCabin)
    {
        float[] w = (float[])src.w.Clone();
        float[] top = (float[])src.top.Clone();
        float[] cw = (float[])src.cw.Clone();
        float[] ctop = (float[])src.ctop.Clone();
        for (int i = 0; i < w.Length; i++) w[i] += dw;
        for (int i = 0; i < top.Length; i++)
        {
            float taper = 1f - Mathf.Abs(i - top.Length / 2f) / (top.Length / 2f) * 0.6f;
            top[i] += dTop * taper;
        }
        for (int i = 0; i < ctop.Length; i++) ctop[i] += dCabin;
        for (int i = 0; i < cw.Length; i++) cw[i] += dw * 0.5f;
        return new BodyStyleDef
        {
            name = src.name, z = src.z, w = w, top = top, bodyY = src.bodyY,
            cz = src.cz, cw = cw, ctop = ctop, cabinY = src.cabinY, cabinZ = src.cabinZ,
            wing = src.wing, spokes = src.spokes, rim = src.rim,
            grilleW = src.grilleW, grilleH = src.grilleH,
            scoop = src.scoop, drl = src.drl, openTop = src.openTop
        };
    }

    /// <summary>Deterministic varied body style for a traffic car.</summary>
    public static BodyStyleDef StyleForTraffic(int instanceId)
    {
        int[] pool = { 0, 1, 8, 2, 3, 5 }; // All[] indices: hatch, coupe, sedan, SUV, muscle, GT
        return All[pool[Mathf.Abs(instanceId) % pool.Length]];
    }
}

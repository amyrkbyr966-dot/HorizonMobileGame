using System.Collections.Generic;
using UnityEngine;

/// <summary>Tokyo open-world data (Phase A — data only, no scene edits).
/// The 7 districts, the 500 m streaming grid, LOD bands, performance
/// budgets, and the discoverable places (landmarks, gas station, garage,
/// viewpoints) for free-roam discovery. District layout follows the
/// 60 sq km dense layout (2026-11-09, new-layout-60sqkm.md): full
/// Forza Horizon 1 size (~7.7x7.7 km), districts packed close, no void.
/// Pure static data: no MonoBehaviour, no scene references, safe to use
/// from any manager.</summary>
public static class DistrictData
{
    // ---- World grid (60 sq km layout, 2026-11-09) ----
    /// <summary>Full city extent, metres. The 60 sq km map is 7700 m
    /// (full Forza Horizon 1 size, ~7.7x7.7 km) — the old 6700 m / 45 km2
    /// layout is superseded (new-layout-60sqkm.md).</summary>
    public const float WorldSize = 7700f;
    /// <summary>Current Shibuya core size (the scene as it exists today).</summary>
    public const float ShibuyaSize = 1000f;
    /// <summary>Streaming cell size, metres.</summary>
    public const float CellSize = 500f;
    /// <summary>Cells per side across the full city: 7700 / 500 = 15.4 -> 16.</summary>
    public const int CellsPerSide = 16;
    /// <summary>Cells loaded around the player (3x3 = 9 cells).</summary>
    public const int LoadedCellRadius = 1;
    /// <summary>Unload radius from the player, metres (hysteresis:
    /// load at 500 m, unload at 750 m, so border driving never thrashes).</summary>
    public const float UnloadRadius = 750f;

    // ---- LOD bands (plan §1.4), distance from camera, metres ----
    public const float Lod0MaxDistance = 150f;
    public const float Lod1MaxDistance = 400f;
    public const float Lod2MaxDistance = 750f;

    // ---- Performance budgets (plan §1.5), per loaded 3x3 area ----
    public const int MaxDrawCalls = 120;
    public const int MaxTrianglesOnScreen = 350000;
    public const int MaxObjectsPerCell = 250;
    public const int MaxTrafficCars = 8;

    public enum DistrictId
    {
        Shibuya = 0,
        Shinjuku = 1,
        Ginza = 2,
        Asakusa = 3,
        Akihabara = 4,
        TokyoBay = 5,
        UenoPark = 6,
        ResidentialSW = 7,
    }

    /// <summary>One district: centre, size, character, landmarks.
    /// Coordinates are district centres in metres, world centred on
    /// (0,0), X east, Z north — same convention as Minimap.WorldToMap.</summary>
    public struct DistrictInfo
    {
        public DistrictId id;
        public string name;
        public string displayName;
        public Vector2 centre;
        public Vector2 size;
        public string character;
        public string[] landmarks;

        public DistrictInfo(DistrictId id, string name, string displayName,
            float centreX, float centreZ, float sizeX, float sizeZ,
            string character, params string[] landmarks)
        {
            this.id = id;
            this.name = name;
            this.displayName = displayName;
            centre = new Vector2(centreX, centreZ);
            size = new Vector2(sizeX, sizeZ);
            this.character = character;
            this.landmarks = landmarks;
        }

        /// <summary>True when a world XZ position falls inside this district.</summary>
        public bool Contains(Vector3 worldPos)
        {
            return Mathf.Abs(worldPos.x - centre.x) <= size.x * 0.5f
                && Mathf.Abs(worldPos.z - centre.y) <= size.y * 0.5f;
        }
    }

    public static readonly DistrictInfo[] Districts = new DistrictInfo[]
    {
        new DistrictInfo(DistrictId.Shibuya, "Shibuya", "Shibuya",
            0f, 0f, 1600f, 1500f,
            "The current 1x1 km scene, untouched — scramble crossing, neon, downtown + 120 m Horizon tower, ring highway.",
            "Scramble Crossing", "Horizon Tower", "Shibuya Neon Row", "Ring Highway"),
        new DistrictInfo(DistrictId.Shinjuku, "Shinjuku", "Shinjuku",
            2030f, 1830f, 2000f, 2000f,
            "Tall glass towers, dense high-rise business district.",
            "Shinjuku Towers", "Metropolitan Plaza"),
        new DistrictInfo(DistrictId.Ginza, "Ginza", "Ginza",
            2000f, 0f, 2000f, 2000f,
            "Luxury shopping streets, wide boulevards, elegant facades.",
            "Ginza Boulevard", "Luxury Row"),
        new DistrictInfo(DistrictId.Asakusa, "Asakusa", "Asakusa",
            0f, 1750f, 1600f, 2000f,
            "Temple gate (torii), traditional low streets, pagoda silhouette.",
            "Asakusa Temple Gate", "Pagoda"),
        new DistrictInfo(DistrictId.Akihabara, "Akihabara", "Akihabara",
            2050f, -1950f, 1800f, 2000f,
            "Neon / electronics canyon, dense signage, narrow streets.",
            "Electric Town", "Neon Canyon"),
        new DistrictInfo(DistrictId.TokyoBay, "Tokyo Bay", "Tokyo Bay",
            0f, -2160f, 2000f, 1800f,
            "Ring highway on bridge over water, port, gas station + garage.",
            "Bay Bridge", "Port", "Gas Station", "Garage"),
        new DistrictInfo(DistrictId.UenoPark, "Ueno Park", "Ueno Park",
            -1950f, 1750f, 1800f, 2000f,
            "Sakura park (spring showcase), trees, ponds, slow scenic roads.",
            "Sakura Grove", "Ueno Pond"),
        new DistrictInfo(DistrictId.ResidentialSW, "Residential", "Residential SW",
            -2400f, -2400f, 2200f, 2200f,
            "Residential / suburbs, low density — fills the south-west corner.",
            "Suburb Hills"),
    };

    /// <summary>District containing a world position: the NEAREST
    /// district centre (2026-11-09: the 60 sq km districts are packed
    /// close and their display sizes overlap, so a first-match
    /// Contains() test would attribute whole neighbours to whichever
    /// district is listed first. Nearest-centre is exact for display
    /// and for the "current district" HUD; Contains() still exists for
    /// callers that need a hard box test).</summary>
    public static DistrictInfo DistrictAt(Vector3 worldPos)
    {
        DistrictInfo best = Districts[0];
        float bestD2 = float.MaxValue;
        foreach (DistrictInfo d in Districts)
        {
            float dx = worldPos.x - d.centre.x;
            float dz = worldPos.z - d.centre.y;
            float d2 = dx * dx + dz * dz;
            if (d2 < bestD2) { bestD2 = d2; best = d; }
        }
        return best;
    }

    /// <summary>District by id (0 = Shibuya). Out-of-range ids clamp to Shibuya.</summary>
    public static DistrictInfo Get(DistrictId id)
    {
        int i = (int)id;
        if (i < 0 || i >= Districts.Length) return Districts[0];
        return Districts[i];
    }

    // ---- Streaming grid helpers (plan §1.3) ----

    /// <summary>Cell coordinates (cx, cz) for a world position.
    /// Cell (0,0) is the south-west corner of the 7700 m world.</summary>
    public static Vector2Int CellForPosition(Vector3 worldPos)
    {
        float half = WorldSize * 0.5f;
        int cx = Mathf.FloorToInt((worldPos.x + half) / CellSize);
        int cz = Mathf.FloorToInt((worldPos.z + half) / CellSize);
        cx = Mathf.Clamp(cx, 0, CellsPerSide - 1);
        cz = Mathf.Clamp(cz, 0, CellsPerSide - 1);
        return new Vector2Int(cx, cz);
    }

    /// <summary>Cell coordinates for the current Shibuya core.
    /// Shibuya (1000x1000 m, centred on 0,0) is cells (6..8, 6..8)
    /// of the 16x16 grid (half world 3850 m: -500 m -> cell 6,
    /// +500 m -> cell 8).</summary>
    public static Vector2Int ShibuyaCellMin { get { return new Vector2Int(6, 6); } }
    public static Vector2Int ShibuyaCellMax { get { return new Vector2Int(8, 8); } }

    /// <summary>World-space centre of a cell.</summary>
    public static Vector3 CellCentre(Vector2Int cell)
    {
        float half = WorldSize * 0.5f;
        return new Vector3(
            -half + (cell.x + 0.5f) * CellSize,
            0f,
            -half + (cell.y + 0.5f) * CellSize);
    }

    /// <summary>Canonical cell-root GameObject name used by
    /// DistrictStreaming (Phase B creates these roots; Phase A only
    /// looks them up). Example: "Cell_06_07".</summary>
    public static string CellRootName(Vector2Int cell)
    {
        return string.Format("Cell_{0:00}_{1:00}", cell.x, cell.y);
    }

    /// <summary>The 3x3 set of cells that must be loaded around a
    /// player cell (the cell itself + 1 ring). Clamped to the grid.</summary>
    public static List<Vector2Int> WantedCells(Vector2Int playerCell)
    {
        var list = new List<Vector2Int>(9);
        for (int dx = -LoadedCellRadius; dx <= LoadedCellRadius; dx++)
            for (int dz = -LoadedCellRadius; dz <= LoadedCellRadius; dz++)
            {
                int cx = playerCell.x + dx;
                int cz = playerCell.y + dz;
                if (cx < 0 || cx >= CellsPerSide || cz < 0 || cz >= CellsPerSide)
                    continue;
                list.Add(new Vector2Int(cx, cz));
            }
        return list;
    }

    /// <summary>LOD tier for an object at a distance from the camera
    /// (plan §1.4). 0 = full detail, 1 = simplified, 2 = silhouette,
    /// 3 = culled (outside the loaded cells).</summary>
    public static int LodTierForDistance(float distance)
    {
        if (distance < Lod0MaxDistance) return 0;
        if (distance < Lod1MaxDistance) return 1;
        if (distance < Lod2MaxDistance) return 2;
        return 3;
    }

    // ---- Discoverable places (free-roam discovery) ----

    public enum PlaceKind { Landmark, GasStation, Garage, Viewpoint, CoinArea }

    /// <summary>One discoverable place. Positions are world XZ.
    /// Shibuya entries use the real 1x1 km scene (Horizon Tower sits
    /// near (388, 446) per CarController's reset comment; the scramble
    /// crossing is the city centre). District entries outside Shibuya
    /// are Phase B positions — the place exists in data now so the
    /// discovery system, HUD count and save format are final, and the
    /// coordinates get verified against the built districts in Phase B.</summary>
    public struct DiscoverablePlace
    {
        public string id;
        public string name;
        public PlaceKind kind;
        public DistrictId district;
        public Vector3 position;
        public float discoverRadius;
        public string hint;

        public DiscoverablePlace(string id, string name, PlaceKind kind,
            DistrictId district, float x, float z, float radius, string hint)
        {
            this.id = id;
            this.name = name;
            this.kind = kind;
            this.district = district;
            position = new Vector3(x, 0f, z);
            discoverRadius = radius;
            this.hint = hint;
        }
    }

    public static readonly DiscoverablePlace[] Places = new DiscoverablePlace[]
    {
        // Shibuya — the current scene (real positions, inside 1000 m).
        new DiscoverablePlace("shibuya_crossing", "Scramble Crossing",
            PlaceKind.Landmark, DistrictId.Shibuya, 0f, 0f, 40f,
            "The famous crossing at the heart of Shibuya"),
        new DiscoverablePlace("shibuya_tower", "Horizon Tower",
            PlaceKind.Landmark, DistrictId.Shibuya, 388f, 446f, 45f,
            "The 120 m downtown tower"),
        new DiscoverablePlace("shibuya_neon", "Neon Row",
            PlaceKind.Landmark, DistrictId.Shibuya, -300f, 150f, 40f,
            "Downtown neon signs"),
        new DiscoverablePlace("shibuya_ring", "Ring Highway",
            PlaceKind.Viewpoint, DistrictId.Shibuya, 0f, 450f, 40f,
            "The ring highway — top-speed run"),
        new DiscoverablePlace("shibuya_gas", "Gas Station",
            PlaceKind.GasStation, DistrictId.Shibuya, -450f, -300f, 35f,
            "Fuel stop on the south-west ring"),
        new DiscoverablePlace("shibuya_garage", "Garage",
            PlaceKind.Garage, DistrictId.Shibuya, 300f, -450f, 35f,
            "Your garage — upgrades live here"),
        new DiscoverablePlace("shibuya_view", "Downtown Viewpoint",
            PlaceKind.Viewpoint, DistrictId.Shibuya, 450f, 300f, 40f,
            "Skyline viewpoint on the east ring"),
        // Other districts — 60 sq km layout centres (2026-11-09).
        new DiscoverablePlace("shinjuku_towers", "Shinjuku Towers",
            PlaceKind.Landmark, DistrictId.Shinjuku, 2030f, 1830f, 45f,
            "Glass towers of the business district"),
        new DiscoverablePlace("ginza_boulevard", "Ginza Boulevard",
            PlaceKind.Landmark, DistrictId.Ginza, 2000f, 0f, 45f,
            "Luxury shopping boulevard"),
        new DiscoverablePlace("asakusa_gate", "Asakusa Temple Gate",
            PlaceKind.Landmark, DistrictId.Asakusa, 0f, 1750f, 45f,
            "The temple gate of old Tokyo"),
        new DiscoverablePlace("akihabara_neon", "Electric Town",
            PlaceKind.Landmark, DistrictId.Akihabara, 2050f, -1950f, 45f,
            "Neon electronics canyon"),
        new DiscoverablePlace("bay_bridge", "Bay Bridge",
            PlaceKind.Viewpoint, DistrictId.TokyoBay, 0f, -2160f, 45f,
            "Ring highway over the water"),
        new DiscoverablePlace("bay_gas", "Bay Gas Station",
            PlaceKind.GasStation, DistrictId.TokyoBay, 300f, -2160f, 35f,
            "Port-side fuel stop"),
        new DiscoverablePlace("bay_garage", "Bay Garage",
            PlaceKind.Garage, DistrictId.TokyoBay, -300f, -2160f, 35f,
            "Bay-side garage"),
        new DiscoverablePlace("ueno_sakura", "Sakura Grove",
            PlaceKind.Landmark, DistrictId.UenoPark, -1950f, 1750f, 45f,
            "Cherry blossom park — best in spring"),
        new DiscoverablePlace("ueno_pond", "Ueno Pond",
            PlaceKind.Viewpoint, DistrictId.UenoPark, -1850f, 1850f, 40f,
            "Quiet pond in the park"),
    };

    /// <summary>Total discoverable places (HUD denominator).</summary>
    public static int TotalPlaces { get { return Places.Length; } }

    // ---- Project PLATEAU map (real Tokyo geometry) ----
    // The converted PLATEAU FBX files live under
    // Assets/Resources/Models/Map/Plateau/ and are loaded at runtime by
    // PlateauMapLoader. Each district chunk is stored in "PLATEAU Unity"
    // coords (metres from Shibuya Crossing, X east, Z north — the same
    // convention as this file) and placed so its real anchor lands on
    // the district's game centre below. The procedural CityVisuals map
    // stays the fallback for any district without PLATEAU files.

    /// <summary>Real anchor of a district in converted PLATEAU Unity
    /// coords (metres from Shibuya Crossing). See PlateauMap.AnchorUnity
    /// for the table and its provenance (pyproj EPSG:6677).</summary>
    public static Vector2 PlateauAnchor(DistrictId id)
    {
        int i = (int)id;
        if (i < 0 || i >= PlateauMap.AnchorUnity.Length) return Vector2.zero;
        return PlateauMap.AnchorUnity[i];
    }

    /// <summary>World offset that places a district's PLATEAU chunk so
    /// its real anchor lands on the district's game centre.</summary>
    public static Vector3 PlateauOffset(DistrictId id)
    {
        return PlateauMap.PlateauOffset(id);
    }

    /// <summary>True when the district has PLATEAU files listed in the
    /// project (ResidentialSW never does — it is fictional suburbs).</summary>
    public static bool HasPlateauMap(DistrictId id)
    {
        PlateauMap.MapFile[] files = PlateauMap.FilesFor(id);
        return files != null && files.Length > 0;
    }
}

using UnityEngine;

/// <summary>Project PLATEAU Tokyo map — static config (data only).
///
/// The real Tokyo 3D map (buildings / roads / bridges / terrain) comes
/// from Japan's Ministry of Land, Infrastructure, Transport and Tourism
/// (MLIT) Project PLATEAU, CC-BY 4.0. The raw FBX files (EPSG:6677,
/// Japan Plane Rectangular CS Zone IX, metres) were converted to
/// Unity-ready FBX by ~/workspace/model-pipeline/plateau_convert.py:
/// re-centred on Shibuya Crossing, mirrored to Unity X=east / Z=north,
/// meshes merged per material, LOD2 facade textures downsized to 512 px
/// and LOD2 tiles decimated to &lt;=150k tris. See
/// ~/workspace/model-pipeline/plateau/PLATEAU_MAP_README.md for the
/// full pipeline record (per-file tri counts, sizes, verification).
///
/// Converted files live under Assets/Resources/Models/Map/Plateau/
/// (Resources so PlateauMapLoader can load them at runtime without a
/// scene edit). Only the game-area tiles are in the project — the full
/// converted set is in ~/workspace/model-pipeline/plateau-out/.
///
/// Coordinate note: converted geometry is in "PLATEAU Unity" coords,
/// metres from Shibuya Crossing (X east, Z north). Each district chunk
/// is placed so its real anchor lands on the district's (fictional,
/// compressed) game centre from DistrictData — see PlateauOffset.
/// The procedural CityVisuals map stays as the fallback: it is used
/// whenever a district has no PLATEAU files in the project.
/// </summary>
public static class PlateauMap
{
    /// <summary>Required attribution (PLATEAU Site Policy / CC-BY 4.0).
    /// Shown on the main menu credits line (MainMenu.creditsText) and
    /// in PlateauMapLoader's load log.</summary>
    public const string Attribution = "Source: Project PLATEAU by MLIT";

    /// <summary>Full credits line for the menu.</summary>
    public const string CreditsLine =
        "3D map data: Source: Project PLATEAU by MLIT";

    /// <summary>Resources folder (relative to Assets/Resources) holding
    /// the converted map. Files are loaded without extension, e.g.
    /// Resources.Load&lt;GameObject&gt;("Models/Map/Plateau/Shibuya/lod2/53393596_bldg_6677").</summary>
    public const string ResourcesRoot = "Models/Map/Plateau";

    /// <summary>True when the PLATEAU loader should replace the
    /// procedural map. Set false to force the old procedural city
    /// (fallback) even when PLATEAU files are present.</summary>
    public static bool UsePlateauMap = true;

    /// <summary>Real anchor of each district in converted Unity coords
    /// (metres from Shibuya Crossing, X east, Z north). Computed with
    /// pyproj EPSG:4326 -&gt; EPSG:6677 from the anchor lat/lon in the
    /// download manifest. Index = (int)DistrictData.DistrictId;
    /// ResidentialSW has no PLATEAU data (0, 0 unused).</summary>
    public static readonly Vector2[] AnchorUnity = new Vector2[]
    {
        new Vector2(0f, 0f),         // Shibuya — Shibuya Crossing
        new Vector2(268f, 3805f),    // Shinjuku
        new Vector2(5750f, 1348f),   // Ginza
        new Vector2(8703f, 6116f),   // Asakusa
        new Vector2(6567f, 4265f),   // Akihabara
        new Vector2(6743f, -3279f),  // TokyoBay — Odaiba waterfront
        new Vector2(6568f, 6217f),   // UenoPark
        new Vector2(0f, 0f),         // ResidentialSW — no PLATEAU data
    };

    /// <summary>World offset that places a district's converted chunk so
    /// its real anchor lands on the district's game centre
    /// (DistrictData.Get(id).centre). Returns (0,0,0) for districts
    /// without PLATEAU data.</summary>
    public static Vector3 PlateauOffset(DistrictData.DistrictId id)
    {
        int i = (int)id;
        if (i < 0 || i >= AnchorUnity.Length || i == 7) return Vector3.zero;
        Vector2 centre = DistrictData.Get(id).centre;
        Vector2 anchor = AnchorUnity[i];
        return new Vector3(centre.x - anchor.x, 0f, centre.y - anchor.y);
    }

    /// <summary>One converted file, relative to ResourcesRoot, without
    /// the .fbx extension. Kind is lod1 / lod2 / brid / tran / dem.</summary>
    public struct MapFile
    {
        public string district;
        public string kind;
        public string file;
        public MapFile(string district, string kind, string file)
        {
            this.district = district;
            this.kind = kind;
            this.file = file;
        }
        public string ResourcePath
        {
            get { return ResourcesRoot + "/" + district + "/" + kind + "/" + file; }
        }
        /// <summary>True for terrain/roads: the loader adds a
        /// MeshCollider so the car can drive on them.</summary>
        public bool NeedsCollider { get { return kind == "dem" || kind == "tran"; } }
    }

    // ---- Converted files present in the project -------------------------
    // GENERATED SECTION — produced from new-layout-60sqkm.json (the
    // 60 sq km dense layout, 2026-11-09) + the conversion results by
    // ~/workspace/model-pipeline/plateau/gen_plateau_map_cs_60sqkm.py.
    // Do not hand-edit: re-run that generator after a conversion batch
    // (it re-runs the conflict pass via compute_new_layout.py first).
    // One owner district per tile (the old registry listed the same tile
    // under 2-3 districts); LOD2 wins over its LOD1 sibling; Ginza
    // files not yet converted are PENDING lines. Asakusa and Ueno Park
    // have NO LOD2 data in the PLATEAU package anywhere near their
    // anchors, so they ship LOD1 massing only (documented fallback,
    // not a conversion gap).

    public static readonly MapFile[] ShibuyaFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM SHIBUYA
        new MapFile("shibuya", "lod2", "53393585_bldg_6677"),
        new MapFile("shibuya", "lod2", "53393586_bldg_6677"),
        new MapFile("shibuya", "lod2", "53393595_bldg_6677"),
        new MapFile("shibuya", "lod2", "53393596_bldg_6677"),
        new MapFile("shibuya", "lod1", "53393584_bldg_6677"),
        new MapFile("shibuya", "lod1", "53393594_bldg_6677"),
        new MapFile("shibuya", "brid", "53393585_brid_6697"),
        new MapFile("shibuya", "brid", "53393586_brid_6697"),
        new MapFile("shibuya", "brid", "53393595_brid_6697"),
        new MapFile("shibuya", "brid", "53393596_brid_6697"),
        new MapFile("shibuya", "dem", "shibuya_533935_dem"),
        new MapFile("shibuya", "tran", "shibuya_533935_tran"),
        // END GENERATED 60SQKM SHIBUYA
    };

    public static readonly MapFile[] ShinjukuFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM SHINJUKU
        new MapFile("shinjuku", "lod2", "53394526_bldg_6677"),
        new MapFile("shinjuku", "lod2", "53394535_bldg_6677"),
        new MapFile("shinjuku", "lod2", "53394536_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394527_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394537_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394546_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394547_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394555_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394556_bldg_6677"),
        new MapFile("shinjuku", "lod1", "53394557_bldg_6677"),
        new MapFile("shinjuku", "brid", "53394526_brid_6677"),
        new MapFile("shinjuku", "brid", "53394535_brid_6677"),
        new MapFile("shinjuku", "dem", "shinjuku_533945_dem"),
        // END GENERATED 60SQKM SHINJUKU
    };

    public static readonly MapFile[] GinzaFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM GINZA
        new MapFile("ginza", "lod2", "53393691_bldg_6677"),
        new MapFile("ginza", "lod2", "53394601a_bldg_6677"),
        new MapFile("ginza", "lod2", "53394601b_bldg_6677"),
        new MapFile("ginza", "lod2", "53394601c_bldg_6677"),
        new MapFile("ginza", "lod2", "53394601d_bldg_6677"),
        new MapFile("ginza", "lod1", "53393692_bldg_6677"),
        new MapFile("ginza", "lod1", "53394602_bldg_6677"),
        new MapFile("ginza", "brid", "53393691_brid_6677"),
        new MapFile("ginza", "brid", "53393692_brid_6677"),
        new MapFile("ginza", "brid", "53394602_brid_6677"),
        new MapFile("ginza", "dem", "ginza_533936_dem"),
        new MapFile("ginza", "dem", "ginza_533946_dem"),
        new MapFile("ginza", "tran", "ginza_533936_tran"),
        new MapFile("ginza", "tran", "ginza_533946_tran"),
        // END GENERATED 60SQKM GINZA
    };

    public static readonly MapFile[] AsakusaFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM ASAKUSA
        new MapFile("asakusa", "lod1", "53394653_bldg_6677"),
        new MapFile("asakusa", "lod1", "53394663_bldg_6677"),
        new MapFile("asakusa", "lod1", "53394673_bldg_6677"),
        new MapFile("asakusa", "dem", "asakusa_533946_dem"),
        new MapFile("asakusa", "tran", "asakusa_533946_tran"),
        // END GENERATED 60SQKM ASAKUSA
    };

    public static readonly MapFile[] AkihabaraFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM AKIHABARA
        new MapFile("akihabara", "lod2", "53394622_bldg_6677"),
        new MapFile("akihabara", "lod1", "53394623_bldg_6677"),
        new MapFile("akihabara", "lod1", "53394632_bldg_6677"),
        new MapFile("akihabara", "lod1", "53394633_bldg_6677"),
        new MapFile("akihabara", "brid", "53394622_brid_6677"),
        new MapFile("akihabara", "dem", "akihabara_533946_dem"),
        new MapFile("akihabara", "tran", "akihabara_533946_tran"),
        // END GENERATED 60SQKM AKIHABARA
    };

    public static readonly MapFile[] TokyoBayFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM TOKYO-BAY
        new MapFile("tokyo-bay", "lod2", "53393641_bldg_6677"),
        new MapFile("tokyo-bay", "lod2", "53393642_bldg_6677"),
        new MapFile("tokyo-bay", "lod2", "53393651_bldg_6677"),
        new MapFile("tokyo-bay", "lod2", "53393652_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393630_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393631_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393632_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393633_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393640_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393650_bldg_6677"),
        new MapFile("tokyo-bay", "lod1", "53393661_bldg_6677"),
        new MapFile("tokyo-bay", "brid", "53393641_brid_6677"),
        new MapFile("tokyo-bay", "brid", "53393642_brid_6677"),
        new MapFile("tokyo-bay", "brid", "53393651_brid_6677"),
        new MapFile("tokyo-bay", "brid", "53393652_brid_6677"),
        new MapFile("tokyo-bay", "brid", "53393661_brid_6677"),
        new MapFile("tokyo-bay", "dem", "tokyo-bay_533936_dem"),
        new MapFile("tokyo-bay", "tran", "tokyo-bay_533936_tran"),
        // END GENERATED 60SQKM TOKYO-BAY
    };

    public static readonly MapFile[] UenoParkFiles = new MapFile[]
    {
        // BEGIN GENERATED 60SQKM UENO
        new MapFile("ueno", "lod1", "53394650_bldg_6677"),
        new MapFile("ueno", "lod1", "53394651_bldg_6677"),
        new MapFile("ueno", "lod1", "53394660_bldg_6677"),
        new MapFile("ueno", "lod1", "53394661_bldg_6677"),
        new MapFile("ueno", "lod1", "53394670_bldg_6677"),
        new MapFile("ueno", "lod1", "53394671_bldg_6677"),
        new MapFile("ueno", "lod1", "53394681_bldg_6677"),
        new MapFile("ueno", "dem", "ueno_533946_dem"),
        new MapFile("ueno", "tran", "ueno_533946_tran"),
        // END GENERATED 60SQKM UENO
    };

    /// <summary>File list for a district id, or null for districts
    /// without PLATEAU data (ResidentialSW).</summary>
    public static MapFile[] FilesFor(DistrictData.DistrictId id)
    {
        switch (id)
        {
            case DistrictData.DistrictId.Shibuya: return ShibuyaFiles;
            case DistrictData.DistrictId.Shinjuku: return ShinjukuFiles;
            case DistrictData.DistrictId.Ginza: return GinzaFiles;
            case DistrictData.DistrictId.Asakusa: return AsakusaFiles;
            case DistrictData.DistrictId.Akihabara: return AkihabaraFiles;
            case DistrictData.DistrictId.TokyoBay: return TokyoBayFiles;
            case DistrictData.DistrictId.UenoPark: return UenoParkFiles;
            default: return null;
        }
    }
}

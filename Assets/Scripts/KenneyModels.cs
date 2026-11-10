using System.Collections.Generic;
using UnityEngine;

/// <summary>Kenney model integration helpers (CC0 kits imported under
/// Assets/Resources/Models/Kenney/). Two material families exist in the
/// kits, and they need different handling:
///  * Atlas kits (CarKit, CityCommercial, CityIndustrial, CitySuburban,
///    CityRoads, ToyCarKit): one shared 512x512 colormap.png per kit.
///    Meshes paint from a 16x16 swatch grid (32x32 px per cell). Cars are
///    recoloured by cloning the atlas and filling the body mesh's dominant
///    UV cell with the car's paint colour.
///  * Colour kits (RacingKit, NatureKit): named materials with a diffuse
///    colour and no atlas (the racing kit's FBX references a colormap.png
///    that does not exist in the kit; its real materials are grey/red/
///    tankco with DiffuseColor). Those materials are converted to URP Lit
///    preserving their colour (and texture where one exists).
/// Kenney models are stylised low-poly (~2k tris, flat colours) — real
/// imported models and a genuine step up from procedural boxes, but NOT
/// AAA-detailed. The detailed Sketchfab models are a later pass.</summary>
public static class KenneyModels
{
    public const string Base = "Models/Kenney/";

    static readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, Material> atlasCache = new Dictionary<string, Material>();
    static readonly Dictionary<string, Material> paintedCache = new Dictionary<string, Material>();
    static readonly Dictionary<string, Texture2D> atlasTexCache = new Dictionary<string, Texture2D>();

    public static GameObject LoadPrefab(string path)
    {
        GameObject p;
        if (!prefabCache.TryGetValue(path, out p))
        {
            p = Resources.Load<GameObject>(path);
            if (p == null) Debug.LogWarning("[KenneyModels] prefab not found: " + path);
            else prefabCache[path] = p;
        }
        return p;
    }

    public static Texture2D AtlasTexture(string kit)
    {
        Texture2D t;
        if (!atlasTexCache.TryGetValue(kit, out t))
        {
            t = Resources.Load<Texture2D>(Base + kit + "/Textures/colormap");
            if (t == null) Debug.LogWarning("[KenneyModels] atlas missing for kit " + kit);
            else atlasTexCache[kit] = t;
        }
        return t;
    }

    /// <summary>URP Lit material using a kit's colour atlas. Flat colours
    /// need some specular to read as paint, but not full metal (no env
    /// map in the showcase scene) — metallic 0.3 / smoothness 0.6.</summary>
    public static Material AtlasMaterial(string kit, float metallic, float smoothness)
    {
        string key = kit + "|" + metallic.ToString("0.00") + "|" + smoothness.ToString("0.00");
        Material m;
        if (atlasCache.TryGetValue(key, out m)) return m;
        Texture2D tex = AtlasTexture(kit);
        m = MeshFactory.Lit("Kenney_" + kit, Color.white, metallic, smoothness);
        if (tex != null) m.SetTexture("_BaseMap", tex);
        atlasCache[key] = m;
        return m;
    }

    /// <summary>Paint column of a mesh: the atlas column (0-15) whose
    /// gradient rows 8-11 carry the most of the mesh's UVs. Kenney's car
    /// atlas lays out paint as vertical gradients in rows 8-11 (e.g.
    /// column 13 = red, 15 = blue, 7 = green, 9 = yellow), glass/trim
    /// greys in rows 4-7 and flat brights in rows 0-3 — so the dominant
    /// cell overall is usually glass grey, NOT paint; the paint column
    /// is the gradient column. Returns -1 when no gradient column holds
    /// at least 5% of the mesh's UVs.</summary>
    public static int PaintColumn(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return -1;
        int[] colCount = new int[16];
        foreach (Vector2 uv in uvs)
        {
            int gy = Mathf.Clamp((int)(uv.y * 16f), 0, 15);
            if (gy < 8 || gy > 11) continue;
            colCount[Mathf.Clamp((int)(uv.x * 16f), 0, 15)]++;
        }
        int best = 0, bx = -1;
        for (int x = 0; x < 16; x++)
            if (colCount[x] > best) { best = colCount[x]; bx = x; }
        return (bx >= 0 && best >= uvs.Length * 0.05f) ? bx : -1;
    }

    /// <summary>Rim column of a wheel mesh: the atlas column (0-15)
    /// whose rows 4-7 carry the most UVs, excluding the tyre cell (5,4).
    /// Used for the R34 gold rims.</summary>
    public static int RimColumn(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return -1;
        int[] colCount = new int[16];
        foreach (Vector2 uv in uvs)
        {
            int gx = Mathf.Clamp((int)(uv.x * 16f), 0, 15);
            int gy = Mathf.Clamp((int)(uv.y * 16f), 0, 15);
            if (gy < 4 || gy > 7) continue;
            if (gx == 5 && gy == 4) continue; // tyre cell
            colCount[gx]++;
        }
        int best = 0, bx = -1;
        for (int x = 0; x < 16; x++)
            if (colCount[x] > best) { best = colCount[x]; bx = x; }
        return bx;
    }

    /// <summary>Painted atlas: clone the kit atlas and fill the mesh's
    /// paint column (rows 8-11) with shaded variants of the car's paint
    /// colour, preserving Kenney's vertical gradient (lightest at the
    /// top, darkest at the bottom). The atlas texture asset must be
    /// readable (TextureImporter.isReadable — set by DumpKenneyModels).
    /// Cached per (kit, colour, column); one 512x512 RGBA32 clone is 1 MB.</summary>
    public static Material PaintedAtlasMaterial(string kit, Color paint, int paintColumn)
    {
        string key = kit + "|" + paint.ToString() + "|col" + paintColumn;
        Material m;
        if (paintedCache.TryGetValue(key, out m)) return m;
        Texture2D src = AtlasTexture(kit);
        if (src == null || paintColumn < 0)
        {
            m = AtlasMaterial(kit, 0.3f, 0.6f);
            paintedCache[key] = m;
            return m;
        }
        Texture2D tex = CloneAtlas(src);
        // Shade factors match Kenney's gradient direction (row 11
        // lightest, row 8 darkest).
        float[] shade = { 0.70f, 0.86f, 1.02f, 1.18f }; // rows 8, 9, 10, 11
        for (int r = 0; r < 4; r++)
        {
            Color32 pc = Shade(paint, shade[r]);
            int y0 = (8 + r) * 32, x0 = paintColumn * 32;
            for (int y = y0; y < y0 + 32 && y < tex.height; y++)
                for (int x = x0; x < x0 + 32 && x < tex.width; x++)
                    tex.SetPixel(x, y, pc);
        }
        tex.Apply(false, false);
        m = MeshFactory.Lit("KenneyPaint_" + kit, Color.white, 0.3f, 0.6f);
        m.SetTexture("_BaseMap", tex);
        paintedCache[key] = m;
        return m;
    }

    /// <summary>Gold atlas for the R34's rims: clone the atlas and fill
    /// the wheel's rim column (rows 4-7) with gold shades.</summary>
    public static Material GoldAtlasMaterial(string kit, int rimColumn)
    {
        string key = kit + "|gold|col" + rimColumn;
        Material m;
        if (paintedCache.TryGetValue(key, out m)) return m;
        Texture2D src = AtlasTexture(kit);
        if (src == null || rimColumn < 0)
        {
            m = AtlasMaterial(kit, 0.3f, 0.6f);
            paintedCache[key] = m;
            return m;
        }
        Texture2D tex = CloneAtlas(src);
        Color gold = new Color(0.85f, 0.65f, 0.25f);
        float[] shade = { 0.75f, 0.9f, 1.05f, 1.15f };
        for (int r = 0; r < 4; r++)
        {
            Color32 pc = Shade(gold, shade[r]);
            int y0 = (4 + r) * 32, x0 = rimColumn * 32;
            for (int y = y0; y < y0 + 32 && y < tex.height; y++)
                for (int x = x0; x < x0 + 32 && x < tex.width; x++)
                    tex.SetPixel(x, y, pc);
        }
        tex.Apply(false, false);
        m = MeshFactory.Lit("KenneyGold_" + kit, Color.white, 0.9f, 0.8f);
        m.SetTexture("_BaseMap", tex);
        paintedCache[key] = m;
        return m;
    }

    static Texture2D CloneAtlas(Texture2D src)
    {
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.filterMode = src.filterMode;
        tex.wrapMode = src.wrapMode;
        tex.SetPixels(src.GetPixels());
        return tex;
    }

    static Color32 Shade(Color c, float f)
    {
        return new Color(
            Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }

    /// <summary>Dominant UV cell (16x16 grid) of a mesh — the atlas swatch
    /// that mesh paints from. UV (0,0) is the texture's bottom-left, the
    /// same origin Texture2D.SetPixel uses, so the cell maps directly to
    /// pixels (cell.x*32, cell.y*32).</summary>
    public static Vector2Int DominantUvCell(Mesh mesh)
    {
        Vector2[] uvs = mesh.uv;
        if (uvs == null || uvs.Length == 0) return new Vector2Int(-1, -1);
        int[,] grid = new int[16, 16];
        foreach (Vector2 uv in uvs)
        {
            int gx = Mathf.Clamp((int)(uv.x * 16f), 0, 15);
            int gy = Mathf.Clamp((int)(uv.y * 16f), 0, 15);
            grid[gx, gy]++;
        }
        int best = 0, bx = 0, by = 0;
        for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++)
            if (grid[x, y] > best) { best = grid[x, y]; bx = x; by = y; }
        return new Vector2Int(bx, by);
    }

    /// <summary>Apply one material to every renderer under root.</summary>
    public static void ApplyMaterial(GameObject root, Material mat)
    {
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }

    /// <summary>Convert imported materials (racing/nature kits: named
    /// materials with diffuse colours, no atlas) to URP Lit, preserving
    /// colour and any texture the material carries.</summary>
    public static void ConvertColorMaterials(GameObject root, float metallic, float smoothness)
    {
        var converted = new Dictionary<Material, Material>();
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material src = mats[i];
                if (src == null) continue;
                Material dst;
                if (!converted.TryGetValue(src, out dst))
                {
                    Color c = src.HasProperty("_Color") ? src.color : Color.white;
                    dst = MeshFactory.Lit("KenneyConv_" + src.name, c, metallic, smoothness);
                    if (src.mainTexture != null) dst.SetTexture("_BaseMap", src.mainTexture);
                    converted[src] = dst;
                }
                if (mats[i] != dst) { mats[i] = dst; changed = true; }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    /// <summary>Overall world-space bounds of an instantiated model.</summary>
    public static Bounds Measure(GameObject root)
    {
        Bounds b = new Bounds(root.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// <summary>Find a descendant transform by name (Kenney FBX children
    /// are named body / wheel-front-left / ...).</summary>
    public static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    /// <summary>Scale a model instance (child of `parent`, already placed
    /// at scale 1) so its world height becomes `targetHeight`, keeping its
    /// base on its current ground plane. Returns the uniform scale used.</summary>
    public static float ScaleToHeight(GameObject inst, float targetHeight)
    {
        Bounds b = Measure(inst);
        if (b.size.y < 0.01f) return 1f;
        float s = targetHeight / b.size.y;
        inst.transform.localScale = inst.transform.localScale * s;
        return s;
    }

    /// <summary>Shift an instance vertically so its bounds sit on baseY
    /// (X/Z untouched — for asymmetric models like street lights whose
    /// bounds centre is not over their base).</summary>
    public static void GroundY(GameObject inst, float baseY)
    {
        Bounds b = Measure(inst);
        inst.transform.position += new Vector3(0f, baseY - b.min.y, 0f);
    }

    /// <summary>After scaling, shift an instance so its bounds sit on
    /// `baseY` and are centred on (cx, cz) in world space.</summary>
    public static void PlaceOnGround(GameObject inst, float cx, float baseY, float cz)
    {
        Bounds b = Measure(inst);
        Vector3 d = new Vector3(cx - b.center.x, baseY - b.min.y, cz - b.center.z);
        inst.transform.position += d;
    }
}

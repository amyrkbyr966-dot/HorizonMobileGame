#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>PLATEAU LOD2 facade-texture FIXUP (2026-11-09, map phase).
///
/// Problem (verified): the converted PLATEAU FBX files carry embedded
/// facade photos, Unity extracts them into &lt;tile&gt;.fbm/*.jpg and creates
/// external URP/Lit .mat files — but links 0/453 materials to textures
/// (_BaseMap.m_Texture: {fileID: 0} in every .mat on disk).
///
/// Why the import hooks failed: PlateauTextureLinker v1 (OnPostprocessModel)
/// and v2 (OnAssignMaterialModel) both logged linked=452, but in-memory
/// material edits during import are NEVER flushed to the external .mat
/// files (Unity itself logs "OnAssignMaterial must return a persistent
/// material (AssetDatabase.CreateAsset)"). Only an explicit
/// AssetDatabase.SaveAssets() OUTSIDE the import persists them.
///
/// This script is that outside pass. It runs AFTER import, loads every
/// external .mat in a tile's Materials/ folder, sets _BaseMap from
/// &lt;tile&gt;.fbm/&lt;material-name&gt;.jpg (.png fallback), marks each
/// changed material dirty, and calls AssetDatabase.SaveAssets() ONCE at
/// the end. Idempotent: materials whose mainTexture is already set are
/// skipped. The "No Name" material (genuinely untextured) is skipped.
///
/// Usage (Unity batch, solo run):
///   Unity -batchmode -nographics -quit -projectPath &lt;proj&gt;
///         -executeMethod PlateauTextureFixup.FixPocTile
///         -logFile /tmp/fixup_poc.log
///   Unity ... -executeMethod PlateauTextureFixup.FixAllLod2 ...
///   Unity ... -executeMethod PlateauTextureFixup.FixTileArg -tile=53393596 ...
///
/// Verify with CheckLod2Textures (diagnostic) + .mat mtimes + YAML grep
/// for _BaseMap fileID. Never trust a log line alone.
/// Bridges are EXCLUDED: their .fbm folders hold .tif files with
/// mismatched names — out of scope for this pass.</summary>
public static class PlateauTextureFixup
{
    const string PlateauRoot = "Assets/Resources/Models/Map/Plateau";
    const string PocFbx = "Assets/Resources/Models/Map/Plateau/shibuya/lod2/53393586_bldg_6677.fbx";

    struct TileResult
    {
        public string tile;
        public int mats, linked, already, noName, otherTile, missing, loadFail;
        public List<string> missingNames;
    }

    // --- Entry points (callable via -executeMethod) ---

    /// <summary>PoC: fix ONLY the shibuya lod2 tile 53393586.</summary>
    public static void FixPocTile()
    {
        var results = new List<TileResult>();
        var texOwner = BuildTextureOwnerMap(new[] { PocFbx });
        results.Add(FixTile(PocFbx, texOwner));
        AssetDatabase.SaveAssets();
        WriteReport(results, "/tmp/plateau_fixup_poc.txt");
        Debug.Log("[PlateauTextureFixup] PoC done. SaveAssets() called. See /tmp/plateau_fixup_poc.txt");
    }

    /// <summary>Fix ALL LOD2 tiles found under Plateau/**/lod2.</summary>
    public static void FixAllLod2()
    {
        string[] tiles = FindLod2Tiles();
        Debug.Log("[PlateauTextureFixup] FixAllLod2: " + tiles.Length + " LOD2 tiles found.");
        var texOwner = BuildTextureOwnerMap(tiles);
        var results = new List<TileResult>();
        foreach (string tile in tiles)
            results.Add(FixTile(tile, texOwner));
        AssetDatabase.SaveAssets();
        WriteReport(results, "/tmp/plateau_fixup_all.txt");
        Debug.Log("[PlateauTextureFixup] FixAllLod2 done. SaveAssets() called. See /tmp/plateau_fixup_all.txt");
    }

    /// <summary>Single-tile mode by tile id, e.g. -tile=53393596 (matches
    /// any lod2 FBX whose file name contains the id).</summary>
    public static void FixTileArg()
    {
        string id = null;
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a.StartsWith("-tile=")) id = a.Substring("-tile=".Length);
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogError("[PlateauTextureFixup] FixTileArg needs -tile=<id>, e.g. -tile=53393596");
            return;
        }
        string tile = FindLod2Tiles().FirstOrDefault(t => Path.GetFileName(t).Contains(id));
        if (tile == null)
        {
            Debug.LogError("[PlateauTextureFixup] No lod2 tile matches id '" + id + "'");
            return;
        }
        var results = new List<TileResult>();
        var texOwner = BuildTextureOwnerMap(new[] { tile });
        results.Add(FixTile(tile, texOwner));
        AssetDatabase.SaveAssets();
        WriteReport(results, "/tmp/plateau_fixup_tile_" + id + ".txt");
        Debug.Log("[PlateauTextureFixup] Tile " + id + " done. SaveAssets() called.");
    }

    // --- Core ---

    static string[] FindLod2Tiles()
    {
        // NOTE (2026-11-09 fix): Path.Combine(dataPath, "../Assets/...") leaves a
        // literal ".." in the path; taking the substring from the FIRST "Assets/"
        // then yields "Assets/../Assets/..." which AssetDatabase.LoadAssetAtPath
        // refuses (null for every material -> loadFail=7369 in the first FixAll
        // run). GetFullPath() normalises the ".." away before enumeration.
        string rootAbs = Path.GetFullPath(Path.Combine(Application.dataPath, "../" + PlateauRoot)).Replace('\\', '/');
        return Directory.GetFiles(rootAbs, "*.fbx", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => p.Contains("/lod2/"))
            .Select(p =>
            {
                int i = p.IndexOf("Assets/");
                return i >= 0 ? p.Substring(i) : p;
            })
            .OrderBy(p => p)
            .ToArray();
    }

    /// <summary>texture basename (no ext) -&gt; owning tile fbx, across all
    /// given tiles. Used to tell "belongs to another tile" apart from
    /// "truly missing" when a district Materials/ folder is shared.</summary>
    static Dictionary<string, string> BuildTextureOwnerMap(string[] tiles)
    {
        var map = new Dictionary<string, string>();
        foreach (string tile in tiles)
        {
            string fbm = FbmDir(tile);
            if (!Directory.Exists(fbm)) continue;
            foreach (string f in Directory.GetFiles(fbm))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png") continue;
                string baseName = Path.GetFileNameWithoutExtension(f);
                if (!map.ContainsKey(baseName)) map[baseName] = tile;
            }
        }
        return map;
    }

    static string FbmDir(string fbxAssetPath)
    {
        string dir = Path.GetDirectoryName(fbxAssetPath).Replace('\\', '/');
        return dir + "/" + Path.GetFileNameWithoutExtension(fbxAssetPath) + ".fbm";
    }

    static TileResult FixTile(string fbxAssetPath, Dictionary<string, string> texOwner)
    {
        var r = new TileResult { tile = fbxAssetPath, missingNames = new List<string>() };
        string dir = Path.GetDirectoryName(fbxAssetPath).Replace('\\', '/');
        string fbmDir = FbmDir(fbxAssetPath);
        string matsDir = dir + "/Materials";
        if (!Directory.Exists(matsDir))
        {
            Debug.LogWarning("[PlateauTextureFixup] " + fbxAssetPath + ": no Materials/ folder, skipped.");
            return r;
        }
        if (!Directory.Exists(fbmDir))
        {
            Debug.LogWarning("[PlateauTextureFixup] " + fbxAssetPath + ": no .fbm folder, skipped.");
            return r;
        }

        string[] matFiles = Directory.GetFiles(matsDir, "*.mat", SearchOption.TopDirectoryOnly);
        System.Array.Sort(matFiles);
        r.mats = matFiles.Length;
        foreach (string matFile in matFiles)
        {
            string matAssetPath = matFile.Replace('\\', '/');
            int i = matAssetPath.IndexOf("Assets/");
            if (i >= 0) matAssetPath = matAssetPath.Substring(i);
            string name = Path.GetFileNameWithoutExtension(matFile);

            if (string.IsNullOrEmpty(name) || name == "No Name")
            {
                r.noName++;
                continue;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matAssetPath);
            if (mat == null)
            {
                r.loadFail++;
                RecordMissing(r, name + " (mat failed to load)");
                continue;
            }
            if (mat.mainTexture != null)
            {
                r.already++;
                continue;
            }

            string texPath = Path.Combine(fbmDir, name + ".jpg").Replace('\\', '/');
            if (!File.Exists(texPath))
            {
                string pngPath = Path.Combine(fbmDir, name + ".png").Replace('\\', '/');
                if (File.Exists(pngPath)) texPath = pngPath;
                else
                {
                    string owner;
                    if (texOwner != null && texOwner.TryGetValue(name, out owner) && owner != fbxAssetPath)
                        r.otherTile++; // belongs to a sibling tile in the same district
                    else
                    {
                        r.missing++;
                        RecordMissing(r, name);
                    }
                    continue;
                }
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null)
            {
                r.missing++;
                RecordMissing(r, name + " (texture file exists but did not load)");
                continue;
            }

            mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            r.linked++;
        }

        Debug.Log("[PlateauTextureFixup] " + fbxAssetPath +
            ": mats=" + r.mats + " linked=" + r.linked + " already=" + r.already +
            " noName=" + r.noName + " otherTile=" + r.otherTile +
            " missing=" + r.missing + " loadFail=" + r.loadFail +
            (r.missingNames.Count > 0 ? " missingNames=[" + string.Join(",", r.missingNames.ToArray()) + "]" : ""));
        return r;
    }

    static void RecordMissing(TileResult r, string name)
    {
        if (r.missingNames.Count < 10) r.missingNames.Add(name);
    }

    static void WriteReport(List<TileResult> results, string path)
    {
        var lines = new List<string>();
        lines.Add("=== PLATEAU TEXTURE FIXUP " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ===");
        int totMats = 0, totLinked = 0, totAlready = 0, totNoName = 0, totOther = 0, totMissing = 0, totFail = 0;
        foreach (TileResult r in results)
        {
            lines.Add(r.tile + ": mats=" + r.mats + " linked=" + r.linked + " already=" + r.already +
                " noName=" + r.noName + " otherTile=" + r.otherTile + " missing=" + r.missing +
                " loadFail=" + r.loadFail +
                (r.missingNames.Count > 0 ? " missingNames=[" + string.Join(",", r.missingNames.ToArray()) + "]" : ""));
            totMats += r.mats; totLinked += r.linked; totAlready += r.already;
            totNoName += r.noName; totOther += r.otherTile; totMissing += r.missing; totFail += r.loadFail;
        }
        lines.Add("TOTAL: mats=" + totMats + " linked=" + totLinked + " already=" + totAlready +
            " noName=" + totNoName + " otherTile=" + totOther + " missing=" + totMissing + " loadFail=" + totFail);
        File.WriteAllText(path, string.Join("\n", lines) + "\n");
    }
}
#endif

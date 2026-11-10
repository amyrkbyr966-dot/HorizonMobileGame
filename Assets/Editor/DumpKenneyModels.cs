#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>One-off diagnostics: after the Kenney FBX files are copied
/// into Assets/Resources/Models/Kenney, instantiate each model and log
/// its true size, wheel positions, UV range and materials, so the
/// runtime integration code can scale/align/recolour them correctly.
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod DumpKenneyModels.Dump -quit -logFile /home/hatch/workspace/kenney-dump.log</summary>
public static class DumpKenneyModels
{
    public static void Dump()
    {
        MakeAtlasesReadable();
        string[] cars = { "sedan", "sedan-sports", "hatchback-sports", "race", "race-future", "suv", "suv-luxury" };
        foreach (string c in cars) DumpModel("Assets/Resources/Models/Kenney/CarKit/" + c + ".fbx", true);
        DumpModel("Assets/Resources/Models/Kenney/CarKit/taxi.fbx", true);
        DumpModel("Assets/Resources/Models/Kenney/CarKit/van.fbx", true);
        DumpModel("Assets/Resources/Models/Kenney/CarKit/truck.fbx", true);
        string[] env =
        {
            "CityCommercial/building-a", "CityCommercial/building-skyscraper-a",
            "CityCommercial/building-skyscraper-e", "CityCommercial/low-detail-building-a",
            "CityCommercial/low-detail-building-wide-a",
            "CityIndustrial/building-a", "CitySuburban/building-type-a",
            "RacingKit/pitsGarage", "RacingKit/pitsOffice", "RacingKit/billboard",
            "RacingKit/grandStand", "RacingKit/tent", "RacingKit/lightPostModern",
            "RacingKit/barrierWhite", "RacingKit/flagCheckers",
            "CityRoads/light-curved", "CityRoads/traffic-light", "CityRoads/road-sign-stop",
            "CityRoads/road-straight", "CityRoads/construction-barrier",
            "NatureKit/tree_default", "NatureKit/tree_oak", "NatureKit/tree_pineDefaultA",
            "NatureKit/tree_cone", "NatureKit/tree_palm", "NatureKit/rock_smallA",
            "ToyCarKit/item-coin-gold",
        };
        foreach (string e in env) DumpModel("Assets/Resources/Models/Kenney/" + e + ".fbx", false);
        Debug.Log("[DumpKenneyModels] DONE");
    }

    // The runtime paint step clones the atlas and fills one swatch, so
    // the atlas textures must be CPU-readable (GetPixels). Set once here.
    static void MakeAtlasesReadable()
    {
        string[] kits = { "CarKit", "CityCommercial", "CityIndustrial", "CitySuburban", "CityRoads", "ToyCarKit" };
        foreach (string kit in kits)
        {
            string path = "Assets/Resources/Models/Kenney/" + kit + "/Textures/colormap.png";
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { Debug.LogWarning("[Dump] no importer for " + path); continue; }
            if (!ti.isReadable)
            {
                ti.isReadable = true;
                ti.SaveAndReimport();
            }
            Debug.Log("[Dump] atlas readable: " + path + " isReadable=" + ti.isReadable);
        }
    }

    static void DumpModel(string path, bool isCar)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogError("[Dump] MISSING prefab: " + path); return; }
        GameObject go = Object.Instantiate(prefab);
        go.name = "DUMP_" + prefab.name;
        // Overall bounds from renderers.
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        Debug.Log($"[Dump] {prefab.name}: overall size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) center=({b.center.x:F2},{b.center.y:F2},{b.center.z:F2}) minY={b.min.y:F2}");
        // Children: names, local positions, mesh bounds (for wheels: the
        // smallest bounds axis is the axle direction).
        foreach (Transform t in go.GetComponentsInChildren<Transform>())
        {
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Mesh m = mf.sharedMesh;
            Bounds mb = m.bounds;
            string uvInfo = "";
            if (isCar && (t.name == "body" || t.name.StartsWith("wheel")) && m.uv != null && m.uv.Length > 0)
            {
                Vector2 mn = m.uv[0], mx = m.uv[0];
                // Dominant UV cell: bucket UVs into a 16x16 grid and count.
                int[,] grid = new int[16, 16];
                foreach (Vector2 uv in m.uv)
                {
                    mn = Vector2.Min(mn, uv); mx = Vector2.Max(mx, uv);
                    int gx = Mathf.Clamp((int)(uv.x * 16f), 0, 15);
                    int gy = Mathf.Clamp((int)(uv.y * 16f), 0, 15);
                    grid[gx, gy]++;
                }
                int best = 0, bx = 0, by = 0;
                for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++)
                    if (grid[x, y] > best) { best = grid[x, y]; bx = x; by = y; }
                uvInfo = $" uvMin=({mn.x:F3},{mn.y:F3}) uvMax=({mx.x:F3},{mx.y:F3}) dominantCell=({bx},{by}) count={best}/{m.uv.Length}";
            }
            Debug.Log($"[Dump]   child {t.name}: localPos=({t.localPosition.x:F2},{t.localPosition.y:F2},{t.localPosition.z:F2}) localRot={t.localRotation.eulerAngles} meshBounds size=({mb.size.x:F2},{mb.size.y:F2},{mb.size.z:F2}) center=({mb.center.x:F2},{mb.center.y:F2},{mb.center.z:F2}) verts={m.vertexCount} submeshes={m.subMeshCount}{uvInfo}");
        }
        // Materials on the imported model.
        var mats = new HashSet<string>();
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            foreach (Material mat in r.sharedMaterials)
                if (mat != null)
                {
                    string tex = mat.mainTexture != null ? mat.mainTexture.name + " " + mat.mainTexture.width + "x" + mat.mainTexture.height : "no-tex";
                    string key = mat.name + " shader=" + mat.shader.name + " color=" + mat.color + " tex=" + tex;
                    if (mats.Add(key)) Debug.Log($"[Dump]   material: {key}");
                }
        Object.DestroyImmediate(go);
    }
}
#endif

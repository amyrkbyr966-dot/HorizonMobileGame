#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Builds the 55 parked/showcase car prefabs at
/// Assets/Resources/Models/Cars/&lt;CarNameNoSpaces&gt;.prefab from the Kenney
/// stand-ins (scaled to the 2.7 m wheelbase, painted in the roster
/// colour). Consumers (CarMeetManager, ShowcaseGarageManager) load these
/// prefabs directly by path; CarVisuals does NOT use them (it loads the
/// raw Kenney FBX via CarModelMap so it can reparent the wheels).
/// When a detailed Sketchfab model lands for a car, the pipeline agent
/// replaces that car's prefab at this same path and adds the index to
/// CarModelMap.DetailedCars — no code change needed anywhere.
/// Paint: one painted atlas PNG per car (the atlas clone with the body
/// mesh's dominant UV cell filled with the roster colour), saved under
/// Models/Cars/Atlas/ and referenced by a material asset under
/// Models/Cars/Materials/. The editor assembly references
/// ImageConversion, so EncodeToPNG works here (it does NOT compile in
/// player code — see the PhotoModeController note).
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildCarPrefabs.Build -quit -logFile /home/hatch/workspace/build-car-prefabs.log</summary>
public static class BuildCarPrefabs
{
    public static void Build()
    {
        string outDir = "Assets/Resources/Models/Cars";
        string atlasDir = outDir + "/Atlas";
        string matDir = outDir + "/Materials";
        System.IO.Directory.CreateDirectory(outDir);
        System.IO.Directory.CreateDirectory(atlasDir);
        System.IO.Directory.CreateDirectory(matDir);

        // Pass 1: write one painted atlas PNG per car — the body's
        // paint-gradient column (rows 8-11) filled with shaded variants
        // of the roster colour (see KenneyModels.PaintColumn; the
        // dominant cell is glass grey, NOT paint).
        for (int i = 0; i < CarSelection.Cars.Length; i++)
        {
            CarDef def = CarSelection.Cars[i];
            string name = def.carName.Replace(" ", "");
            string style = CarModelMap.KenneyStyleForCar[i];
            GameObject prefab = KenneyModels.LoadPrefab("Models/Kenney/CarKit/" + style);
            if (prefab == null) { Debug.LogError("[BuildCarPrefabs] missing Kenney style " + style); continue; }
            Transform body = KenneyModels.FindDeep(prefab.transform, "body");
            int paintColumn = -1;
            if (body != null)
            {
                MeshFilter mf = body.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    paintColumn = KenneyModels.PaintColumn(mf.sharedMesh);
            }
            Texture2D src = KenneyModels.AtlasTexture("CarKit");
            if (src == null || paintColumn < 0)
            {
                Debug.LogWarning("[BuildCarPrefabs] no paint column for " + name + " — unpainted");
                continue;
            }
            Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.SetPixels(src.GetPixels());
            float[] shade = { 0.70f, 0.86f, 1.02f, 1.18f }; // rows 8, 9, 10, 11
            for (int r = 0; r < 4; r++)
            {
                Color32 pc = Shade(def.bodyColor, shade[r]);
                int y0 = (8 + r) * 32, x0 = paintColumn * 32;
                for (int y = y0; y < y0 + 32; y++)
                    for (int x = x0; x < x0 + 32; x++)
                        tex.SetPixel(x, y, pc);
            }
            tex.Apply(false, false);
            System.IO.File.WriteAllBytes(atlasDir + "/" + name + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh();

        // Pass 2: material assets + prefabs.
        int built = 0, failed = 0;
        for (int i = 0; i < CarSelection.Cars.Length; i++)
        {
            try { BuildOne(i, outDir, atlasDir, matDir); built++; }
            catch (System.Exception e)
            {
                Debug.LogError("[BuildCarPrefabs] car " + i + " failed: " + e.Message);
                failed++;
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildCarPrefabs] DONE built={built} failed={failed}");
    }

    static void BuildOne(int index, string outDir, string atlasDir, string matDir)
    {
        CarDef def = CarSelection.Cars[index];
        string name = def.carName.Replace(" ", "");
        string style = CarModelMap.KenneyStyleForCar[index];
        GameObject prefab = KenneyModels.LoadPrefab("Models/Kenney/CarKit/" + style);
        if (prefab == null) throw new System.Exception("missing Kenney style " + style);

        // Painted material asset (falls back to the plain atlas when the
        // painted PNG was not written).
        Texture2D painted = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasDir + "/" + name + ".png");
        Material mat;
        string matPath = matDir + "/" + name + ".mat";
        if (painted != null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_Color", Color.white);
            mat.SetTexture("_BaseMap", painted);
            mat.SetFloat("_Metallic", 0.3f);
            mat.SetFloat("_Smoothness", 0.6f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat = KenneyModels.AtlasMaterial("CarKit", 0.3f, 0.6f);
        }

        GameObject inst = Object.Instantiate(prefab);
        inst.name = name;
        // Scale to the 2.7 m wheelbase (same math as CarVisuals).
        Transform wfl = KenneyModels.FindDeep(inst.transform, "wheel-front-left");
        Transform wrl = KenneyModels.FindDeep(inst.transform, "wheel-back-left");
        if (wfl == null || wrl == null) throw new System.Exception("missing wheel nodes in " + style);
        float wb = Mathf.Abs(wfl.localPosition.z - wrl.localPosition.z);
        if (wb < 0.1f) throw new System.Exception("degenerate wheelbase in " + style);
        float s = 2.7f / wb;
        inst.transform.localScale = Vector3.one * s;
        inst.transform.localPosition = Vector3.zero; // Kenney origin is at ground level (minY 0).
        KenneyModels.ApplyMaterial(inst, mat);
        // R34 gold rims (same atlas trick as CarVisuals, as an asset).
        if (index == 38) ApplyGoldRimsAsset(inst, atlasDir, matDir, name);
        string path = outDir + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
    }

    static void ApplyGoldRimsAsset(GameObject inst, string atlasDir, string matDir, string name)
    {
        Texture2D src = KenneyModels.AtlasTexture("CarKit");
        if (src == null) return;
        Transform wheel = KenneyModels.FindDeep(inst.transform, "wheel-front-left");
        if (wheel == null) return;
        MeshFilter mf = wheel.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        int rimColumn = KenneyModels.RimColumn(mf.sharedMesh);
        if (rimColumn < 0) return;
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.SetPixels(src.GetPixels());
        Color gold = new Color(0.85f, 0.65f, 0.25f);
        float[] shade = { 0.75f, 0.9f, 1.05f, 1.15f }; // rows 4, 5, 6, 7
        for (int r = 0; r < 4; r++)
        {
            Color32 pc = Shade(gold, shade[r]);
            int y0 = (4 + r) * 32, x0 = rimColumn * 32;
            for (int y = y0; y < y0 + 32; y++)
                for (int x = x0; x < x0 + 32; x++)
                    tex.SetPixel(x, y, pc);
        }
        tex.Apply(false, false);
        System.IO.File.WriteAllBytes(atlasDir + "/" + name + "_gold.png", tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.Refresh();
        Texture2D painted = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasDir + "/" + name + "_gold.png");
        if (painted == null) return;
        Material goldMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        goldMat.SetColor("_BaseColor", Color.white);
        goldMat.SetColor("_Color", Color.white);
        goldMat.SetTexture("_BaseMap", painted);
        goldMat.SetFloat("_Metallic", 0.9f);
        goldMat.SetFloat("_Smoothness", 0.8f);
        AssetDatabase.CreateAsset(goldMat, matDir + "/" + name + "_gold.mat");
        foreach (string w in new string[] { "wheel-front-left", "wheel-front-right",
            "wheel-back-left", "wheel-back-right" })
        {
            Transform t = KenneyModels.FindDeep(inst.transform, w);
            Renderer r = t != null ? t.GetComponent<Renderer>() : null;
            if (r != null) r.sharedMaterial = goldMat;
        }
    }

    static Color32 Shade(Color c, float f)
    {
        return new Color(
            Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }
}
#endif

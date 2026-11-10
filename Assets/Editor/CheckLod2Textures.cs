#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>DIAGNOSTIC (temporary, 2026-11-09): LOD2 facade-texture check.
/// Force-reimports ONE PLATEAU tile (shibuya lod2 53393586) with the
/// current PlateauImportSettings, then counts how many of its materials
/// have mainTexture linked. Writes a plain-text report to
/// /tmp/lod2_texture_check.txt AND logs the same lines via Debug.Log.
/// Delete after the verdict is reported.</summary>
public static class CheckLod2Textures
{
    const string FbxPath = "Assets/Resources/Models/Map/Plateau/shibuya/lod2/53393586_bldg_6677.fbx";

    public static void Run()
    {
        var lines = new List<string>();
        try
        {
            lines.Add("=== LOD2 TEXTURE CHECK 2026-11-09 ===");
            lines.Add("fbx: " + FbxPath);
            lines.Add("fbx exists on disk: " + File.Exists(Path.Combine(Application.dataPath, "../" + FbxPath)));
            lines.Add("fbx bytes: " + new FileInfo(Path.Combine(Application.dataPath, "../" + FbxPath)).Length);

            // Force reimport with the CURRENT PlateauImportSettings postprocessor.
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // What did the import produce on disk?
            string dir = Path.GetDirectoryName(Path.Combine(Application.dataPath, "../" + FbxPath));
            string fbm = Path.Combine(dir, "53393586_bldg_6677.fbm");
            lines.Add(".fbm folder exists: " + Directory.Exists(fbm));
            if (Directory.Exists(fbm))
            {
                string[] texFiles = Directory.GetFiles(fbm);
                lines.Add(".fbm file count: " + texFiles.Length);
                for (int i = 0; i < Mathf.Min(5, texFiles.Length); i++)
                    lines.Add("  .fbm sample: " + Path.GetFileName(texFiles[i]) + " " + new FileInfo(texFiles[i]).Length + " bytes");
            }
            string[] matsOnDisk = Directory.GetFiles(dir, "*.mat", SearchOption.AllDirectories);
            lines.Add(".mat files next to fbx (recursive): " + matsOnDisk.Length);
            for (int i = 0; i < Mathf.Min(5, matsOnDisk.Length); i++)
                lines.Add("  .mat sample: " + matsOnDisk[i]);

            // Load the imported model and count materials/textures.
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            lines.Add("model root loaded: " + (root != null));
            if (root == null)
            {
                lines.Add("RESULT: FAIL - model did not load");
            }
            else
            {
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                lines.Add("renderers: " + renderers.Length);
                var uniqueMats = new HashSet<Material>();
                int withTex = 0, withoutTex = 0, noUv = 0;
                var samples = new List<string>();
                foreach (Renderer r in renderers)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && (mf.sharedMesh.uv == null || mf.sharedMesh.uv.Length == 0))
                        noUv++;
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (!uniqueMats.Add(m)) continue;
                        if (m.mainTexture != null)
                        {
                            withTex++;
                            if (samples.Count < 10)
                                samples.Add("  TEX: mat=" + m.name + " tex=" + m.mainTexture.name +
                                            " " + m.mainTexture.width + "x" + m.mainTexture.height +
                                            " shader=" + m.shader.name);
                        }
                        else
                        {
                            withoutTex++;
                            if (samples.Count < 10 && withoutTex <= 3)
                                samples.Add("  NOTEX: mat=" + m.name + " shader=" + m.shader.name + " color=" + m.color);
                        }
                    }
                }
                lines.Add("unique materials: " + uniqueMats.Count);
                lines.Add("materials WITH mainTexture: " + withTex);
                lines.Add("materials WITHOUT mainTexture: " + withoutTex);
                lines.Add("meshes with NO uv attribute: " + noUv);
                lines.AddRange(samples);
                lines.Add(withTex > 0
                    ? "RESULT: SUCCESS - " + withTex + "/" + uniqueMats.Count + " materials have textures"
                    : "RESULT: FAIL - 0/" + uniqueMats.Count + " materials have textures");
            }
        }
        catch (System.Exception ex)
        {
            lines.Add("RESULT: EXCEPTION - " + ex.GetType().Name + ": " + ex.Message);
            lines.Add(ex.StackTrace);
        }
        string report = string.Join("\n", lines);
        File.WriteAllText("/tmp/lod2_texture_check.txt", report);
        Debug.Log(report);
    }
}
#endif

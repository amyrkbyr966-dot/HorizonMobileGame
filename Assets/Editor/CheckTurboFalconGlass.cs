#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>One-off check/fix for the Turbo Falcon glass material
/// (early-access build, 2026-11-08). The fixed TurboFalcon.fbx
/// (md5 d55491...) has a pure-white OPAQUE "Glass" material, so the
/// canopy would render as a white slab in-game. The FBX importer is
/// set to Import-via-Material-Naming (materialSearch = everywhere),
/// so a project material named exactly "Glass" is used instead of
/// generating one from the FBX. This script dumps the FBX's imported
/// materials, creates that Glass material (URP Lit, transparent dark
/// tint, matching the approved Blender renders) if missing, forces a
/// reimport, and dumps again to prove the fix landed. Run:
/// Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod CheckTurboFalconGlass.Run -quit -logFile /tmp/unity-glass-check.log
/// </summary>
public static class CheckTurboFalconGlass
{
    const string FbxPath = "Assets/Resources/Models/Cars/TurboFalcon.fbx";
    const string GlassMatPath = "Assets/Resources/Models/Cars/Materials/Glass.mat";

    public static void Run()
    {
        Debug.Log("[GlassCheck] BEFORE fix:");
        DumpMaterials();
        EnsureGlassMaterial();
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("[GlassCheck] AFTER fix + reimport:");
        DumpMaterials();
        Debug.Log("[GlassCheck] DONE");
    }

    static void DumpMaterials()
    {
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
        int count = 0;
        foreach (Object o in all)
        {
            if (o is Material m)
            {
                count++;
                string surf = m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("0.0") : "n/a";
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta;
                Debug.Log($"[GlassCheck] MAT name={m.name} shader={m.shader.name} color=({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00}) _Surface={surf} queue={m.renderQueue}");
            }
        }
        Debug.Log($"[GlassCheck] {count} materials in {FbxPath}");
    }

    static void EnsureGlassMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(GlassMatPath);
        if (existing != null)
        {
            Debug.Log("[GlassCheck] Glass.mat already exists at " + GlassMatPath);
            return;
        }
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("[GlassCheck] URP Lit shader not found!");
            return;
        }
        var glass = new Material(lit);
        glass.name = "Glass";
        glass.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.11f, 0.4f));
        glass.SetFloat("_Surface", 1f); // Transparent
        glass.SetFloat("_Blend", 0f);   // Alpha
        glass.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0f);
        glass.SetFloat("_Metallic", 0.2f);
        glass.SetFloat("_Smoothness", 0.9f);
        glass.renderQueue = 3000;
        System.IO.Directory.CreateDirectory(Application.dataPath + "/Resources/Models/Cars/Materials");
        AssetDatabase.CreateAsset(glass, GlassMatPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[GlassCheck] Created " + GlassMatPath + " (URP Lit, transparent dark tint, alpha 0.4)");
    }
}
#endif

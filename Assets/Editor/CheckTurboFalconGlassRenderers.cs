#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Ground-truth check for the Turbo Falcon glass fix: reports
/// the materials actually referenced by the model's MeshRenderers
/// (what renders in-game), not the FBX sub-assets. Run:
/// Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod CheckTurboFalconGlassRenderers.Run -quit -logFile /tmp/unity-glass-renderers.log
/// </summary>
public static class CheckTurboFalconGlassRenderers
{
    const string FbxPath = "Assets/Resources/Models/Cars/TurboFalcon.fbx";

    public static void Run()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (root == null)
        {
            Debug.LogError("[GlassRenderers] Could not load " + FbxPath);
            return;
        }
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log($"[GlassRenderers] {renderers.Length} MeshRenderers in {FbxPath}");
        bool glassFound = false;
        foreach (MeshRenderer r in renderers)
        {
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (m.name != "Glass") continue;
                glassFound = true;
                string surf = m.HasProperty("_Surface") ? m.GetFloat("_Surface").ToString("0.0") : "n/a";
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta;
                Debug.Log($"[GlassRenderers] GLASS on '{r.gameObject.name}': shader={m.shader.name} color=({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00}) _Surface={surf} queue={m.renderQueue} assetPath={AssetDatabase.GetAssetPath(m)}");
            }
        }
        if (!glassFound) Debug.LogError("[GlassRenderers] NO renderer uses a material named Glass!");
        else Debug.Log("[GlassRenderers] DONE");
    }
}
#endif

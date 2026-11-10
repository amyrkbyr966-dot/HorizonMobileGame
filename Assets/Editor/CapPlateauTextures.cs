#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

/// <summary>
/// Cap PLATEAU map texture import size for the STANDALONE build target
/// (and the Default platform), using explicit per-platform settings.
/// 2026-11-10: the Linux build OOM-kills inside the 6 GB cell limit.
/// 256px cap (~1.3 GB RGBA32) still OOM'd at 34 min into the build;
/// 128px (~0.37 GB) is the temporary verification-build cap so the
/// import + drive test can be proven in-game. The cap is reversible
/// (re-run with 256) once the build fits; 256px remains the design target.
/// Background: .fbm JPEGs reach 4096x4096, ~16.3 GB as RGBA32 at full res.
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod CapPlateauTextures.CapAll -quit -logFile ~/workspace/unity-texcap-2026-11-10.log
/// Only touches textures under Assets/Resources/Models/Map/Plateau.
/// Source .jpg files on disk are untouched; only the .meta import settings change.
/// </summary>
public static class CapPlateauTextures
{
    public const int CapSize = 128;
    public static void CapAll()
    {
        const string root = "Assets/Resources/Models/Map/Plateau";
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { root });
        Debug.Log("[CapPlateauTextures] found " + guids.Length + " textures under " + root);
        int changed = 0, already = 0, skipped = 0;
        foreach (string g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { skipped++; continue; }
            bool need = false;
            foreach (string platform in new[] { "Standalone", "DefaultTexturePlatform" })
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                if (ps.maxTextureSize != CapSize || !ps.overridden)
                {
                    ps.maxTextureSize = CapSize;
                    ps.overridden = true;
                    ti.SetPlatformTextureSettings(ps);
                    need = true;
                }
            }
            if (need) changed++; else already++;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string summary = "found=" + guids.Length + " changed=" + changed
            + " already" + CapSize + "=" + already + " skipped=" + skipped;
        Debug.Log("[CapPlateauTextures] DONE " + summary);
        File.WriteAllText("/tmp/plateau_texcap.txt", summary + "\n");
    }
}
#endif

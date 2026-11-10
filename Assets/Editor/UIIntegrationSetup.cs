#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// UI integration setup (UI phase, 2026-11-10). Run once in batchmode:
///   Unity -batchmode -nographics -projectPath <proj>
///     -executeMethod UIIntegrationSetup.SetupUIArt -quit -logFile <log>
/// 1. Sets TextureImporter settings for everything under Assets/UI/Art
///    (Sprite 2D/UI, Tight mesh, no mipmaps, ASTC 6x6 Android /
///    DXT5 Standalone, 9-slice borders for buttons/panels).
/// 2. Creates TMP font assets from the staged TTFs (Orbitron, Rajdhani
///    x3, Vazirmatn) and sets Vazirmatn as the fallback on each, so
///    Persian labels render via the fallback list.
/// 3. Writes a GUID report (sprites + font assets + font material
///    fileIDs) to ~/workspace/model-pipeline/ui-setup-report.txt for
///    the prefab YAML edit step.
/// ValidateUI() loads both prefabs + both scenes and fails loudly if
/// any prefab YAML is broken (run after the YAML edits).
/// </summary>
public static class UIIntegrationSetup
{
    const string ArtRoot = "Assets/UI/Art";
    const string ReportPath = "/home/hatch/workspace/model-pipeline/ui-setup-report.txt";

    public static void SetupUIArt()
    {
        AssetDatabase.Refresh();
        var report = new List<string>();

        // ---- 1. Texture import settings ----
        string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot });
        int texCount = 0;
        foreach (string g in texGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            // 9-slice borders for buttons/panels (Kenney art is drawn
            // for 9-slicing); bars are _l/_m/_r pieces, icons/photos none.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            string file = Path.GetFileName(path);
            if (file.StartsWith("button-")) settings.spriteBorder = new Vector4(16, 16, 16, 16);
            else if (file.StartsWith("panel-")) settings.spriteBorder = new Vector4(12, 12, 12, 12);
            else settings.spriteBorder = Vector4.zero;
            importer.SetTextureSettings(settings);
            // Android: ASTC 6x6 (size budget); Standalone: DXT5.
            var android = new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = 2048,
                format = TextureImporterFormat.ASTC_6x6, compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(android);
            var standalone = new TextureImporterPlatformSettings
            {
                name = "Standalone", overridden = true, maxTextureSize = 2048,
                format = TextureImporterFormat.DXT5, compressionQuality = 100
            };
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
            report.Add("SPRITE\t" + path + "\t" + g);
            texCount++;
        }
        Debug.Log("[UIIntegrationSetup] Textures configured: " + texCount);

        // ---- 2. TMP font assets ----
        // Character ranges to populate into each atlas. Latin fonts get
        // ASCII + Latin-1 + general punctuation; Vazirmatn also gets the
        // full Arabic blocks INCLUDING the presentation forms (FB50-FDFF,
        // FE70-FEFF) that PersianTextShaper outputs, plus ZWNJ/ZWJ.
        uint[][] latinRanges = new uint[][]
        {
            new uint[] { 0x0020, 0x007E }, new uint[] { 0x00A0, 0x00FF },
            new uint[] { 0x2010, 0x205F }, new uint[] { 0x20AC, 0x20AC },
            new uint[] { 0x2212, 0x2212 },
        };
        uint[][] arabicRanges = new uint[][]
        {
            new uint[] { 0x0020, 0x007E }, new uint[] { 0x00A0, 0x00FF },
            new uint[] { 0x0600, 0x06FF }, new uint[] { 0x0750, 0x077F },
            new uint[] { 0xFB50, 0xFDFF }, new uint[] { 0xFE70, 0xFEFF },
            new uint[] { 0x200C, 0x200D }, new uint[] { 0x2010, 0x205F },
            new uint[] { 0x20AC, 0x20AC }, new uint[] { 0x2212, 0x2212 },
        };
        var fonts = new (string ttf, string asset, int size, int atlas, bool multi, uint[][] ranges)[]
        {
            ("Assets/UI/Art/Fonts/Orbitron-Variable.ttf", "Assets/UI/Art/Fonts/Orbitron-SDF.asset", 90, 2048, true, latinRanges),
            ("Assets/UI/Art/Fonts/Rajdhani-Bold.ttf", "Assets/UI/Art/Fonts/Rajdhani-Bold-SDF.asset", 60, 2048, true, latinRanges),
            ("Assets/UI/Art/Fonts/Rajdhani-SemiBold.ttf", "Assets/UI/Art/Fonts/Rajdhani-SemiBold-SDF.asset", 60, 2048, true, latinRanges),
            ("Assets/UI/Art/Fonts/Rajdhani-Regular.ttf", "Assets/UI/Art/Fonts/Rajdhani-Regular-SDF.asset", 60, 2048, true, latinRanges),
            ("Assets/UI/Art/Fonts/Vazirmatn-Variable.ttf", "Assets/UI/Art/Fonts/Vazirmatn-SDF.asset", 48, 2048, true, arabicRanges),
        };
        TMP_FontAsset vazirmatn = null;
        var created = new Dictionary<string, TMP_FontAsset>();
        foreach (var f in fonts)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(f.ttf);
            if (font == null) { Debug.LogError("[UIIntegrationSetup] Font not found: " + f.ttf); continue; }
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(f.asset) != null)
                AssetDatabase.DeleteAsset(f.asset);
            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(
                font, f.size, 5, GlyphRenderMode.SDFAA, f.atlas, f.atlas, AtlasPopulationMode.Dynamic, f.multi);
            if (fa == null) { Debug.LogError("[UIIntegrationSetup] CreateFontAsset failed: " + f.ttf); continue; }
            // Pre-warm the atlas with the characters the UI needs. (Dynamic
            // assets also populate on demand at runtime, so text still
            // renders even if a character was missed here — Static assets
            // would render blank instead, which is why Dynamic is used.)
            var sb = new System.Text.StringBuilder();
            foreach (var r in f.ranges)
                for (uint c = r[0]; c <= r[1]; c++) sb.Append((char)c);
            bool added = fa.TryAddCharacters(sb.ToString(), out string missingChars);
            Debug.Log("[UIIntegrationSetup] " + f.asset + " TryAddCharacters ok=" + added +
                " missing=" + (missingChars == null ? 0 : missingChars.Length) +
                " glyphs=" + fa.glyphTable.Count + " chars=" + fa.characterTable.Count);
            AssetDatabase.CreateAsset(fa, f.asset);
            foreach (Texture2D tex in fa.atlasTextures)
                if (tex != null) AssetDatabase.AddObjectToAsset(tex, f.asset);
            if (fa.material != null) AssetDatabase.AddObjectToAsset(fa.material, f.asset);
            AssetDatabase.SaveAssets();
            created[f.asset] = fa;
            if (f.asset.EndsWith("Vazirmatn-SDF.asset")) vazirmatn = fa;
            Debug.Log("[UIIntegrationSetup] Font asset created: " + f.asset +
                " glyphs=" + fa.characterTable.Count);
        }
        // Vazirmatn as fallback on every other font asset, so FA labels
        // render even when the primary font has no Arabic glyphs.
        Debug.Log("[UIIntegrationSetup] Setting Vazirmatn fallback...");
        if (vazirmatn != null)
        {
            foreach (var kv in created)
            {
                if (kv.Value == vazirmatn) continue;
                // Freshly created font assets have a null fallback list
                // (the NRE that killed the first run) — initialise it.
                var table = kv.Value.fallbackFontAssetTable;
                if (table == null)
                {
                    table = new List<TMP_FontAsset>();
                    kv.Value.fallbackFontAssetTable = table;
                }
                if (!table.Contains(vazirmatn))
                    table.Add(vazirmatn);
                EditorUtility.SetDirty(kv.Value);
            }
        }
        Debug.Log("[UIIntegrationSetup] Saving assets...");
        AssetDatabase.SaveAssets();
        Debug.Log("[UIIntegrationSetup] Writing report...");
        foreach (var kv in created)
        {
            string guid = AssetDatabase.AssetPathToGUID(kv.Key);
            long matFileId = ReadMaterialFileId(kv.Key);
            report.Add("FONT\t" + kv.Key + "\t" + guid + "\t" + matFileId);
            Debug.Log("[UIIntegrationSetup] Report line: " + kv.Key + " guid=" + guid + " matFileId=" + matFileId);
        }

        File.WriteAllLines(ReportPath, report.ToArray());
        Debug.Log("[UIIntegrationSetup] DONE. Report: " + ReportPath);
    }

    static long ReadMaterialFileId(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!File.Exists(full)) return 0;
        string text = File.ReadAllText(full);
        // Unity 6 fileIDs are 64-bit and may be negative (e.g. 8508357141320995048
        // or -6236365517726703309) — int.Parse overflows on them (run 4 bug).
        var m = Regex.Match(text, @"--- !u!21 &(-?\d+)");
        return m.Success ? long.Parse(m.Groups[1].Value) : 0;
    }

    public static void ValidateUI()
    {
        var errors = new List<string>();
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/MainMenuUI.prefab");
        GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/HUD.prefab");
        if (menu == null) errors.Add("MainMenuUI.prefab failed to load (broken YAML?)");
        if (hud == null) errors.Add("HUD.prefab failed to load (broken YAML?)");
        if (menu != null)
            Debug.Log("[UIIntegrationSetup] MainMenuUI objects: " +
                menu.GetComponentsInChildren<Transform>(true).Length);
        if (hud != null)
            Debug.Log("[UIIntegrationSetup] HUD objects: " +
                hud.GetComponentsInChildren<Transform>(true).Length);
        SceneAsset menuScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        SceneAsset mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Main.unity");
        if (menuScene == null) errors.Add("MainMenu.unity failed to load");
        if (mainScene == null) errors.Add("Main.unity failed to load");
        foreach (string name in new[] { "Orbitron-SDF", "Rajdhani-Bold-SDF", "Rajdhani-SemiBold-SDF", "Rajdhani-Regular-SDF", "Vazirmatn-SDF" })
        {
            TMP_FontAsset fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Art/Fonts/" + name + ".asset");
            if (fa == null) errors.Add("Font asset missing: " + name);
            else Debug.Log("[UIIntegrationSetup] Font OK: " + name + " glyphs=" + fa.characterTable.Count +
                " fallbacks=" + fa.fallbackFontAssetTable.Count);
        }
        if (errors.Count > 0)
            throw new System.Exception("[UIIntegrationSetup] VALIDATION FAILED: " + string.Join(" | ", errors.ToArray()));
        Debug.Log("[UIIntegrationSetup] VALIDATION OK");
    }
}
#endif

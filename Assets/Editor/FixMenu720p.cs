#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Phase 2+3 scene fixes (run once in batch mode, then the scene
/// files carry the fixes):
///  1. Menu overflow at 720p (round-3 bug #5): the MainMenu layout was
///     designed at 1920x1080 with no CanvasScaler, so the bottom buttons
///     (LANGUAGE) were cut off at 1280x720. Adds a CanvasScaler
///     (Scale With Screen Size, 1920x1080 ref) to MenuCanvas, so the
///     whole menu scales down and fits 720p. At 1080p the scale is 1.
///  2. Black sky (round-3 bug #6): creates the real Unlit material asset
///     Assets/Resources/Materials/SkyPanorama.mat (panorama texture,
///     double-sided) that CityVisuals.ApplySky loads. A runtime-built
///     Lit+emission material lost its _EMISSION variant in the player
///     build and rendered black, hiding the skybox.
///  3. Clock/sky mismatch (round-3 bug #6): the Main scene serialized
///     DayNightCycle.startHour = 6 (sun on the horizon, black sky, clock
///     "06:00 Day") while the code default is 10. Sets it to 10.
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod FixMenu720p.Fix -quit -logFile /tmp/unity-fix-menu720p.log
/// Does NOT touch the showcase scene.</summary>
public static class FixMenu720p
{
    public static void Fix()
    {
        CreateSkyMaterial();
        FixMenuCanvas();
        FixDayStartHour();
        AssetDatabase.SaveAssets();
        Debug.Log("[FixMenu720p] done");
    }

    static void CreateSkyMaterial()
    {
        const string dir = "Assets/Resources/Materials";
        const string path = dir + "/SkyPanorama.mat";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("[FixMenu720p] URP Unlit shader not found, sky material NOT created");
                return;
            }
            mat = new Material(unlit) { name = "SkyPanorama" };
            AssetDatabase.CreateAsset(mat, path);
            Debug.Log("[FixMenu720p] created " + path);
        }
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Textures/skyPanorama.png");
        if (tex != null) mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        // The camera sits INSIDE the dome sphere: render both faces.
        mat.SetInt("_Cull", 0);
        mat.renderQueue = 1000;
        EditorUtility.SetDirty(mat);
        Debug.Log("[FixMenu720p] sky material ready, texture=" + (tex != null));
    }

    static void FixMenuCanvas()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        GameObject canvasGo = GameObject.Find("MenuCanvas");
        if (canvasGo == null)
        {
            Debug.LogError("[FixMenu720p] MenuCanvas not found in MainMenu.unity");
            return;
        }
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixMenu720p] MainMenu.unity: CanvasScaler added (1920x1080 ref)");
    }

    static void FixDayStartHour()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        DayNightCycle cycle = Object.FindFirstObjectByType<DayNightCycle>();
        if (cycle == null)
        {
            Debug.LogError("[FixMenu720p] DayNightCycle not found in Main.unity");
            return;
        }
        cycle.startHour = 10f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixMenu720p] Main.unity: DayNightCycle.startHour = 10");
    }
}
#endif

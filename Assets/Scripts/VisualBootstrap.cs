using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Visual bootstrap: sky, fog, post-processing volume (bloom,
/// vignette, ACES tonemapping, colour grading) and the city rebuild are
/// applied whenever a scene loads, from GameManager.Awake. The volume is
/// built in code so no scene file has to be hand-edited. Target look:
/// Forza Horizon 1 (2012) at modern resolution — warm sun, soft bloom,
/// filmic grading, sharp 720p+ image.</summary>
public static class VisualBootstrap
{
    static bool subscribed;

    public static void Init()
    {
        if (subscribed) return;
        subscribed = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyForScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyForScene(scene);
    }

    static void ApplyForScene(Scene scene)
    {
        try { CreatePostVolume(); } catch (System.Exception e) { Debug.LogWarning("[VisualBootstrap] post volume failed: " + e.Message); }
        try { CityVisuals.ApplySky(); } catch (System.Exception e) { Debug.LogWarning("[VisualBootstrap] sky failed: " + e.Message); }
        EnablePostOnCameras();
        if (scene.name == "Main" && GameObject.Find("CityVisualsRoot") == null)
        {
            // PLATEAU first: if the converted real-Tokyo Shibuya chunk
            // loads, it REPLACES the procedural 1x1 km city (the
            // procedural code stays as the fallback — see below).
            bool plateauShibuya = false;
            try { plateauShibuya = PlateauMapLoader.Ensure(); }
            catch (System.Exception e) { Debug.LogWarning("[VisualBootstrap] PLATEAU load failed, using procedural map: " + e.Message); }
            if (!plateauShibuya)
            {
                try { CityVisuals.Apply(); } catch (System.Exception e) { Debug.LogWarning("[VisualBootstrap] city failed: " + e.Message); }
            }
            else Debug.Log("[VisualBootstrap] PLATEAU Shibuya active (" + PlateauMap.Attribution + ") — procedural city skipped");
        }
    }

    static void EnablePostOnCameras()
    {
        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }
    }

    static void CreatePostVolume()
    {
        if (GameObject.Find("VisualPostVolume") != null) return;
        GameObject go = new GameObject("VisualPostVolume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Bloom bloom = profile.Add<Bloom>();
        bloom.active = true;
        bloom.threshold.value = 0.85f; bloom.threshold.overrideState = true;
        bloom.intensity.value = 0.7f; bloom.intensity.overrideState = true;
        bloom.scatter.value = 0.65f; bloom.scatter.overrideState = true;

        Vignette vignette = profile.Add<Vignette>();
        vignette.active = true;
        vignette.color.value = Color.black; vignette.color.overrideState = true;
        vignette.intensity.value = 0.2f; vignette.intensity.overrideState = true;
        vignette.smoothness.value = 0.45f; vignette.smoothness.overrideState = true;

        ColorAdjustments grade = profile.Add<ColorAdjustments>();
        grade.active = true;
        // Brighter than the old 0.08 exposure: the game read dark in
        // software rendering, so lift the whole image and add punch.
        grade.postExposure.value = 0.32f; grade.postExposure.overrideState = true;
        grade.contrast.value = 8f; grade.contrast.overrideState = true;
        grade.saturation.value = 10f; grade.saturation.overrideState = true;

        WhiteBalance wb = profile.Add<WhiteBalance>();
        wb.active = true;
        wb.temperature.value = 4f; wb.temperature.overrideState = true;

        Tonemapping tm = profile.Add<Tonemapping>();
        tm.active = true;
        tm.mode.value = TonemappingMode.ACES; tm.mode.overrideState = true;

        volume.profile = profile;
        Debug.Log("[VisualBootstrap] post volume created (bloom 0.55, ACES, vignette 0.27)");
    }
}

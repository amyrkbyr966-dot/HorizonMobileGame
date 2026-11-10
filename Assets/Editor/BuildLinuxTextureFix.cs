#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Linux x86_64 build for the PLATEAU texture-fix review
/// (2026-11-09 map phase). Same settings as BuildLinuxReview.Build
/// (MainMenu first, so the menu -&gt; FREE ROAM -&gt; Shibuya flow can be
/// driven and verified), but outputs to a separate folder so it does
/// not clobber the known-good MainFirst build.
/// Output: ~/workspace/HorizonMobileGame-Linux-TextureFix/
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildLinuxTextureFix.Build -quit -logFile /home/hatch/workspace/unity-build-linux-texturefix.log
/// </summary>
public static class BuildLinuxTextureFix
{
    public static void Build()
    {
        Debug.Log("[BuildLinuxTextureFix] Switching active build target to StandaloneLinux64...");
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        // PlayerPrefs path on Linux (~/.config/unity3d/<company>/<product>)
        // comes from these names: a build made while they were empty wrote
        // prefs under unknown/unknown and stranded the player's save there
        // (2026-11-09). Pin them so no build can regress.
        PlayerSettings.companyName = "HorizonMobile";
        PlayerSettings.productName = "Horizon Mobile";
        PlayerSettings.SetScriptingBackend(
            UnityEditor.Build.NamedBuildTarget.Standalone,
            ScriptingImplementation.Mono2x);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[]
        {
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore
        });
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultIsFullScreen = false;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;

        string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Main.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Linux-TextureFix/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[BuildLinuxTextureFix] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxTextureFix] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxTextureFix] Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildLinuxTextureFix] Linux build SUCCEEDED");
        }
    }
}
#endif

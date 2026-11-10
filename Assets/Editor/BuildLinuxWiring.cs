#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Linux x86_64 build for the district-WIRING verification
/// (2026-11-09 map phase, wiring worker). Same settings as
/// BuildLinuxTextureFix.Build (MainMenu first, menu -&gt; FREE ROAM flow),
/// outputs to a separate folder so it does not clobber the TextureFix build.
/// Output: ~/workspace/HorizonMobileGame-Linux-Wiring/
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildLinuxWiring.Build -quit -logFile /home/hatch/workspace/unity-build-linux-wiring.log
/// </summary>
public static class BuildLinuxWiring
{
    public static void Build()
    {
        Debug.Log("[BuildLinuxWiring] Switching active build target to StandaloneLinux64...");
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
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;

        string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Main.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Linux-Wiring/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[BuildLinuxWiring] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxWiring] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxWiring] Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildLinuxWiring] Linux build SUCCEEDED");
        }
    }
}
#endif

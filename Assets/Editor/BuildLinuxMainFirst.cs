#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Diagnostic/media build: same as BuildLinux but starts directly in
/// the Main scene (open world), bypassing the main menu. Used to capture
/// gameplay photos/video while the menu's pointer input is being diagnosed.
/// Outputs to ~/workspace/LinuxBuildMainFirst (does not clobber LinuxBuild).
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildLinuxMainFirst.Build -quit -logFile /tmp/unity-build-linux-mainfirst.log
/// </summary>
public static class BuildLinuxMainFirst
{
    public static void Build()
    {
        Debug.Log("[BuildLinuxMainFirst] Switching active build target to StandaloneLinux64...");
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

        string[] scenes = { "Assets/Scenes/Main.unity", "Assets/Scenes/MainMenu.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/LinuxBuildMainFirst/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[BuildLinuxMainFirst] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxMainFirst] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxMainFirst] Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildLinuxMainFirst] Linux build SUCCEEDED");
        }
    }
}
#endif

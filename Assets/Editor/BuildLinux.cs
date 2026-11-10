#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Editor-only build entry point for the Linux x86_64 player.
/// Run from the command line:
///   Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///     -executeMethod BuildLinux.Build -quit -logFile /tmp/unity-build-linux.log
/// Note: ApplyMobileSettings switches the active target to Android on load,
/// so this method switches it back to StandaloneLinux64 first.
/// Scripting backend is Mono, not IL2CPP: the Linux IL2CPP build failed
/// 2026-11-07 (39s, 103 errors) with "Unable to find an Linux Sysroot" /
/// "No Linux sysroot found for x64" because com.unity.toolchain.linux-x86_64
/// is not installed/resolved (Unity removed it from Packages/manifest.json
/// during that run). The Mono Linux player is built into this editor install,
/// so Mono builds without that toolchain. Android/APK can still use IL2CPP later.
/// </summary>
public static class BuildLinux
{
    public static void Build()
    {
        Debug.Log("[BuildLinux] Switching active build target to StandaloneLinux64...");
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
            locationPathName = "/home/hatch/workspace/LinuxBuild/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[BuildLinux] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinux] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinux] Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildLinux] Linux build SUCCEEDED");
        }
    }
}
#endif

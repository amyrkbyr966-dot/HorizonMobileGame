#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Linux x86_64 build for review media. Same settings as
/// BuildLinux, but outputs to a separate folder so it does not clobber
/// the running player in ~/workspace/LinuxBuild.
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildLinuxReview.Build -quit -logFile /home/hatch/workspace/unity-build-linux-review.log
/// </summary>
public static class BuildLinuxReview
{
    public static void Build()
    {
        Debug.Log("[BuildLinuxReview] Switching active build target to StandaloneLinux64...");
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        // PlayerPrefs path on Linux (~/.config/unity3d/<company>/<product>)
        // comes from these names: a build made while they were empty wrote
        // prefs under unknown/unknown and stranded the player's save there
        // (2026-11-09). Pin them so no build can regress.
        PlayerSettings.companyName = "HorizonMobile";
        PlayerSettings.productName = "Horizon Mobile";
        // Mono, not IL2CPP: the Linux IL2CPP toolchain/sysroot is not
        // usable on this VM ("No Toolchain found for host platform" /
        // "Unable to find an Linux Sysroot"), while a Mono Linux player
        // builds and runs fine here. Mono is also much faster to build.
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
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Linux/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[BuildLinuxReview] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxReview] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxReview] Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildLinuxReview] Linux build SUCCEEDED");
        }
    }

    /// <summary>Release Main-first build for driving capture: the menu's
    /// pointer input does not respond to XTest clicks under Xvfb, so this
    /// starts directly in the Main scene. Output:
    /// ~/workspace/HorizonMobileGame-Linux-MainFirst/</summary>
    public static void BuildMainFirst()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        // PlayerPrefs path on Linux: pin company/product (see Build()).
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
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Linux-MainFirst/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxReview] MAINFIRST RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxReview] MAINFIRST Linux build FAILED");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Showcase build: only the ShowcaseTrio scene (three hero
    /// cars on a Tokyo street at sunset), for the trio screenshot.
    /// Output: ~/workspace/HorizonMobileGame-Showcase/</summary>
    public static void BuildShowcase()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        // PlayerPrefs path on Linux: pin company/product (see Build()).
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

        string[] scenes = { "Assets/Scenes/ShowcaseTrio.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Showcase/HorizonShowcase.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxReview] SHOWCASE RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxReview] SHOWCASE Linux build FAILED");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Development build (full stack traces in the player log)
    /// into a separate folder, for diagnosing runtime exceptions.</summary>
    public static void BuildDev()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
        // PlayerPrefs path on Linux: pin company/product (see Build()).
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

        // Main scene first: skips the menu (whose buttons do not respond
        // to XTest clicks under Xvfb) so the game scene can be diagnosed
        // directly.
        string[] scenes = { "Assets/Scenes/Main.unity", "Assets/Scenes/MainMenu.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Linux-Dev/HorizonMobile.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.Development
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildLinuxReview] DEV RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildLinuxReview] DEV Linux build FAILED");
            EditorApplication.Exit(1);
        }
    }
}
#endif

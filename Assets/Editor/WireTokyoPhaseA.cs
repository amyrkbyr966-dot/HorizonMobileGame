#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Tokyo Phase A editor pass: wire the new open-world managers
/// into the Main scene and build Linux players for the free-roam
/// playtest. Run in batch mode, e.g.:
///   Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///     -executeMethod WireTokyoPhaseA.WireAndBuildMainFirst -quit
///     -logFile /home/hatch/workspace/unity-build-phaseA.log
///
/// Output folders are PhaseA-specific so this never clobbers the
/// race-fix / sound agents' running players (MainFirst / LinuxBuild).
/// Platform stays Linux (StandaloneLinux64) — never runs
/// ApplyMobileSettings (that switches to Android).</summary>
public static class WireTokyoPhaseA
{
    const string MainScenePath = "Assets/Scenes/Main.unity";

    static GameObject EnsureManager<T>(string goName) where T : Component
    {
        var go = GameObject.Find(goName);
        if (go == null)
        {
            go = new GameObject(goName);
            Debug.Log("[WireTokyoPhaseA] created GameObject " + goName);
        }
        if (go.GetComponent<T>() == null)
        {
            go.AddComponent<T>();
            Debug.Log("[WireTokyoPhaseA] added " + typeof(T).Name + " to " + goName);
        }
        else Debug.Log("[WireTokyoPhaseA] " + goName + " already has " + typeof(T).Name);
        return go;
    }

    /// <summary>Open Main, add DistrictStreaming / SeasonManager /
    /// FreeRoamManager (DistrictData is static data — no GameObject),
    /// save the scene. Idempotent.</summary>
    public static void WireOnly()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Debug.Log("[WireTokyoPhaseA] opened scene " + scene.name + " (" + scene.path + ")");
        EnsureManager<DistrictStreaming>("DistrictStreaming");
        EnsureManager<SeasonManager>("SeasonManager");
        EnsureManager<FreeRoamManager>("FreeRoamManager");
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        Debug.Log("[WireTokyoPhaseA] scene saved=" + saved + " path=" + scene.path);
        // Verify the managers resolve (catches a missing script compile).
        Debug.Log("[WireTokyoPhaseA] verify: DistrictStreaming="
            + (Object.FindFirstObjectByType<DistrictStreaming>() != null)
            + " SeasonManager=" + (Object.FindFirstObjectByType<SeasonManager>() != null)
            + " FreeRoamManager=" + (Object.FindFirstObjectByType<FreeRoamManager>() != null));
    }

    static void BuildLinux(string[] scenes, string outPath, string tag)
    {
        Debug.Log("[WireTokyoPhaseA] " + tag + ": switching target to StandaloneLinux64...");
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
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

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outPath,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None
        };
        Debug.Log("[WireTokyoPhaseA] " + tag + ": starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[WireTokyoPhaseA] {tag} RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[WireTokyoPhaseA] " + tag + " Linux build FAILED");
            EditorApplication.Exit(1);
        }
        else Debug.Log("[WireTokyoPhaseA] " + tag + " Linux build SUCCEEDED");
    }

    /// <summary>Wire the managers into Main, save, then build the
    /// Main-first Linux player (starts directly in Main) to the
    /// PhaseA folder. This is the prescribed BuildMainFirst flow,
    /// redirected to a PhaseA-only output folder.</summary>
    public static void WireAndBuildMainFirst()
    {
        WireOnly();
        BuildLinux(
            new[] { "Assets/Scenes/Main.unity", "Assets/Scenes/MainMenu.unity" },
            "/home/hatch/workspace/HorizonMobileGame-Linux-PhaseA/HorizonMobile.x86_64",
            "MAINFIRST-PHASEA");
    }

    /// <summary>Menu-first Linux player (MainMenu first, then Main) to
    /// the PhaseA-Menu folder — the real user path into free-roam
    /// (MainMenu -> Free Roam button -> Main). Scene must already be
    /// wired (WireAndBuildMainFirst saves it first).</summary>
    public static void BuildMenuFirst()
    {
        BuildLinux(
            new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Main.unity" },
            "/home/hatch/workspace/HorizonMobileGame-Linux-PhaseA-Menu/HorizonMobile.x86_64",
            "MENUFIRST-PHASEA");
    }

}
#endif

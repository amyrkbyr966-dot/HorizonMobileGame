#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Early-access Android APK build (2026-11-08): one car only
/// (Turbo Falcon #5 — see CarSelection.EarlyAccessSingleCar), full
/// current map + real Shibuya PLATEAU tile (loaded at runtime from
/// Resources by PlateauMapLoader), IL2CPP ARM64, target &lt;400 MB.
/// Outputs to ~/workspace/HorizonMobileGame-Android-EarlyAccess/HorizonMobile.apk.
/// Run: Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod BuildAndroid.Build -quit -logFile ~/workspace/unity-build-android-earlyaccess.log
/// </summary>
public static class BuildAndroid
{
    /// <summary>Point Unity at the shadow Android SDK via reflection:
    /// AndroidExternalToolsSettings lives in an assembly the default
    /// editor assembly does not reference, but the type is loaded at
    /// runtime (Burst uses its ndkRootPath). Logs the available
    /// properties so a missing/wrong property name is visible.</summary>
    static void SetShadowSdkRoot()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
            if (type == null) continue;
            var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var names = new System.Collections.Generic.List<string>();
            foreach (var p in props) names.Add(p.Name);
            Debug.Log("[BuildAndroid] AndroidExternalToolsSettings props: " + string.Join(", ", names));
            var prop = type.GetProperty("sdkRootPath");
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(null, "/home/hatch/workspace/android-sdk-shadow");
                Debug.Log("[BuildAndroid] Shadow SDK root set: " + prop.GetValue(null));
            }
            else
            {
                Debug.LogError("[BuildAndroid] sdkRootPath property not found or not writable");
            }
            // Gradle heap 2048 MB (attempt 4, 2026-11-08): attempt 3's
            // Gradle ran with -Xmx4096m (Unity's default, passed on the
            // java command line, overriding gradleTemplate.properties)
            // and the daemon died instantly with "Could not dispatch a
            // message to the daemon" — a 4 GB heap cannot be reserved
            // on this 7.7 GB VM next to Unity + the coordinator's
            // Blender jobs. 2048 MB matches gradleTemplate.properties.
            var heapProp = type.GetProperty("maxJvmHeapSize");
            if (heapProp != null && heapProp.CanWrite)
            {
                heapProp.SetValue(null, 2048);
                Debug.Log("[BuildAndroid] Gradle maxJvmHeapSize set: " + heapProp.GetValue(null));
            }
            else
            {
                Debug.Log("[BuildAndroid] maxJvmHeapSize property not found or not writable");
            }
            return;
        }
        Debug.LogError("[BuildAndroid] AndroidExternalToolsSettings type not found in loaded assemblies");
    }

    public static void Build()
    {
        // Shadow Android SDK (2026-11-08): the stock sdkmanager --list
        // crashes in this VM's egress proxy (Java HttpURLConnection
        // CONNECT-tunneling bug, NoSuchElementException), which fails
        // the build at "Prepare For Build". The shadow SDK is the stock
        // SDK with a wrapper sdkmanager that answers --list from the
        // local installed list (verified working) plus pre-accepted
        // SDK licenses. See ~/workspace/android-sdk-shadow/.
        // Use the shadow Android SDK (2026-11-08): the stock sdkmanager
        // --list crashes in this VM's egress proxy (Java HttpURLConnection
        // CONNECT-tunneling bug, NoSuchElementException), which fails the
        // build at "Prepare For Build". The shadow SDK is the stock SDK
        // with a wrapper sdkmanager that answers --list from the local
        // installed list (verified working) plus pre-accepted SDK
        // licenses. See ~/workspace/android-sdk-shadow/.
        // NOTE: Unity 6000 reads the SDK root from
        // AndroidExternalToolsSettings.sdkRootPath, which lives in an
        // assembly the default editor assembly cannot reference at
        // compile time — so it is set via reflection in SetShadowSdkRoot()
        // below. (ANDROID_SDK_ROOT / ANDROID_HOME env vars and the
        // EditorPrefs "AndroidSdkRoot" key were both ignored by Unity;
        // the build kept using the stock SDK.)
        SetShadowSdkRoot();
        Debug.Log("[BuildAndroid] Switching active build target to Android...");
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Android, BuildTarget.Android);
        // PlayerPrefs path comes from these names: a build made while
        // they were empty wrote prefs under unknown/unknown on Linux
        // and stranded the player's save there (2026-11-09). Pin them
        // so no build can regress (same values as ProjectSettings).
        PlayerSettings.companyName = "HorizonMobile";
        PlayerSettings.productName = "Horizon Mobile";
        PlayerSettings.SetScriptingBackend(
            BuildTargetGroup.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,
            "com.horizonmobile.game");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
        {
            UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
        });

        string[] scenes = { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Main.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "/home/hatch/workspace/HorizonMobileGame-Android-EarlyAccess/HorizonMobile.apk",
            target = BuildTarget.Android,
            options = BuildOptions.None
        };
        Debug.Log("[BuildAndroid] Starting BuildPipeline.BuildPlayer...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[BuildAndroid] RESULT={s.result} time={s.totalTime} errors={s.totalErrors} warnings={s.totalWarnings} size={s.totalSize} bytes output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            Debug.LogError("[BuildAndroid] Android build FAILED");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log("[BuildAndroid] Android build SUCCEEDED");
        }
    }
}
#endif

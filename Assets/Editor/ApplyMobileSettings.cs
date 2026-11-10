#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Editor-only: runs automatically when the project is opened and
/// applies the mobile / Android settings, so the project builds for Android
/// (IL2CPP, ARM64, landscape) even if a ProjectSettings field was lost
/// while the project was authored by hand. Safe to delete afterwards.</summary>
[InitializeOnLoad]
public static class ApplyMobileSettings
{
    static ApplyMobileSettings()
    {
        PlayerSettings.companyName = "HorizonMobile";
        PlayerSettings.productName = "Horizon Mobile";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,
            "com.horizonmobile.game");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,
            ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
        {
            UnityEngine.Rendering.GraphicsDeviceType.Vulkan,
            UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3
        });
        QualitySettings.SetQualityLevel(2, true);
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
    }
}
#endif

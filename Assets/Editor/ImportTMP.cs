using UnityEditor;
using UnityEngine;

/// <summary>One-off import of TextMeshPro Essential Resources (the default
/// LiberationSans SDF font asset). The menu/HUD scenes reference the font
/// by guid, but Assets/TextMeshPro was never imported, so every TMP_Text
/// throws a NullReferenceException at startup and renders no text.
/// Run: Unity -batchmode -nographics -projectPath ... -executeMethod ImportTMP.Import -quit
/// </summary>
public static class ImportTMP
{
    public static void Import()
    {
        string pkg = "/home/hatch/workspace/HorizonMobileGame/Library/PackageCache/com.unity.ugui@9175b88b288c/Package Resources/TMP Essential Resources.unitypackage";
        Debug.Log("[ImportTMP] Importing " + pkg);
        AssetDatabase.ImportPackage(pkg, false);
        AssetDatabase.Refresh();
        Debug.Log("[ImportTMP] Import done");
    }
}

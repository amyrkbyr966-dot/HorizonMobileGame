using UnityEditor;
using UnityEngine;

/// <summary>Imports the TMP Essential Resources package (LiberationSans SDF font
/// and default TMP materials) from the com.unity.ugui package cache, so that
/// TMP_Text components referencing font GUID 8f586378... actually render.
/// Run once in batch mode: -executeMethod ImportTMPResources.Import</summary>
public static class ImportTMPResources
{
    public static void Import()
    {
        string[] found = System.IO.Directory.GetFiles(
            "Library/PackageCache", "TMP Essential Resources.unitypackage",
            System.IO.SearchOption.AllDirectories);
        if (found.Length == 0)
        {
            Debug.LogError("[ImportTMP] TMP Essential Resources.unitypackage not found in Library/PackageCache");
            return;
        }
        string pkg = found[0];
        Debug.Log("[ImportTMP] importing " + pkg);
        AssetDatabase.ImportPackage(pkg, false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ImportTMP] done");
    }
}

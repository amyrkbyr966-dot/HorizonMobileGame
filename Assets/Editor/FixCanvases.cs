using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Adds a GraphicRaycaster to every Canvas that lacks one, in both
/// scenes, and saves them. Without a GraphicRaycaster the EventSystem can
/// never hit UI buttons, so no menu/HUD button can be clicked or tapped.
/// Run in batch mode: -executeMethod FixCanvases.Fix</summary>
public static class FixCanvases
{
    public static void Fix()
    {
        foreach (string path in new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Main.unity" })
        {
            Scene scene = EditorSceneManager.OpenScene(path);
            int added = 0;
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go.GetComponent<Canvas>() != null && go.GetComponent<GraphicRaycaster>() == null)
                {
                    go.AddComponent<GraphicRaycaster>();
                    added++;
                    Debug.Log("[FixCanvases] added GraphicRaycaster to '" + go.name + "' in " + path);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[FixCanvases] " + path + ": added " + added + " raycaster(s)");
        }
        Debug.Log("[FixCanvases] done");
    }
}

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Pause/back-button wiring (2026-11-08 menu fix, part 2).
/// ResumeButton / QuitButton (inside the inactive PausePanel) and
/// ResultsContinueButton (inside the inactive ResultsPanel) in Main.unity
/// had NO input handler at all — only an Image and a Slider, no onClick,
/// no TouchButton — so even once the pause panel shows, its buttons are
/// dead. Attach TouchButton with the new Resume / QuitToMenu actions
/// (TouchButton.ButtonAction, appended after Nitro so serialized values
/// of the old actions never shift). Run:
/// Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod FixPauseButtons.Fix -quit -logFile /tmp/fix-pause-buttons.log
/// </summary>
public static class FixPauseButtons
{
    static void Wire(string objectName, TouchButton.ButtonAction action)
    {
        var go = GameObject.Find(objectName);
        if (go == null)
        {
            // Inactive parents are invisible to GameObject.Find; search all.
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
                if (t.name == objectName) { go = t.gameObject; break; }
        }
        if (go == null) { Debug.LogError("[FixPauseButtons] NOT FOUND: " + objectName); return; }
        var tb = go.GetComponent<TouchButton>();
        if (tb == null)
        {
            tb = go.AddComponent<TouchButton>();
            Debug.Log("[FixPauseButtons] ADDED TouchButton to " + objectName);
        }
        tb.action = action;
        Debug.Log("[FixPauseButtons] " + objectName + " -> TouchButton action=" + action);
    }

    public static void Fix()
    {
        var s = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        // Audit: does the HUD pause button already have its handler?
        var pauseBtn = GameObject.Find("PauseButton");
        if (pauseBtn != null)
        {
            var tb = pauseBtn.GetComponent<TouchButton>();
            Debug.Log("[FixPauseButtons] PauseButton TouchButton=" + (tb != null ? tb.action.ToString() : "MISSING"));
        }
        else Debug.LogWarning("[FixPauseButtons] PauseButton not found by GameObject.Find");
        Wire("ResumeButton", TouchButton.ButtonAction.Resume);
        Wire("RestartButton", TouchButton.ButtonAction.Restart);
        Wire("QuitButton", TouchButton.ButtonAction.QuitToMenu);
        Wire("ResultsContinueButton", TouchButton.ButtonAction.QuitToMenu);
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
        Debug.Log("[FixPauseButtons] DONE saved Assets/Scenes/Main.unity");
    }
}
#endif

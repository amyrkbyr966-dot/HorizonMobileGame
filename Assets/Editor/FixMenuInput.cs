#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Menu-input fix (2026-11-08): the menu was stuck on a real phone
/// because BOTH scenes used InputSystemUIInputModule while the project runs
/// the OLD input system only (ProjectSettings activeInputHandler = 0) and no
/// script uses UnityEngine.InputSystem at all — and the module's referenced
/// .inputactions asset does not even exist in Assets. With the old handler
/// the new Input System backend is disabled, so the module never delivers
/// pointer events: MenuButton.OnPointerDown never fires, every menu button
/// is dead (mouse AND touch). There are also zero Button.onClick wirings in
/// either scene, so the EventSystem path is the ONLY input path.
/// Fix: replace InputSystemUIInputModule with StandaloneInputModule (the
/// old-system module; handles mouse + touch) on the EventSystem of every
/// scene in Build Settings. Run:
/// Unity -batchmode -nographics -projectPath ~/workspace/HorizonMobileGame
///   -executeMethod FixMenuInput.Fix -quit -logFile /tmp/fix-menu-input.log
/// </summary>
public static class FixMenuInput
{
    public static void Fix()
    {
        int fixedCount = 0;
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            var s = EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
            Debug.Log("[FixMenuInput] Scene: " + s.name);
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            Debug.Log("[FixMenuInput] EventSystems in " + s.name + ": " + eventSystems.Length);
            foreach (var es in eventSystems)
            {
                var go = es.gameObject;
                var inputModules = go.GetComponents<BaseInputModule>();
                Debug.Log("[FixMenuInput] " + go.name + " input modules: "
                    + string.Join(", ", System.Array.ConvertAll(inputModules, m => m.GetType().Name)));
                foreach (var m in inputModules)
                {
                    if (m is InputSystemUIInputModule)
                    {
                        Debug.Log("[FixMenuInput] REMOVING InputSystemUIInputModule from " + go.name);
                        Object.DestroyImmediate(m);
                    }
                }
                if (go.GetComponent<StandaloneInputModule>() == null)
                {
                    go.AddComponent<StandaloneInputModule>();
                    Debug.Log("[FixMenuInput] ADDED StandaloneInputModule to " + go.name);
                    fixedCount++;
                }
                var after = go.GetComponents<BaseInputModule>();
                Debug.Log("[FixMenuInput] After fix, " + go.name + " modules: "
                    + string.Join(", ", System.Array.ConvertAll(after, m => m.GetType().Name)));
            }
            // Audit the menu buttons: what component actually carries the click.
            var menuButtons = Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None);
            foreach (var mb in menuButtons)
            {
                var btn = mb.GetComponent<Button>();
                var slider = mb.GetComponent<Slider>();
                var img = mb.GetComponent<Image>();
                Debug.Log("[FixMenuInput] MenuButton " + mb.gameObject.name
                    + " action=" + mb.action
                    + " Button=" + (btn != null)
                    + " Slider=" + (slider != null)
                    + " Image.raycastTarget=" + (img != null ? img.raycastTarget.ToString() : "no-image")
                    + " onClickCalls=" + (btn != null ? btn.onClick.GetPersistentEventCount().ToString() : "-"));
            }
            EditorSceneManager.MarkSceneDirty(s);
            EditorSceneManager.SaveScene(s);
            Debug.Log("[FixMenuInput] Saved " + scene.path);
        }
        Debug.Log("[FixMenuInput] DONE fixedCount=" + fixedCount);
    }
}
#endif

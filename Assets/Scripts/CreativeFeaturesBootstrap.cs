using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>CREATIVE FEATURES BOOTSTRAP — wires the creative
/// features into the Main scene at runtime (same pattern as
/// VisualBootstrap: no scene file is hand-edited). On every Main
/// scene load it ensures exactly one GameObject "CreativeFeatures"
/// carrying all nine managers; on any other scene it leaves
/// everything alone. Idempotent across scene reloads (street races
/// reload Main) — existing components are reused, never duplicated.
/// Scene wiring that still needs the Phase A editor pass: nothing
/// for logic — these managers are fully functional from here. What
/// still needs the editor pass is VISUAL: importing the Kenney kits
/// into Resources/Models (TougeGenerator / CarMeetManager /
/// ShowcaseGarageManager load them by path), and the touch buttons
/// for Photo Mode / challenge accept / radio next (public APIs
/// already exist for them).</summary>
public static class CreativeFeaturesBootstrap
{
    static bool subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (subscribed) return;
        subscribed = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Main") return;
        GameObject root = GameObject.Find("CreativeFeatures");
        if (root == null)
        {
            root = new GameObject("CreativeFeatures");
            Debug.Log("[CreativeFeatures] bootstrap: attaching creative managers");
        }
        Ensure<CarMeetManager>(root);
        Ensure<TougeGenerator>(root);
        Ensure<PhotoModeController>(root);
        Ensure<FestivalManager>(root);
        Ensure<NightRaceManager>(root);
        Ensure<RadioManager>(root);
        Ensure<WanganRunManager>(root);
        Ensure<ViewpointManager>(root);
        Ensure<ShowcaseGarageManager>(root);
    }

    static T Ensure<T>(GameObject root) where T : Component
    {
        T c = root.GetComponent<T>();
        if (c == null) c = root.AddComponent<T>();
        return c;
    }
}

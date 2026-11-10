using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Fixes the player car's wheels: the wheel holders (WheelFL/FR/RL/RR)
/// were built with a CompositeCollider2D instead of a WheelCollider, so the
/// CarController's wheelFL/wheelFR/wheelRL/wheelRR fields deserialize to null
/// and FixedUpdate throws a NullReferenceException every frame (the car can
/// never move). This replaces the 2D collider with a real WheelCollider on
/// each wheel holder and wires it into the CarController, in both the
/// PlayerCar prefab and the Main scene's PlayerCar.
/// Run in batch mode: -executeMethod FixWheels.Fix</summary>
public static class FixWheels
{
    static void FixCar(GameObject carRoot, string where)
    {
        var controller = carRoot.GetComponent<CarController>();
        if (controller == null)
        {
            Debug.LogError("[FixWheels] no CarController on " + carRoot.name + " (" + where + ")");
            return;
        }
        string[] names = { "WheelFL", "WheelFR", "WheelRL", "WheelRR" };
        WheelCollider[] colliders = new WheelCollider[4];
        for (int i = 0; i < names.Length; i++)
        {
            Transform holder = carRoot.transform.Find(names[i]);
            if (holder == null)
            {
                Debug.LogError("[FixWheels] missing " + names[i] + " on " + carRoot.name + " (" + where + ")");
                continue;
            }
            // Remove ALL 2D physics (CompositeCollider2D + the Rigidbody2D
            // Unity auto-created for it) — a WheelCollider cannot coexist
            // with a Rigidbody2D on the same GameObject.
            foreach (var c2d in holder.GetComponents<Collider2D>())
                Object.DestroyImmediate(c2d);
            var rb2d = holder.GetComponent<Rigidbody2D>();
            if (rb2d != null) Object.DestroyImmediate(rb2d);
            var wc = holder.GetComponent<WheelCollider>();
            if (wc == null) wc = holder.gameObject.AddComponent<WheelCollider>();
            if (wc == null)
            {
                Debug.LogError("[FixWheels] could not add WheelCollider to " + names[i] + " (" + where + ")");
                continue;
            }
            wc.radius = 0.35f;
            wc.suspensionDistance = 0.25f;
            wc.mass = 20f;
            var spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            colliders[i] = wc;
            Debug.Log("[FixWheels] " + where + ": WheelCollider on " + names[i] + " radius=" + wc.radius);
        }
        controller.wheelFL = colliders[0];
        controller.wheelFR = colliders[1];
        controller.wheelRL = colliders[2];
        controller.wheelRR = colliders[3];
        Debug.Log("[FixWheels] " + where + ": wired CarController wheels");
    }

    public static void Fix()
    {
        // 1) Prefab (source of truth for future spawns)
        const string prefabPath = "Assets/Prefabs/PlayerCar.prefab";
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        FixCar(prefabRoot, "prefab");
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log("[FixWheels] prefab saved");

        // 2) Main scene PlayerCar (standalone object, not a prefab instance)
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[FixWheels] no GameObject tagged Player in Main.unity");
            return;
        }
        FixCar(player, "Main scene");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixWheels] Main.unity saved");
        Debug.Log("[FixWheels] done");
    }
}

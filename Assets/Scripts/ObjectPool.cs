using System.Collections.Generic;
using UnityEngine;

/// <summary>Simple GameObject pool: reuses inactive instances instead of
/// Instantiate/Destroy churn (traffic, coins, repair pickups).
/// Static, so any system can use it without wiring.</summary>
public static class ObjectPool
{
    static readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
    static Transform root;

    static Transform Root()
    {
        if (root == null)
        {
            var go = new GameObject("ObjectPoolRoot");
            Object.DontDestroyOnLoad(go);
            root = go.transform;
        }
        return root;
    }

    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            var go = Object.Instantiate(prefab);
            Release(go);
        }
    }

    public static GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;
        string key = prefab.name;
        Queue<GameObject> queue;
        if (pools.TryGetValue(key, out queue) && queue.Count > 0)
        {
            var go = queue.Dequeue();
            go.transform.position = position;
            go.transform.rotation = rotation;
            go.SetActive(true);
            return go;
        }
        var fresh = Object.Instantiate(prefab, position, rotation);
        return fresh;
    }

    public static void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.SetParent(Root(), false);
        string key = go.name.Replace("(Clone)", "").Trim();
        Queue<GameObject> queue;
        if (!pools.TryGetValue(key, out queue))
        {
            queue = new Queue<GameObject>();
            pools[key] = queue;
        }
        queue.Enqueue(go);
    }

    public static int PooledCount(string prefabName)
    {
        Queue<GameObject> queue;
        return pools.TryGetValue(prefabName, out queue) ? queue.Count : 0;
    }
}

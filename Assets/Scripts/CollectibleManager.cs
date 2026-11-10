using System.Collections.Generic;
using UnityEngine;

/// <summary>Collectible hunt: coins scattered around the world. Counts
/// collected coins, pays their value in money, and finishes the hunt
/// (reward + HUD message) when every coin is collected. If no coins are
/// placed in the scene, it spawns a ring of coins along the roads at
/// runtime so the mode always works. Active in every mode, but the
/// finish reward only applies in Collectibles mode.</summary>
public class CollectibleManager : MonoBehaviour
{
    public static CollectibleManager Instance { get; private set; }

    public int CollectedCount { get; private set; }
    public int TotalCount { get; private set; }
    public bool HuntFinished { get; private set; }

    List<Collectible> coins = new List<Collectible>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        coins.AddRange(FindObjectsByType<Collectible>(FindObjectsSortMode.None));
        if (coins.Count == 0) SpawnCoins();
        TotalCount = coins.Count;
    }

    public void Collect(Collectible coin)
    {
        if (coin == null || HuntFinished) return;
        CollectedCount++;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(coin.value);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCoin();
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Coin! " + CollectedCount + " / " + TotalCount + "  (+$" + coin.value + ")");
        if (CollectedCount >= TotalCount) FinishHunt();
    }

    void FinishHunt()
    {
        HuntFinished = true;
        if (GameManager.Instance != null && GameManager.Instance.Mode == GameManager.GameMode.Collectibles)
        {
            GameManager.Instance.AddMoney(500);
            var hud = FindFirstObjectByType<HUD>();
            if (hud != null) hud.ShowMessage("All coins collected! +$500");
        }
    }

    void SpawnCoins()
    {
        // 12 coins along the road grid, alternating NS / EW roads.
        Vector3[] spots = new Vector3[]
        {
            new Vector3(-300f, 1.2f, 0f), new Vector3(-150f, 1.2f, -150f),
            new Vector3(0f, 1.2f, -300f), new Vector3(150f, 1.2f, 150f),
            new Vector3(300f, 1.2f, 0f), new Vector3(300f, 1.2f, -300f),
            new Vector3(-300f, 1.2f, 300f), new Vector3(0f, 1.2f, 300f),
            new Vector3(150f, 1.2f, -300f), new Vector3(-150f, 1.2f, 300f),
            new Vector3(0f, 1.2f, 0f), new Vector3(-300f, 1.2f, -150f),
        };
        foreach (var pos in spots)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Coin_Runtime";
            go.tag = "Coin";
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.4f, 0.2f, 1.4f);
            go.transform.Rotate(90f, 0f, 0f);
            var col = go.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(1f, 0.8f, 0.1f);
                mat.SetFloat("_Metallic", 0.6f);
                mat.SetFloat("_Smoothness", 0.7f);
                rend.material = mat;
            }
            var coin = go.AddComponent<Collectible>();
            coins.Add(coin);
        }
    }
}

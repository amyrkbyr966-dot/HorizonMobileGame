using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>North-up overhead minimap for the 7700 x 7700 m world
/// (60 sq km layout, 2026-11-09 — was 1000 x 1000 m). Green
/// dot = player, red dots = traffic, yellow dots = checkpoints. If a
/// MinimapPanel / dot templates are wired in the HUD, they are reused;
/// otherwise the panel and dots are built in code so the minimap works
/// even if the prefab wiring is lost. One Image per dot, no textures.</summary>
public class Minimap : MonoBehaviour
{
    public RectTransform minimapPanel;
    public Image playerDotPrefab;
    public Image trafficDotPrefab;
    public Image checkpointDotPrefab;
    /// <summary>World span the minimap covers. Set from
    /// DistrictData.WorldSize in Start() (overrides the stale
    /// inspector value) so the map always matches the layout.</summary>
    public float worldSize = 1000f;
    public float panelSize = 180f;

    List<Image> trafficDots = new List<Image>();
    List<Image> checkpointDots = new List<Image>();
    Image playerDot;
    Transform player;
    List<Transform> traffic = new List<Transform>();
    List<Transform> checkpoints = new List<Transform>();
    float refreshTimer;

    void Start()
    {
        // The layout owns the world size (60 sq km = 7700 m): take it
        // from DistrictData, not from the inspector / scene value.
        worldSize = DistrictData.WorldSize;
        if (minimapPanel == null)
        {
            var panelObj = GameObject.Find("MinimapPanel");
            if (panelObj != null) minimapPanel = panelObj.GetComponent<RectTransform>();
        }
        if (minimapPanel == null) minimapPanel = BuildPanel();
        // FIX (2026-11-11, semi-final): the minimap did not render in
        // Build #7 (0 green player-dot pixels even at spawn, though the
        // scene wiring is correct — active, enabled, under HUDCanvas).
        // Root cause could not be determined from code inspection, so
        // make it bulletproof: force the panel active and to the FRONT
        // (SetAsLastSibling) so no other HUD element can draw over it,
        // make the background more opaque (0.6) so the panel reads as a
        // dark square even over the dark city, and log clearly so the
        // next build's Player.log proves whether Start() ran.
        minimapPanel.gameObject.SetActive(true);
        minimapPanel.SetAsLastSibling();
        var panelImg = minimapPanel.GetComponent<Image>();
        if (panelImg != null) panelImg.color = new Color(0f, 0f, 0f, 0.6f);
        NormalizePanel(minimapPanel);
        Debug.Log("[Minimap] initialized: panel active, brought to front, worldSize=" + worldSize);
        // FIX (2026-11-11, semi-final): the HUD prefab ships
        // three STATIC dots as children of MinimapPanel
        // (MinimapPlayerDot / MinimapTrafficDot / MinimapCheckpointDot)
        // that nothing ever moves — they sit frozen, one of them on top
        // of the panel centre, on top of the real moving dots. That made
        // the minimap lie (a phantom player dot glued to the centre,
        // checkpoint dots that never track the gates) and corrupted any
        // screen reading of the player position. Switch them off: this
        // script builds its own dots below and keeps them updated.
        foreach (Transform child in minimapPanel)
        {
            if (child.name.Contains("Dot")) child.gameObject.SetActive(false);
        }
        playerDot = BuildDot(minimapPanel, new Color(0.1f, 0.9f, 0.2f), 16f);
        RefreshTargets();
    }

    void Update()
    {
        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            RefreshTargets();
        }
        if (player != null && playerDot != null)
            playerDot.rectTransform.anchoredPosition = ClampToPanel(WorldToMap(player.position));
        for (int i = 0; i < trafficDots.Count && i < traffic.Count; i++)
        {
            if (traffic[i] == null) continue;
            trafficDots[i].rectTransform.anchoredPosition = ClampToPanel(WorldToMap(traffic[i].position));
            trafficDots[i].gameObject.SetActive(true);
        }
        for (int i = 0; i < checkpointDots.Count && i < checkpoints.Count; i++)
        {
            if (checkpoints[i] == null) continue;
            checkpointDots[i].rectTransform.anchoredPosition = ClampToPanel(WorldToMap(checkpoints[i].position));
        }
    }

    void RefreshTargets()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        traffic.Clear();
        foreach (var go in GameObject.FindGameObjectsWithTag("Traffic"))
            traffic.Add(go.transform);
        while (trafficDots.Count < traffic.Count)
            trafficDots.Add(BuildDot(minimapPanel, new Color(0.9f, 0.15f, 0.15f), 9f));
        for (int i = 0; i < trafficDots.Count; i++)
            trafficDots[i].gameObject.SetActive(i < traffic.Count);

        checkpoints.Clear();
        var cps = GameObject.FindGameObjectsWithTag("Checkpoint");
        System.Array.Sort(cps, (a, b) => a.name.CompareTo(b.name));
        foreach (var go in cps) checkpoints.Add(go.transform);
        while (checkpointDots.Count < checkpoints.Count)
            checkpointDots.Add(BuildDot(minimapPanel, new Color(1f, 0.85f, 0.1f), 8f));
        for (int i = 0; i < checkpointDots.Count; i++)
            checkpointDots[i].gameObject.SetActive(i < checkpoints.Count);
    }

    Vector2 WorldToMap(Vector3 worldPos)
    {
        // World is centred on (0,0), 1000 m across. North (+Z) is up.
        float x = (worldPos.x / worldSize + 0.5f) * panelSize - panelSize * 0.5f;
        float y = (worldPos.z / worldSize + 0.5f) * panelSize - panelSize * 0.5f;
        return new Vector2(x, y);
    }

    /// <summary>Clamp a dot to the panel so it never flies off the map.
    /// FIX (2026-11-11, semi-final): after driving ~6 km from spawn the
    /// player dot landed at y=±140 — outside the 180 px panel — so the
    /// minimap looked empty at speed. Clamping pins the dot to the
    /// panel edge, which is the correct minimap behaviour.</summary>
    Vector2 ClampToPanel(Vector2 p)
    {
        float half = panelSize * 0.5f - 8f;
        return new Vector2(Mathf.Clamp(p.x, -half, half), Mathf.Clamp(p.y, -half, half));
    }

    /// <summary>Pin the panel fully on-screen, top-right: anchor and
    /// pivot (1,1), 16 px from the right edge, 110 px below the top, at
    /// panelSize. Called for a prefab-supplied panel as well as a
    /// code-built one, so a bad prefab anchor can never push the map
    /// (and the western checkpoints / player dot) off-screen again.</summary>
    void NormalizePanel(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -110f);
        rt.sizeDelta = new Vector2(panelSize, panelSize);
    }

    RectTransform BuildPanel()
    {
        var canvas = FindFirstObjectByType<Canvas>();
        var go = new GameObject("MinimapPanel");
        go.transform.SetParent(canvas != null ? canvas.transform : transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.45f);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-16f, -110f);
        rt.sizeDelta = new Vector2(panelSize, panelSize);
        return rt;
    }

    Image BuildDot(RectTransform parent, Color color, float size)
    {
        var go = new GameObject("MinimapDot");
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }
}

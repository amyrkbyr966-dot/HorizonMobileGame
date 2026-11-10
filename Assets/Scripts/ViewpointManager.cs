using System.Collections.Generic;
using UnityEngine;

/// <summary>VIEWPOINT PHOTO SPOTS (my own idea #2) — the scenic
/// viewpoints already in DistrictData.Places (kind == Viewpoint)
/// become a photo collection. Drive to a viewpoint, open Photo Mode
/// (P), frame the shot and capture it (C): if the player car is
/// within 60 m of a viewpoint when the photo is taken, the spot is
/// marked photographed — $300 + 100 XP per new spot, progress saved
/// in PlayerPrefs ("Viewpoint_Photo_<placeId>"). Hooks
/// PhotoModeController only through its public PhotoCaptured event
/// and reads DistrictData.Places (public data) — no edits anywhere.
/// Why this exists: it gives photo mode a purpose beyond the shot
/// itself, and turns exploring the open world into a collection
/// game — the exact loop (drive somewhere beautiful, photograph it)
/// that makes a Horizon-style open world feel alive.</summary>
public class ViewpointManager : MonoBehaviour
{
    public static ViewpointManager Instance { get; private set; }

    public const int PhotoRewardMoney = 300;
    public const int PhotoRewardXP = 100;
    public const float PhotoRadius = 60f;

    /// <summary>Viewpoints photographed so far (this save).</summary>
    public int PhotographedCount { get; private set; }
    /// <summary>Total viewpoints in the world.</summary>
    public int TotalViewpoints { get; private set; }

    readonly List<DistrictData.DiscoverablePlace> viewpoints = new List<DistrictData.DiscoverablePlace>();
    readonly HashSet<string> photographed = new HashSet<string>();
    bool subscribed;
    HUD hud;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        foreach (var place in DistrictData.Places)
            if (place.kind == DistrictData.PlaceKind.Viewpoint)
                viewpoints.Add(place);
        TotalViewpoints = viewpoints.Count;
        foreach (var place in viewpoints)
            if (PlayerPrefs.GetInt("Viewpoint_Photo_" + place.id, 0) == 1)
                photographed.Add(place.id);
        PhotographedCount = photographed.Count;
    }

    void Update()
    {
        // Subscribe once the photo controller exists (bootstrap order).
        if (!subscribed && PhotoModeController.Instance != null)
        {
            PhotoModeController.Instance.PhotoCaptured += OnPhotoCaptured;
            subscribed = true;
        }
    }

    void OnDestroy()
    {
        if (subscribed && PhotoModeController.Instance != null)
            PhotoModeController.Instance.PhotoCaptured -= OnPhotoCaptured;
    }

    void OnPhotoCaptured(string path)
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        Vector3 flat = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
        DistrictData.DiscoverablePlace? nearest = null;
        float bestD = PhotoRadius;
        foreach (var place in viewpoints)
        {
            Vector3 p = new Vector3(place.position.x, 0f, place.position.z);
            float d = Vector3.Distance(flat, p);
            if (d < bestD) { bestD = d; nearest = place; }
        }
        if (nearest == null) return;
        var spot = nearest.Value;
        if (photographed.Contains(spot.id))
        {
            ShowMessage("Photo saved - " + spot.name + " (already in your collection)", 3f);
            return;
        }
        photographed.Add(spot.id);
        PhotographedCount = photographed.Count;
        PlayerPrefs.SetInt("Viewpoint_Photo_" + spot.id, 1);
        PlayerPrefs.Save();
        var gm = GameManager.Instance;
        if (gm != null) gm.AddMoney(PhotoRewardMoney);
        PlayerProfile.AddXP(PhotoRewardXP);
        ShowMessage("Viewpoint photographed: " + spot.name + "  (" + PhotographedCount + "/" + TotalViewpoints
            + ")  +$" + PhotoRewardMoney + "  +" + PhotoRewardXP + " XP", 5f);
        Debug.Log("[Viewpoint] photographed " + spot.name + " (" + PhotographedCount + "/" + TotalViewpoints + ")");
    }

    /// <summary>True when this viewpoint is already photographed.</summary>
    public bool IsPhotographed(string placeId)
    {
        return photographed.Contains(placeId);
    }

    /// <summary>Hint for the Phase B HUD: nearest unphotographed
    /// viewpoint and how far away it is ("Bay Bridge Viewpoint - 1.2 km").</summary>
    public string NearestUnphotographedHint()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return "";
        Vector3 flat = new Vector3(player.transform.position.x, 0f, player.transform.position.z);
        DistrictData.DiscoverablePlace? best = null;
        float bestD = float.MaxValue;
        foreach (var place in viewpoints)
        {
            if (photographed.Contains(place.id)) continue;
            Vector3 p = new Vector3(place.position.x, 0f, place.position.z);
            float d = Vector3.Distance(flat, p);
            if (d < bestD) { bestD = d; best = place; }
        }
        if (best == null) return "All viewpoints photographed!";
        return best.Value.name + " - " + (bestD / 1000f).ToString("0.0") + " km";
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

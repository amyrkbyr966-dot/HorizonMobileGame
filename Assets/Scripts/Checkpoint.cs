using UnityEngine;

/// <summary>Checkpoint gate. The trigger collider (Is Trigger) calls the
/// RaceManager when the player drives through, in order.</summary>
[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    public int checkpointIndex = 0;
    public RaceManager raceManager;
    public Color activeColor = Color.green;
    public Color passedColor = Color.yellow;

    Renderer rend;
    Color originalColor;

    void Awake()
    {
        // FIX (2026-11-07, race blocker #2): the prefab trigger was
        // 9 m x 2 m; rotated 90 deg at several gates that left only ~2 m
        // across the road, so a car on the road could miss the gate
        // entirely. Force a 16 m x 16 m trigger (roads are 14 m wide):
        // full road width in BOTH axes, so the gate fires whatever the
        // approach angle is. The visual posts/bar stay 9 m apart.
        var col = GetComponent<BoxCollider>();
        if (col != null && (col.size.x < 16f || col.size.z < 16f))
            col.size = new Vector3(16f, 4f, 16f);
        rend = GetComponentInChildren<Renderer>();
        if (rend != null) originalColor = rend.material.color;
        if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        bool accepted = raceManager == null || checkpointIndex == raceManager.NextCheckpoint;
        if (raceManager != null) raceManager.OnCheckpointReached(checkpointIndex);
        if (TimeTrialManager.Instance != null) TimeTrialManager.Instance.OnCheckpointReached(checkpointIndex);
        if (accepted && SoundManager.Instance != null) SoundManager.Instance.PlayCheckpoint();
        if (rend != null) rend.material.color = passedColor;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (rend != null) rend.material.color = originalColor;
    }
}

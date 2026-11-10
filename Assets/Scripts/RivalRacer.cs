using System.Collections.Generic;
using UnityEngine;

/// <summary>Competitive AI rival for Street Race / Championship races.
/// Follows the RaceManager checkpoints in order (distance-based, no
/// triggers), with rubber-banding: slows when well ahead of the player,
/// speeds up when behind. Progress is laps * checkpoints + gate index,
/// so Championship can rank it against the player. Kinematic rigidbody,
/// like TrafficCar, so it never tips over.</summary>
[RequireComponent(typeof(Rigidbody))]
public class RivalRacer : MonoBehaviour
{
    public float cruiseSpeed = 36f;
    public float turnSpeed = 3f;
    public int rivalIndex = 0;
    public Color bodyColor = Color.magenta;

    public List<Transform> checkpoints = new List<Transform>();
    public int NextCheckpoint { get; private set; }
    public int CurrentLap { get; private set; } = 1;
    public bool Finished { get; private set; }
    public int TotalLaps = 3;

    /// <summary>Race progress in "gates" (lap * gates + gate index).</summary>
    public float Progress
    {
        get { return (CurrentLap - 1) * Mathf.Max(1, checkpoints.Count) + NextCheckpoint; }
    }

    Rigidbody rb;
    RaceManager raceManager;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
    }

    void Start()
    {
        raceManager = FindFirstObjectByType<RaceManager>();
        if (checkpoints.Count == 0 && raceManager != null)
            checkpoints = new List<Transform>(raceManager.checkpoints);
        if (checkpoints.Count == 0)
        {
            var found = GameObject.FindGameObjectsWithTag("Checkpoint");
            System.Array.Sort(found, (a, b) => a.name.CompareTo(b.name));
            foreach (var go in found) checkpoints.Add(go.transform);
        }
        // Stagger rivals behind the player start line.
        transform.position += new Vector3(-6f - rivalIndex * 5f, 0f, -8f - rivalIndex * 4f);
        var body = transform.Find("Body");
        if (body != null)
        {
            var rend = body.GetComponent<Renderer>();
            if (rend != null) rend.material.color = bodyColor;
        }
    }

    void FixedUpdate()
    {
        if (Finished || checkpoints.Count == 0) return;
        Transform target = checkpoints[NextCheckpoint];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 12f)
        {
            NextCheckpoint++;
            if (NextCheckpoint >= checkpoints.Count)
            {
                NextCheckpoint = 0;
                CurrentLap++;
                if (CurrentLap > TotalLaps) { Finished = true; return; }
            }
            return;
        }
        float desiredAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredAngle,
            turnSpeed * 120f * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Euler(0f, newAngle, 0f));
        rb.MovePosition(rb.position + transform.forward * EffectiveSpeed() * Time.fixedDeltaTime);
    }

    float EffectiveSpeed()
    {
        float speed = cruiseSpeed + rivalIndex * 1.5f;
        // Rubber-banding vs the player race progress.
        if (raceManager != null)
        {
            float playerProgress = (raceManager.CurrentLap - 1) * Mathf.Max(1, checkpoints.Count)
                + raceManager.NextCheckpoint;
            float diff = Progress - playerProgress;
            if (diff > 2f) speed *= 0.88f;       // rival well ahead: ease off
            else if (diff < -2f) speed *= 1.15f; // rival behind: catch up
        }
        return speed;
    }
}

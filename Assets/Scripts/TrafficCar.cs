using System.Collections.Generic;
using UnityEngine;

/// <summary>Simple traffic AI: follows the waypoint loop, slows / stops
/// when something is ahead (raycast), keeps driving otherwise. Kinematic
/// rigidbody moved with MovePosition / MoveRotation so it never tips over.</summary>
[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    public float cruiseSpeed = 12f;
    public float turnSpeed = 2.5f;
    public float stopDistance = 8f;
    public List<Transform> waypoints = new List<Transform>();

    int currentWaypoint = 0;
    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
        CarVisuals.Rebuild(transform, isPlayer: false);
    }

    void Start()
    {
        if (waypoints.Count == 0)
        {
            var found = GameObject.FindGameObjectsWithTag("Waypoint");
            var list = new List<Transform>();
            foreach (var go in found) list.Add(go.transform);
            list.Sort((a, b) => a.name.CompareTo(b.name));
            waypoints = list;
        }
        if (waypoints.Count > 0)
        {
            // Start at the NEAREST waypoint and then follow the ring in
            // order (fixed 2026-11-07, round-3 bug #1). The old code
            // picked a RANDOM waypoint and beelined to it in a straight
            // line — diagonally across the city, through buildings and
            // across the player's road — so a T-bone with this kinematic
            // (infinite-mass) car injected a huge solver impulse that
            // launched the player car into the void. The waypoints are
            // already in ring order (00..15), so sequential advance
            // keeps traffic on the roads, where the player can see it.
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                float d = (waypoints[i].position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }
            currentWaypoint = nearest;
        }
    }

    void FixedUpdate()
    {
        if (waypoints.Count == 0) return;
        Transform target = waypoints[currentWaypoint];
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude < 8f)
        {
            currentWaypoint = (currentWaypoint + 1) % waypoints.Count;
            return;
        }
        float desiredAngle = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
        float newAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.y, desiredAngle,
            turnSpeed * 120f * Time.fixedDeltaTime);
        rb.MoveRotation(Quaternion.Euler(0f, newAngle, 0f));
        float speed = IsBlocked() ? 0f : cruiseSpeed;
        rb.MovePosition(rb.position + transform.forward * speed * Time.fixedDeltaTime);
    }

    bool IsBlocked()
    {
        RaycastHit hit;
        Vector3 origin = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(origin, transform.forward, out hit, stopDistance))
        {
            if (hit.collider.CompareTag("Player") || hit.collider.CompareTag("Traffic"))
                return true;
        }
        return false;
    }
}

using UnityEngine;

/// <summary>Chase camera: smooth follow behind the car, looks ahead along
/// the road, widens FOV as speed rises (sense of speed).</summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float distance = 10f;
    public float height = 4.5f;
    public float smoothSpeed = 6f;
    public float lookAhead = 8f;
    public float baseFov = 60f;
    // FIX (2026-11-11, semi-final): the old maxFov 75 made the car
    // shrink to a dot at top speed (259 km/h reads as a speck — the
    // wide angle pulls the whole world in). Cap the widening at 68 and
    // make it ramp more gently (speed/80) so the car stays readable
    // while still giving a sense of speed.
    public float maxFov = 68f;
    public float fovSpeedDivisor = 80f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    void LateUpdate()
    {
        if (target == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else return;
        }
        // Never follow a non-finite target (round-3 bug #1): a NaN car
        // used to drag the camera into the void, showing an empty sky.
        Vector3 tp = target.position;
        if (float.IsNaN(tp.x) || float.IsNaN(tp.y) || float.IsNaN(tp.z)) return;
        Vector3 desired = tp - target.forward * distance + Vector3.up * height;
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        Vector3 lookPoint = target.position + target.forward * lookAhead + Vector3.up * 1.5f;
        transform.LookAt(lookPoint);
        if (cam != null)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            cam.fieldOfView = Mathf.Lerp(baseFov, maxFov, Mathf.Clamp01(speed / fovSpeedDivisor));
        }
    }
}

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
    public float maxFov = 75f;

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
            cam.fieldOfView = Mathf.Lerp(baseFov, maxFov, Mathf.Clamp01(speed / 55f));
        }
    }
}

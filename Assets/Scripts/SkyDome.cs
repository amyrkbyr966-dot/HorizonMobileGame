using UnityEngine;

/// <summary>Keeps the sky dome centred on the main camera (XZ) so the
/// camera can never drive outside it, while the dome stays small enough
/// that distance fog barely touches it.</summary>
public class SkyDome : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        transform.position = new Vector3(cam.transform.position.x, 0f, cam.transform.position.z);
    }
}

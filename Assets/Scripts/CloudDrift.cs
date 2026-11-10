using UnityEngine;

/// <summary>Drifting cloud layer: a transparent plane that slides slowly
/// across the sky and wraps around the city bounds.</summary>
public class CloudDrift : MonoBehaviour
{
    public float speed = 2f;
    public float wrap = 650f;
    void Update()
    {
        transform.position += Vector3.right * speed * Time.deltaTime;
        if (transform.position.x > wrap) transform.position -= new Vector3(wrap * 2f, 0f, 0f);
    }
}

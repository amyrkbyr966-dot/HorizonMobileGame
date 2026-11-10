using UnityEngine;

/// <summary>Sound-side trigger for tunnel reverb: attach to a tunnel (or any
/// enclosed space) with a trigger collider. When the player drives in, the
/// engine + whistle tones get the tunnel reverb (SoundManager.SetTunnelMode);
/// when they leave, it switches back off. This component places NO zones
/// itself — the world builders attach it to tunnel colliders when tunnels
/// exist. No tunnel geometry exists in the world yet, so nothing calls it
/// in-game today; the reverb preset itself is verified at compile time and
/// by the SoundManager's SetupTunnelReverb values.</summary>
[RequireComponent(typeof(Collider))]
public class TunnelZone : MonoBehaviour
{
    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && SoundManager.Instance != null)
            SoundManager.Instance.SetTunnelMode(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && SoundManager.Instance != null)
            SoundManager.Instance.SetTunnelMode(false);
    }
}

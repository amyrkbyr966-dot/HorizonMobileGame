using UnityEngine;

/// <summary>One spinning coin. Trigger (Is Trigger) so the player only
/// has to drive through it. Bobs and spins so it is visible from afar.
/// Tag must be "Coin" (added to TagManager) and the collider a trigger.</summary>
public class Collectible : MonoBehaviour
{
    public int value = 50;
    public bool collected;

    float baseY;
    float spinSpeed = 120f;

    void Awake()
    {
        baseY = transform.position.y;
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (collected) return;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
        Vector3 p = transform.position;
        p.y = baseY + 0.3f * Mathf.Sin(Time.time * 2f);
        transform.position = p;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player")) return;
        collected = true;
        if (CollectibleManager.Instance != null) CollectibleManager.Instance.Collect(this);
        gameObject.SetActive(false);
    }
}

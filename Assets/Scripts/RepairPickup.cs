using UnityEngine;

/// <summary>Green repair pickup in the world: drive through it to restore
/// 25 health for free (no money). Spins like a coin so it is easy to
/// spot. Tag must be "Repair" (added to TagManager).</summary>
[RequireComponent(typeof(BoxCollider))]
public class RepairPickup : MonoBehaviour
{
    public float repairAmount = 25f;
    public bool used;

    void Awake()
    {
        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
    }

    void Update()
    {
        if (used) return;
        transform.Rotate(0f, 90f * Time.deltaTime, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player")) return;
        var damage = other.GetComponent<CarDamage>();
        if (damage == null || damage.Health >= damage.maxHealth) return;
        used = true;
        damage.Repair(repairAmount);
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Repaired +" + Mathf.RoundToInt(repairAmount) + " HP");
        gameObject.SetActive(false);
    }
}

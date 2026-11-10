using UnityEngine;

/// <summary>Damage model for the player car. Collisions cost health based
/// on impact speed; below 50 health a smoke puff appears and handling
/// worsens, below 30 the car is slower still. At 0 the car is wrecked:
/// it stops, the HUD shows WRECKED and the run ends. Repair pickups in
/// the world restore health for free; pressing R repairs at a garage
/// for money (2 per health point). Attach to PlayerCar.</summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CarController))]
public class CarDamage : MonoBehaviour
{
    public float maxHealth = 100f;
    public float minImpactSpeed = 3f;
    public float damagePerMs = 1.5f;
    public int repairCostPerPoint = 2;
    /// <summary>Set by UpgradeSystem armor (1 = no armor, less = tougher).</summary>
    public float damageMultiplier = 1f;

    public float Health { get; private set; }
    public bool IsWrecked { get; private set; }
    public float HealthNormalized { get { return Mathf.Clamp01(Health / maxHealth); } }

    CarController car;
    Rigidbody rb;
    GameObject smoke;
    float baseHandling;
    float lastCrashTime;

    void Awake()
    {
        car = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        Health = maxHealth;
        baseHandling = car != null ? car.handlingMultiplier : 1f;
        smoke = MakeSmokePuff();
    }

    void Update()
    {
        if (IsWrecked) return;
        // Visual + handling degradation at low health.
        if (smoke != null) smoke.SetActive(Health < 50f);
        if (smoke != null && smoke.activeSelf)
        {
            float s = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            smoke.transform.localScale = new Vector3(s, s, s);
        }
        if (car != null)
        {
            if (Health < 30f) car.handlingMultiplier = baseHandling * 0.6f;
            else if (Health < 50f) car.handlingMultiplier = baseHandling * 0.8f;
            else car.handlingMultiplier = baseHandling;
        }
        if (Input.GetKeyDown(KeyCode.R)) RepairAtGarage();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsWrecked) return;
        float impact = collision.relativeVelocity.magnitude;
        if (impact < minImpactSpeed) return;
        if (Time.time - lastCrashTime < 0.5f) return; // one hit per scrape
        lastCrashTime = Time.time;
        TakeDamage(impact * damagePerMs);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCrash();
    }

    public void TakeDamage(float amount)
    {
        if (IsWrecked || amount <= 0f) return;
        Health = Mathf.Max(0f, Health - amount * damageMultiplier);
        if (Health <= 0f) Wreck();
    }

    public void Repair(float amount)
    {
        if (IsWrecked) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayRepair();
    }

    /// <summary>Pay to repair from the garage (R key). Returns false if
    /// fully repaired already or the player cannot afford it.</summary>
    public bool RepairAtGarage()
    {
        if (IsWrecked || Health >= maxHealth) return false;
        int missing = Mathf.CeilToInt(maxHealth - Health);
        int cost = missing * repairCostPerPoint;
        if (GameManager.Instance == null || GameManager.Instance.Money < cost) return false;
        GameManager.Instance.AddMoney(-cost);
        Health = maxHealth;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayRepair();
        return true;
    }

    public void ResetDamage()
    {
        Health = maxHealth;
        IsWrecked = false;
        if (car != null) car.handlingMultiplier = baseHandling;
        if (smoke != null) smoke.SetActive(false);
    }

    void Wreck()
    {
        IsWrecked = true;
        if (rb != null) rb.linearVelocity *= 0.3f;
        if (car != null) car.enabled = false;
        if (GameManager.Instance != null) GameManager.Instance.GameOver("WRECKED!");
    }

    GameObject MakeSmokePuff()
    {
        var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.name = "SmokePuff";
        puff.transform.SetParent(transform, false);
        puff.transform.localPosition = new Vector3(0f, 1.1f, 1.4f);
        puff.transform.localScale = Vector3.one * 0.7f;
        var col = puff.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var rend = puff.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.15f, 0.15f, 0.15f, 0.6f);
            rend.material = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        puff.SetActive(false);
        return puff;
    }
}

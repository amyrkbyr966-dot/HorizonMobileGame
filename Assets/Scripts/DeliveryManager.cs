using UnityEngine;

/// <summary>Timed delivery missions: drive to a pickup point, then to a
/// delivery point before the timer runs out. Reward = $500 + $10 per
/// second left. Markers are created in code (no scene wiring needed).
/// Attach to a GameObject in the Main scene.</summary>
public class DeliveryManager : MonoBehaviour
{
    public static DeliveryManager Instance { get; private set; }

    public enum DeliveryState { Idle, ToPickup, ToDeliver, Done }
    public DeliveryState State { get; private set; } = DeliveryState.Idle;
    public float TimeLeft { get; private set; }
    public float TimeLimit = 120f;

    // Landmarks from the world (gas station, garage, downtown, desert).
    static readonly Vector3[] Landmarks = new Vector3[]
    {
        new Vector3(-75f, 0f, -425f),   // Gas station
        new Vector3(75f, 0f, -480f),    // Garage (moved 2026-11-10 from
        // the old lot at (75, -440) to the new garage building at
        // (75, 6, -480) in Main.unity; y stays 0 like every other
        // landmark — the car drives at ground level, the building's
        // y=6 is its model origin, and MoveMarker forces y=1 anyway)
        new Vector3(420f, 0f, 420f),    // Horizon Tower
        new Vector3(-375f, 0f, -375f),  // Desert
        new Vector3(300f, 0f, 300f),    // Downtown
        new Vector3(0f, 0f, 450f),      // Highway ring
    };

    GameObject marker;
    Vector3 pickupPoint;
    Vector3 deliveryPoint;
    CarController playerCar;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (GameManager.Instance != null && GameManager.Instance.Mode == GameManager.GameMode.Delivery)
            StartDelivery();
    }

    void Update()
    {
        if (State == DeliveryState.Idle || State == DeliveryState.Done) return;
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
            else return;
        }
        TimeLeft -= Time.deltaTime;
        if (TimeLeft <= 0f)
        {
            FailDelivery();
            return;
        }
        Vector3 target = State == DeliveryState.ToPickup ? pickupPoint : deliveryPoint;
        if (Vector3.Distance(playerCar.transform.position, target) < 14f)
        {
            if (State == DeliveryState.ToPickup)
            {
                State = DeliveryState.ToDeliver;
                MoveMarker(deliveryPoint, new Color(0.1f, 0.8f, 0.2f));
                var hud = FindFirstObjectByType<HUD>();
                if (hud != null) hud.ShowMessage("Package picked up! Deliver it! " + Mathf.RoundToInt(TimeLeft) + "s left", 4f);
            }
            else CompleteDelivery();
        }
        if (marker != null)
            marker.transform.Rotate(0f, 90f * Time.deltaTime, 0f);
    }

    public void StartDelivery()
    {
        pickupPoint = Landmarks[Random.Range(0, Landmarks.Length)];
        do { deliveryPoint = Landmarks[Random.Range(0, Landmarks.Length)]; }
        while (Vector3.Distance(deliveryPoint, pickupPoint) < 200f);
        TimeLeft = TimeLimit;
        State = DeliveryState.ToPickup;
        MoveMarker(pickupPoint, new Color(1f, 0.8f, 0.1f));
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Delivery: pick up the package! " + Mathf.RoundToInt(TimeLeft) + "s", 4f);
    }

    void CompleteDelivery()
    {
        State = DeliveryState.Done;
        // Economy rebalance (2026-11-11, semi-final): $300 + $5/s -> $500 + $10/s.
        int reward = 500 + Mathf.RoundToInt(TimeLeft) * 10;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(reward);
        PlayerProfile.AddXP(150);
        if (marker != null) marker.SetActive(false);
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Delivered! +$" + reward, 5f);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayDelivery();
        SaveManager.Save();
    }

    void FailDelivery()
    {
        State = DeliveryState.Done;
        if (marker != null) marker.SetActive(false);
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage("Delivery failed - out of time!", 4f);
    }

    void MoveMarker(Vector3 pos, Color color)
    {
        if (marker == null)
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "DeliveryMarker";
            marker.transform.localScale = new Vector3(6f, 0.3f, 6f);
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
        marker.SetActive(true);
        marker.transform.position = new Vector3(pos.x, 1f, pos.z);
        var rend = marker.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 0.6f);
            rend.material = mat;
        }
    }
}

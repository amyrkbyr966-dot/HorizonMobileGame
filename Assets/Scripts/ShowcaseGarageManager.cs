using UnityEngine;

/// <summary>SHOWCASE GARAGE (my own idea #3) — a collection showroom
/// at the Shibuya garage (DistrictData garage place, 300, -450).
/// Your owned cars stand on a slowly rotating turntable so you can
/// walk around and admire the collection you have built — the
/// payoff for earning cars, not just a menu list. Public API:
///   EnterGarage() / ExitGarage() / NextCar() / PreviousCar() /
///   ShowCar(index) / ToggleOwnedOnly() / CurrentCarIndex.
/// The display car is built with the existing public
/// CarVisuals.Rebuild in the car's real roster paint (and prefers a
/// real imported model at Resources/Models/Cars/<name> when the
/// model-integration agent has landed one — same hook as the car
/// meet). Only unlocked cars are shown by default (CarSelection.
/// IsUnlocked, public); OwnedOnly off shows the whole 55-car roster
/// so you can browse what to save up for. Stats are announced with
/// HUD.ShowMessage — no HUD edit. The showroom activates when you
/// drive near the garage in free-roam, or by calling EnterGarage().</summary>
public class ShowcaseGarageManager : MonoBehaviour
{
    public static ShowcaseGarageManager Instance { get; private set; }

    /// <summary>Garage showroom position (Shibuya garage place).</summary>
    public static readonly Vector3 GaragePosition = new Vector3(300f, 0f, -450f);
    public const float ActivationRadius = 40f;

    /// <summary>True while the showroom is open.</summary>
    public bool IsOpen { get; private set; }
    /// <summary>Roster index of the car on the turntable.</summary>
    public int CurrentCarIndex { get; private set; } = -1;
    /// <summary>When true, Next/Previous skip locked cars.</summary>
    public bool OwnedOnly { get; private set; } = true;

    GameObject showroomRoot;
    GameObject displayCar;
    float turntableAngle;
    HUD hud;
    float checkTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (IsOpen && displayCar != null)
        {
            turntableAngle += Time.deltaTime * 25f;
            displayCar.transform.rotation = Quaternion.Euler(0f, turntableAngle, 0f);
        }

        // Auto-open when the player drives up to the garage in free-roam.
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.5f;
        var gm = GameManager.Instance;
        if (gm == null || gm.IsPaused || gm.IsGameOver) return;
        if (gm.Mode != GameManager.GameMode.FreeRoam) return;
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        float d = Vector3.Distance(
            new Vector3(player.transform.position.x, 0f, player.transform.position.z),
            new Vector3(GaragePosition.x, 0f, GaragePosition.z));
        if (d <= ActivationRadius && !IsOpen) EnterGarage();
        else if (d > ActivationRadius + 60f && IsOpen) ExitGarage();
    }

    /// <summary>Open the showroom on the first owned car.</summary>
    public void EnterGarage()
    {
        if (IsOpen) return;
        IsOpen = true;
        showroomRoot = new GameObject("ShowcaseGarage");
        showroomRoot.transform.position = GaragePosition;
        // Warm showroom light over the turntable (real light; the
        // turntable platform geometry arrives with Phase B).
        var lightGo = new GameObject("ShowroomLight");
        lightGo.transform.SetParent(showroomRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(10f, 6f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.intensity = 40f;
        light.range = 25f;
        int first = FirstOwnedCar();
        ShowCar(first >= 0 ? first : CarSelection.SelectedIndex);
        Debug.Log("[ShowcaseGarage] opened");
    }

    public void ExitGarage()
    {
        if (!IsOpen) return;
        IsOpen = false;
        CurrentCarIndex = -1;
        if (showroomRoot != null) Destroy(showroomRoot);
        showroomRoot = null;
        displayCar = null;
        Debug.Log("[ShowcaseGarage] closed");
    }

    /// <summary>Show a roster car on the turntable.</summary>
    public void ShowCar(int carIndex)
    {
        if (!IsOpen || showroomRoot == null) return;
        carIndex = Mathf.Clamp(carIndex, 0, CarSelection.Cars.Length - 1);
        if (displayCar != null) Destroy(displayCar);
        var def = CarSelection.Cars[carIndex];
        displayCar = new GameObject("ShowcaseCar_" + def.carName.Replace(" ", ""));
        displayCar.transform.SetParent(showroomRoot.transform, false);
        displayCar.transform.localPosition = new Vector3(10f, 0f, 0f);
        turntableAngle = 0f;

        string prefabPath = "Models/Cars/" + def.carName.Replace(" ", "");
        GameObject prefab = Resources.Load<GameObject>(prefabPath);
        if (prefab != null)
        {
            Instantiate(prefab, displayCar.transform, false);
        }
        else
        {
            var body = new GameObject("Body");
            body.transform.SetParent(displayCar.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            body.AddComponent<MeshFilter>();
            var mr = body.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", def.bodyColor);
            mat.SetColor("_Color", def.bodyColor);
            mr.sharedMaterial = mat;
            CarVisuals.Rebuild(displayCar.transform, false);
        }

        CurrentCarIndex = carIndex;
        bool owned = CarSelection.IsUnlocked(carIndex);
        ShowMessage(def.carName + "  -  " + Mathf.RoundToInt(def.topSpeedKmh) + " km/h  -  $"
            + def.price + (owned ? "  (owned)" : "  (not owned - save up!)"), 4f);
        Debug.Log("[ShowcaseGarage] showing " + def.carName + " (owned=" + owned + ")");
    }

    public void NextCar()
    {
        if (!IsOpen) return;
        ShowCar(StepCar(CurrentCarIndex, 1));
    }

    public void PreviousCar()
    {
        if (!IsOpen) return;
        ShowCar(StepCar(CurrentCarIndex, -1));
    }

    public void ToggleOwnedOnly()
    {
        OwnedOnly = !OwnedOnly;
        ShowMessage(OwnedOnly ? "Showroom: owned cars only" : "Showroom: full roster", 3f);
        if (IsOpen) ShowCar(CurrentCarIndex);
    }

    int StepCar(int from, int dir)
    {
        int count = CarSelection.Cars.Length;
        for (int i = 1; i <= count; i++)
        {
            int idx = (from + dir * i + count * 10) % count;
            if (!OwnedOnly || CarSelection.IsUnlocked(idx)) return idx;
        }
        return from;
    }

    int FirstOwnedCar()
    {
        for (int i = 0; i < CarSelection.Cars.Length; i++)
            if (CarSelection.IsUnlocked(i)) return i;
        return -1;
    }

    void ShowMessage(string msg, float seconds)
    {
        if (hud == null) hud = FindFirstObjectByType<HUD>();
        if (hud != null) hud.ShowMessage(msg, seconds);
    }
}

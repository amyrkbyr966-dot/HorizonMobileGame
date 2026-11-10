using UnityEngine;

/// <summary>Fifty-five selectable cars (Tokyo roster, updated 2026-11-07): the 8
/// original cars (indices 0-7, never reordered — saves depend on them)
/// plus 47 new cars from Germany, England, America, Italy,
/// France, Japan and the 2026 model-year group (indices 8-54). All Chinese
/// cars and the non-sporty American SUV were removed 2026-11-07 so the
/// freed effort goes to more detail per car. Stats are applied to the player
/// CarController at spawn (CarController.Awake calls ApplyToCar). Cars
/// beyond the first cost money earned from races / time trials / coins /
/// deliveries and are saved in PlayerPrefs, so the choice survives scene
/// loads and restarts. See also UpgradeSystem for per-car upgrades.</summary>
[System.Serializable]
public class CarDef
{
    public string carName;
    public float topSpeedKmh;
    public float acceleration;
    public float handling;
    public int price;
    public Color bodyColor;
    /// <summary>Electric car: gets Battery/Motor upgrades instead of
    /// Engine, and an electric engine voice in SoundManager.</summary>
    public bool electric;

    public CarDef(string name, float topSpeedKmh, float acceleration, float handling, int price, Color bodyColor, bool electric = false)
    {
        carName = name;
        this.topSpeedKmh = topSpeedKmh;
        this.acceleration = acceleration;
        this.handling = handling;
        this.price = price;
        this.bodyColor = bodyColor;
        this.electric = electric;
    }
}

public static class CarSelection
{
    /// <summary>EARLY ACCESS (2026-11-08): only ONE car is available —
    /// car #5 Turbo Falcon (the detailed imported model). While true,
    /// SelectedIndex always returns EarlyAccessCarIndex, the setter is
    /// a no-op (so SaveManager restores, menu buy/select and the
    /// showcase cannot switch the car), IsUnlocked is true only for
    /// #5 and TryBuy refuses every other car. This is a code lock,
    /// not a PlayerPrefs preset — the player cannot switch cars.
    /// Set false for the full 55-car version.</summary>
    public const bool EarlyAccessSingleCar = true;
    public const int EarlyAccessCarIndex = 5;

    public static readonly CarDef[] Cars = new CarDef[]
    {
        new CarDef("City Sprint", 165f, 0.8f, 1.00f, 0, new Color(0.85f, 0.10f, 0.10f)),
        new CarDef("Street GT", 198f, 1.0f, 1.05f, 1500, new Color(0.10f, 0.25f, 0.80f)),
        new CarDef("Desert Fox", 205f, 0.95f, 1.02f, 2500, new Color(0.85f, 0.45f, 0.10f)),
        new CarDef("Night Hawk", 220f, 1.05f, 1.08f, 3200, new Color(0.35f, 0.15f, 0.65f)),
        new CarDef("Horizon R", 235f, 1.25f, 1.15f, 4000, new Color(0.95f, 0.75f, 0.10f)),
        new CarDef("Turbo Falcon", 245f, 1.30f, 1.12f, 6000, new Color(0.10f, 0.70f, 0.85f)),
        new CarDef("Apex GT", 258f, 1.35f, 1.20f, 8500, new Color(0.95f, 0.95f, 0.95f)),
        new CarDef("Horizon X", 275f, 1.45f, 1.25f, 12000, new Color(0.08f, 0.08f, 0.10f)),
        // Germany (8-14) — brand names altered to avoid copyright.
        new CarDef("BMV M4 Coupé", 250f, 1.30f, 1.18f, 7500, new Color(0.10f, 0.30f, 0.85f)),
        new CarDef("BMV M8 Gran", 270f, 1.35f, 1.15f, 11000, new Color(0.15f, 0.15f, 0.18f)),
        new CarDef("BMV i9 Roadster", 260f, 1.40f, 1.20f, 12000, new Color(0.92f, 0.92f, 0.95f)),
        new CarDef("Mercidez AMG GT", 285f, 1.38f, 1.22f, 15000, new Color(0.05f, 0.45f, 0.25f)),
        new CarDef("Mercidez G-Klass", 220f, 1.05f, 1.00f, 8000, new Color(0.08f, 0.08f, 0.10f)),
        new CarDef("Auddi R9", 305f, 1.45f, 1.25f, 18000, new Color(0.85f, 0.10f, 0.10f)),
        new CarDef("Porscha 911 Carrera", 290f, 1.42f, 1.28f, 16000, new Color(0.95f, 0.80f, 0.10f)),
        // England (15-19)
        new CarDef("Bentlee Continental", 265f, 1.25f, 1.10f, 14000, new Color(0.75f, 0.78f, 0.80f)),
        new CarDef("Rolls-Royca Ghost", 240f, 1.10f, 1.02f, 16000, new Color(0.05f, 0.10f, 0.30f)),
        new CarDef("McLarren 720S", 325f, 1.50f, 1.30f, 22000, new Color(0.95f, 0.45f, 0.05f)),
        new CarDef("McLarren P2", 350f, 1.60f, 1.35f, 38000, new Color(0.90f, 0.20f, 0.05f)),
        new CarDef("Aston Martyn Vantage", 285f, 1.38f, 1.22f, 15500, new Color(0.05f, 0.35f, 0.20f)),
        // America (20-25) — sporty only; Cadilac Escalade removed 2026-11-07
        // (non-sporty SUV). Teslo Model S Plaid stays: 325 km/h performance sedan.
        // All Chinese cars removed 2026-11-07 (BYE Seal, XPang P7, Zeekr 001).
        new CarDef("Forde Mustang GT", 260f, 1.30f, 1.10f, 8000, new Color(0.80f, 0.10f, 0.10f)),
        new CarDef("Forde GT", 330f, 1.52f, 1.30f, 28000, new Color(0.10f, 0.25f, 0.70f)),
        new CarDef("Chevrolett Corvette", 315f, 1.48f, 1.28f, 20000, new Color(0.95f, 0.75f, 0.05f)),
        new CarDef("Dodga Challenger", 275f, 1.28f, 1.05f, 9500, new Color(0.10f, 0.10f, 0.12f)),
        new CarDef("Teslo Model S Plaid", 325f, 1.55f, 1.25f, 21000, new Color(0.93f, 0.93f, 0.93f), true),
        new CarDef("Shelbi Cobra", 295f, 1.40f, 1.20f, 17000, new Color(0.15f, 0.30f, 0.80f)),
        // Italy (26-32)
        new CarDef("Ferrarri 488", 330f, 1.52f, 1.30f, 26000, new Color(0.90f, 0.05f, 0.05f)),
        new CarDef("Ferrarri LaFerrarri", 350f, 1.62f, 1.35f, 42000, new Color(0.85f, 0.05f, 0.08f)),
        new CarDef("Lamborghinni Huracan", 325f, 1.50f, 1.32f, 24000, new Color(0.60f, 0.85f, 0.10f)),
        new CarDef("Lamborghinni Countach", 340f, 1.58f, 1.33f, 36000, new Color(0.90f, 0.90f, 0.88f)),
        new CarDef("Maseratii MC20", 320f, 1.50f, 1.30f, 23000, new Color(0.10f, 0.20f, 0.60f)),
        new CarDef("Alfa Romea Giulia", 245f, 1.22f, 1.15f, 6800, new Color(0.75f, 0.08f, 0.10f)),
        new CarDef("Paganii Huayra", 335f, 1.56f, 1.32f, 34000, new Color(0.70f, 0.75f, 0.85f)),
        // France (33-37)
        new CarDef("Bpcati Chiron", 350f, 1.62f, 1.35f, 45000, new Color(0.05f, 0.15f, 0.55f)),
        new CarDef("Bpcati Bolide", 345f, 1.60f, 1.34f, 40000, new Color(0.05f, 0.08f, 0.20f)),
        new CarDef("Peugeott 508", 225f, 1.12f, 1.08f, 4800, new Color(0.50f, 0.52f, 0.55f)),
        new CarDef("Renaut Clio RS", 210f, 1.10f, 1.12f, 3800, new Color(0.95f, 0.80f, 0.10f)),
        new CarDef("Alpina A110", 250f, 1.28f, 1.20f, 7200, new Color(0.20f, 0.35f, 0.85f)),
        // Japan (38-45) — added 2026-11-07, MSAIN R34 GT first.
        new CarDef("MSAIN R34 GT", 300f, 1.48f, 1.30f, 26000, new Color(0.10f, 0.20f, 0.75f)),
        new CarDef("Toyata Supra", 290f, 1.45f, 1.28f, 19000, new Color(0.90f, 0.40f, 0.05f)),
        new CarDef("Hondaa NSX", 305f, 1.46f, 1.30f, 21000, new Color(0.85f, 0.10f, 0.12f)),
        new CarDef("Mazdza RX-7", 280f, 1.40f, 1.26f, 15000, new Color(0.95f, 0.80f, 0.05f)),
        new CarDef("Subaro WRX STI", 255f, 1.32f, 1.18f, 9000, new Color(0.05f, 0.20f, 0.65f)),
        new CarDef("Lexuss LFA", 325f, 1.55f, 1.32f, 30000, new Color(0.92f, 0.92f, 0.90f)),
        new CarDef("MSAIN Silvia S15", 250f, 1.35f, 1.22f, 8500, new Color(0.10f, 0.45f, 0.25f)),
        new CarDef("Toyata GR86", 230f, 1.25f, 1.20f, 7500, new Color(0.85f, 0.15f, 0.10f)),
        // 2026 model-year group (46-54) — all verified real 2026 models.
        // Xiaomii SU7 Ultra removed 2026-11-07 (Chinese) — group is now 9 models.
        new CarDef("Chevrolett Corvette ZR1", 355f, 1.62f, 1.34f, 36000, new Color(0.95f, 0.75f, 0.05f)),
        new CarDef("Forde Mustang GTD", 325f, 1.55f, 1.30f, 30000, new Color(0.05f, 0.15f, 0.55f)),
        new CarDef("Lamborghinni Temerario", 340f, 1.58f, 1.33f, 32000, new Color(0.35f, 0.75f, 0.10f)),
        new CarDef("Bpcati Tourbillon", 365f, 1.65f, 1.36f, 48000, new Color(0.05f, 0.08f, 0.25f)),
        new CarDef("McLarren W1", 350f, 1.64f, 1.35f, 44000, new Color(0.95f, 0.45f, 0.05f)),
        new CarDef("Toyata GR Supra Final Edition", 250f, 1.38f, 1.26f, 12000, new Color(0.85f, 0.05f, 0.10f)),
        new CarDef("Mercidez AMG GT63 Pro", 317f, 1.50f, 1.30f, 20000, new Color(0.25f, 0.45f, 0.35f)),
        new CarDef("Auddi RS6 Avant", 305f, 1.48f, 1.28f, 16000, new Color(0.20f, 0.22f, 0.25f)),
        new CarDef("Porscha 911 Turbo S", 320f, 1.56f, 1.33f, 24000, new Color(0.92f, 0.92f, 0.92f)),
    };

    public static int SelectedIndex
    {
        get
        {
            if (EarlyAccessSingleCar) return EarlyAccessCarIndex;
            return Mathf.Clamp(PlayerPrefs.GetInt("SelectedCar", 0), 0, Cars.Length - 1);
        }
        set
        {
            if (EarlyAccessSingleCar) return;
            PlayerPrefs.SetInt("SelectedCar", Mathf.Clamp(value, 0, Cars.Length - 1)); PlayerPrefs.Save();
        }
    }

    public static CarDef SelectedCar
    {
        get { return Cars[SelectedIndex]; }
    }

    public static bool IsUnlocked(int index)
    {
        if (EarlyAccessSingleCar) return index == EarlyAccessCarIndex;
        if (index <= 0) return true;
        return PlayerPrefs.GetInt("CarUnlocked_" + index, 0) == 1;
    }

    /// <summary>Buy with saved money. Returns false if already owned or
    /// if the player cannot afford it; money is only taken on success.</summary>
    public static bool TryBuy(int index)
    {
        if (index < 0 || index >= Cars.Length) return false;
        if (EarlyAccessSingleCar && index != EarlyAccessCarIndex) return false;
        if (IsUnlocked(index)) return true;
        int money = PlayerPrefs.GetInt("Money", 1000);
        if (money < Cars[index].price) return false;
        PlayerPrefs.SetInt("Money", money - Cars[index].price);
        PlayerPrefs.SetInt("CarUnlocked_" + index, 1);
        PlayerPrefs.Save();
        if (GameManager.Instance != null) GameManager.Instance.RefreshMoneyFromPrefs();
        return true;
    }

    public static void ApplyToCar(CarController car)
    {
        if (car == null) return;
        CarDef def = SelectedCar;
        car.ApplyStats(def.topSpeedKmh / 3.6f, def.acceleration, def.handling);
        car.SetBodyColor(def.bodyColor);
    }
}

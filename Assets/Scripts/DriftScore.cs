using UnityEngine;

/// <summary>Drift scoring. While the car is skidding at speed, drift
/// points build up (faster skid = faster points). When the car straightens
/// out for a moment, the points are banked into money ($1 per 20 points)
/// and the total is saved in PlayerPrefs ("TotalDriftPoints"). A skid that
/// ends in a crash banks nothing, so drifting stays risky. Attach to
/// PlayerCar (or any GameObject in the Main scene); it finds the player
/// car itself. HUD shows the live / total drift only when its driftText
/// field is assigned.</summary>
public class DriftScore : MonoBehaviour
{
    public float pointsPerSecondAtFullSpeed = 120f;
    public float minSpeedMs = 10f;
    public float bankDelay = 0.8f;       // straight time before banking
    public int moneyPerPoints = 20;      // $1 per 20 drift points

    public float CurrentDriftPoints { get; private set; }
    public int TotalDriftPoints { get; private set; }
    public int TotalDriftMoney { get; private set; }
    public bool IsDrifting { get; private set; }

    CarController playerCar;
    CarDamage carDamage;
    HUD hud;
    float straightTimer;

    void Awake()
    {
        TotalDriftPoints = PlayerPrefs.GetInt("TotalDriftPoints", 0);
        TotalDriftMoney = PlayerPrefs.GetInt("TotalDriftMoney", 0);
    }

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) playerCar = p.GetComponent<CarController>();
        if (playerCar != null) carDamage = playerCar.GetComponent<CarDamage>();
        hud = FindFirstObjectByType<HUD>();
    }

    void Update()
    {
        if (playerCar == null || (carDamage != null && carDamage.IsWrecked))
        {
            if (CurrentDriftPoints > 0f) CurrentDriftPoints = 0f;
            IsDrifting = false;
            return;
        }

        IsDrifting = playerCar.IsSkidding && playerCar.GetSpeedMs() > minSpeedMs;
        if (IsDrifting)
        {
            float speedNorm = Mathf.Clamp01(playerCar.GetSpeedMs() / Mathf.Max(1f, playerCar.maxSpeed));
            CurrentDriftPoints += pointsPerSecondAtFullSpeed * speedNorm * Time.deltaTime;
            straightTimer = 0f;
        }
        else if (CurrentDriftPoints > 0f)
        {
            straightTimer += Time.deltaTime;
            if (straightTimer >= bankDelay) BankPoints();
        }
    }

    void BankPoints()
    {
        int points = Mathf.RoundToInt(CurrentDriftPoints);
        CurrentDriftPoints = 0f;
        straightTimer = 0f;
        if (points <= 0) return;
        int money = points / moneyPerPoints;
        TotalDriftPoints += points;
        TotalDriftMoney += money;
        PlayerPrefs.SetInt("TotalDriftPoints", TotalDriftPoints);
        PlayerPrefs.SetInt("TotalDriftMoney", TotalDriftMoney);
        PlayerPrefs.Save();
        if (money > 0 && GameManager.Instance != null)
            GameManager.Instance.AddMoney(money);
        if (hud != null)
            hud.ShowMessage("Drift +" + points + " pts  (+$" + money + ")", 3f);
    }
}

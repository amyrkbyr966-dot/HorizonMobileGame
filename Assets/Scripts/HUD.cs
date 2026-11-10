using UnityEngine;
using TMPro;

/// <summary>Heads-up display: speed, lap / checkpoint, race time, money.
/// Wired to the TMP_Text objects on the HUD canvas.</summary>
public class HUD : MonoBehaviour
{
    public TMP_Text speedText;
    public TMP_Text lapText;
    public TMP_Text timeText;
    public TMP_Text moneyText;
    public TMP_Text messageText;
    public TMP_Text coinsText;
    public TMP_Text healthText;
    public TMP_Text clockText;
    public TMP_Text modeText;
    public TMP_Text driftText;
    public TMP_Text weatherText;
    public TMP_Text achievementText;
    public GameObject resultsPanel;
    /// <summary>UI phase (2026-11-10): speed-dial needle, HP bar fill,
    /// day/night clock icon, achievement toast icon. Wired in the HUD
    /// prefab; looked up by name when unwired (same pattern as the
    /// text fields). Null-safe throughout.</summary>
    public UnityEngine.UI.Image dialNeedle;
    public UnityEngine.UI.Image healthBarFill;
    public UnityEngine.UI.Image clockIcon;
    public UnityEngine.UI.Image achievementIcon;
    public Sprite sunIcon;
    public Sprite moonIcon;
    bool lastNight;
    // Results-panel title/info text (children of ResultsPanel in the
    // HUD prefab: "ResultsTitle" / "ResultsInfo"). They are set from
    // code so the panel says what actually happened — see
    // ShowResultsPanel below.
    public TMP_Text resultsTitleText;
    public TMP_Text resultsInfoText;

    public CarController playerCar;
    public RaceManager raceManager;
    public GameManager gameManager;
    public CarDamage carDamage;
    public CollectibleManager collectibleManager;
    public TimeTrialManager timeTrialManager;
    public DayNightCycle dayNightCycle;
    public DriftScore driftScore;
    public WeatherSystem weatherSystem;
    public QualityScaler qualityScaler;
    public FreeRoamManager freeRoamManager;

    float achievementTimer;
    float refindTimer;
    bool qualityHintShown;

    void Start()
    {
        if (playerCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) playerCar = p.GetComponent<CarController>();
        }
        if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
        if (gameManager == null) gameManager = GameManager.Instance;
        if (carDamage == null) carDamage = FindFirstObjectByType<CarDamage>();
        if (collectibleManager == null) collectibleManager = FindFirstObjectByType<CollectibleManager>();
        if (timeTrialManager == null) timeTrialManager = FindFirstObjectByType<TimeTrialManager>();
        if (dayNightCycle == null) dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        if (driftScore == null) driftScore = FindFirstObjectByType<DriftScore>();
        if (weatherSystem == null) weatherSystem = FindFirstObjectByType<WeatherSystem>();
        if (qualityScaler == null) qualityScaler = FindFirstObjectByType<QualityScaler>();
        if (freeRoamManager == null) freeRoamManager = FindFirstObjectByType<FreeRoamManager>();
        // Round-3 bug #4/#6 root cause: these four text fields were
        // null in the scene (never wired in the HUD prefab) and Start()
        // never looked them up, so the coins counter, HP, clock and mode
        // showed only their static scene placeholder text ("Coins 0 / 12",
        // "06:00 Day") forever. Look them up by name like the others.
        if (coinsText == null) coinsText = FindText("CoinsText");
        if (healthText == null) healthText = FindText("HealthText");
        if (clockText == null) clockText = FindText("ClockText");
        if (modeText == null) modeText = FindText("ModeText");
        if (weatherText == null) weatherText = FindText("WeatherText");
        if (achievementText == null) achievementText = FindText("AchievementText");
        if (driftText == null) driftText = FindText("DriftText");
        if (dialNeedle == null) dialNeedle = FindImage("DialNeedle");
        if (healthBarFill == null) healthBarFill = FindImage("HealthBarFill");
        if (clockIcon == null) clockIcon = FindImage("ClockIcon");
        // The achievement icon starts INACTIVE in the prefab, so
        // GameObject.Find cannot see it — look under AchievementText
        // (transform.Find sees inactive children).
        if (achievementIcon == null && achievementText != null)
        {
            var t = achievementText.transform.Find("AchievementIcon");
            if (t != null) achievementIcon = t.GetComponent<UnityEngine.UI.Image>();
        }
        // Results title/info live under ResultsPanel, which is
        // INACTIVE at Start — GameObject.Find cannot see inactive
        // objects, so the plain FindText lookup returns null and the
        // panel keeps its static prefab text. Search the panel's own
        // children (including inactive ones) instead.
        if (resultsPanel != null)
        {
            if (resultsTitleText == null) resultsTitleText = FindTextIn(resultsPanel, "ResultsTitle");
            if (resultsInfoText == null) resultsInfoText = FindTextIn(resultsPanel, "ResultsInfo");
        }
        if (speedText != null)
        {
            // Round-3 bug #7: a huge speed value wrapped onto two lines
            // and overlapped the DRIFT line. Never wrap the speed.
            speedText.textWrappingMode = TextWrappingModes.NoWrap;
        }
        // Removed features (2026-11-07): the nitro / wanted HUD objects
        // were deleted from HUD.prefab by the UI pass (2026-11-10), so
        // the runtime hiding is no longer needed.
    }

    UnityEngine.UI.Image FindImage(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<UnityEngine.UI.Image>() : null;
    }

    TMP_Text FindText(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    /// <summary>Find a TMP_Text by name under a parent, including
    /// INACTIVE children (GameObject.Find misses those). Used for the
    /// results-panel texts, whose panel starts hidden.</summary>
    TMP_Text FindTextIn(GameObject parent, string objectName)
    {
        var texts = parent.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
            if (t.gameObject.name == objectName) return t;
        return null;
    }

    void Update()
    {
        // Re-find any manager that was missing at Start (init-order
        // safety): cheap, at most once a second, only while null.
        refindTimer -= Time.deltaTime;
        if (refindTimer <= 0f)
        {
            refindTimer = 1f;
            if (playerCar == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerCar = p.GetComponent<CarController>();
            }
            if (gameManager == null) gameManager = GameManager.Instance;
            if (raceManager == null) raceManager = FindFirstObjectByType<RaceManager>();
            if (collectibleManager == null) collectibleManager = FindFirstObjectByType<CollectibleManager>();
            if (carDamage == null) carDamage = FindFirstObjectByType<CarDamage>();
            if (dayNightCycle == null) dayNightCycle = FindFirstObjectByType<DayNightCycle>();
            if (driftScore == null) driftScore = FindFirstObjectByType<DriftScore>();
            if (weatherSystem == null) weatherSystem = FindFirstObjectByType<WeatherSystem>();
            if (timeTrialManager == null) timeTrialManager = FindFirstObjectByType<TimeTrialManager>();
            if (freeRoamManager == null) freeRoamManager = FindFirstObjectByType<FreeRoamManager>();
            if (coinsText == null) coinsText = FindText("CoinsText");
            if (healthText == null) healthText = FindText("HealthText");
            if (clockText == null) clockText = FindText("ClockText");
            if (modeText == null) modeText = FindText("ModeText");
            if (resultsPanel != null)
            {
                if (resultsTitleText == null) resultsTitleText = FindTextIn(resultsPanel, "ResultsTitle");
                if (resultsInfoText == null) resultsInfoText = FindTextIn(resultsPanel, "ResultsInfo");
            }
        }
        if (playerCar != null && speedText != null)
        {
            // Round-3 bug #7: clamp the displayed speed to 0..999 km/h
            // (and guard NaN) so the text can never wrap or overflow.
            float kmh = playerCar.GetSpeedKmh();
            if (float.IsNaN(kmh) || float.IsInfinity(kmh)) kmh = 0f;
            speedText.text = Mathf.RoundToInt(Mathf.Clamp(kmh, 0f, 999f)) + " km/h";
        }
        // UI phase (2026-11-10): speed-dial needle — 0 km/h at -120°
        // (down-left), 360 km/h at +120° (down-right), 180 km/h up.
        if (dialNeedle != null && playerCar != null)
        {
            float kmh = playerCar.GetSpeedKmh();
            if (float.IsNaN(kmh) || float.IsInfinity(kmh)) kmh = 0f;
            float angle = -120f + (Mathf.Clamp(kmh, 0f, 360f) / 360f) * 240f;
            dialNeedle.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
        }
        // Tokyo Phase A: in free-roam the race lap line is meaningless —
        // show the district / discovered-count / coins line instead.
        // NEVER show the race line in free-roam: the old code fell
        // through to raceManager whenever freeRoamManager was null
        // (first frames, refind gap), and raceManager is ALWAYS present
        // in Main (it sits on the GameManager object), so any gap leaked
        // "Lap 1 / 3 - CP 1/6" into free-roam (seen in the MainFirst
        // roam screenshots). The race line is shown only for an actual
        // race mode; every other mode gets a blank line, never a race
        // line — including a missing gameManager (unknown mode).
        if (lapText != null)
        {
            if (gameManager != null && gameManager.Mode == GameManager.GameMode.FreeRoam)
            {
                lapText.text = freeRoamManager != null ? freeRoamManager.StatusLine() : "";
            }
            else if (gameManager != null && IsRaceMode(gameManager.Mode) && raceManager != null)
            {
                lapText.text = raceManager.RaceFinished
                    ? "FINISH"
                    : "Lap " + raceManager.CurrentLap + " / " + raceManager.totalLaps
                      + "  -  CP " + (raceManager.NextCheckpoint + 1) + "/"
                      + Mathf.Max(1, raceManager.checkpoints.Count);
            }
            else
            {
                lapText.text = "";
            }
        }
        if (timeText != null && gameManager != null)
            timeText.text = FormatTime(gameManager.ElapsedTime);
        if (moneyText != null && gameManager != null)
            moneyText.text = "$ " + gameManager.Money;
        if (coinsText != null && collectibleManager != null)
            coinsText.text = "Coins " + collectibleManager.CollectedCount + " / " + collectibleManager.TotalCount;
        if (healthText != null && carDamage != null)
            healthText.text = "HP " + Mathf.RoundToInt(carDamage.Health) + "%";
        if (healthBarFill != null && carDamage != null)
            healthBarFill.fillAmount = Mathf.Clamp01(carDamage.Health / 100f);
        if (clockText != null && dayNightCycle != null)
            clockText.text = dayNightCycle.ClockText + (dayNightCycle.IsNight ? "  Night" : "  Day");
        if (clockIcon != null && dayNightCycle != null && dayNightCycle.IsNight != lastNight)
        {
            lastNight = dayNightCycle.IsNight;
            Sprite s = lastNight ? moonIcon : sunIcon;
            if (s != null) clockIcon.sprite = s;
        }
        if (driftText != null && driftScore != null)
            driftText.text = driftScore.IsDrifting || driftScore.CurrentDriftPoints > 0f
                ? "Drift " + Mathf.RoundToInt(driftScore.CurrentDriftPoints) + " pts"
                : "Drift total " + driftScore.TotalDriftPoints + " pts";
        if (weatherText != null && weatherSystem != null)
            weatherText.text = weatherSystem.WeatherLabel;
        if (achievementText != null && achievementTimer > 0f)
        {
            achievementTimer -= Time.deltaTime;
            if (achievementTimer <= 0f)
            {
                achievementText.text = "";
                if (achievementIcon != null) achievementIcon.gameObject.SetActive(false);
            }
        }
        if (modeText != null && gameManager != null)
        {
            string mode = gameManager.Mode.ToString();
            if (timeTrialManager != null && gameManager.Mode == GameManager.GameMode.TimeTrial && !timeTrialManager.Finished)
                mode += "  " + TimeTrialManager.Format(timeTrialManager.Elapsed) + " / " + TimeTrialManager.Format(timeTrialManager.targetTime);
            // Tokyo Phase A: show the current season next to the mode in free-roam.
            if (gameManager.Mode == GameManager.GameMode.FreeRoam && SeasonManager.Instance != null)
                mode += "  -  " + SeasonManager.Instance.SeasonLabel;
            modeText.text = mode;
        }
        // Results panel: show the outcome that actually happened (see
        // ShowResultsPanel). The toast still carries the game-over
        // reason ("WRECKED!") for a few seconds.
        if (gameManager != null && gameManager.IsGameOver && resultsPanel != null && !resultsPanel.activeSelf)
            ShowMessage(gameManager.GameOverReason, 5f);
        ShowResultsPanel();
        // One-time hint when the adaptive scaler had to step down from a
        // manually chosen Ultra twice: the device cannot hold Ultra.
        if (!qualityHintShown && qualityScaler != null && qualityScaler.SuggestLowerQuality)
        {
            qualityHintShown = true;
            ShowMessage("Tip: Ultra is heavy on this device - try High in Settings", 6f);
        }
    }

    /// <summary>Modes that run a lap / checkpoint race (RaceManager).
    /// TimeTrial has its own timer line in modeText, Delivery and
    /// Collectibles have no laps — none of them ever shows the race
    /// line (see the lapText block in Update).</summary>
    static bool IsRaceMode(GameManager.GameMode mode)
    {
        return mode == GameManager.GameMode.Race
            || mode == GameManager.GameMode.StreetRace
            || mode == GameManager.GameMode.Championship;
    }

    /// <summary>Achievement toast: shows in the gold centre text for 4 s.</summary>
    public void ShowAchievement(string text)
    {
        if (achievementText == null) { ShowMessage(text, 4f); return; }
        achievementText.text = text;
        if (achievementIcon != null) achievementIcon.gameObject.SetActive(true);
        achievementTimer = 4f;
    }

    public void ShowMessage(string msg, float seconds = 3f)
    {
        if (messageText == null) return;
        messageText.text = msg;
        CancelInvoke(nameof(ClearMessage));
        Invoke(nameof(ClearMessage), seconds);
    }

    /// <summary>Results panel (fixed 2026-11-07, Phase A playtest):
    /// the panel's title/info are STATIC text in the HUD prefab
    /// ("RACE FINISHED! / Reward added to your money."), so when a run
    /// ended by wrecking (HP 0%, GameOver "WRECKED!") the panel still
    /// claimed a finished race and a reward — even though no
    /// checkpoint was passed, no race was running, and no reward is
    /// paid (the reward is only added in RaceManager.FinishRace).
    /// Set the title/info from the real outcome: a finished race keeps
    /// "RACE FINISHED!" + reward, a wreck says "WRECKED!" and pays
    /// nothing. Race-finished wins if both are somehow true.</summary>
    void ShowResultsPanel()
    {
        if (resultsPanel == null || resultsPanel.activeSelf) return;
        bool finished = raceManager != null && raceManager.RaceFinished;
        bool wrecked = gameManager != null && gameManager.IsGameOver;
        if (!finished && !wrecked) return;
        resultsPanel.SetActive(true);
        if (resultsTitleText != null)
            resultsTitleText.text = finished ? "RACE FINISHED!" : "WRECKED!";
        if (resultsInfoText != null)
            resultsInfoText.text = finished
                ? "Reward added to your money."
                : "Your car is wrecked. No reward.";
    }

    void ClearMessage()
    {
        if (messageText != null) messageText.text = "";
    }

    static string FormatTime(float t)
    {
        int m = Mathf.FloorToInt(t / 60f);
        int s = Mathf.FloorToInt(t % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}

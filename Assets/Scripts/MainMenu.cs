using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>Main menu: shows saved money, starts a race / time trial /
/// collectible hunt / free roam, quits the game, and lets the player
/// pick one of the three cars. Car selection is saved in PlayerPrefs
/// (CarSelection) and applied to the player car when Main loads.
/// Buttons are wired in the MainMenu scene; the text fields are updated
/// here so the menu works even if a button was wired by MenuButton.</summary>
public class MainMenu : MonoBehaviour
{
    public TMP_Text moneyText;
    public TMP_Text titleText;
    public TMP_Text carNameText;
    public TMP_Text carStatsText;
    public TMP_Text carPriceText;
    /// <summary>Optional credits line (wire in the MainMenu scene when
    /// the credits text exists; null-safe — the attribution is also in
    /// PlateauMap.CreditsLine). Shows the PLATEAU map attribution
    /// required by the CC-BY 4.0 licence.</summary>
    public TMP_Text creditsText;

    TMP_Text qualityLabel;

    // Button labels (for the EN/FA language switch — found by name in
    // Start, like HUD.cs does for its unwired fields).
    TMP_Text raceLabel;
    TMP_Text freeRoamLabel;
    TMP_Text timeTrialLabel;
    TMP_Text collectiblesLabel;
    TMP_Text streetRaceLabel;
    TMP_Text deliveryLabel;
    TMP_Text championshipLabel;
    TMP_Text garageLabel;
    TMP_Text quitLabel;
    UnityEngine.UI.Image ownedStateIcon;
    GarageUpgradesUI garageUI;

    /// <summary>Owned/locked icons next to the car price, wired in the
    /// prefab (check-mark / padlock).</summary>
    public Sprite ownedIconSprite;
    public Sprite lockedIconSprite;

    /// <summary>UI asset attribution (ui-assets-manifest §4c).</summary>
    const string UiCreditsLine = "UI icons: game-icons.net — Delapouite, Lorc, Sbed, Carl Olsen, Guard13007 (CC BY 3.0). Menu photos: \"Shibuya Crossing, Aerial\" © David Kernan (CC BY 4.0); \"Shibuya crossing at night, Tokyo, Japan\" © Joli Rumi (CC BY-SA 4.0); \"Shibuya Crossing Tokyo At Night\" (CC BY 3.0). UI kit: Kenney (CC0). Fonts: Orbitron, Rajdhani, Vazirmatn (SIL OFL 1.1).";

    int previewIndex;

    void Start()
    {
        previewIndex = CarSelection.SelectedIndex;
        // The QUALITY button label shows the current tier + resolution
        // (e.g. "QUALITY: Medium 720p") so the 4 tiers are visible.
        var ql = GameObject.Find("SettingsQualityButtonLabel");
        if (ql != null) qualityLabel = ql.GetComponent<TMP_Text>();
        // UI phase (2026-11-10): the car card texts + credits text were
        // never wired in the MainMenu scene (fileID 0), so RefreshMenu()
        // silently did nothing for them. Look them up by name like
        // HUD.cs does for its unwired fields.
        if (carNameText == null) carNameText = FindText("CarNameText");
        if (carStatsText == null) carStatsText = FindText("CarStatsText");
        if (carPriceText == null) carPriceText = FindText("CarPriceText");
        if (creditsText == null) creditsText = FindText("CreditsText");
        // Button labels (for the EN/FA language switch).
        raceLabel = FindText("RaceButtonLabel");
        freeRoamLabel = FindText("FreeRoamButtonLabel");
        timeTrialLabel = FindText("TimeTrialButtonLabel");
        collectiblesLabel = FindText("CollectiblesButtonLabel");
        streetRaceLabel = FindText("StreetRaceButtonLabel");
        deliveryLabel = FindText("DeliveryButtonLabel");
        championshipLabel = FindText("ChampionshipButtonLabel");
        garageLabel = FindText("GarageButtonLabel");
        quitLabel = FindText("QuitButtonLabel");
        // Owned/locked state icon next to the price + garage upgrade slots.
        var os = GameObject.Find("OwnedStateIcon");
        if (os != null) ownedStateIcon = os.GetComponent<UnityEngine.UI.Image>();
        var gp = GameObject.Find("GarageUpgradesPanel");
        if (gp != null) garageUI = gp.GetComponent<GarageUpgradesUI>();
        // Removed feature (2026-11-07): police chase was removed
        // from the game, but the MainMenu scene still ships the
        // POLICE CHASE button. Hide it at runtime (the scene file
        // is never edited by hand). Hiding only removes the button
        // from view/input; no menu code references it.
        var police = GameObject.Find("PoliceChaseButton");
        if (police != null) police.SetActive(false);
        // Credits: PLATEAU map attribution (CC-BY 4.0, required) +
        // the UI asset attribution (ui-assets-manifest §4c).
        if (creditsText != null)
            creditsText.text = PlateauMap.CreditsLine + "\n" + UiCreditsLine;
        RefreshMenu();
    }

    static TMP_Text FindText(string objectName)
    {
        var go = GameObject.Find(objectName);
        return go != null ? go.GetComponent<TMP_Text>() : null;
    }

    void RefreshMenu()
    {
        if (moneyText != null)
            moneyText.text = "$ " + PlayerPrefs.GetInt("Money", 1000);
        if (qualityLabel != null)
            qualityLabel.text = "QUALITY: " + SettingsManager.QualityName();
        CarDef def = CarSelection.Cars[previewIndex];
        if (carNameText != null) carNameText.text = def.carName;
        if (carStatsText != null)
        {
            // FIX (2026-11-11, semi-final): the stats string (43 chars)
            // wrapped to a second line ("x1.12") that overlapped the
            // SELECTED/price text below it — the stats box is 420 px at
            // fontSize 26 (~35 chars/line). Compact one-line format +
            // fontSize 21 so it always fits on one line.
            carStatsText.text = Mathf.RoundToInt(def.topSpeedKmh) + " km/h  Accel x" + def.acceleration.ToString("0.00")
                + "  Handling x" + def.handling.ToString("0.00");
            carStatsText.fontSize = 21;
        }
        if (carPriceText != null)
        {
            if (CarSelection.IsUnlocked(previewIndex))
                carPriceText.text = previewIndex == CarSelection.SelectedIndex ? "SELECTED" : "Owned - tap Select";
            else
                carPriceText.text = "Price: $" + def.price;
        }
        if (ownedStateIcon != null && ownedIconSprite != null && lockedIconSprite != null)
        {
            bool unlocked = CarSelection.IsUnlocked(previewIndex);
            ownedStateIcon.sprite = unlocked ? ownedIconSprite : lockedIconSprite;
            ownedStateIcon.color = unlocked
                ? new Color(0.3f, 0.85f, 0.4f, 1f)   // owned: green check
                : new Color(1f, 0.55f, 0.3f, 1f);  // locked: orange padlock
        }
        if (garageUI != null) garageUI.Refresh(previewIndex);
        // Language switch: in Persian mode the button labels use the
        // shaped FA strings; in English mode the prefab's own labels
        // ("START RACE", ...) stay untouched.
        SetLabel(raceLabel, "Race");
        SetLabel(freeRoamLabel, "FreeRoam");
        SetLabel(timeTrialLabel, "TimeTrial");
        SetLabel(collectiblesLabel, "Coins");
        SetLabel(streetRaceLabel, "StreetRace");
        SetLabel(deliveryLabel, "Delivery");
        SetLabel(championshipLabel, "Championship");
        SetLabel(garageLabel, "Garage");
        SetLabel(quitLabel, "Quit");
    }

    /// <summary>Set a button label to its Persian form when FA is
    /// active (shaped for TMP via PersianTextShaper); English mode
    /// keeps the prefab's label.</summary>
    static void SetLabel(TMP_Text label, string key)
    {
        if (label == null || SettingsManager.Language != 1) return;
        string text = SettingsManager.GetLabel(key);
        label.text = PersianTextShaper.ContainsArabic(text) ? PersianTextShaper.Shape(text) : text;
    }

    public void OnNextCar()
    {
        // Early access: only Turbo Falcon (#5) exists — no cycling.
        if (CarSelection.EarlyAccessSingleCar) previewIndex = CarSelection.EarlyAccessCarIndex;
        else previewIndex = (previewIndex + 1) % CarSelection.Cars.Length;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
    }

    public void OnPrevCar()
    {
        // Early access: only Turbo Falcon (#5) exists — no cycling.
        if (CarSelection.EarlyAccessSingleCar) previewIndex = CarSelection.EarlyAccessCarIndex;
        else previewIndex = (previewIndex + CarSelection.Cars.Length - 1) % CarSelection.Cars.Length;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
    }

    public void OnBuyCar()
    {
        if (CarSelection.TryBuy(previewIndex))
        {
            CarSelection.SelectedIndex = previewIndex;
            RefreshMenu();
        }
    }

    public void OnSelectCar()
    {
        if (!CarSelection.IsUnlocked(previewIndex)) return;
        CarSelection.SelectedIndex = previewIndex;
        RefreshMenu();
    }

    public void OnStartRace()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartRace();
        else SceneManager.LoadScene("Main");
    }

    public void OnFreeRoam()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartFreeRoam();
        else SceneManager.LoadScene("Main");
    }

    public void OnTimeTrial()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartTimeTrial();
        else SceneManager.LoadScene("Main");
    }

    public void OnCollectibles()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartCollectibles();
        else SceneManager.LoadScene("Main");
    }

    public void OnStreetRace()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartStreetRace();
        else SceneManager.LoadScene("Main");
    }

    public void OnDelivery()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartDelivery();
        else SceneManager.LoadScene("Main");
    }

    public void OnChampionship()
    {
        if (GameManager.Instance != null) GameManager.Instance.StartChampionship();
        else SceneManager.LoadScene("Main");
    }

    /// <summary>Garage: the car selector on this menu IS the garage -
    /// pick a car, buy it, and apply upgrades from the same screen.
    /// This button just refreshes the view and plays a click.</summary>
    public void OnGarage()
    {
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
    }

    public void OnSettingsQuality()
    {
        SettingsManager.CycleQuality();
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
    }

    public void OnSettingsLanguage()
    {
        SettingsManager.ToggleLanguage();
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
    }

    public void OnQuit()
    {
        Application.Quit();
    }
}

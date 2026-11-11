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

    // Car preview (3D turntable) — semi-final 2026-11-11. The preview
    // area between the < > arrows was an empty grey box; this renders
    // the selected car live on a small stage far above the menu diorama
    // (so the preview camera sees only the car) into a RenderTexture
    // shown in a RawImage between the arrows.
    RenderTexture previewRT;
    Camera previewCam;
    GameObject previewCar;
    UnityEngine.UI.RawImage previewImage;
    float previewAngle;
    const float PreviewStageY = 1000f;

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
        // Semi-final (2026-11-11): show the real Shibuya night photo
        // as the menu background instead of the old flat 3D diorama.
        SetupMenuBackground();
        // Semi-final (2026-11-11): live 3D car preview between the
        // < > arrows (was an empty grey box).
        SetupCarPreview();
        RefreshMenu();
    }

    /// <summary>Live 3D car preview: a small stage far above the menu
    /// diorama, a dedicated camera rendering only the car into a
    /// RenderTexture, and a RawImage between the Prev/Next arrow
    /// buttons showing it. The stage floats at y=1000 so the preview
    /// camera (far plane 100) never sees the diorama below. If the car
    /// prefab fails to load, the RawImage is hidden (no worse than the
    /// old empty box).</summary>
    void SetupCarPreview()
    {
        var canvasObj = GameObject.Find("MenuCanvas");
        if (canvasObj == null) return;
        var prevBtn = GameObject.Find("PrevCarButton");
        var nextBtn = GameObject.Find("NextCarButton");
        if (prevBtn == null || nextBtn == null) return;
        var prevRT = prevBtn.GetComponent<RectTransform>();
        var nextRT = nextBtn.GetComponent<RectTransform>();
        if (prevRT == null || nextRT == null) return;
        // RawImage centred in the gap between the two arrow buttons,
        // sized to the gap (computed from the buttons at runtime, so it
        // adapts to the canvas scaling).
        float midX = (prevRT.anchoredPosition.x + nextRT.anchoredPosition.x) * 0.5f;
        float midY = (prevRT.anchoredPosition.y + nextRT.anchoredPosition.y) * 0.5f;
        float gapW = Mathf.Abs(nextRT.anchoredPosition.x - prevRT.anchoredPosition.x) - prevRT.sizeDelta.x;
        float gapH = prevRT.sizeDelta.y;
        if (gapW < 40f) gapW = 40f;
        if (GameObject.Find("CarPreviewImage") != null) return;
        var go = new GameObject("CarPreviewImage");
        go.transform.SetParent(canvasObj.transform, false);
        previewImage = go.AddComponent<UnityEngine.UI.RawImage>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(midX, midY);
        rt.sizeDelta = new Vector2(gapW, gapH);
        // RenderTexture + preview camera + light on the high stage.
        previewRT = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        previewRT.Create();
        previewImage.texture = previewRT;
        var camGo = new GameObject("CarPreviewCamera");
        previewCam = camGo.AddComponent<Camera>();
        previewCam.targetTexture = previewRT;
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0.07f, 0.07f, 0.09f, 1f);
        previewCam.fieldOfView = 40f;
        previewCam.nearClipPlane = 0.1f;
        previewCam.farClipPlane = 100f;
        previewCam.transform.position = new Vector3(5.5f, PreviewStageY + 1.6f, -7.5f);
        previewCam.transform.LookAt(new Vector3(0f, PreviewStageY + 0.6f, 0f));
        var lightGo = new GameObject("CarPreviewLight");
        var pl = lightGo.AddComponent<Light>();
        pl.type = LightType.Directional;
        pl.intensity = 0.9f;
        pl.transform.position = new Vector3(0f, PreviewStageY + 10f, 0f);
        pl.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RefreshCarPreview();
    }

    /// <summary>Swap the preview car to the currently selected one
    /// (called on Start and whenever the user cycles cars). Normalises
    /// the model to ~4.5 m long and grounds it on the preview stage.</summary>
    void RefreshCarPreview()
    {
        if (previewCam == null) return;
        if (previewCar != null) { Destroy(previewCar); previewCar = null; }
        bool detailed;
        GameObject prefab = CarModelMap.Resolve(previewIndex, out detailed);
        if (prefab == null)
        {
            if (previewImage != null) previewImage.gameObject.SetActive(false);
            return;
        }
        previewCar = Instantiate(prefab);
        previewCar.transform.position = new Vector3(0f, PreviewStageY, 0f);
        previewCar.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
        var renderers = previewCar.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (longest > 0.01f && (longest < 3f || longest > 6f))
            {
                float s = 4.5f / longest;
                previewCar.transform.localScale = Vector3.one * s;
                b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
            }
            // Ground it: lift so the lowest point sits on the stage.
            previewCar.transform.position = new Vector3(0f,
                previewCar.transform.position.y + (PreviewStageY - b.min.y), 0f);
        }
        if (previewImage != null) previewImage.gameObject.SetActive(true);
    }

    void Update()
    {
        // Turntable: slow rotation so the car is seen from all sides.
        if (previewCar != null)
        {
            previewAngle += Time.deltaTime * 15f;
            previewCar.transform.rotation = Quaternion.Euler(0f, 30f + previewAngle, 0f);
        }
    }

    void OnDestroy()
    {
        if (previewRT != null) { previewRT.Release(); previewRT = null; }
    }

    /// <summary>Menu background: show the real Shibuya night photo
    /// full-screen behind the menu UI, replacing the old flat 3D diorama
    /// (blue sky, flat green ground, grey slab buildings). The photo is
    /// loaded from Resources (copied there from Assets/UI/Art/Menu) and
    /// stretched to fill the screen as the first child of MenuCanvas, so
    /// it sits behind every button and text. If the photo fails to load,
    /// the old diorama stays (no crash).</summary>
    void SetupMenuBackground()
    {
        var canvasObj = GameObject.Find("MenuCanvas");
        if (canvasObj == null) return;
        var tex = Resources.Load<Texture2D>("UI/Menu/shibuya-crossing-night-1920");
        if (tex == null)
        {
            Debug.LogWarning("[MainMenu] menu background photo not found in Resources/UI/Menu — keeping the 3D diorama.");
            return;
        }
        // Don't add it twice (e.g. if Start runs again after a scene reload).
        if (GameObject.Find("MenuBackgroundPhoto") != null) return;
        var go = new GameObject("MenuBackgroundPhoto");
        go.transform.SetParent(canvasObj.transform, false);
        go.transform.SetSiblingIndex(0);
        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        img.preserveAspect = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        RefreshCarPreview();
    }

    public void OnPrevCar()
    {
        // Early access: only Turbo Falcon (#5) exists — no cycling.
        if (CarSelection.EarlyAccessSingleCar) previewIndex = CarSelection.EarlyAccessCarIndex;
        else previewIndex = (previewIndex + CarSelection.Cars.Length - 1) % CarSelection.Cars.Length;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayClick();
        RefreshMenu();
        RefreshCarPreview();
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

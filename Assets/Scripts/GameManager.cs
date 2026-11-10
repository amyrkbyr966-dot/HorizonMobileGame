using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Session state: money, timer, pause, scene flow. Persists across
/// scene loads (DontDestroyOnLoad) and saves money in PlayerPrefs.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameMode { Race, FreeRoam, TimeTrial, Collectibles, StreetRace, Delivery, Championship }

    public int Money { get; private set; }
    public float ElapsedTime { get; private set; }
    public bool IsPaused { get; private set; }
    public bool IsRacing { get; private set; }
    public bool IsGameOver { get; private set; }
    public string GameOverReason { get; private set; }
    public GameMode Mode { get; private set; } = GameMode.Race;

    /// <summary>Async scene loading (UI phase, 2026-11-10): the menu
    /// shows a progress bar while Main loads instead of a frozen
    /// black screen. Read by LoadingPanelUI; set by LoadMainAsync.</summary>
    public static bool IsLoading { get; private set; }
    public static float LoadProgress { get; private set; }

    public int startingMoney = 1000;

    CarController profileCar;
    Vector3 profileLastPos;
    float profileTimer;

    /// <summary>Pause panel (Main scene HUD, "PausePanel"). It starts
    /// INACTIVE and no script ever showed it (2026-11-08 menu fix), so
    /// pausing froze the game with no way back to the menu. Synced to
    /// IsPaused on every scene load / pause toggle. Found by name among
    /// INACTIVE objects — GameObject.Find cannot see inactive objects.</summary>
    GameObject pausePanel;

    /// <summary>True once a mode was started explicitly (StartRace,
    /// StartFreeRoam, ...). A direct Main-scene launch never calls one,
    /// so the race timer stayed frozen at 00:00 (round-3 bug #3) and the
    /// mode stayed Race with IsRacing false. AutoStartIfNeeded starts a
    /// race session in that case, so normal play always has a running
    /// timer and live lap / checkpoint tracking.</summary>
    bool sessionStarted;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SaveManager.Load();
        Money = PlayerPrefs.GetInt("Money", startingMoney);
        SettingsManager.ApplyAll();
        VisualBootstrap.Init();
        Application.targetFrameRate = SettingsManager.Quality == 0 ? 30 : 60;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        AutoStartIfNeeded();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pausePanel = null;
        FindPausePanel();
        SyncPausePanel();
        if (scene.name == "Main") AutoStartIfNeeded();
    }

    void FindPausePanel()
    {
        var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var t in all)
            if (t.name == "PausePanel") { pausePanel = t.gameObject; return; }
    }

    void SyncPausePanel()
    {
        if (pausePanel != null) pausePanel.SetActive(IsPaused);
    }

    void AutoStartIfNeeded()
    {
        if (sessionStarted) return;
        if (SceneManager.GetActiveScene().name != "Main") return;
        sessionStarted = true;
        Mode = GameMode.Race;
        IsRacing = true;
        ElapsedTime = 0f;
    }

    void Update()
    {
        // The timer runs in every Main-scene mode (race, free roam,
        // collectibles, delivery), not only while IsRacing — it is the
        // session clock the HUD shows (round-3 bug #3).
        if (!IsPaused && !IsGameOver && SceneManager.GetActiveScene().name == "Main")
            ElapsedTime += Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
        // Career stats: top speed + distance driven, once a second.
        profileTimer -= Time.deltaTime;
        if (profileTimer <= 0f)
        {
            profileTimer = 1f;
            if (profileCar == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    profileCar = p.GetComponent<CarController>();
                    if (profileCar != null) profileLastPos = profileCar.transform.position;
                }
            }
            else if (!IsPaused)
            {
                PlayerProfile.RecordTopSpeed(profileCar.GetSpeedKmh());
                float moved = Vector3.Distance(profileCar.transform.position, profileLastPos);
                if (moved > 0f && moved < 200f) PlayerProfile.RecordDistance(moved / 1000f);
                profileLastPos = profileCar.transform.position;
            }
        }
    }

    public void StartFreeRoam()
    {
        sessionStarted = true;
        Mode = GameMode.FreeRoam;
        IsRacing = false;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartRace()
    {
        sessionStarted = true;
        Mode = GameMode.Race;
        IsRacing = true;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartTimeTrial()
    {
        sessionStarted = true;
        Mode = GameMode.TimeTrial;
        IsRacing = true;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartCollectibles()
    {
        sessionStarted = true;
        Mode = GameMode.Collectibles;
        IsRacing = false;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartStreetRace()
    {
        sessionStarted = true;
        Mode = GameMode.StreetRace;
        IsRacing = true;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartDelivery()
    {
        sessionStarted = true;
        Mode = GameMode.Delivery;
        IsRacing = false;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    public void StartChampionship()
    {
        sessionStarted = true;
        Mode = GameMode.Championship;
        IsRacing = true;
        ElapsedTime = 0f;
        StartCoroutine(LoadMainAsync());
    }

    /// <summary>Load the Main scene asynchronously so the menu can
    /// show a progress bar while it loads (UI phase, 2026-11-10).
    /// The menu scene stays visible until the load is ~90% done, then
    /// the new scene activates. GameManager survives the load
    /// (DontDestroyOnLoad), so this coroutine is not interrupted.</summary>
    IEnumerator LoadMainAsync()
    {
        IsLoading = true;
        LoadProgress = 0f;
        AsyncOperation op = SceneManager.LoadSceneAsync("Main");
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            LoadProgress = op.progress / 0.9f;
            yield return null;
        }
        LoadProgress = 1f;
        yield return null; // let the panel render 100% for one frame
        op.allowSceneActivation = true;
        IsLoading = false;
    }

    /// <summary>End the run (wrecked car). HUD shows the reason.</summary>
    public void GameOver(string reason)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        GameOverReason = reason;
        IsRacing = false;
    }

    /// <summary>Re-read money after CarSelection spends it directly.</summary>
    public void RefreshMoneyFromPrefs()
    {
        Money = PlayerPrefs.GetInt("Money", Money);
    }

    public void AddMoney(int amount)
    {
        Money += amount;
        PlayerPrefs.SetInt("Money", Money);
        PlayerPrefs.Save();
        if (amount > 0) SaveManager.Save();
    }

    public void AddScore(int amount) { AddMoney(amount); }

    public void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        if (pausePanel == null) FindPausePanel();
        SyncPausePanel();
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SyncPausePanel();
    }

    public void RestartRace()
    {
        Resume();
        IsGameOver = false;
        GameOverReason = "";
        ElapsedTime = 0f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToMenu()
    {
        sessionStarted = false;
        Resume();
        SceneManager.LoadScene("MainMenu");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

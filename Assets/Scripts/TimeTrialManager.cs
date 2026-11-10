using UnityEngine;

/// <summary>Time trial: pass all checkpoints in order, once, against the
/// clock. Only active when GameManager.Mode is TimeTrial; in other modes
/// it does nothing. Checkpoint.cs forwards every gate to this manager
/// as well as to RaceManager. Beat targetTime to earn $800 (otherwise
/// $200 for finishing), best time saved in PlayerPrefs.</summary>
public class TimeTrialManager : MonoBehaviour
{
    public static TimeTrialManager Instance { get; private set; }

    public float targetTime = 120f;
    public int NextCheckpoint { get; private set; }
    public int TotalCheckpoints { get; private set; }
    public bool Finished { get; private set; }
    public bool Success { get; private set; }
    public float Elapsed { get; private set; }

    float startTime;
    bool running;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        var cps = GameObject.FindGameObjectsWithTag("Checkpoint");
        TotalCheckpoints = cps.Length;
        if (GameManager.Instance != null && GameManager.Instance.Mode == GameManager.GameMode.TimeTrial)
        {
            running = true;
            startTime = Time.time;
        }
    }

    void Update()
    {
        if (running && !Finished) Elapsed = Time.time - startTime;
    }

    public void OnCheckpointReached(int index)
    {
        if (!running || Finished || index != NextCheckpoint) return;
        NextCheckpoint++;
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCheckpoint();
        if (NextCheckpoint >= TotalCheckpoints) Finish();
    }

    void Finish()
    {
        Finished = true;
        running = false;
        Success = Elapsed <= targetTime;
        int reward = Success ? 800 : 200;
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(reward);
        float best = PlayerPrefs.GetFloat("BestTimeTrial", 0f);
        if (best <= 0f || Elapsed < best)
        {
            PlayerPrefs.SetFloat("BestTimeTrial", Elapsed);
            PlayerPrefs.Save();
        }
        var hud = FindFirstObjectByType<HUD>();
        if (hud != null)
        {
            hud.ShowMessage(Success
                ? "Time Trial BEATEN! " + Format(Elapsed) + "  (+$" + reward + ")"
                : "Time Trial done in " + Format(Elapsed) + " (target " + Format(targetTime) + ")  (+$" + reward + ")", 5f);
        }
    }

    public static string Format(float t)
    {
        int m = Mathf.FloorToInt(t / 60f);
        int s = Mathf.FloorToInt(t % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }
}

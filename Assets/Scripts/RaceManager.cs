using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Lap / checkpoint race logic. Checkpoints must be passed in
/// order (Checkpoint_00, Checkpoint_01, ...). Finishing pays a reward
/// based on total race time.</summary>
public class RaceManager : MonoBehaviour
{
    public List<Transform> checkpoints = new List<Transform>();
    public int totalLaps = 3;

    public int CurrentLap { get; private set; } = 1;
    public int NextCheckpoint { get; private set; } = 0;
    public bool RaceFinished { get; private set; }
    public float BestLapTime { get; private set; }

    float lapStartTime;
    float raceStartTime;

    // Verification telemetry (2026-11-07): the automated race driver
    // cannot steer from the minimap — the dot centroid jitters ~+-6 m
    // and the movement heading is the slide direction, not the facing,
    // so it steered the car into BuildingExtra2_17 every run. Log the
    // TRUE player pose (position, facing yaw, speed) 4x a second so
    // the driver can steer from ground truth. Cheap (~4 lines/s) and
    // harmless in production; strip it if it ever shows up in a
    // profile. CarController.GetSpeedMs is the velocity magnitude.
    CarController telemCar;
    float telemTimer;

    void Update()
    {
        telemTimer -= Time.deltaTime;
        if (telemTimer > 0f) return;
        telemTimer = 0.25f;
        if (telemCar == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) telemCar = p.GetComponent<CarController>();
            if (telemCar == null) return;
        }
        Vector3 pos = telemCar.transform.position;
        float yaw = telemCar.transform.eulerAngles.y;
        string telemLine = string.Format("[TELEM] t={0:0.0} x={1:0.0} z={2:0.0} yaw={3:0.0} speed={4:0.0} cp={5} lap={6}",
            Time.time, pos.x, pos.z, yaw, telemCar.GetSpeedMs(), NextCheckpoint, CurrentLap);
        Debug.Log(telemLine);
        // Also write the latest sample to a file: Player.log is
        // buffered by Unity and arrives in bursts (5-17 s gaps), which
        // is unusable for a live driver. WriteAllText opens/writes/
        // closes each call, so the file is always fresh.
        try { File.WriteAllText(Application.persistentDataPath + "/telem.txt", telemLine); }
        catch (System.Exception) { }
    }

    void Start()
    {
        raceStartTime = Time.time;
        lapStartTime = Time.time;
        if (checkpoints.Count == 0)
        {
            var found = GameObject.FindGameObjectsWithTag("Checkpoint");
            foreach (var go in found) checkpoints.Add(go.transform);
            checkpoints.Sort((a, b) => a.name.CompareTo(b.name));
        }
    }

    public void OnCheckpointReached(int index)
    {
        if (RaceFinished) return;
        if (index != NextCheckpoint) return; // must be in order
        NextCheckpoint++;
        if (NextCheckpoint >= checkpoints.Count)
        {
            NextCheckpoint = 0;
            float lapTime = Time.time - lapStartTime;
            if (BestLapTime <= 0f || lapTime < BestLapTime) BestLapTime = lapTime;
            CurrentLap++;
            lapStartTime = Time.time;
            if (CurrentLap > totalLaps) FinishRace();
        }
    }

    void FinishRace()
    {
        RaceFinished = true;
        int raceSeconds = Mathf.RoundToInt(Time.time - raceStartTime);
        int reward = 500 + Mathf.Max(0, totalLaps * 1000 - raceSeconds);
        if (GameManager.Instance != null) GameManager.Instance.AddMoney(reward);
        // Championship: record the finish so points advance, the season
        // can end and the season reward pays. Before this call the
        // championship never advanced (RecordRaceFinish had no callers)
        // and paid $0. Position is ranked by race progress vs rivals
        // at the finish moment; with no rivals in the scene the player
        // takes 1st (rivals still need to be spawned for championship
        // races — no RivalRacer prefab or scene object exists yet).
        if (GameManager.Instance != null
            && GameManager.Instance.Mode == GameManager.GameMode.Championship
            && Championship.Instance != null)
        {
            Championship.Instance.RecordRaceFinish(GetPlayerPosition());
        }
    }

    /// <summary>Player's finishing position (1-based), ranked by race
    /// progress against rivals at the finish moment. A rival that
    /// already finished, or is further along the course, counts as ahead.</summary>
    int GetPlayerPosition()
    {
        float playerProgress = GetProgress();
        int position = 1;
        foreach (var rival in FindObjectsByType<RivalRacer>(FindObjectsSortMode.None))
        {
            if (rival == null) continue;
            if (rival.Finished || rival.Progress > playerProgress) position++;
        }
        return position;
    }

    /// <summary>Race progress in gates (lap * gates + gate index), for
    /// rivals and championship standings.</summary>
    public float GetProgress()
    {
        return (CurrentLap - 1) * Mathf.Max(1, checkpoints.Count) + NextCheckpoint;
    }

    public float GetLapProgress()
    {
        if (checkpoints.Count == 0) return 0f;
        return (float)NextCheckpoint / checkpoints.Count;
    }

    public float GetCurrentLapTime()
    {
        return Time.time - lapStartTime;
    }
}

using UnityEngine;

/// <summary>First-run driving tutorial. Shows one step at a time in the
/// HUD message area and advances when the player actually does the action.
/// Runs once (PlayerPrefs "TutorialDone"); skipped on later runs.
/// Attach to a GameObject in the Main scene.</summary>
public class TutorialManager : MonoBehaviour
{
    enum Step { Steer, Gas, Brake, Checkpoint, Done }

    Step step = Step.Steer;
    bool active;
    HUD hud;
    float stepTimer;

    void Start()
    {
        if (PlayerPrefs.GetInt("TutorialDone", 0) == 1) { enabled = false; return; }
        active = true;
        hud = FindFirstObjectByType<HUD>();
        ShowStep();
    }

    void Update()
    {
        if (!active) return;
        var controls = MobileControls.Instance;
        var race = FindFirstObjectByType<RaceManager>();
        stepTimer += Time.deltaTime;
        switch (step)
        {
            case Step.Steer:
                if (controls != null && Mathf.Abs(controls.Steer) > 0.2f) Advance();
                break;
            case Step.Gas:
                if (controls != null && controls.Throttle > 0.5f) Advance();
                break;
            case Step.Brake:
                if (controls != null && controls.Brake > 0.5f) Advance();
                break;
            case Step.Checkpoint:
                if (race != null && race.NextCheckpoint > 0) Advance();
                break;
        }
    }

    void ShowStep()
    {
        stepTimer = 0f;
        if (hud == null) return;
        switch (step)
        {
            case Step.Steer:
                hud.ShowMessage("Tutorial 1/4: drag the left side of the screen to steer", 30f);
                break;
            case Step.Gas:
                hud.ShowMessage("Tutorial 2/4: hold GAS to accelerate", 30f);
                break;
            case Step.Brake:
                hud.ShowMessage("Tutorial 3/4: hold BRAKE to slow down / reverse", 30f);
                break;
            case Step.Checkpoint:
                hud.ShowMessage("Tutorial 4/4: drive through the green checkpoint gates!", 30f);
                break;
        }
    }

    void Advance()
    {
        step++;
        if (step >= Step.Done)
        {
            active = false;
            PlayerPrefs.SetInt("TutorialDone", 1);
            PlayerPrefs.Save();
            if (hud != null) hud.ShowMessage("Tutorial complete! +$200", 5f);
            if (GameManager.Instance != null) GameManager.Instance.AddMoney(200);
            return;
        }
        ShowStep();
    }
}

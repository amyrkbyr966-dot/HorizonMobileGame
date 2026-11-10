using UnityEngine;

/// <summary>Touch input hub. Singleton so CarController and TouchButton can
/// read it without wiring. Steering: drag on the left half of the screen.
/// Gas / brake / steer buttons: TouchButton components call the setters.
/// Keyboard is read directly by CarController, so the game is also
/// playable in the editor.</summary>
public class MobileControls : MonoBehaviour
{
    public static MobileControls Instance { get; private set; }

    /// <summary>-1 (full left) .. 1 (full right)</summary>
    public float Steer { get; private set; }
    /// <summary>0 .. 1</summary>
    public float Throttle { get; private set; }
    /// <summary>0 .. 1</summary>
    public float Brake { get; private set; }

    public bool useTouchSteering = true;

    /// <summary>Steering sensitivity, 0.5..2 (set by SettingsManager).</summary>
    public float steeringSensitivity = 1f;

    int steeringTouchId = -1;
    float steerTouchStartX;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        if (useTouchSteering) HandleTouchSteering();
    }

    void HandleTouchSteering()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            // Left half of the screen, lower 70%: steering drag zone.
            if (t.position.x >= Screen.width * 0.5f || t.position.y > Screen.height * 0.7f)
                continue;
            if (t.phase == TouchPhase.Began)
            {
                steeringTouchId = t.fingerId;
                steerTouchStartX = t.position.x;
            }
            else if (t.fingerId == steeringTouchId &&
                     (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
            {
                Steer = Mathf.Clamp((t.position.x - steerTouchStartX) / 200f * steeringSensitivity, -1f, 1f);
            }
            else if (t.fingerId == steeringTouchId &&
                     (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            {
                steeringTouchId = -1;
                Steer = 0f;
            }
        }
    }

    public void SetGas(bool pressed) { Throttle = pressed ? 1f : 0f; }
    public void SetBrake(bool pressed) { Brake = pressed ? 1f : 0f; }

    /// <summary>dir: -1 left, 0 none, 1 right (from steer buttons).</summary>
    public void SetSteerButton(float dir) { Steer = Mathf.Clamp(dir * steeringSensitivity, -1f, 1f); }

    public void ResetInputs()
    {
        Steer = 0f;
        Throttle = 0f;
        Brake = 0f;
        steeringTouchId = -1;
    }
}

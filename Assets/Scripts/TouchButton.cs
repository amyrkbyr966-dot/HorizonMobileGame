using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Hold-to-drive UI button. Attach to a Button GameObject together
/// with the Button component; no EventTrigger asset is needed because this
/// script implements the pointer interfaces directly.</summary>
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public enum ButtonAction { Gas, Brake, SteerLeft, SteerRight, Pause, Nitro, Resume, QuitToMenu, Restart }

    public ButtonAction action = ButtonAction.Gas;

    public void OnPointerDown(PointerEventData eventData) { Apply(true); }
    public void OnPointerUp(PointerEventData eventData) { Apply(false); }
    public void OnPointerExit(PointerEventData eventData) { Apply(false); }

    /// <summary>Release the input if the button is disabled mid-press
    /// (scene change, HUD hide). A lost pointer-up used to leave GAS or
    /// BRAKE stuck at 1, which could pin the car at spawn (round-3 bug #2).</summary>
    void OnDisable() { Apply(false); }

    void Apply(bool pressed)
    {
        // Pause-menu actions (2026-11-08 menu fix): Resume / QuitToMenu
        // do not need MobileControls, so handle them BEFORE its null
        // check — the pause panel must work even if the hub is gone.
        if (action == ButtonAction.Resume)
        {
            if (pressed && GameManager.Instance != null) GameManager.Instance.Resume();
            return;
        }
        if (action == ButtonAction.QuitToMenu)
        {
            if (pressed && GameManager.Instance != null) GameManager.Instance.QuitToMenu();
            return;
        }
        if (action == ButtonAction.Restart)
        {
            if (pressed && GameManager.Instance != null) GameManager.Instance.RestartRace();
            return;
        }
        if (MobileControls.Instance == null) return;
        switch (action)
        {
            case ButtonAction.Gas:
                MobileControls.Instance.SetGas(pressed);
                break;
            case ButtonAction.Brake:
                MobileControls.Instance.SetBrake(pressed);
                break;
            case ButtonAction.SteerLeft:
                MobileControls.Instance.SetSteerButton(pressed ? -1f : 0f);
                break;
            case ButtonAction.SteerRight:
                MobileControls.Instance.SetSteerButton(pressed ? 1f : 0f);
                break;
            case ButtonAction.Pause:
                if (pressed && GameManager.Instance != null)
                    GameManager.Instance.TogglePause();
                break;
            case ButtonAction.Nitro:
                // Removed feature (2026-11-07): nitro was deleted from
                // the game. The enum value is kept (it is last, so it
                // cannot shift any serialized action) and the NITRO button
                // is hidden at runtime by HUD; pressing it does nothing.
                break;
        }
    }
}

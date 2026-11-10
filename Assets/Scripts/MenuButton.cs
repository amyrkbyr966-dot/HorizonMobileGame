using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>Main-menu button (pointer-down, no onClick wiring needed).
/// Action is chosen in the Inspector.</summary>
public class MenuButton : MonoBehaviour, IPointerDownHandler
{
    public enum MenuAction { Race, FreeRoam, Quit, TimeTrial, Collectibles, NextCar, PrevCar, BuyCar, SelectCar, StreetRace, PoliceChase, Delivery, Championship, Garage, SettingsQuality, SettingsLanguage }

    public MenuAction action = MenuAction.Race;

    public void OnPointerDown(PointerEventData eventData)
    {
        // Log every tap: the stuck-menu bug (2026-11-08) was silent — no
        // log line proved whether a tap ever reached a button. Keep this
        // line: it is the only phone-side evidence that input works.
        Debug.Log("[MenuButton] tap action=" + action + " on " + gameObject.name);
        var menu = FindFirstObjectByType<MainMenu>();
        switch (action)
        {
            case MenuAction.Race:
                if (GameManager.Instance != null) GameManager.Instance.StartRace();
                else SceneManager.LoadScene("Main");
                break;
            case MenuAction.FreeRoam:
                if (GameManager.Instance != null) GameManager.Instance.StartFreeRoam();
                else SceneManager.LoadScene("Main");
                break;
            case MenuAction.Quit:
                Application.Quit();
                break;
            case MenuAction.TimeTrial:
                if (menu != null) menu.OnTimeTrial();
                break;
            case MenuAction.Collectibles:
                if (menu != null) menu.OnCollectibles();
                break;
            case MenuAction.NextCar:
                if (menu != null) menu.OnNextCar();
                break;
            case MenuAction.PrevCar:
                if (menu != null) menu.OnPrevCar();
                break;
            case MenuAction.BuyCar:
                if (menu != null) menu.OnBuyCar();
                break;
            case MenuAction.SelectCar:
                if (menu != null) menu.OnSelectCar();
                break;
            case MenuAction.StreetRace:
                if (menu != null) menu.OnStreetRace();
                break;
            case MenuAction.PoliceChase:
                // Removed feature (2026-11-07): police chase was deleted
                // from the game. The enum value is kept so the serialized
                // action numbers in the menu scene/prefab keep mapping to
                // the same actions (Delivery/Championship/... follow it).
                // The button itself is hidden at runtime by MainMenu.
                break;
            case MenuAction.Delivery:
                if (menu != null) menu.OnDelivery();
                break;
            case MenuAction.Championship:
                if (menu != null) menu.OnChampionship();
                break;
            case MenuAction.Garage:
                if (menu != null) menu.OnGarage();
                break;
            case MenuAction.SettingsQuality:
                if (menu != null) menu.OnSettingsQuality();
                break;
            case MenuAction.SettingsLanguage:
                if (menu != null) menu.OnSettingsLanguage();
                break;
        }
    }
}

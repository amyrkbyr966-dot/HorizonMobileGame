using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Garage upgrade slots on the main menu car card (4 slots x 5 levels,
/// per car/class — UpgradeSystem.SlotsForCar). UI-only: it reads the
/// slot set + levels for the preview car and sets the part icons +
/// level pips. It does NOT buy or apply upgrades (that logic lives in
/// UpgradeSystem / the garage economy follow-up); the slots are
/// display-only until the purchase flow is wired.
/// Slot children are found by name under this panel ("Slot0Icon",
/// "Slot0Name", "Slot0Pip0".."Slot0Pip4", ...), matching the prefab.
/// </summary>
public class GarageUpgradesUI : MonoBehaviour
{
    // Part icons, wired in the prefab (Assets/UI/Art/Garage/Icons).
    public Sprite engineIcon;
    public Sprite aeroIcon;
    public Sprite tiresIcon;
    public Sprite brakesIcon;
    public Sprite armorIcon;
    public Sprite superchargerIcon;
    public Sprite suspensionIcon;
    public Sprite batteryIcon;
    public Sprite motorIcon;
    public Sprite lightweightIcon;
    public Sprite pipFilled;
    public Sprite pipEmpty;

    public void Refresh(int carIndex)
    {
        UpgradeSystem.UpgradeSlot[] slots = UpgradeSystem.SlotsForCar(carIndex);
        for (int i = 0; i < 4; i++)
        {
            UpgradeSystem.UpgradeSlot slot = slots[i];
            Image icon = FindImage("Slot" + i + "Icon");
            if (icon != null) icon.sprite = IconFor(slot);
            TMP_Text name = FindText("Slot" + i + "Name");
            if (name != null) name.text = UpgradeSystem.SlotName(slot);
            int level = UpgradeSystem.GetLevel(carIndex, slot);
            for (int j = 0; j < 5; j++)
            {
                Image pip = FindImage("Slot" + i + "Pip" + j);
                if (pip != null) pip.sprite = j < level ? pipFilled : pipEmpty;
            }
        }
    }

    Sprite IconFor(UpgradeSystem.UpgradeSlot slot)
    {
        switch (slot)
        {
            case UpgradeSystem.UpgradeSlot.Engine: return engineIcon;
            case UpgradeSystem.UpgradeSlot.Aero: return aeroIcon;
            case UpgradeSystem.UpgradeSlot.Tires: return tiresIcon;
            case UpgradeSystem.UpgradeSlot.Brakes: return brakesIcon;
            case UpgradeSystem.UpgradeSlot.Armor: return armorIcon;
            case UpgradeSystem.UpgradeSlot.Supercharger: return superchargerIcon;
            case UpgradeSystem.UpgradeSlot.Suspension: return suspensionIcon;
            case UpgradeSystem.UpgradeSlot.Battery: return batteryIcon;
            case UpgradeSystem.UpgradeSlot.Motor: return motorIcon;
            case UpgradeSystem.UpgradeSlot.Lightweight: return lightweightIcon;
            default: return null;
        }
    }

    Image FindImage(string objectName)
    {
        Transform t = transform.Find(objectName);
        return t != null ? t.GetComponent<Image>() : null;
    }

    TMP_Text FindText(string objectName)
    {
        Transform t = transform.Find(objectName);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }
}

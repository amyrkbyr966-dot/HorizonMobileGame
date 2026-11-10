using UnityEngine;

/// <summary>Garage upgrades, PER CAR and PER CLASS (2026-11-07 redesign).
/// Every car has 4 upgrade slots, 5 levels each, but the slots differ by
/// what the car is — a supercar gets an aero kit, a muscle car gets a
/// supercharger, an SUV gets suspension, an EV gets a battery and a
/// bigger motor, a small city car gets lightweight parts. Nitro is gone
/// (removed from the game 2026-11-07) and Brakes replaced it as a core
/// slot. Levels are saved per car ("Upgrade_<carIndex>_<slotPos>") and
/// applied to the player car at spawn (CarController.ApplyToCar).
/// Costs scale with the car's price so a hypercar's upgrades cost more
/// than a city car's. LevelsCsv/ApplyLevelsCsv keep the old 4-number
/// shape for SaveManager, but now mean the SELECTED car's 4 slots.</summary>
public static class UpgradeSystem
{
    public enum UpgradeSlot
    {
        Engine, Tires, Brakes, Armor, Aero,
        Supercharger, Suspension, Battery, Motor, Lightweight
    }

    public const int MaxLevel = 5;
    public const int SlotsPerCar = 4;

    // Cost to buy INTO level L (index 0 unused), before the car's
    // price multiplier is applied.
    public static readonly int[] LevelCosts = new int[] { 0, 400, 800, 1400, 2200, 3200 };

    /// <summary>Display name for the garage UI.</summary>
    public static string SlotName(UpgradeSlot slot)
    {
        switch (slot)
        {
            case UpgradeSlot.Engine: return "Engine";
            case UpgradeSlot.Tires: return "Tires";
            case UpgradeSlot.Brakes: return "Brakes";
            case UpgradeSlot.Armor: return "Armor";
            case UpgradeSlot.Aero: return "Aero Kit";
            case UpgradeSlot.Supercharger: return "Supercharger";
            case UpgradeSlot.Suspension: return "Suspension";
            case UpgradeSlot.Battery: return "Battery";
            case UpgradeSlot.Motor: return "Electric Motor";
            case UpgradeSlot.Lightweight: return "Lightweight Parts";
            default: return slot.ToString();
        }
    }

    /// <summary>What one level of this slot does, for the garage UI.</summary>
    public static string SlotDescription(UpgradeSlot slot)
    {
        switch (slot)
        {
            case UpgradeSlot.Engine: return "+8% acceleration, +5% top speed";
            case UpgradeSlot.Tires: return "+7% grip";
            case UpgradeSlot.Brakes: return "+10% braking power";
            case UpgradeSlot.Armor: return "-8% damage taken";
            case UpgradeSlot.Aero: return "+12% downforce, +2% top speed";
            case UpgradeSlot.Supercharger: return "+10% acceleration, +4% top speed";
            case UpgradeSlot.Suspension: return "+5% grip, -5% damage taken";
            case UpgradeSlot.Battery: return "+6% top speed, +3% acceleration";
            case UpgradeSlot.Motor: return "+9% acceleration, +3% top speed";
            case UpgradeSlot.Lightweight: return "+4% acceleration, +3% handling";
            default: return "";
        }
    }

    /// <summary>The 4 upgrade slots for a car, by its class (see
    /// CarBodyStyles.ClassForCar) and whether it is electric
    /// (CarDef.electric). Order is fixed per car so the save keys stay
    /// stable: slot position 0-3 inside this car's set.</summary>
    public static UpgradeSlot[] SlotsForCar(int carIndex)
    {
        CarDef def = CarSelection.Cars[Mathf.Clamp(carIndex, 0, CarSelection.Cars.Length - 1)];
        if (def.electric)
            return new UpgradeSlot[] { UpgradeSlot.Battery, UpgradeSlot.Motor, UpgradeSlot.Tires, UpgradeSlot.Brakes };
        switch (CarBodyStyles.ClassForCar(carIndex))
        {
            case 2: // supercar
            case 7: // hypercar
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Aero, UpgradeSlot.Tires, UpgradeSlot.Brakes };
            case 1: // GT
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Aero, UpgradeSlot.Tires, UpgradeSlot.Armor };
            case 8: // coupe
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Tires, UpgradeSlot.Brakes, UpgradeSlot.Armor };
            case 6: // roadster
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Lightweight, UpgradeSlot.Tires, UpgradeSlot.Brakes };
            case 4: // muscle
                return new UpgradeSlot[] { UpgradeSlot.Supercharger, UpgradeSlot.Tires, UpgradeSlot.Brakes, UpgradeSlot.Armor };
            case 3: // SUV / off-road
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Suspension, UpgradeSlot.Tires, UpgradeSlot.Armor };
            case 5: // sedan
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Tires, UpgradeSlot.Brakes, UpgradeSlot.Armor };
            default: // 0 city / kei
                return new UpgradeSlot[] { UpgradeSlot.Engine, UpgradeSlot.Lightweight, UpgradeSlot.Tires, UpgradeSlot.Brakes };
        }
    }

    static int SlotPosition(int carIndex, UpgradeSlot slot)
    {
        UpgradeSlot[] slots = SlotsForCar(carIndex);
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] == slot) return i;
        return -1;
    }

    static string Key(int carIndex, UpgradeSlot slot)
    {
        return "Upgrade_" + carIndex + "_" + SlotPosition(carIndex, slot);
    }

    public static int GetLevel(int carIndex, UpgradeSlot slot)
    {
        if (SlotPosition(carIndex, slot) < 0) return 0;
        return Mathf.Clamp(PlayerPrefs.GetInt(Key(carIndex, slot), 0), 0, MaxLevel);
    }

    /// <summary>Level of a slot on the currently selected car.</summary>
    public static int GetLevel(UpgradeSlot slot)
    {
        return GetLevel(CarSelection.SelectedIndex, slot);
    }

    /// <summary>Price multiplier: a $45,000 hypercar's upgrades cost 4x a
    /// free city car's. Rounds to the nearest 10.</summary>
    public static float CostMultiplier(int carIndex)
    {
        CarDef def = CarSelection.Cars[Mathf.Clamp(carIndex, 0, CarSelection.Cars.Length - 1)];
        return 1f + def.price / 15000f;
    }

    public static int CostForNextLevel(int carIndex, UpgradeSlot slot)
    {
        int level = GetLevel(carIndex, slot);
        if (level >= MaxLevel) return -1;
        int raw = Mathf.RoundToInt(LevelCosts[level + 1] * CostMultiplier(carIndex) / 10f) * 10;
        return raw;
    }

    public static int CostForNextLevel(UpgradeSlot slot)
    {
        return CostForNextLevel(CarSelection.SelectedIndex, slot);
    }

    public static bool TryBuy(int carIndex, UpgradeSlot slot)
    {
        int cost = CostForNextLevel(carIndex, slot);
        if (cost < 0) return false;
        if (GameManager.Instance == null || GameManager.Instance.Money < cost) return false;
        GameManager.Instance.AddMoney(-cost);
        PlayerPrefs.SetInt(Key(carIndex, slot), GetLevel(carIndex, slot) + 1);
        PlayerPrefs.Save();
        SaveManager.Save();
        return true;
    }

    public static bool TryBuy(UpgradeSlot slot)
    {
        return TryBuy(CarSelection.SelectedIndex, slot);
    }

    /// <summary>Selected car's 4 slot levels as "a,b,c,d" (SaveManager).</summary>
    public static string LevelsCsv()
    {
        int carIndex = CarSelection.SelectedIndex;
        UpgradeSlot[] slots = SlotsForCar(carIndex);
        string[] parts = new string[slots.Length];
        for (int i = 0; i < slots.Length; i++)
            parts[i] = GetLevel(carIndex, slots[i]).ToString();
        return string.Join(",", parts);
    }

    public static void ApplyLevelsCsv(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return;
        int carIndex = CarSelection.SelectedIndex;
        UpgradeSlot[] slots = SlotsForCar(carIndex);
        string[] parts = csv.Split(',');
        for (int i = 0; i < parts.Length && i < slots.Length; i++)
        {
            int v;
            if (int.TryParse(parts[i], out v))
                PlayerPrefs.SetInt(Key(carIndex, slots[i]), Mathf.Clamp(v, 0, MaxLevel));
        }
        PlayerPrefs.Save();
    }

    public static void ApplyToCar(CarController car)
    {
        if (car == null) return;
        int carIndex = CarSelection.SelectedIndex;
        UpgradeSlot[] slots = SlotsForCar(carIndex);
        float damageTaken = 1f;
        for (int i = 0; i < slots.Length; i++)
        {
            int level = GetLevel(carIndex, slots[i]);
            if (level <= 0) continue;
            float l = level;
            switch (slots[i])
            {
                case UpgradeSlot.Engine:
                    car.accelerationMultiplier *= 1f + 0.08f * l;
                    car.maxSpeed *= 1f + 0.05f * l;
                    break;
                case UpgradeSlot.Supercharger:
                    car.accelerationMultiplier *= 1f + 0.10f * l;
                    car.maxSpeed *= 1f + 0.04f * l;
                    break;
                case UpgradeSlot.Motor:
                    car.accelerationMultiplier *= 1f + 0.09f * l;
                    car.maxSpeed *= 1f + 0.03f * l;
                    break;
                case UpgradeSlot.Battery:
                    car.maxSpeed *= 1f + 0.06f * l;
                    car.accelerationMultiplier *= 1f + 0.03f * l;
                    break;
                case UpgradeSlot.Tires:
                    car.gripFactor *= 1f + 0.07f * l;
                    break;
                case UpgradeSlot.Brakes:
                    car.brakeMultiplier *= 1f + 0.10f * l;
                    break;
                case UpgradeSlot.Armor:
                    damageTaken *= 1f - 0.08f * l;
                    break;
                case UpgradeSlot.Aero:
                    car.downforceMultiplier *= 1f + 0.12f * l;
                    car.maxSpeed *= 1f + 0.02f * l;
                    break;
                case UpgradeSlot.Suspension:
                    car.gripFactor *= 1f + 0.05f * l;
                    damageTaken *= 1f - 0.05f * l;
                    break;
                case UpgradeSlot.Lightweight:
                    car.accelerationMultiplier *= 1f + 0.04f * l;
                    car.handlingMultiplier *= 1f + 0.03f * l;
                    break;
            }
        }
        var damage = car.GetComponent<CarDamage>();
        if (damage != null) damage.damageMultiplier = damageTaken;
    }
}

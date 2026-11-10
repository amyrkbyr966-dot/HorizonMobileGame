using UnityEngine;

/// <summary>Adaptive quality scaler for low-end phones: watches the
/// rolling frame rate and steps the Unity quality level down when the
/// game runs below 25 fps for a while, back up when it runs above
/// 55 fps. Only uses QualitySettings (core Unity), so it compiles
/// without any URP assembly reference. Attach to a GameObject in
/// the Main scene.
///
/// Caps (fixed 2026-11-07, TokyoExpansionPlan §5): the scaler never
/// raises the level above the player's manually chosen tier and never
/// drops below Low, so it can only move inside the tier the player
/// picked. If it steps down twice from a manual Ultra, SuggestLowerQuality
/// goes true and the HUD suggests High once.</summary>
public class QualityScaler : MonoBehaviour
{
    public float checkInterval = 2f;
    public float lowFps = 25f;
    public float highFps = 55f;

    public float AvgFps { get; private set; } = 60f;
    public int CurrentQualityLevel { get; private set; }

    /// <summary>True once the scaler stepped down twice from manual Ultra.</summary>
    public bool SuggestLowerQuality { get; private set; }

    float timer;
    float frameSum;
    int frameCount;
    int lowStreak;
    int manualLevel;
    int stepsDown;

    void Start()
    {
        manualLevel = SettingsManager.TierQualityLevel(SettingsManager.Quality);
        CurrentQualityLevel = QualitySettings.GetQualityLevel();
        if (CurrentQualityLevel > manualLevel)
        {
            CurrentQualityLevel = manualLevel;
            QualitySettings.SetQualityLevel(CurrentQualityLevel, true);
        }
    }

    void Update()
    {
        frameSum += 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        frameCount++;
        timer += Time.unscaledDeltaTime;
        if (timer < checkInterval) return;
        AvgFps = frameCount > 0 ? frameSum / frameCount : 60f;
        frameSum = 0f;
        frameCount = 0;
        timer = 0f;
        if (AvgFps < lowFps)
        {
            lowStreak++;
            // Floor: the Low tier's level (1). Never below the tier floor.
            if (lowStreak >= 3 && CurrentQualityLevel > SettingsManager.TierQualityLevel(0))
            {
                CurrentQualityLevel--;
                QualitySettings.SetQualityLevel(CurrentQualityLevel, true);
                QualitySettings.pixelLightCount = Mathf.Max(1, QualitySettings.pixelLightCount - 1);
                lowStreak = 0;
                stepsDown++;
                if (SettingsManager.Quality == 3 && stepsDown >= 2)
                    SuggestLowerQuality = true;
            }
        }
        else
        {
            lowStreak = 0;
            // Ceiling: never above the manually chosen tier's level.
            if (AvgFps > highFps && CurrentQualityLevel < manualLevel)
            {
                CurrentQualityLevel++;
                QualitySettings.SetQualityLevel(CurrentQualityLevel, true);
            }
        }
    }
}

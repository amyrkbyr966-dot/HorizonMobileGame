using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Loading screen for the main menu (fixes the "long load with no
/// loading indicator" issue): while GameManager loads the Main scene
/// asynchronously, this panel shows a progress bar + percentage over
/// the menu. The panel root stays active; only the LoadingContent
/// child is toggled (deactivating the root would stop Update and the
/// panel could never come back). Content starts INACTIVE in the
/// prefab; GameManager.IsLoading drives it.
/// </summary>
public class LoadingPanelUI : MonoBehaviour
{
    GameObject content;
    Image barFill;
    TMP_Text percentText;

    void Start()
    {
        var t = transform.Find("LoadingContent");
        content = t != null ? t.gameObject : null;
        if (content != null)
        {
            var bar = content.transform.Find("LoadingBarFill");
            if (bar != null) barFill = bar.GetComponent<Image>();
            var pct = content.transform.Find("LoadingPercentText");
            if (pct != null) percentText = pct.GetComponent<TMP_Text>();
            content.SetActive(false);
        }
    }

    void Update()
    {
        if (content == null) return;
        bool loading = GameManager.IsLoading;
        if (content.activeSelf != loading) content.SetActive(loading);
        if (!loading) return;
        float p = Mathf.Clamp01(GameManager.LoadProgress);
        if (barFill != null) barFill.fillAmount = p;
        if (percentText != null) percentText.text = Mathf.RoundToInt(p * 100f) + "%";
    }
}

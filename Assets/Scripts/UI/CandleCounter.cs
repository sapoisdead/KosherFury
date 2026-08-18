
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CandleCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI counterText;

    void Start()
    {
        if (MenorahManager.Instance == null) return;
        MenorahManager.Instance.OnCandleCountChanged += UpdateDisplay;
        UpdateDisplay();
    }

    void OnDestroy()
    {
        if (MenorahManager.Instance == null) return;
        MenorahManager.Instance.OnCandleCountChanged -= UpdateDisplay;
    }

    private void UpdateDisplay()
    {
        counterText.text = MenorahManager.Instance.CollectedCount + "/7";
    }
}

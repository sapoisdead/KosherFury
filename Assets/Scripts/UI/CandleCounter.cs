
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CandleCounter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI counterText;

    void Start()
    {
        MenorahManager.Instance.OnCandleCountChanged += UpdateDisplay;
        UpdateDisplay();
    }

    void OnDestroy()
    {
        MenorahManager.Instance.OnCandleCountChanged -= UpdateDisplay;
    }

    private void UpdateDisplay()
    {
        counterText.text = MenorahManager.Instance.CollectedCount + "/7";
    }
}

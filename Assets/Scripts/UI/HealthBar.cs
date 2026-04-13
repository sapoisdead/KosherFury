using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private Health playerHealth;
    private RectTransform fillRect;

    private void Awake()
    {
        if (fillImage != null)
            fillRect = fillImage.GetComponent<RectTransform>();
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            if (PlayerManager.Instance?.PlayerTransform != null)
                playerHealth = PlayerManager.Instance.PlayerTransform.GetComponentInChildren<Health>();
            return;
        }

        float ratio = Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth);

        // Scala il fill modificando l'anchor destro — funziona senza sprite
        if (fillRect != null)
        {
            Vector2 max = fillRect.anchorMax;
            max.x = ratio;
            fillRect.anchorMax = max;
        }
    }
}

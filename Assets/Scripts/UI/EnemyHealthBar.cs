using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private RectTransform fillRect;
    [Tooltip("Immagine del fill, per il colore. Lasciare vuoto per non cambiare mai colore.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Color normalColor = Color.red;
    [Tooltip("Colore quando invulnerabile: oggi vale solo per un simp in modalita' scudo, nessun altro caso lo mette a true.")]
    [SerializeField] private Color shieldColor = Color.gray;

    private Transform cam;

    private void Start()
    {
        cam = Camera.main?.transform;
        if (health == null)
            health = GetComponentInParent<Health>();
    }

    private void LateUpdate()
    {
        // Guarda sempre verso la camera
        if (cam != null)
            transform.LookAt(transform.position + cam.forward);

        if (health == null || fillRect == null) return;

        float ratio = Mathf.Clamp01(health.CurrentHealth / health.MaxHealth);
        Vector2 max = fillRect.anchorMax;
        max.x = ratio;
        fillRect.anchorMax = max;

        if (fillImage != null)
            fillImage.color = health.IsInvincible ? shieldColor : normalColor;
    }
}

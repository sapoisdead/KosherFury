using UnityEngine;

public class EnemyHealthBar : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private RectTransform fillRect;

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
    }
}

using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsInvincible { get; set; }

    private Animator animator;
    private static readonly int TakeHitHash = Animator.StringToHash("Take_hit");

    private void Awake()
    {
        CurrentHealth = maxHealth;
        animator = GetComponent<Animator>();
    }

    public void SetHealth(float amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 0f, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return;
        if (IsInvincible) return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);
        animator?.SetTrigger(TakeHitHash);

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        gameObject.SetActive(false);
    }
}

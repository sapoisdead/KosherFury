using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;

    private Animator animator;
    private static readonly int TakeHitHash = Animator.StringToHash("Take_hit");

    private void Awake()
    {
        CurrentHealth = maxHealth;
        animator = GetComponent<Animator>();
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);
        animator?.SetTrigger(TakeHitHash);

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} è morto!");
        gameObject.SetActive(false);
    }
}

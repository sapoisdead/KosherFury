using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private EnemyController enemyController;
    [SerializeField] private float smoothTime = 0.1f;

    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private float currentSpeed;
    private float speedVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();
    }

    private void Update()
    {
        float targetSpeed = enemyController != null ? enemyController.GetSpeed() : 0f;

        currentSpeed = Mathf.SmoothDamp(
            currentSpeed,
            targetSpeed,
            ref speedVelocity,
            smoothTime
        );

        animator.SetFloat(SpeedHash, currentSpeed);
    }
}

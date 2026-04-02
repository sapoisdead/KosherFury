using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float smoothTime = 0.1f;

    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private float currentSpeed;
    private float speedVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        float targetSpeed = playerMovement != null ? playerMovement.GetSpeed() : 0f;

        currentSpeed = Mathf.SmoothDamp(
            currentSpeed,
            targetSpeed,
            ref speedVelocity,
            smoothTime
        );

        animator.SetFloat(SpeedHash, currentSpeed);
    }

    public void TriggerJump()
    {
        animator.SetTrigger(JumpHash);
    }
}

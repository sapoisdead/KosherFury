using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private PunchHitbox punchHitbox;

    private Animator animator;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int PunchHash = Animator.StringToHash("Punch");
    private float currentSpeed;
    private float speedVelocity;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (punchHitbox == null)
            punchHitbox = GetComponentInChildren<PunchHitbox>();
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

    public void TriggerPunch()
    {
        animator.SetTrigger(PunchHash);
    }

    // Chiamato dall'Animation Event sul clip Kosher_punch
    public void OnPunchHit()
    {
        punchHitbox?.CheckHit();
    }
}

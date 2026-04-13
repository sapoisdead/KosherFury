using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private PunchHitbox punchHitbox;

    private Animator animator;
    private Health health;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int PunchRightHash = Animator.StringToHash("Punch_right");
    private static readonly int PunchLeftHash = Animator.StringToHash("Punch_left");
    private float currentSpeed;
    private float speedVelocity;
    private bool nextPunchIsLeft;
    private bool punchQueued;

    private static readonly int PunchRightStateHash = Animator.StringToHash("Punch_Right");
    private static readonly int PunchLeftStateHash  = Animator.StringToHash("Punch_Left");
    private static readonly int TakeHitStateHash    = Animator.StringToHash("Take_hit");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

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

        var currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool inPunchState = currentState.shortNameHash == PunchRightStateHash || currentState.shortNameHash == PunchLeftStateHash;
        bool inHitState   = currentState.shortNameHash == TakeHitStateHash;
        if (health != null) health.IsInvincible = inPunchState || inHitState;

        if (punchQueued && !animator.IsInTransition(0))
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.shortNameHash == TakeHitStateHash)
            {
                punchQueued = false;
                return;
            }

            bool inPunch = state.shortNameHash == PunchRightStateHash || state.shortNameHash == PunchLeftStateHash;
            if (!inPunch || state.normalizedTime >= 0.5f)
            {
                punchQueued = false;
                FirePunch();
            }
        }
    }

    public void TriggerJump()
    {
        animator.SetTrigger(JumpHash);
    }

    public void TriggerPunch()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);

        if (state.shortNameHash == TakeHitStateHash)
            return;

        bool inPunch = state.shortNameHash == PunchRightStateHash || state.shortNameHash == PunchLeftStateHash;

        if (!inPunch)
            FirePunch();
        else
            punchQueued = true; // una sola coda, lo spam non aggiunge altri
    }

    private void FirePunch()
    {
        animator.SetTrigger(nextPunchIsLeft ? PunchLeftHash : PunchRightHash);
        nextPunchIsLeft = !nextPunchIsLeft;
    }

    public void ResetPunch()
    {
        punchQueued = false;
        nextPunchIsLeft = false;
        animator.ResetTrigger(PunchRightHash);
        animator.ResetTrigger(PunchLeftHash);
    }

    public void OnRightPunchHit()
    {
        punchHitbox?.CheckHit();
    }

    public void OnLeftPunchHit()
    {
        punchHitbox?.CheckHit();
    }
}

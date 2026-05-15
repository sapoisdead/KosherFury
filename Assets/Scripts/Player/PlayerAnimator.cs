using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private PunchHitbox punchHitboxRight;
    [SerializeField] private PunchHitbox punchHitboxLeft;

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

    private static readonly int SlashHash           = Animator.StringToHash("Slash");
    private static readonly int PunchRightStateHash = Animator.StringToHash("Punch_Right");
    private static readonly int PunchLeftStateHash  = Animator.StringToHash("Punch_Left");
    private static readonly int SlashStateHash      = Animator.StringToHash("Slash");
    private static readonly int TakeHitStateHash    = Animator.StringToHash("Take_hit");

    private bool slashQueued;
    private WeaponManager weaponManager;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        weaponManager = GetComponent<WeaponManager>();

        if (punchHitboxRight == null || punchHitboxLeft == null)
        {
            var hitboxes = GetComponentsInChildren<PunchHitbox>();
            foreach (var hb in hitboxes)
            {
                if (hb.name.Contains("Right") && punchHitboxRight == null) punchHitboxRight = hb;
                else if (hb.name.Contains("Left") && punchHitboxLeft == null) punchHitboxLeft = hb;
            }
        }
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
        bool inSlashState = currentState.shortNameHash == SlashStateHash;
        bool inHitState   = currentState.shortNameHash == TakeHitStateHash;
        if (health != null) health.IsInvincible = inPunchState || inSlashState || inHitState;

        if (slashQueued && !animator.IsInTransition(0))
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == TakeHitStateHash) { slashQueued = false; }
            else if (state.shortNameHash != SlashStateHash || state.normalizedTime >= 0.5f)
            { slashQueued = false; FireSlash(); }
        }

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
        if (weaponManager != null && weaponManager.CurrentWeapon != null && !weaponManager.CurrentWeapon.isFists)
        {
            TriggerSlash();
            return;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        bool inPunch = state.shortNameHash == PunchRightStateHash || state.shortNameHash == PunchLeftStateHash;
        if (!inPunch) FirePunch();
        else punchQueued = true;
    }

    private void TriggerSlash()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        bool inSlash = state.shortNameHash == SlashStateHash;
        if (!inSlash) FireSlash();
        else slashQueued = true;
    }

    private void FireSlash()
    {
        animator.SetTrigger(SlashHash);
    }

    public void OnSlashStart()
    {
        weaponManager?.CurrentHitbox?.EnableHitbox();
    }

    public void OnSlashEnd()
    {
        weaponManager?.CurrentHitbox?.DisableHitbox();
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
        float? dmg = weaponManager?.CurrentWeapon?.isFists == true ? weaponManager.CurrentWeapon.damage : (float?)null;
        punchHitboxRight?.CheckHit(dmg);
    }

    public void OnLeftPunchHit()
    {
        float? dmg = weaponManager?.CurrentWeapon?.isFists == true ? weaponManager.CurrentWeapon.damage : (float?)null;
        punchHitboxLeft?.CheckHit(dmg);
    }
}

using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private PunchHitbox punchHitboxRight;
    [SerializeField] private PunchHitbox punchHitboxLeft;

    private Animator animator;
    private NavMeshAgent agent;
    private float currentSpeed;
    private float speedVelocity;
    private bool nextPunchIsLeft;

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int PunchRightHash  = Animator.StringToHash("Punch_right");
    private static readonly int PunchLeftHash   = Animator.StringToHash("Punch_left");
    private static readonly int TakeHitHash     = Animator.StringToHash("Take_hit");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent    = GetComponent<NavMeshAgent>();

        if (punchHitboxRight == null || punchHitboxLeft == null)
        {
            var hitboxes = GetComponentsInChildren<PunchHitbox>(true);
            foreach (var hb in hitboxes)
            {
                if (hb.name.Contains("Right") && punchHitboxRight == null) punchHitboxRight = hb;
                else if (hb.name.Contains("Left") && punchHitboxLeft == null) punchHitboxLeft = hb;
            }
        }
    }

    private void Update()
    {
        float targetSpeed = agent != null ? agent.velocity.magnitude : 0f;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, smoothTime);
        animator.SetFloat(SpeedHash, currentSpeed);
    }

    public bool IsBeingHit()
    {
        return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == TakeHitHash;
    }

    public void TriggerAttack()
    {
        animator.SetTrigger(nextPunchIsLeft ? PunchLeftHash : PunchRightHash);
        nextPunchIsLeft = !nextPunchIsLeft;
    }

    private void OnRightPunchHit() => punchHitboxRight?.CheckHit();
    private void OnLeftPunchHit()  => punchHitboxLeft?.CheckHit();
}

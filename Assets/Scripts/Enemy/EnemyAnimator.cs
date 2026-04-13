using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private float smoothTime = 0.1f;

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

    // Ricevitori degli animation event del controller condiviso col player
    private void OnRightPunchHit() { }
    private void OnLeftPunchHit() { }
}

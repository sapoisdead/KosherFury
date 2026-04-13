using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float updateRate = 0.2f;
    [SerializeField] private float stoppingDistance = 2f;

    private NavMeshAgent agent;
    private float updateTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.stoppingDistance = stoppingDistance;
    }

    private void Start()
    {
        if (target == null && PlayerManager.Instance != null)
            target = PlayerManager.Instance.PlayerTransform;
    }

    private void Update()
    {
        updateTimer -= Time.deltaTime;

        if (updateTimer <= 0f)
        {
            agent.SetDestination(target.position);
            updateTimer = updateRate;
        }
    }

    public float GetSpeed()
    {
        return agent.velocity.magnitude;
    }
}

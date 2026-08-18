using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using BehaviourTree;

public class EnemyBT : BehaviourTree.BehaviourTreeBase
{
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float abandonRange = 10f;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private float attackCooldown = 1.5f;

    private NavMeshAgent agent;
    private EnemyAnimator enemyAnimator;
    private Transform player;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    protected override Node SetupTree()
    {
        agent.stoppingDistance = 0f;
        player = PlayerManager.Instance.PlayerTransform;

        return new Selector(new List<Node>
        {
            // Ramo 1: vede il player → attaccalo o inseguilo
            new Sequence(new List<Node>
            {
                new CanSeePlayer(transform, player, detectionRange),
                new Selector(new List<Node>
                {
                    // albero legacy (usato solo da Enemy_old.prefab): i parametri del
                    // ciclo a commit sono derivati dai suoi due campi storici
                    new AttackPlayer(agent, transform, player, enemyAnimator,
                                     attackRange, rotationSpeed: 10f, aimTolerance: 25f,
                                     strikeDistance: attackRange * 0.9f,
                                     spacingDistance: attackRange * 1.6f,
                                     commitMin: attackCooldown, commitMax: attackCooldown * 1.7f,
                                     retreatDuration: 0.7f, closingTimeout: 2.5f),
                    new ChasePlayer(agent, player, 1.2f)
                })
            }),

            // Ramo 2: player troppo lontano → torna allo spawn e riallineati
            new Sequence(new List<Node>
            {
                new PlayerTooFar(transform, player, abandonRange),
                new ReturnToSpawn(agent, spawnPosition, spawnRotation),
                new RotateToSpawn(agent, spawnRotation)
            }),

            // Fallback: sta fermo
            new Idle(agent)
        });
    }
}

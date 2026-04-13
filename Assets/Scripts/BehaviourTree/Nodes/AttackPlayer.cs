using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: attacca il player se è nel raggio d'attacco
    // Ritorna Failure se il player è fuori range (→ ChasePlayer prende il controllo)
    // Ritorna Running mentre attacca
    public class AttackPlayer : Node
    {
        private NavMeshAgent agent;
        private Transform enemy;
        private Transform player;
        private EnemyAnimator enemyAnimator;
        private float attackRange;
        private float damage;
        private float cooldown;
        private float lastAttackTime = -999f;

        public AttackPlayer(NavMeshAgent agent, Transform enemy, Transform player, EnemyAnimator enemyAnimator, float attackRange, float damage, float cooldown)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.enemyAnimator = enemyAnimator;
            this.attackRange = attackRange;
            this.damage = damage;
            this.cooldown = cooldown;
        }

        public override NodeState Evaluate()
        {
            float distance = Vector3.Distance(enemy.position, player.position);

            if (distance > attackRange)
                return NodeState.Failure;

            agent.ResetPath();

            if (Time.time - lastAttackTime >= cooldown && !(enemyAnimator?.IsBeingHit() ?? false))
            {
                lastAttackTime = Time.time;
                enemyAnimator?.TriggerAttack();

                Health health = player.GetComponent<Health>();
                if (health != null)
                {
                    health.TakeDamage(damage);
                    Debug.Log($"[AttackPlayer] Colpito player per {damage} danni. Vita rimasta: {health.CurrentHealth}");
                }
            }

            return NodeState.Running;
        }
    }
}

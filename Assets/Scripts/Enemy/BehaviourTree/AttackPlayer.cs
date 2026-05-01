using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: attacca il player se è nel raggio d'attacco
    // Il danno è gestito da PunchHitbox tramite animation event
    public class AttackPlayer : Node
    {
        private NavMeshAgent agent;
        private Transform enemy;
        private Transform player;
        private EnemyAnimator enemyAnimator;
        private float attackRange;
        private float cooldown;
        private float lastAttackTime = -999f;
        private float rotationSpeed;

        public AttackPlayer(NavMeshAgent agent, Transform enemy, Transform player, EnemyAnimator enemyAnimator, float attackRange, float cooldown, float rotationSpeed = 8f)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.enemyAnimator = enemyAnimator;
            this.attackRange = attackRange;
            this.cooldown = cooldown;
            this.rotationSpeed = rotationSpeed;
        }

        public override NodeState Evaluate()
        {
            float distance = Vector3.Distance(enemy.position, player.position);

            if (distance > attackRange)
                return NodeState.Failure;

            agent.ResetPath();

            // Ruota sempre verso il player durante l'attacco
            Vector3 dir = (player.position - enemy.position).normalized;
            dir.y = 0f;
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                enemy.rotation = Quaternion.Slerp(enemy.rotation, targetRot, Time.deltaTime * rotationSpeed);
            }

            if (Time.time - lastAttackTime >= cooldown && !(enemyAnimator?.IsBeingHit() ?? false))
            {
                lastAttackTime = Time.time;
                enemyAnimator?.TriggerAttack();
            }

            return NodeState.Running;
        }
    }
}

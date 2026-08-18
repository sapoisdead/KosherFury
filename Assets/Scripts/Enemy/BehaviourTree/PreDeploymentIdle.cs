using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: prima di essere chiamata in difesa, la guardia se ne sta ferma
    // rivolta verso Vana (schiena al player) e non reagisce a nulla — nessun
    // rilevamento, nessun attacco, per costruzione: non e' un raggio di
    // rilevamento a zero, e' l'assenza di qualunque nodo che guardi il player.
    // Ritorna sempre Running.
    public class PreDeploymentIdle : Node
    {
        private readonly NavMeshAgent agent;
        private readonly Transform enemy;
        private readonly Transform vana;
        private readonly EnemyAnimator enemyAnimator;
        private readonly float turnSpeed;

        private bool confirmedPraising;

        public PreDeploymentIdle(NavMeshAgent agent, Transform enemy, Transform vana, EnemyAnimator enemyAnimator, float turnSpeed)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.vana = vana;
            this.enemyAnimator = enemyAnimator;
            this.turnSpeed = turnSpeed;
        }

        public override NodeState Evaluate()
        {
            if (agent.hasPath) agent.ResetPath();

            // Ripete il trigger finche' non si vede DAVVERO lo stato Praise, non
            // solo una volta: chiamare SetTrigger nello stesso frame in cui
            // l'Animator si abilita (prima che lo state machine abbia
            // inizializzato lo stato di default) puo' perderlo in silenzio. Una
            // volta confermato smette di chiamarlo, costa nulla nel frattempo.
            if (!confirmedPraising && enemyAnimator != null)
            {
                if (enemyAnimator.IsPraising())
                {
                    confirmedPraising = true;

                    // Disarma il trigger rimasto acceso dalle chiamate fatte
                    // durante la fusione: senza questo restava armato e faceva
                    // reinginocchiare la guardia in pieno combattimento, appena
                    // rientrava in Idle sul proprio slot (vedi ResetPraise).
                    enemyAnimator.ResetPraise();
                }
                else
                {
                    enemyAnimator.TriggerPraise();
                }
            }

            Vector3 lookDir = vana.position - enemy.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.0001f)
            {
                enemy.rotation = Quaternion.RotateTowards(
                    enemy.rotation, Quaternion.LookRotation(lookDir.normalized, Vector3.up), turnSpeed * Time.deltaTime);
            }

            return NodeState.Running;
        }
    }
}

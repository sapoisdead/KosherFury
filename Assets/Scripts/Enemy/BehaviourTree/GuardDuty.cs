using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: tiene la guardia ferma al proprio posto FISSO nella formazione
    // attorno a Vana (vedi GuardianFormation: nessuna guardia insegue il
    // player, e' lui che deve arrivare a tiro di una di loro), e attacca quando
    // il player entra a tiro. Stesse cautele di SimpShieldMode di prima
    // (avoidance disattivata, self-heal se l'agent non e' ancora sulla NavMesh,
    // arrivo con margine stretto). Ritorna SEMPRE Failure prima del
    // dispiegamento e dopo il rilascio: e' cosi' che il Selector che la
    // contiene cade sul ramo di combattimento normale in entrambi i casi, senza
    // bisogno di due rami diversi per "non ancora chiamata" e "rilasciata".
    public class GuardDuty : Node
    {
        private readonly GuardianBT self;
        private readonly NavMeshAgent agent;
        private readonly Transform enemy;
        private readonly Transform player;
        private readonly EnemyAnimator enemyAnimator;
        private readonly CombatState combatState;
        private readonly float attackRange;
        private readonly float arriveDistance;
        private readonly float returnDistance;
        private readonly float turnSpeed;
        private readonly float moveSpeed;

        private bool inPosition;
        private bool hasCommandedTarget;
        private bool attackTriggered;
        private Vector3 lastCommandedTarget;

        public GuardDuty(GuardianBT self, NavMeshAgent agent, Transform enemy, Transform player,
                         EnemyAnimator enemyAnimator, CombatState combatState,
                         float attackRange, float arriveDistance, float returnDistance,
                         float turnSpeed, float moveSpeed)
        {
            this.self = self;
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.enemyAnimator = enemyAnimator;
            this.combatState = combatState;
            this.attackRange = attackRange;
            this.arriveDistance = arriveDistance;
            this.returnDistance = returnDistance;
            this.turnSpeed = turnSpeed;
            this.moveSpeed = moveSpeed;
        }

        public override NodeState Evaluate()
        {
            if (!GuardianFormation.TryGetAssignment(self, out Vector3 target, out Quaternion facing))
            {
                inPosition = false;
                hasCommandedTarget = false;
                return NodeState.Failure;
            }

            // Stordita: FERMA dov'e', non torna al proprio slot. Senza questo la
            // guardia camminava verso il posto mentre girava la posa di stun, e
            // la finestra di punizione non si leggeva come un bersaglio fermo.
            // Il posto lo riprende da sola appena lo stordimento passa.
            if (enemyAnimator != null && enemyAnimator.IsBeingHit())
            {
                if (agent.isOnNavMesh)
                {
                    if (agent.hasPath) agent.ResetPath();
                    agent.velocity = Vector3.zero;
                }
                hasCommandedTarget = false;   // al risveglio ridà la destinazione
                return NodeState.Running;
            }

            agent.speed = moveSpeed;

            // ridai destinazione solo se lo slot voluto e' cambiato (la
            // formazione si e' ridistribuita perche' una guardia e' morta): non
            // spammare SetDestination allo stesso punto ogni frame
            if (!hasCommandedTarget || Vector3.Distance(lastCommandedTarget, target) > 0.05f)
            {
                if (agent.isOnNavMesh) agent.SetDestination(target);
                lastCommandedTarget = target;
                hasCommandedTarget = true;
                inPosition = false;
                agent.updateRotation = true;   // durante il tragitto guarda dove va
            }

            // Autoriparazione: l'agent puo' non essere ancora agganciato alla
            // NavMesh quando arriva la prima SetDestination (es. appena
            // instanziato), che allora fallisce zitta.
            //
            // Vale SOLO durante il tragitto. All'arrivo il nodo fa ResetPath, e
            // senza il vincolo su !inPosition questa riga rimetteva la
            // destinazione il frame successivo: la guardia restava a inseguire
            // in eterno un punto a pochi centimetri, con picchi di 0.8 m/s sul
            // posto. Il parametro Speed non scendeva mai sotto la soglia di 0.1,
            // quindi l'Animator restava in Locomotion e i piedi continuavano a
            // camminare pur senza spostarsi.
            if (!inPosition && agent.isOnNavMesh && !agent.hasPath && !agent.pathPending)
                agent.SetDestination(target);

            if (!inPosition && agent.isOnNavMesh)
            {
                Vector3 here = enemy.position;
                Vector3 there = target;
                here.y = there.y = 0f;

                if (Vector3.Distance(here, there) <= arriveDistance)
                {
                    agent.ResetPath();
                    agent.velocity = Vector3.zero;   // o l'inerzia residua tiene su Speed
                    agent.updateRotation = false;
                    inPosition = true;
                }
            }
            // Rientro con isteresi. Arrivata sul posto la guardia non ridava piu'
            // la destinazione: spinta via dalle compagne, dal knockback o da un
            // colpo, restava dove l'avevano lasciata e l'anello si deformava.
            // La soglia di rientro e' piu' larga di arriveDistance apposta: con
            // una sola soglia la guardia oscillerebbe dentro e fuori, che e' il
            // modo in cui era gia' tornata la camminata sul posto.
            else if (inPosition && agent.isOnNavMesh)
            {
                Vector3 here = enemy.position;
                Vector3 there = target;
                here.y = there.y = 0f;

                if (Vector3.Distance(here, there) > returnDistance)
                {
                    inPosition = false;
                    hasCommandedTarget = false;   // cosi' il blocco sopra ridà la destinazione
                }
            }

            // Postazione fissa: una volta arrivata guarda sempre nella stessa
            // direzione, verso l'esterno del semicerchio (di spalle a Vana), MAI
            // verso il player — e' lui che deve venire a tiro di lei, non il
            // contrario. Mentre e' ancora in cammino ci pensa l'agent (sopra).
            if (inPosition)
            {
                enemy.rotation = Quaternion.RotateTowards(enemy.rotation, facing, turnSpeed * Time.deltaTime);
            }

            // Attacca SOLO da ferma, cioe' una volta presa la postazione. Prima
            // questo blocco girava anche durante il tragitto: a una guardia il
            // cui percorso passava vicino al player il ResetPath qui sotto
            // annullava il viaggio, poi l'autoriparazione lo faceva ripartire, e
            // il frame dopo veniva annullato di nuovo. Era il motivo per cui
            // certe guardie non arrivavano mai al proprio posto.
            bool isAttacking = combatState != null && combatState.IsAttacking;
            if (isAttacking) attackTriggered = false;

            if (inPosition && !isAttacking && !attackTriggered
                && Vector3.Distance(enemy.position, player.position) <= attackRange)
            {
                agent.ResetPath();   // si ferma di netto per il colpo, niente scivolata a meta' swing
                enemyAnimator.TriggerAttack();
                // Fra il trigger e l'ingresso nello stato c'e' la fusione, e li'
                // IsAttacking e' ancora falso: senza questo il trigger veniva
                // riarmato a ogni frame di quella finestra e restava carico per
                // un attacco in piu' a vuoto (stesso difetto gia' visto su Praise).
                attackTriggered = true;
            }

            return NodeState.Running;
        }
    }
}

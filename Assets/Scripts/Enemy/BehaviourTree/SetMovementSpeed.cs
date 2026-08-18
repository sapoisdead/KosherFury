using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: decide se il minion corre o cammina, e ritorna sempre Success cosi'
    // puo' stare in testa a una Sequence senza alterarne l'esito.
    //
    // La corsa serve solo a colmare la distanza: una volta entrato nel raggio di
    // combattimento il minion passa a passo di camminata, perche' li' si muove di
    // lato e all'indietro con le clip direzionali, che sono cicli a velocita' di
    // camminata. Farlo strafare a velocita' di corsa gli farebbe slittare i piedi.
    //
    // Le soglie di ingresso e di uscita dalla corsa sono diverse (isteresi): con una
    // soglia sola, un minion fermo sul bordo alternerebbe corsa e camminata a ogni
    // frame, e il blend tree sfarfallerebbe fra Run e le clip di camminata.
    public class SetMovementSpeed : Node
    {
        private NavMeshAgent agent;
        private Transform enemy;
        private Transform player;
        private float walkSpeed;
        private float runSpeed;
        private float combatRange;
        private float hysteresis;
        private bool isRunning = true;

        public SetMovementSpeed(NavMeshAgent agent, Transform enemy, Transform player,
                                float walkSpeed, float runSpeed, float combatRange, float hysteresis = 1f)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.walkSpeed = walkSpeed;
            this.runSpeed = runSpeed;
            this.combatRange = combatRange;
            this.hysteresis = hysteresis;
        }

        // Variante a velocita' fissa, per i rami che non hanno un bersaglio (rientro allo spawn)
        public SetMovementSpeed(NavMeshAgent agent, float speed)
        {
            this.agent = agent;
            this.runSpeed = speed;
            this.walkSpeed = speed;
        }

        public override NodeState Evaluate()
        {
            if (player == null || enemy == null)
            {
                agent.speed = runSpeed;
                return NodeState.Success;
            }

            float distance = Vector3.Distance(enemy.position, player.position);

            if (isRunning && distance < combatRange)
                isRunning = false;
            else if (!isRunning && distance > combatRange + hysteresis)
                isRunning = true;

            agent.speed = isRunning ? runSpeed : walkSpeed;
            return NodeState.Success;
        }
    }
}

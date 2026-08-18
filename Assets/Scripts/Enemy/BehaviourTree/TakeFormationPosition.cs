using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: si registra come "impegnato" col player e si muove verso uno slot
    // di formazione assegnato dal MinionCoordinator, a distanza di combattimento
    // ma senza accalcarsi con gli altri minion.
    //
    // La rotazione del NavMeshAgent viene disattivata: il minion si muove verso la
    // sua posizione ma resta sempre rivolto verso il player (strafe), altrimenti
    // gli darebbe le spalle mentre si riposiziona.
    //
    // Ritorna sempre Running.
    public class TakeFormationPosition : Node
    {
        private NavMeshAgent agent;
        private Transform enemy;
        private Transform player;
        private float standoffRadius;
        private float formationArc;
        private float rotationSpeed;

        public TakeFormationPosition(NavMeshAgent agent, Transform enemy, Transform player,
                                     float standoffRadius, float formationArc = 140f, float rotationSpeed = 10f)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.standoffRadius = standoffRadius;
            this.formationArc = formationArc;
            this.rotationSpeed = rotationSpeed;
        }

        public override NodeState Evaluate()
        {
            MinionCoordinator.Register(enemy);

            Vector3 offset = MinionCoordinator.GetFormationOffset(enemy, standoffRadius, player, formationArc);

            agent.updateRotation = false;   
            agent.stoppingDistance = 0.15f;
            agent.SetDestination(player.position + offset);

            FaceTarget.Rotate(enemy, player, rotationSpeed);

            return NodeState.Running;
        }
    }
}

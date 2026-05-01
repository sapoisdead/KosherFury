using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: insegue il player tramite NavMeshAgent
    public class ChasePlayer : Node
    {
        private NavMeshAgent agent;
        private Transform player;
        private float stoppingDistance;

        public ChasePlayer(NavMeshAgent agent, Transform player, float stoppingDistance = 1.5f)
        {
            this.agent = agent;
            this.player = player;
            this.stoppingDistance = stoppingDistance;
        }

        public override NodeState Evaluate()
        {
            agent.updateRotation = true;
            agent.stoppingDistance = stoppingDistance;
            agent.SetDestination(player.position);
            return NodeState.Running;
        }
    }
}

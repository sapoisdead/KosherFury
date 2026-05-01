using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: torna alla posizione di spawn
    // Ritorna Running finché non è arrivato, poi Success
    public class ReturnToSpawn : Node
    {
        private NavMeshAgent agent;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public ReturnToSpawn(NavMeshAgent agent, Vector3 spawnPosition, Quaternion spawnRotation)
        {
            this.agent = agent;
            this.spawnPosition = spawnPosition;
            this.spawnRotation = spawnRotation;
        }

        public override NodeState Evaluate()
        {
            agent.SetDestination(spawnPosition);

            if (agent.pathPending)
                return NodeState.Running;

            if (agent.remainingDistance > agent.stoppingDistance + 0.1f)
                return NodeState.Running;

            agent.ResetPath();
            return NodeState.Success;
        }
    }
}

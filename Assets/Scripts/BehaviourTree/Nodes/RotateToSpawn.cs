using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: ruota il nemico verso la rotazione di spawn
    // Ritorna Running finché non è allineato, poi Success
    public class RotateToSpawn : Node
    {
        private NavMeshAgent agent;
        private Quaternion spawnRotation;
        private const float RotationSpeed = 180f;
        private const float RotationThreshold = 1f;

        public RotateToSpawn(NavMeshAgent agent, Quaternion spawnRotation)
        {
            this.agent = agent;
            this.spawnRotation = spawnRotation;
        }

        public override NodeState Evaluate()
        {
            agent.updateRotation = false;

            float angle = Quaternion.Angle(agent.transform.rotation, spawnRotation);
            if (angle <= RotationThreshold)
            {
                agent.transform.rotation = spawnRotation;
                return NodeState.Success;
            }

            agent.transform.rotation = Quaternion.RotateTowards(
                agent.transform.rotation,
                spawnRotation,
                RotationSpeed * Time.deltaTime
            );
            return NodeState.Running;
        }
    }
}

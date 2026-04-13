using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: il nemico sta fermo, non fa nulla
    // Ritorna sempre Running — è il fallback del Selector
    public class Idle : Node
    {
        private NavMeshAgent agent;

        public Idle(NavMeshAgent agent)
        {
            this.agent = agent;
        }

        public override NodeState Evaluate()
        {
            if (agent.hasPath)
                agent.ResetPath();

            return NodeState.Running;
        }
    }
}

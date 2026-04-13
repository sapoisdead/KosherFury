using System.Collections.Generic;

namespace BehaviourTree
{
    // Ritorna Success se ALMENO UN figlio ritorna Success
    // Ritorna Failure se TUTTI i figli ritornano Failure
    public class Selector : Composite
    {
        public Selector(List<Node> children) : base(children) { }

        public override NodeState Evaluate()
        {
            foreach (Node child in children)
            {
                NodeState result = child.Evaluate();

                if (result == NodeState.Success) return NodeState.Success;
                if (result == NodeState.Running)  return NodeState.Running;
            }

            return NodeState.Failure;
        }
    }
}

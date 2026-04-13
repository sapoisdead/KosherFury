using System.Collections.Generic;

namespace BehaviourTree
{
    // Ritorna Success solo se TUTTI i figli ritornano Success
    // Si ferma e ritorna Failure al primo figlio che fallisce
    public class Sequence : Composite
    {
        public Sequence(List<Node> children) : base(children) { }

        public override NodeState Evaluate()
        {
            foreach (Node child in children)
            {
                NodeState result = child.Evaluate();

                if (result == NodeState.Failure) return NodeState.Failure;
                if (result == NodeState.Running)  return NodeState.Running;
            }

            return NodeState.Success;
        }
    }
}

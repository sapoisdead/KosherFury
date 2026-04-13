namespace BehaviourTree
{
    public enum NodeState { Success, Failure, Running }

    public abstract class Node
    {
        protected NodeState state;

        public abstract NodeState Evaluate();
    }
}

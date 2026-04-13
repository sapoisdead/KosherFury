using System.Collections.Generic;

namespace BehaviourTree
{
    // Classe base per i nodi che hanno figli
    public abstract class Composite : Node
    {
        protected List<Node> children = new List<Node>();

        public Composite(List<Node> children)
        {
            this.children = children;
        }
    }
}

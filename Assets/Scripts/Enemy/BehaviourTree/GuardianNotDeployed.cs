namespace BehaviourTree
{
    // Condition: successo finche' GuardianFormation non ha ancora dispiegato le
    // guardie. Serve a limitare il ramo di combattimento "da simp normale" (con
    // raggio di rilevamento) al solo periodo PRIMA del dispiegamento: una volta
    // dispiegate (in guardia o gia' rilasciate), questo nodo fallisce sempre e il
    // Selector prova gli altri rami.
    public class GuardianNotDeployed : Node
    {
        public override NodeState Evaluate()
        {
            return GuardianFormation.IsDeployed ? NodeState.Failure : NodeState.Success;
        }
    }
}

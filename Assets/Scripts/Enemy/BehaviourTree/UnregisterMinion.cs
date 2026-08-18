using UnityEngine;

namespace BehaviourTree
{
    // Action: rimuove il nemico dal coordinamento dei minion (formazione + slot
    // d'attacco). Ritorna sempre Success, va messo prima di ReturnToSpawn.
    public class UnregisterMinion : Node
    {
        private Transform enemy;

        public UnregisterMinion(Transform enemy)
        {
            this.enemy = enemy;
        }

        public override NodeState Evaluate()
        {
            MinionCoordinator.Unregister(enemy);
            return NodeState.Success;
        }
    }
}

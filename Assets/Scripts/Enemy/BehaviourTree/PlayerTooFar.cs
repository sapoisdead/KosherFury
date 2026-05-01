using UnityEngine;

namespace BehaviourTree
{
    // Condition: ritorna Success se il player è oltre abandonRange dal nemico
    public class PlayerTooFar : Node
    {
        private Transform enemy;
        private Transform player;
        private float abandonRange;

        public PlayerTooFar(Transform enemy, Transform player, float abandonRange)
        {
            this.enemy = enemy;
            this.player = player;
            this.abandonRange = abandonRange;
        }

        public override NodeState Evaluate()
        {
            float distance = Vector3.Distance(enemy.position, player.position);
            return distance > abandonRange ? NodeState.Success : NodeState.Failure;
        }
    }
}

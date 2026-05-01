using UnityEngine;

namespace BehaviourTree
{
    // Condition: ritorna Success se il nemico vede il player (raycast)
    // Rimane Success per alertDuration secondi dopo l'ultimo avvistamento
    public class CanSeePlayer : Node
    {
        private Transform enemy;
        private Transform player;
        private float detectionRange;
        private float fieldOfView;
        private float eyeHeight;
        private float alertDuration;
        private float lastSeenTime = -999f;
        private bool alerted = false;

        public CanSeePlayer(Transform enemy, Transform player, float detectionRange, float fieldOfView = 90f, float eyeHeight = 1.5f, float alertDuration = 3f)
        {
            this.enemy = enemy;
            this.player = player;
            this.detectionRange = detectionRange;
            this.fieldOfView = fieldOfView;
            this.eyeHeight = eyeHeight;
            this.alertDuration = alertDuration;
        }

        public override NodeState Evaluate()
        {
            if (CanSee())
            {
                lastSeenTime = Time.time;
                alerted = true;
                return NodeState.Success;
            }

            if (alerted && Time.time - lastSeenTime <= alertDuration)
                return NodeState.Success;

            // Alert scaduto: resetta lo stato
            alerted = false;
            return NodeState.Failure;
        }

        private bool CanSee()
        {
            Vector3 eyePosition = enemy.position + Vector3.up * eyeHeight;
            Vector3 playerCenter = player.position + Vector3.up * eyeHeight;
            Vector3 direction = playerCenter - eyePosition;

            if (direction.magnitude > detectionRange)
                return false;

            // Se già in combat usa FOV 360°, altrimenti il FOV normale
            float effectiveFov = alerted ? 360f : fieldOfView;
            float angle = Vector3.Angle(enemy.forward, direction);
            if (angle > effectiveFov * 0.5f)
                return false;

            if (Physics.Raycast(eyePosition, direction.normalized, out RaycastHit hit, detectionRange))
                return hit.transform == player || hit.transform.IsChildOf(player);

            return false;
        }
    }
}

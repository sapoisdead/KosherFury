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
        private Health health;

        public CanSeePlayer(Transform enemy, Transform player, float detectionRange, float fieldOfView = 90f,
                            float eyeHeight = 1.5f, float alertDuration = 3f, Health health = null, bool startAlerted = false)
        {
            this.enemy = enemy;
            this.player = player;
            this.detectionRange = detectionRange;
            this.fieldOfView = fieldOfView;
            this.eyeHeight = eyeHeight;
            this.alertDuration = alertDuration;
            this.health = health;

            // per un minion accompagnato fino al suo punto d'ingresso (vedi
            // MinionEntrance): e' stato chiamato apposta per raggiungere il
            // player, non deve "scoprirlo" da zero con FOV/raycast — che vicino
            // alla folla di guardiani/altri simp puo' restare bloccato per
            // qualche frame e lasciarlo fermo senza motivo apparente.
            if (startAlerted)
            {
                alerted = true;
                lastSeenTime = Time.time;
            }
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

        // Sotto questa distanza il player e' considerato visto senza raycast: a
        // contatto il raggio parte gia' dentro al suo collider e non registra
        // nessun urto, facendo "sparire" il player proprio mentre lo si attacca.
        private const float TouchRange = 2f;

        private bool CanSee()
        {
            // Prendere un colpo vale come avvistamento, indipendentemente da dove sta
            // guardando: chi ti colpisce alle spalle non ha bisogno di essere visto per
            // essere ingaggiato. Il primo Success alza alerted, che porta il FOV a 360,
            // quindi da li' in poi lo insegue con la logica normale.
            if (health != null && Time.time - health.LastDamagedTime <= alertDuration)
                return true;

            Vector3 eyePosition = enemy.position + Vector3.up * eyeHeight;
            Vector3 playerCenter = player.position + Vector3.up * eyeHeight;
            Vector3 direction = playerCenter - eyePosition;

            float distance = direction.magnitude;

            if (distance > detectionRange)
                return false;

            // Se già in combat usa FOV 360°, altrimenti il FOV normale
            float effectiveFov = alerted ? 360f : fieldOfView;
            float angle = Vector3.Angle(enemy.forward, direction);
            if (angle > effectiveFov * 0.5f)
                return false;

            // corpo a corpo: niente raycast, e' addosso
            if (distance <= TouchRange)
                return true;

            if (Physics.Raycast(eyePosition, direction.normalized, out RaycastHit hit, detectionRange))
                return hit.transform == player || hit.transform.IsChildOf(player);

            return false;
        }
    }
}

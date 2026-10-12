using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Attacco speciale di DemonVale: quando il player e' troppo lontano per un
    // attacco normale ma abbastanza vicino da poter essere raggiunto in un balzo,
    // il demone chiude la distanza con Leap_attack invece di corrergli dietro
    // e poi ingaggiare come Vale. E' l'unica differenza di fondo fra i due alberi.
    //
    // Lo spostamento e' guidato via codice (agent.Move), non da root motion: su
    // questo rig Generic Mecanim non estrae root motion dalla clip (hasRootMotion
    // resta false anche con XZ sbloccato e motion node impostato — verificato,
    // non e' un problema risolvibile lato import). Muovere esplicitamente per la
    // durata del balzo e' piu' fragile solo sulla distanza (da tarare a occhio),
    // ma e' affidabile: non dipende da un'estrazione che qui non funziona.
    //
    // Il movimento parte all'evento OnLeapLaunch sulla clip, non al trigger: c'e'
    // un caricamento prima dello stacco che non deve spostare il personaggio.
    // OnLeapLand (se presente) chiude subito lo spostamento in anticipo sul timer.
    public class LeapAttack : Node
    {
        private enum Phase { Ready, Aiming, Leaping }

        private readonly NavMeshAgent agent;
        private readonly Transform enemy;
        private readonly Transform player;
        private readonly DemonValeAnimator enemyAnimator;
        private readonly CombatState combat;

        private readonly float minRange;
        private readonly float maxRange;
        private readonly float aimTolerance;
        private readonly float rotationSpeed;
        private readonly float cooldown;
        private readonly float travelDistance;
        private readonly float travelDuration;
        private readonly float arcHeight;

        private Phase phase = Phase.Ready;
        private float nextAvailableTime;
        private bool leapStarted;
        private float phaseEnteredAt;
        private Vector3 leapDirection;
        private float distanceCovered;
        private bool launched;
        private float launchedAt;
        private float effectiveTravelDistance;

        public LeapAttack(NavMeshAgent agent, Transform enemy, Transform player, DemonValeAnimator enemyAnimator,
                           float minRange, float maxRange, float aimTolerance, float rotationSpeed, float cooldown,
                           float travelDistance = 2.5f, float travelDuration = 1.2f, float arcHeight = 0.6f)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.enemyAnimator = enemyAnimator;
            this.combat = enemy.GetComponent<CombatState>();

            this.minRange = minRange;
            this.maxRange = maxRange;
            this.aimTolerance = aimTolerance;
            this.rotationSpeed = rotationSpeed;
            this.cooldown = cooldown;
            this.travelDistance = travelDistance;
            this.travelDuration = travelDuration;
            this.arcHeight = arcHeight;
        }

        public override NodeState Evaluate()
        {
            if (phase == Phase.Leaping)
                return ContinueLeap();

            float distance = Vector3.Distance(enemy.position, player.position);
            if (distance < minRange || distance > maxRange || Time.time < nextAvailableTime)
            {
                phase = Phase.Ready;
                return NodeState.Failure;
            }

            // Impegnato a girarsi verso il player: da qui non si torna indietro,
            // altrimenti un demone quasi allineato non partirebbe mai (rivaluterebbe
            // range/cooldown da capo a ogni frame invece di finire la rotazione).
            phase = Phase.Aiming;

            agent.updateRotation = false;
            float misalignment = FaceTarget.Rotate(enemy, player, rotationSpeed);
            if (misalignment > aimTolerance)
                return NodeState.Running;

            StartLeap();
            return NodeState.Running;
        }

        private void StartLeap()
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.velocity = Vector3.zero;
            agent.updatePosition = true; // ci muoviamo con agent.Move(), non con root motion

            leapDirection = enemy.forward;
            leapDirection.y = 0f;
            leapDirection.Normalize();
            distanceCovered = 0f;
            launched = false;

            enemyAnimator.TriggerLeapAttack();
            phase = Phase.Leaping;
            phaseEnteredAt = Time.time;
            leapStarted = false;
        }

        private NodeState ContinueLeap()
        {
            bool attacking = combat != null && combat.IsAttacking;
            if (attacking) leapStarted = true;

            if (!launched)
            {
                // Il caricamento prima dello stacco non deve spostare il personaggio:
                // si aspetta l'evento sulla clip (OnLeapLaunch), non il trigger.
                // Margine di sicurezza largo se l'evento non arriva mai (vedi nota
                // su IsPraising/ResetPraise in EnemyAnimator: stessa classe di
                // problema), cosi' non resta bloccato in eterno ad aspettare.
                bool launchTimeout = Time.time - phaseEnteredAt > 1.2f;
                if (!enemyAnimator.LeapLaunched && !launchTimeout)
                    return NodeState.Running;

                launched = true;
                launchedAt = Time.time;

                // NON si adatta alla distanza reale dal player: la clip e'
                // coreografata (gambe, corpo) per uno spostamento pieno di
                // travelDistance, fisso. Accorciare solo lo spostamento del
                // Transform mentre la posa continua a saltare per intero produce
                // un disallineamento fra il corpo visivo e la posizione reale,
                // che si vede come un teletrasporto in avanti a fine animazione.
                // Il range di innesco (leapMinRange/leapMaxRange su DemonValeBT)
                // e' la leva giusta per evitare di scavalcare il player: va tenuto
                // stretto intorno alla distanza per cui l'animazione e' pensata.
                effectiveTravelDistance = travelDistance;
            }

            float elapsed = Time.time - launchedAt;

            // Distribuisce lo spostamento sulla durata del balzo invece di scattare
            // subito: ease-out semplice, rallenta verso la fine come un vero atterraggio.
            float t = Mathf.Clamp01(elapsed / travelDuration);

            // L'evento di atterraggio (se arriva prima che il timer finisca) chiude
            // subito lo spostamento, invece di lasciarlo proseguire dopo che i piedi
            // hanno gia' toccato terra nella clip.
            if (enemyAnimator.LeapLanded) t = 1f;

            float eased = 1f - (1f - t) * (1f - t);
            float targetCovered = effectiveTravelDistance * eased;
            float step = targetCovered - distanceCovered;

            if (step > 0f)
            {
                if (agent.isOnNavMesh) agent.Move(leapDirection * step);
                else enemy.position += leapDirection * step;
                distanceCovered = targetCovered;
            }

            // Arco verticale sopra lo spostamento orizzontale: agent.Move() tiene
            // gia' l'agent incollato alla superficie NavMesh per X/Z, qui si
            // aggiunge solo un'elevazione cosmetica in Y (parabola, picco a meta'
            // balzo) senza toccare la logica di navigazione. Va riapplicata ogni
            // frame perche' il prossimo agent.Move() ricampiona l'altezza dal
            // terreno e cancella l'offset del frame precedente da solo.
            if (arcHeight > 0f)
            {
                float arc = Mathf.Sin(Mathf.PI * t) * arcHeight;
                var p = enemy.position;
                p.y += arc;
                enemy.position = p;
            }

            // Margine di sicurezza complessivo se l'animazione non esce mai dallo
            // stato di attacco (trigger mangiato dopo il lancio).
            bool safetyTimeout = !leapStarted && elapsed > 0.5f;

            // Finisce quando lo spostamento e' completo E l'animazione e' uscita
            // dallo stato di attacco: aspettare solo l'uno o solo l'altro
            // lascerebbe l'animazione a meta' o il movimento tagliato.
            bool travelDone = t >= 1f;

            if ((leapStarted && !attacking && travelDone) || safetyTimeout)
            {
                EndLeap();
                return NodeState.Success;
            }

            return NodeState.Running;
        }

        private void EndLeap()
        {
            phase = Phase.Ready;
            nextAvailableTime = Time.time + cooldown;
        }
    }
}

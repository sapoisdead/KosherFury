using UnityEngine;
using UnityEngine.AI;

namespace BehaviourTree
{
    // Action: gestisce il minion che ha ottenuto uno slot d'attacco.
    //
    // Non decide "sono in portata? allora colpisco" a ogni frame: quello lo teneva
    // incollato al player, e lo faceva partire dal limite estremo della sua portata,
    // dove il colpo non arriva. Qui l'attacco e' un IMPEGNO in quattro tempi:
    //
    //   Spacing    tiene una distanza fuori dal tiro del player, in attesa del suo
    //              momento. E' lo stato di riposo: non e' addosso, ma minaccia.
    //   Closing    ha deciso di attaccare: entra fino alla distanza di colpo. Non
    //              torna indietro finche' non ha colpito o non e' stato interrotto.
    //   Striking   il colpo e' partito. Sta fermo e lo lascia finire.
    //   Retreating esce dalla portata del player e ricarica il prossimo impegno.
    //
    // L'avvicinamento fa parte dell'attacco: e' cosi' che il colpo arriva a segno
    // per costruzione, invece che per fortuna.
    //
    // Il simp e' surclassato in portata (arriva a ~0.90 m, il player a ~1.10 m):
    // per colpire DEVE entrare nella zona in cui puo' essere colpito. La finestra
    // di rischio e' voluta, ed e' il momento in cui il player puo' punirlo.
    public class AttackPlayer : Node
    {
        private enum Mode { Spacing, Closing, Striking, Retreating, Evading }

        private readonly NavMeshAgent agent;
        private readonly Transform enemy;
        private readonly Transform player;
        private readonly EnemyAnimator enemyAnimator;
        private readonly CombatState combat;

        private readonly float attackRange;     // entro cui il colpo arriva davvero
        private readonly float strikeDistance;  // distanza a cui punta quando entra
        private readonly float spacingDistance; // distanza di riposo, fuori dal tiro del player
        private readonly float rotationSpeed;
        private readonly float aimTolerance;
        private readonly float commitMin, commitMax;
        private readonly float retreatDuration;
        private readonly float closingTimeout;
        private readonly int maxChain;

        // --- reattivita' al player (pezzo 3) ---
        private readonly CombatState playerCombat;
        private readonly float reactionDelay;    // randomizzato per istanza
        private readonly float evadeChance;
        private readonly float evadeDuration;
        private readonly float playerReach;
        private readonly float punishCooldown;

        private AttackPhase rawPlayerPhase = AttackPhase.None;
        private AttackPhase seenPlayerPhase = AttackPhase.None;   // cio' che il simp "sa"
        private float phaseChangedAt;
        private int rolledForAttackId = -1;
        private bool evadeThisAttack;
        private float nextPunishTime;

        private Mode mode = Mode.Spacing;
        private float nextCommitTime;
        private float modeEnteredAt;
        private int chainsLeft;
        private bool strikeStarted;

        public AttackPlayer(NavMeshAgent agent, Transform enemy, Transform player, EnemyAnimator enemyAnimator,
                            float attackRange, float rotationSpeed, float aimTolerance,
                            float strikeDistance, float spacingDistance,
                            float commitMin, float commitMax, float retreatDuration, float closingTimeout,
                            int maxChain = 3,
                            float reactionDelayMin = 0.16f, float reactionDelayMax = 0.26f,
                            float evadeChance = 0f, float evadeDuration = 0.35f,
                            float playerReach = 1.15f, float punishCooldown = 1.2f)
        {
            this.agent = agent;
            this.enemy = enemy;
            this.player = player;
            this.enemyAnimator = enemyAnimator;
            this.combat = enemy.GetComponent<CombatState>();

            this.attackRange = attackRange;
            this.strikeDistance = strikeDistance;
            this.spacingDistance = spacingDistance;
            this.rotationSpeed = rotationSpeed;
            this.aimTolerance = aimTolerance;
            this.commitMin = commitMin;
            this.commitMax = commitMax;
            this.retreatDuration = retreatDuration;
            this.closingTimeout = closingTimeout;
            this.maxChain = Mathf.Max(1, maxChain);

            this.playerCombat = player.GetComponent<CombatState>();
            // Ogni minion ha il PROPRIO tempo di reazione: se fosse identico per tutti,
            // un gruppo schiverebbe e punirebbe allo stesso istante, e si vedrebbe.
            this.reactionDelay = Random.Range(reactionDelayMin, reactionDelayMax);
            this.evadeChance = evadeChance;
            this.evadeDuration = evadeDuration;
            this.playerReach = playerReach;
            this.punishCooldown = punishCooldown;

            ScheduleNextCommit();
        }

        public override NodeState Evaluate()
        {
            MinionCoordinator.Register(enemy);

            // Lo slot va chiesto PRIMA del controllo di distanza: e' lo slot che da'
            // il diritto di avvicinarsi, altrimenti nessuno arriverebbe mai a tiro.
            if (!MinionCoordinator.TryAcquireAttackSlot(enemy))
            {
                mode = Mode.Spacing;
                return NodeState.Failure;
            }

            float distance = Vector3.Distance(enemy.position, player.position);

            // player sparito dall'ingaggio: molla lo slot e lascialo a un altro
            if (distance > spacingDistance * 3f)
            {
                MinionCoordinator.ReleaseAttackSlot(enemy);
                mode = Mode.Spacing;
                return NodeState.Failure;
            }

            agent.updateRotation = false;   // la rotazione la gestiamo noi, verso il player
            float misalignment = FaceTarget.Rotate(enemy, player, rotationSpeed);

            UpdatePerception();

            // Essere colpiti annulla l'impegno in corso: senza questo un simp
            // interrotto a meta' resterebbe convinto di stare ancora attaccando.
            // Il controllo esclude anche Retreating: lo stordimento dura piu' frame, e
            // senza questo rientrava nel modo a ogni frame azzerando il cronometro,
            // lasciando il simp ad arretrare all'infinito finche' era stordito.
            if (enemyAnimator != null && enemyAnimator.IsBeingHit()
                && mode != Mode.Striking && mode != Mode.Retreating)
                EnterMode(Mode.Retreating);

            switch (mode)
            {
                case Mode.Spacing:    Spacing(distance);              break;
                case Mode.Closing:    Closing(distance, misalignment); break;
                case Mode.Striking:   Striking(distance, misalignment); break;
                case Mode.Retreating: Retreating(distance);           break;
                case Mode.Evading:    Evading();                      break;
            }

            return NodeState.Running;
        }

        // ------------------------------------------------------------------ modi

        // Reazioni allo stato del player. Vengono valutate dai modi NON impegnati
        // (Spacing e Retreating): una volta partito il colpo il simp si e' impegnato
        // e non deve piu' cambiare idea, o non colpirebbe mai.
        //
        // Stavano dentro Spacing, ma con l'attesa fra impegni scesa a 0.25-0.6s il
        // simp ci transita appena, e le reazioni non scattavano quasi mai.
        private bool TryReact(float distance)
        {
            // Il player sta caricando un colpo e siamo a portata: arretra invece di
            // restare fermo a incassare.
            //
            // Tenuta spenta (evadeChance = 0): lo startup degli attacchi del player
            // dura 0.10-0.20s, meno del ritardo di reazione del simp, quindi il
            // caricamento e' gia' finito quando il simp se ne accorge. Il ramo resta
            // qui pronto per quando gli attacchi del player saranno piu' telegrafati.
            if (evadeChance > 0f
                && seenPlayerPhase == AttackPhase.Startup && distance <= playerReach && EvadeThisAttack())
            {
                EnterMode(Mode.Evading);
                return true;
            }

            // il player ha appena speso un colpo ed e' scoperto: salta l'attesa e
            // attacca subito. E' la punizione del recovery.
            if (seenPlayerPhase == AttackPhase.Recovery && Time.time >= nextPunishTime)
            {
                nextPunishTime = Time.time + punishCooldown;
                chainsLeft = Random.Range(1, maxChain + 1);
                EnterMode(Mode.Closing);
                return true;
            }

            return false;
        }

        private void Spacing(float distance)
        {
            if (TryReact(distance)) return;

            HoldDistance(spacingDistance);

            if (Time.time >= nextCommitTime)
            {
                // Quanti colpi prova a concatenare in questo impegno. Casuale: una
                // raffica sempre della stessa lunghezza si impara a memoria in due
                // scambi, e non c'e' piu' niente da leggere.
                chainsLeft = Random.Range(1, maxChain + 1);
                EnterMode(Mode.Closing);
            }
        }

        private void Closing(float distance, float misalignment)
        {
            MoveTo(strikeDistance);

            // Chiude fino a strikeDistance prima di colpire, invece di far partire il
            // colpo appena entra in attackRange: attackRange e' il limite oltre cui il
            // colpo NON arriva, non la distanza a cui conviene tirarlo. Partendo dal
            // limite estremo il colpo manca in continuazione, che era il problema.
            bool inRange = distance <= Mathf.Min(strikeDistance + 0.05f, attackRange);
            bool aimed   = misalignment <= aimTolerance;

            if (inRange && aimed)
            {
                enemyAnimator?.TriggerAttack();
                chainsLeft--;
                strikeStarted = false;
                EnterMode(Mode.Striking);
                return;
            }

            // Se non riesce a chiudere (player che scappa, strada bloccata) rinuncia
            // invece di inseguire all'infinito: rinunciare e riprovare si legge molto
            // meglio di un inseguimento ostinato.
            if (Time.time - modeEnteredAt > closingTimeout)
                EnterMode(Mode.Retreating);
        }

        private void Striking(float distance, float misalignment)
        {
            // Stordito a meta' del proprio colpo: FERMO. E' il caso della parata —
            // il colpo viene parato proprio mentre si e' qui — e Striking e'
            // escluso dal passaggio forzato a Retreating, quindi senza questo
            // controllo il simp continuava ad avanzare con l'incalzare della
            // raffica mentre era gia' stordito e punibile.
            if (enemyAnimator != null && enemyAnimator.IsBeingHit())
            {
                StopMoving();
                return;
            }

            // Se ha ancora colpi da concatenare AVANZA anche durante il proprio colpo,
            // invece di restare fermo: la spinta allontana il bersaglio di ~0.12 m a
            // ogni impatto, e senza recuperarla il colpo successivo partirebbe sempre
            // fuori portata. E' l'incalzare che rende la raffica una raffica.
            if (chainsLeft > 0)
                MoveTo(strikeDistance);
            else
                agent.ResetPath();

            bool attacking = combat != null && combat.IsAttacking;

            // Aspetta che l'attacco sia DAVVERO partito prima di considerarlo finito.
            // Fra il trigger e l'ingresso nello stato passa la fusione dell'Animator,
            // e in quella finestra IsAttacking e' ancora falso: uscire subito faceva
            // rilanciare il trigger a vuoto, spezzando la raffica invece di concatenarla.
            if (attacking)
            {
                strikeStarted = true;
                TryChainNow(distance, misalignment);
                return;
            }

            // margine di sicurezza se l'attacco non parte proprio (trigger mangiato)
            if (!strikeStarted && Time.time - modeEnteredAt < 0.5f) return;

            // Colpo finito: se ha ancora colpi da concatenare li tira di seguito,
            // senza tornare a distanza. E' quello che trasforma un colpo isolato in
            // una raffica, e obbliga il player a difendersi invece che a scansarsi.
            if (chainsLeft > 0)
                EnterMode(Mode.Closing);
            else
                EnterMode(Mode.Retreating);
        }

        // Fa partire il colpo successivo mentre il precedente e' ancora in corso, non
        // appena finito. Il momento e' la fase di RECOVERY: la finestra attiva si e'
        // gia' chiusa, quindi il colpo in corso ha gia' fatto il suo danno, e il
        // successivo puo' innestarsi senza rubarglielo.
        //
        // Serve perche' aspettare la fine dell'animazione lasciava oltre un secondo di
        // buco fra un colpo e l'altro: le transizioni rapide fra attacchi non venivano
        // mai usate, perche' il trigger arrivava quando lo stato era gia' uscito.
        private void TryChainNow(float distance, float misalignment)
        {
            if (chainsLeft <= 0) return;
            if (combat == null || !combat.IsPunishable) return;

            // tolleranza piu' larga che in Closing: fra un colpo e l'altro il bersaglio
            // arretra di poco per la spinta, e non vale la pena spezzare la raffica
            if (distance > Mathf.Min(strikeDistance + 0.2f, attackRange)) return;
            if (misalignment > aimTolerance) return;

            enemyAnimator?.TriggerAttack();
            chainsLeft--;
            strikeStarted = false;
            EnterMode(Mode.Striking);
        }

        private void Retreating(float distance)
        {
            // Stordito: FERMO, non arretra. Il controllo va PRIMA di MoveTo, o
            // l'ordine inverso faceva arretrare comunque a ogni frame — la posa
            // di stordimento girava mentre il simp scivolava all'indietro, e la
            // finestra di punizione non si leggeva come un bersaglio inchiodato.
            if (enemyAnimator != null && enemyAnimator.IsBeingHit())
            {
                StopMoving();
                return;
            }

            MoveTo(spacingDistance);

            // la ritirata e' interrompibile da una reazione: se il player si scopre
            // proprio adesso, non ha senso finire di arretrare
            if (TryReact(distance)) return;

            if (Time.time - modeEnteredAt > retreatDuration)
            {
                // Fine dell'impegno: lo slot torna disponibile. E' questo che fa girare
                // il turno d'attacco fra i minion. Prima lo si teneva finche' si era in
                // ingaggio, quindi attaccava sempre e solo lo stesso e gli altri si
                // limitavano a orbitare: l'unico modo di liberarlo era ucciderlo.
                MinionCoordinator.ReleaseAttackSlot(enemy);

                ScheduleNextCommit();
                EnterMode(Mode.Spacing);
            }
        }

        // Passo indietro fuori dalla portata del player. Dura poco: e' una reazione
        // a un colpo specifico, non una fuga. Alla fine torna in Spacing, dove il
        // player sara' con ogni probabilita' in recovery e quindi punibile.
        private void Evading()
        {
            MoveTo(playerReach + 0.35f);

            if (Time.time - modeEnteredAt > evadeDuration)
                EnterMode(Mode.Spacing);
        }

        // --------------------------------------------------------- percezione

        // Il simp non legge lo stato del player istantaneamente: lo "vede" solo dopo
        // il proprio tempo di reazione. Senza questo ritardo schiverebbe e punirebbe
        // sul frame esatto, cosa che si legge come imbroglio piu' che come bravura.
        private void UpdatePerception()
        {
            AttackPhase actual = playerCombat != null ? playerCombat.Phase : AttackPhase.None;

            if (actual != rawPlayerPhase)
            {
                rawPlayerPhase = actual;
                phaseChangedAt = Time.time;
            }

            if (Time.time - phaseChangedAt >= reactionDelay)
                seenPlayerPhase = rawPlayerPhase;
        }

        // Decide una sola volta per colpo del player se schivarlo, e ricorda la
        // decisione: interrogando il caso a ogni frame, su decine di frame di startup
        // la probabilita' di schivare diventerebbe di fatto certezza.
        private bool EvadeThisAttack()
        {
            if (playerCombat == null) return false;

            if (rolledForAttackId != playerCombat.AttackId)
            {
                rolledForAttackId = playerCombat.AttackId;
                evadeThisAttack = Random.value < evadeChance;
            }

            return evadeThisAttack;
        }

        // ------------------------------------------------------------- movimento

        // Punta un punto sull'anello alla distanza voluta, invece del centro del
        // player: puntare al centro lo faceva finire addosso, con la distanza finale
        // decisa dal caso.
        // Inchioda il minion dov'e'. Non si usa agent.isStopped perche' HitStop e
        // Knockback lo salvano e ripristinano: scriverlo da qui puo' lasciarlo
        // congelato per sempre se i due si sovrappongono. Azzerare percorso e
        // velocita' ottiene lo stesso effetto senza contendere quel flag.
        private void StopMoving()
        {
            if (!agent.isOnNavMesh) return;
            if (agent.hasPath) agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        private void MoveTo(float ringRadius)
        {
            Vector3 fromPlayer = enemy.position - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.sqrMagnitude < 0.01f) fromPlayer = -player.forward;
            fromPlayer.Normalize();

            agent.stoppingDistance = 0.05f;
            agent.SetDestination(player.position + fromPlayer * ringRadius);
        }

        // Come MoveTo, ma con una fascia morta: senza, il minion correggerebbe di
        // continuo di pochi centimetri e vibrerebbe sul posto.
        private void HoldDistance(float ringRadius)
        {
            float distance = Vector3.Distance(enemy.position, player.position);
            if (Mathf.Abs(distance - ringRadius) < 0.15f)
            {
                agent.ResetPath();
                return;
            }
            MoveTo(ringRadius);
        }

        // ---------------------------------------------------------------- utility

        private void EnterMode(Mode next)
        {
            mode = next;
            modeEnteredAt = Time.time;
        }

        // Intervallo casuale fra un impegno e l'altro: se fosse fisso, piu' simp
        // attaccherebbero a cadenza sincronizzata e la cosa si leggerebbe subito.
        private void ScheduleNextCommit()
        {
            nextCommitTime = Time.time + Random.Range(commitMin, commitMax);
        }
    }
}

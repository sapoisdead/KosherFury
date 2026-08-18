using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BehaviourTree;

// Albero delle guardie speciali di Vana, tre stati impliciti nell'ordine dei
// rami del Selector (nessuno stato da tracciare a parte):
//
//   1. GuardDuty: tiene la formazione a scudo. Attiva SOLO fra il dispiegamento
//      e il PROPRIO rilascio (ritorna Failure fuori da quella finestra, vedi
//      GuardDuty). Il rilascio e' graduale e per-guardia, non collettivo: una
//      puo' essere gia' all'attacco mentre le compagne tengono ancora il posto.
//   2. Prima del dispiegamento: ferma, rivolta verso Vana, cieca al player per
//      costruzione (nessun nodo qui guarda il player, non e' un raggio a zero:
//      vedi PreDeploymentIdle). Nessuna reazione possibile finche' Vana non le
//      chiama in difesa.
//   3. Dopo il rilascio: stesso ramo Attacca-o-aspetta-il-turno di ValerioBT,
//      senza raggio di rilevamento ne' ritorno allo spawn: una volta rilasciate
//      non mollano mai, per costruzione (nessun nodo che possa farle desistere).
public class GuardianBT : BehaviourTreeBase
{
    [Header("Difesa (mentre e' a scudo attorno a Vana)")]
    [Tooltip("Entro questa distanza dal player attacca invece di limitarsi a bloccare.")]
    [SerializeField] private float attackRangeWhileGuarding = 1.15f;
    [Tooltip("Entro quanto dal proprio slot si considera arrivata.")]
    [SerializeField] private float arriveDistance = 0.15f;
    [Tooltip("Velocita' di rotazione (gradi/secondo) mentre guarda il player mantenendo la formazione.")]
    [SerializeField] private float turnSpeed = 480f;
    [Tooltip("Velocita' durante la corsa verso il proprio posto e mentre fa da bloccante.")]
    [SerializeField] private float moveSpeedWhileGuarding = 24f;
    [Tooltip("Moltiplicatore sul danno inflitto mentre e' a scudo.")]
    [SerializeField] private float guardDamageMultiplier = 6f;
    [Tooltip("Moltiplicatore sul knockback subito mentre e' a scudo. Vicino a 0: senza, il player la spingerebbe via a furia di colpi.")]
    [SerializeField, Range(0f, 1f)] private float guardKnockbackMultiplier = 0f;

    [Header("Rilascio (fase 2: aggressiva, danno ancora piu' alto)")]
    [Tooltip("Moltiplicatore sul danno una volta rilasciata: piu' alto di guardDamageMultiplier, e' la fase 2 della bossfight.")]
    [SerializeField] private float releasedDamageMultiplier = 12f;
    [Tooltip("Velocita' di spostamento una volta rilasciata. moveSpeedWhileGuarding (24) serve solo a far chiudere lo scudo in fretta al dispiegamento: senza questo campo restava impostata anche in fase 2, e le guardie inseguivano il player a 2.5x la sua corsa, con le gambe fuori sincrono (le soglie dei blend tree sono in m/s reali). Tenuta sotto la corsa del player (9.6) perche' resti possibile disimpegnarsi.")]
    [SerializeField] private float releasedMoveSpeed = 8f;

    [Header("Attacco (usato sia prima del dispiegamento che dopo il rilascio)")]
    [Tooltip("Entro questa distanza il colpo arriva davvero. Misurato sul pugno della guardia: connette fino a ~1.15-1.20 m, non piu' a 1.25.")]
    [SerializeField] private float attackRange = 1.05f;
    [Tooltip("Distanza a cui punta quando decide di attaccare. Poco sopra il minimo fisico fra i due corpi (0.80 m: capsula + collider del player).")]
    [SerializeField] private float strikeDistance = 1.0f;
    [Tooltip("Distanza di riposo fra un attacco e l'altro. Deve restare sopra la portata del player (1.05-1.10 m), o si fa colpire gratis mentre aspetta.")]
    [SerializeField] private float spacingDistance = 1.4f;
    [Tooltip("Attesa fra un impegno e il successivo, molto piu' corta di un simp normale: e' quello che le rende 'estremamente aggressive'.")]
    [SerializeField] private float commitIntervalMin = 0.2f;
    [Tooltip("Estremo alto dell'attesa fra un impegno e il successivo. L'intervallo e' casuale fra min e max, o piu' guardie attaccherebbero a cadenza sincronizzata.")]
    [SerializeField] private float commitIntervalMax = 0.5f;
    [Tooltip("Quanto resta arretrata dopo aver colpito, prima di poter ricominciare.")]
    [SerializeField] private float retreatDuration = 0.2f;
    [Tooltip("Se non riesce a chiudere la distanza entro questo tempo, rinuncia all'impegno e riprova piu' tardi.")]
    [SerializeField] private float closingTimeout = 2.5f;
    [Tooltip("Massimo numero di colpi concatenati in un singolo impegno. Ogni volta ne sceglie a caso da 1 a questo: una raffica di lunghezza fissa si impara a memoria. Piu' alto di un simp normale (3).")]
    [SerializeField] private int maxChain = 4;
    [Tooltip("Quanto puo' essere disallineata rispetto al player per attaccare comunque (gradi). Piu' basso = piu' precisa, ma attacca meno spesso.")]
    [SerializeField] private float aimTolerance = 25f;
    [Tooltip("Velocita' con cui si gira verso il player durante il combattimento (non mentre tiene la formazione: li' vale turnSpeed).")]
    [SerializeField] private float rotationSpeed = 12f;
    [Tooltip("Ritardo di reazione molto piu' basso di un simp normale: quasi nessun tempo di lettura per il player.")]
    [SerializeField] private float reactionDelayMin = 0.05f;
    [Tooltip("Estremo alto del ritardo di reazione. Ogni guardia ne pesca uno suo in questo intervallo, o il gruppo reagirebbe all'unisono.")]
    [SerializeField] private float reactionDelayMax = 0.12f;
    [Tooltip("Portata dei colpi del player, misurata: 1.05-1.10 m. E' la distanza sotto la quale conviene arretrare per non farsi punire.")]
    [SerializeField] private float playerReach = 1.15f;
    [Tooltip("Intervallo minimo fra due punizioni, piu' corto di un simp normale.")]
    [SerializeField] private float punishCooldown = 0.6f;

    [Header("Posizionamento (mentre aspetta il turno d'attacco da rilasciata)")]
    [Tooltip("Distanza a cui si tiene dal player mentre aspetta il proprio turno d'attacco (vedi MinionCoordinator: solo alcune attaccano insieme).")]
    [SerializeField] private float formationStandoff = 1.5f;
    [Tooltip("Ampiezza dell'arco davanti al player entro cui le guardie in attesa si dispongono (gradi). 360 = tutto intorno.")]
    [SerializeField] private float formationArc = 140f;

    private NavMeshAgent agent;
    private EnemyAnimator enemyAnimator;
    private Health health;
    private Knockback knockback;
    private CombatState combatState;
    private Transform player;
    private Transform vana;
    private ObstacleAvoidanceType normalAvoidance;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        health = GetComponent<Health>();
        knockback = GetComponent<Knockback>();

        var vanaBoss = Object.FindFirstObjectByType<VanaBoss>();
        if (vanaBoss != null) vana = vanaBoss.transform;

        combatState = GetComponent<CombatState>();
        if (combatState == null) combatState = gameObject.AddComponent<CombatState>();

        normalAvoidance = agent.obstacleAvoidanceType;

        GuardianFormation.Register(this);
    }

    private void OnDisable()
    {
        // Health.Die() disabilita questo componente: e' cosi' che una guardia
        // morta esce dal roster e la formazione si stringe sulle sopravvissute.
        MinionCoordinator.Unregister(transform);
        GuardianFormation.Unregister(this);
    }

    protected override Node SetupTree()
    {
        agent.stoppingDistance = 0f;
        player = PlayerManager.Instance.PlayerTransform;

        // usati solo dopo il rilascio (fase 2): stesso identico ramo
        // Attacca-o-aspetta-il-turno di ValerioBT, riusato as-is
        var attack = new AttackPlayer(agent, transform, player, enemyAnimator,
                         attackRange, rotationSpeed, aimTolerance,
                         strikeDistance, spacingDistance,
                         commitIntervalMin, commitIntervalMax, retreatDuration, closingTimeout, maxChain,
                         reactionDelayMin, reactionDelayMax,
                         evadeChance: 0f, evadeDuration: 0.35f,
                         playerReach: playerReach, punishCooldown: punishCooldown);
        var waitTurn = new TakeFormationPosition(agent, transform, player, formationStandoff, formationArc, rotationSpeed);
        var combatOrWait = new Selector(new List<Node> { attack, waitTurn });

        return new Selector(new List<Node>
        {
            new GuardDuty(this, agent, transform, player, enemyAnimator, combatState,
                          attackRangeWhileGuarding, arriveDistance, turnSpeed, moveSpeedWhileGuarding),

            new Sequence(new List<Node>
            {
                new GuardianNotDeployed(),
                new PreDeploymentIdle(agent, transform, vana, enemyAnimator, turnSpeed)
            }),

            // dispiegata e rilasciata (o GuardDuty fallita per qualunque altro
            // motivo a dispiegamento avvenuto): aggressiva senza limiti
            combatOrWait,

            new Idle(agent)
        });
    }

    // Chiamato da GuardianFormation.Deploy() per ogni guardia gia' registrata.
    public void EnterGuardDuty()
    {
        // Seconda rete sul trigger di Praise: PreDeploymentIdle lo disarma
        // quando conferma la posa, ma se il dispiegamento arriva proprio mentre
        // la fusione Idle->Praise e' ancora in corso quella conferma non avviene
        // mai, e il trigger resterebbe armato fino al primo rientro in Idle —
        // cioe' in pieno combattimento. Da qui in poi la posa non serve piu'.
        if (enemyAnimator != null) enemyAnimator.ResetPraise();

        if (enemyAnimator != null) enemyAnimator.DamageMultiplier = guardDamageMultiplier;
        if (knockback != null) knockback.ForceMultiplier = guardKnockbackMultiplier;

        // incassa il danno ma non flincha: un'interruzione romperebbe la
        // postazione fissa che deve tenere. Il danno normale resta invariato,
        // salta solo Stagger() (vedi Health.IsUnstaggerable).
        if (health != null) health.IsUnstaggerable = true;

        // l'obstacle avoidance fra NavMeshAgent userebbe il raggio dell'agent
        // (piu' grande della capsule collider) e terrebbe le guardie separate
        // piu' del voluto, riaprendo i varchi che la formazione deve chiudere.
        if (agent != null) agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }

    // Chiamato da GuardianFormation quando tocca a QUESTA guardia passare
    // all'attacco: il rilascio e' graduale, non tutte insieme (vedi BeginRelease).
    public void ReleaseToAggressive()
    {
        if (enemyAnimator != null) enemyAnimator.DamageMultiplier = releasedDamageMultiplier;
        if (knockback != null) knockback.ForceMultiplier = 1f;
        if (agent != null) agent.obstacleAvoidanceType = normalAvoidance;

        // Da qui in poi GuardDuty non gira piu' (ritorna Failure a rilascio
        // avvenuto) e nessun altro nodo di questo albero tocca agent.speed:
        // se non la si riportasse qui, resterebbe a moveSpeedWhileGuarding.
        if (agent != null) agent.speed = releasedMoveSpeed;

        // rilasciata: torna interrompibile come qualunque altro combattente
        if (health != null) health.IsUnstaggerable = false;
    }
}

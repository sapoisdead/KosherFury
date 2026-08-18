using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using BehaviourTree;

public class ValerioBT : BehaviourTreeBase
{
    [Header("Ingaggio")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float abandonRange = 10f;

    [Header("Attacco")]
    [Tooltip("Entro questa distanza il colpo arriva davvero. Misurato: il colpo piu' corto in rotazione tocca il player fino a 0.99 m, ma dal vivo regge fino a 0.90.")]
    [SerializeField] private float attackRange = 0.9f;
    [Tooltip("Distanza a cui punta quando decide di attaccare. Poco sopra il minimo fisico (0.80 m: capsula del simp + collider del player).")]
    [SerializeField] private float strikeDistance = 0.85f;
    [Tooltip("Distanza di riposo fra un attacco e l'altro. Deve restare sopra 1.10 m, che e' la portata del player: sotto, si fa colpire gratis.")]
    [SerializeField] private float spacingDistance = 1.3f;
    [Tooltip("Attesa minima e massima fra un impegno e il successivo. Casuale, o piu' simp attaccherebbero a cadenza sincronizzata.")]
    [SerializeField] private float commitIntervalMin = 0.6f;
    [SerializeField] private float commitIntervalMax = 1.4f;
    [Tooltip("Quanto resta arretrato dopo aver colpito, prima di poter ricominciare.")]
    [SerializeField] private float retreatDuration = 0.35f;
    [Tooltip("Se non riesce a chiudere la distanza entro questo tempo, rinuncia e riprova piu' tardi.")]
    [SerializeField] private float closingTimeout = 2.5f;
    [Tooltip("Massimo numero di colpi concatenati in un singolo impegno. Ogni volta ne sceglie a caso da 1 a questo: una raffica di lunghezza fissa si impara a memoria.")]
    [SerializeField] private int maxChain = 3;
    [Tooltip("Quanto puo' essere disallineato rispetto al player per attaccare comunque (gradi). Piu' basso = piu' preciso, ma attacca meno spesso.")]
    [SerializeField] private float aimTolerance = 25f;
    [Tooltip("Velocita' con cui si gira verso il player.")]
    [SerializeField] private float rotationSpeed = 10f;
    [Tooltip("Distanza minima dal player: piu' vicino di cosi' arretra invece di restargli addosso.")]
    [SerializeField] private float minDistance = 0.7f;

    [Header("Velocita'")]
    [Tooltip("Velocita' con cui corre per raggiungere il player.")]
    [SerializeField] private float runSpeed = 4.2f;
    [Tooltip("Velocita' in combattimento, quando si muove nelle 4 direzioni restando rivolto al player. Deve combaciare con le soglie del blend tree Locomotion2D, o i piedi slittano.")]
    [SerializeField] private float walkSpeed = 1.8f;
    [Tooltip("Entro questa distanza dal player passa da corsa a camminata.")]
    [SerializeField] private float combatRange = 3f;
    [Tooltip("Margine oltre combatRange per tornare a correre. Evita che sul bordo alterni corsa e camminata.")]
    [SerializeField] private float speedHysteresis = 1f;

    [Header("Reattivita' al player")]
    [Tooltip("Ritardo di reazione: quanto ci mette ad accorgersi di cosa sta facendo il player. Sotto ~0.15s si legge come imbroglio. Ogni minion ne pesca uno suo in questo intervallo, o il gruppo reagirebbe all'unisono.")]
    [SerializeField] private float reactionDelayMin = 0.16f;
    [SerializeField] private float reactionDelayMax = 0.26f;
    [Tooltip("Probabilita' di arretrare quando vede il player caricare un colpo. DISATTIVATA (0): lo startup degli attacchi del player misura 0.10-0.20s, meno del ritardo di reazione, quindi il simp non fa in tempo ad accorgersene. Ha senso riattivarla solo con attacchi del player piu' telegrafati. 1 = schiva sempre, e diventa impossibile colpirlo.")]
    [Range(0f, 1f)][SerializeField] private float evadeChance = 0f;
    [Tooltip("Durata del passo indietro difensivo.")]
    [SerializeField] private float evadeDuration = 0.35f;
    [Tooltip("Portata dei colpi del player, misurata: 1.05-1.10 m. E' la distanza sotto la quale conviene arretrare.")]
    [SerializeField] private float playerReach = 1.15f;
    [Tooltip("Intervallo minimo fra due punizioni, per non incatenarle a ogni singolo colpo che tiri.")]
    [SerializeField] private float punishCooldown = 1.2f;

    [Header("Coordinamento")]
    [Tooltip("Quanti minion possono attaccare insieme. E' un valore condiviso da tutti: l'ultimo che parte lo impone.")]
    [SerializeField] private int maxSimultaneousAttackers = 2;
    [Tooltip("Quanto un minion deve lasciar passare gli altri dopo il proprio turno d'attacco.")]
    [SerializeField] private float attackSlotCooldown = 1.2f;

    [Header("Posizionamento")]
    [Tooltip("Distanza a cui si tiene dal player mentre aspetta il turno d'attacco.")]
    [SerializeField] private float formationStandoff = 1.4f;
    [Tooltip("Ampiezza dell'arco davanti al player entro cui i minion si dispongono (gradi). 360 = tutto intorno.")]
    [SerializeField] private float formationArc = 140f;

    private NavMeshAgent agent;
    private EnemyAnimator enemyAnimator;
    private Health health;
    private Transform player;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool startAlerted;

    // Chiamato da MinionEntrance all'arrivo: un minion accompagnato fin li'
    // sa gia' dov'e' il player (e' stato mandato apposta), non deve scoprirlo
    // da zero con FOV/raycast. Va chiamato PRIMA che l'albero venga costruito,
    // come SetHome.
    public void MarkAlerted() => startAlerted = true;

    // Ridefinisce il punto a cui il minion torna quando il player si allontana.
    // Serve a chi nasce altrove rispetto a dove combattera': un minion che scende
    // dalla scalinata deve considerare "casa" i piedi della scalinata, non la cima.
    //
    // Va chiamato PRIMA che l'albero venga costruito, cioe' mentre il componente e'
    // ancora disattivato: i nodi ricevono questi valori per copia alla costruzione.
    public void SetHome(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAnimator = GetComponent<EnemyAnimator>();
        health = GetComponent<Health>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
    }

    private void OnDisable()
    {
        MinionCoordinator.Unregister(transform);
    }

    protected override Node SetupTree()
    {
        agent.stoppingDistance = 0f;
        player = PlayerManager.Instance.PlayerTransform;

        MinionCoordinator.MaxAttackers = maxSimultaneousAttackers;
        MinionCoordinator.SlotCooldown = attackSlotCooldown;

        return new Selector(new List<Node>
        {
            // Ramo 1: vede il player -> attaccalo (se c'e' uno slot libero) o
            // tieni una posizione di formazione attorno a lui in attesa del turno
            new Sequence(new List<Node>
            {
                // health passato in modo che un colpo alle spalle valga come avvistamento
                new CanSeePlayer(transform, player, detectionRange, health: health, startAlerted: startAlerted),
                // corre finche' e' lontano, cammina appena entra in raggio di combattimento
                new SetMovementSpeed(agent, transform, player, walkSpeed, runSpeed, combatRange, speedHysteresis),
                new Selector(new List<Node>
                {
                    new AttackPlayer(agent, transform, player, enemyAnimator,
                                     attackRange, rotationSpeed, aimTolerance,
                                     strikeDistance, spacingDistance,
                                     commitIntervalMin, commitIntervalMax, retreatDuration, closingTimeout, maxChain,
                                     reactionDelayMin, reactionDelayMax,
                                     evadeChance, evadeDuration, playerReach, punishCooldown),
                    new TakeFormationPosition(agent, transform, player, formationStandoff, formationArc, rotationSpeed)
                })
            }),

            // Ramo 2: player troppo lontano -> molla la formazione e torna allo spawn
            new Sequence(new List<Node>
            {
                new PlayerTooFar(transform, player, abandonRange),
                new UnregisterMinion(transform),
                new SetMovementSpeed(agent, runSpeed),   // il rientro allo spawn e' un trasferimento, non combattimento
                new ReturnToSpawn(agent, spawnPosition, spawnRotation),
                new RotateToSpawn(agent, spawnRotation)
            }),

            // Fallback: sta fermo
            new Idle(agent)
        });
    }
}

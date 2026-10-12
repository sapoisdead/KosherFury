using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using BehaviourTree;

// Sulla falsa riga di ValerioBT: stessa struttura (ingaggio/formazione/rientro
// allo spawn), con in piu' il Leap_attack per chiudere la distanza. E' l'unica
// vera differenza di comportamento fra i due: vedi LeapAttack.
public class DemonValeBT : BehaviourTreeBase
{
    [Header("Ingaggio")]
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float abandonRange = 10f;

    [Header("Attacco")]
    [SerializeField] private float attackRange = 0.9f;
    [SerializeField] private float strikeDistance = 0.85f;
    [SerializeField] private float spacingDistance = 1.3f;
    [SerializeField] private float commitIntervalMin = 0.6f;
    [SerializeField] private float commitIntervalMax = 1.4f;
    [SerializeField] private float retreatDuration = 0.35f;
    [SerializeField] private float closingTimeout = 2.5f;
    [SerializeField] private int maxChain = 3;
    [SerializeField] private float aimTolerance = 25f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float minDistance = 0.7f;

    [Header("Leap attack")]
    [Tooltip("Distanza minima per considerare il leap: sotto questa soglia conviene un attacco normale. Va tenuta vicina a leapTravelDistance, non c'e' compensazione dinamica: la clip e' coreografata per uno spostamento fisso, farla scattare da piu' vicino scavalca il player (il corpo salta per intero comunque, il Transform si ferma prima e il disallineamento si vede a fine animazione).")]
    [SerializeField] private float leapMinRange = 2.2f;
    [Tooltip("Distanza massima raggiungibile dal balzo. Misurato in Blender: circa 2.5m.")]
    [SerializeField] private float leapMaxRange = 2.8f;
    [Tooltip("Intervallo minimo fra un leap e il successivo, cosi' non diventa il solo attacco che il demone usa.")]
    [SerializeField] private float leapCooldown = 5f;
    [Tooltip("Quanto si sposta durante il balzo, in metri: fisso, deve combaciare con lo spostamento coreografato nella clip (non si adatta alla distanza reale dal player). Root motion non disponibile su questo rig: lo spostamento e' guidato via codice, va tarato a occhio guardando la clip.")]
    [SerializeField] private float leapTravelDistance = 2.5f;
    [Tooltip("Durata dello spostamento, in secondi: deve corrispondere a quanto dura visivamente lo scatto in avanti nella clip Leap_attack.")]
    [SerializeField] private float leapTravelDuration = 1.2f;
    [Tooltip("Altezza dell'arco del balzo, in metri (picco a meta' balzo). 0 per uno scivolamento piatto senza salto.")]
    [SerializeField] private float leapArcHeight = 0.6f;
    [Tooltip("Tolleranza di mira PRIMA di far partire il balzo, in gradi. Va tenuta stretta: a differenza di un attacco normale (che corregge di continuo), il leap si impegna in un'unica direzione fissa, quindi un errore angolare si traduce direttamente in quanto manca il player lateralmente (a 2.5m, 25 gradi di errore = oltre 1m di scarto).")]
    [SerializeField] private float leapAimTolerance = 8f;

    [Header("Velocita'")]
    [SerializeField] private float runSpeed = 4.2f;
    [SerializeField] private float walkSpeed = 1.8f;
    [SerializeField] private float combatRange = 3f;
    [SerializeField] private float speedHysteresis = 1f;

    [Header("Reattivita' al player")]
    [SerializeField] private float reactionDelayMin = 0.16f;
    [SerializeField] private float reactionDelayMax = 0.26f;
    [Range(0f, 1f)][SerializeField] private float evadeChance = 0f;
    [SerializeField] private float evadeDuration = 0.35f;
    [SerializeField] private float playerReach = 1.15f;
    [SerializeField] private float punishCooldown = 1.2f;

    [Header("Coordinamento")]
    [SerializeField] private int maxSimultaneousAttackers = 2;
    [SerializeField] private float attackSlotCooldown = 1.2f;

    [Header("Posizionamento")]
    [SerializeField] private float formationStandoff = 1.4f;
    [SerializeField] private float formationArc = 140f;

    private NavMeshAgent agent;
    private DemonValeAnimator enemyAnimator;
    private Health health;
    private Transform player;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool startAlerted;
    private bool suppressCoordinatorOverride;

    public void MarkAlerted() => startAlerted = true;

    // Chiamato da VanaBoss quando questo DemonVale nasce per necromanzia, a
    // fight gia' in fase 2: in quel momento MinionCoordinator.MaxAttackers e'
    // gia' governato da VanaBoss.releasedMaxAttackers (piu' alto del default
    // di questo albero). Senza questo flag, SetupTree() lo sovrascriverebbe
    // silenziosamente al proprio valore piu' basso (campo static, condiviso
    // da TUTTI i minion) al frame successivo allo spawn, vanificando il buff
    // di fase 2 per il resto della bossfight. Chi istanzia un DemonVale fuori
    // dal contesto di Vana non la chiama e mantiene il comportamento di sempre.
    public void SuppressCoordinatorOverride() => suppressCoordinatorOverride = true;

    public void SetHome(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyAnimator = GetComponent<DemonValeAnimator>();
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

        if (!suppressCoordinatorOverride)
        {
            MinionCoordinator.MaxAttackers = maxSimultaneousAttackers;
            MinionCoordinator.SlotCooldown = attackSlotCooldown;
        }

        return new Selector(new List<Node>
        {
            new Sequence(new List<Node>
            {
                new CanSeePlayer(transform, player, detectionRange, health: health, startAlerted: startAlerted),
                new SetMovementSpeed(agent, transform, player, walkSpeed, runSpeed, combatRange, speedHysteresis),
                new Selector(new List<Node>
                {
                    new LeapAttack(agent, transform, player, enemyAnimator,
                                   leapMinRange, leapMaxRange, leapAimTolerance, rotationSpeed, leapCooldown,
                                   leapTravelDistance, leapTravelDuration, leapArcHeight),
                    new AttackPlayer(agent, transform, player, enemyAnimator,
                                     attackRange, rotationSpeed, aimTolerance,
                                     strikeDistance, spacingDistance,
                                     commitIntervalMin, commitIntervalMax, retreatDuration, closingTimeout, maxChain,
                                     reactionDelayMin, reactionDelayMax,
                                     evadeChance, evadeDuration, playerReach, punishCooldown),
                    new TakeFormationPosition(agent, transform, player, formationStandoff, formationArc, rotationSpeed)
                })
            }),

            new Sequence(new List<Node>
            {
                new PlayerTooFar(transform, player, abandonRange),
                new UnregisterMinion(transform),
                new SetMovementSpeed(agent, runSpeed),
                new ReturnToSpawn(agent, spawnPosition, spawnRotation),
                new RotateToSpawn(agent, spawnRotation)
            }),

            new Idle(agent)
        });
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using BehaviourTree;

// Controller della bossfight di Vana: dispiega la squadra fissa di guardie
// (GuardianFormation/GuardianBT) al primo superamento della soglia, poi tiene
// sempre circa normalSimpConcurrentCap simp normali vivi in scena (il primo
// gruppo arriva tutto insieme, i morti vengono rimpiazzati uno alla volta) fino
// a un budget cumulativo, rilascia le guardie in modalita' aggressiva a budget
// esaurito, ed esegue la sequenza finale una volta che anche le guardie sono
// morte.
//
// Le fasi "di transizione animata" (ordine di scudo, richiamo, rilascio,
// sequenza finale) sospendono prossimita' e richiami finche' non si torna a
// Idle, cosi' un evento non interrompe mai un'animazione a meta'. I due
// attacchi speciali (Cast_1 proiettile a distanza, Cast_2 radiale a terra —
// vedi SpecialAttackGate) sono l'eccezione voluta: girano anche durante
// quelle fasi (a parte il momento esatto del lancio, che comunque rispetta
// phase == Idle da dentro TryFireRangedAttack/TryFireRadialSlam), perche'
// altrimenti smetterebbero di scattare per l'intera fase 2 — vedi il commento
// su SpecialAttackGate.
//
// E' un vero Behaviour Tree (BehaviourTreeBase, come ValerioBT/GuardianBT), ma
// la logica interna resta quasi tutta quella di prima: le coroutine che
// aspettano la fine di un'animazione (WaitForStateEnd) sono delicate e gia'
// testate, riscriverle da capo come nodi non avrebbe aggiunto nulla se non
// rischio. I nodi sotto sono wrapper sottili che chiamano dentro gli stessi
// metodi privati di prima.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Animator))]
public class VanaBoss : BehaviourTreeBase
{
    private enum Phase { Idle, ShieldOrder, RecallOrder, ReleaseOrder, FinalSequence, DefeatIdle, Dead }

    [Header("Riferimenti")]
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject simpPrefab;
    [Tooltip("Le guardie speciali, gia' piazzate in scena vicino a Vana fin dall'inizio. Roster fisso: chi muore prima del rilascio non viene rimpiazzato.")]
    [SerializeField] private List<GuardianBT> guardianSimps = new();
    [Tooltip("Punti candidati per il richiamo di nuovi simp normali (pattern WaveSpawner: si campiona la NavMesh vicino al punto scelto).")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Soglia di dispiegamento")]
    [Tooltip("Entro questa distanza dal player, Vana dispiega le guardie. Scatta UNA SOLA VOLTA per tutta la bossfight: da qui in poi le guardie restano in formazione fino al rilascio scriptato, mai piu' per isteresi di distanza.")]
    [SerializeField] private float shieldTriggerDistance = 10f;

    [Header("Formazione delle guardie")]
    [Tooltip("Raggio della formazione attorno a Vana. Deve restare fuori dall'ingombro del piccolo bimah rialzato su cui sta (~1.26m dal centro all'angolo, capsula guardia 0.48m di raggio: sotto ~1.9-2.0m le guardie ci finirebbero dentro). Con 14 guardie (il roster attuale) e raggio 2.0 la corda fra slot adiacenti (vedi SlotDirection) resta sotto la somma dei due raggi capsula (0.96m): l'anello torna a chiudere i varchi anche a questo raggio piu' largo. Con meno guardie il conto non torna piu' (6 lasciava ~1m di varco): il numero minimo per sigillare a raggio 2.0 e' 13, vedi VANA_BOSSFIGHT.md.")]
    [SerializeField] private float shieldRadius = 2.0f;
    [Tooltip("Ampiezza della formazione. 360 = anello completo attorno a Vana (default, copre anche il retro): un arco piu' stretto lascia scoperto tutto il lato opposto a dove puntava il centro al momento del dispiegamento. Con un arco <360 la direzione si congela guardando verso il player in quell'istante.")]
    [SerializeField] private float shieldArcDegrees = 360f;

    [Header("Richiamo simp normali")]
    [Tooltip("Distanza dal centro di Vana, verso il player, a cui un simp normale appena richiamato punta per iniziare a combattere. Deve restare oltre shieldRadius, altrimenti punterebbe dentro l'anello delle guardie invece che davanti a loro. Senza questo, un simp sceso dalle scale finiva sempre dietro Vana (dal lato opposto a dove arriva il player): oltre a essere brutto, restava li' senza vedere nessuno.")]
    [SerializeField] private float normalSimpArrivalOffset = 3f;
    [Tooltip("Ampiezza dell'arco (gradi) su cui si distribuiscono i punti d'arrivo dei simp normali, centrato verso il player: un punto fisso unico faceva incastrare fra loro i simp richiamati in rapida successione, e contro l'anello delle guardie (che sta fermo, non fa avoidance).")]
    [SerializeField] private float normalSimpArrivalArc = 120f;
    [Tooltip("Variazione casuale di raggio oltre normalSimpArrivalOffset, stessa ragione dell'arco: disperde ulteriormente i punti d'arrivo invece di farli convergere tutti alla stessa distanza.")]
    [SerializeField] private float normalSimpArrivalSpread = 1.5f;
    [Tooltip("Quanti simp normali restano vivi in scena contemporaneamente a regime. Il primissimo richiamo (nessuno ancora vivo) ne fa arrivare subito un gruppo intero fino a questo tetto in un colpo solo; da li' in poi ogni simp che muore ne fa richiamare uno per tenersi al tetto, fino a esaurimento del budget totale sotto.")]
    [SerializeField] private int normalSimpConcurrentCap = 6;
    [Tooltip("Massimo di simp normali generati in tutta la bossfight (cumulativo, non contemporanei). Esaurito questo, i morti non vengono piu' rimpiazzati: quando anche l'ultimo muore, le guardie vengono rilasciate (fase 2).")]
    [SerializeField] private int maxTotalSpawns = 40;

    [Header("Fase 2 (guardie rilasciate)")]
    [Tooltip("Tetto di attaccanti simultanei per MinionCoordinator una volta rilasciate le guardie: piu' alto del normale (2), per farle sentire davvero opprimenti.")]
    [SerializeField] private int releasedMaxAttackers = 4;
    [Tooltip("Quante guardie passano all'attacco per ondata. Il rilascio e' graduale a ondate: ne partono queste, e la successiva parte SOLO quando sono state uccise tutte quelle dell'ondata in corso (uccidendone una non ne arriva subito un'altra). Le altre restano in formazione (immuni a flinch, knockback azzerato) e l'anello si stringe sui rimasti: lo scudo si assottiglia un'ondata per volta invece di sparire tutto insieme.")]
    [SerializeField] private int releaseBatchSize = 3;

    [Header("Attacco a distanza (Cast_1)")]
    [Tooltip("Prefab del proiettile (Combat/Projectile). Lasciare vuoto disattiva l'attacco senza toccare il resto dell'albero.")]
    [SerializeField] private Projectile projectilePrefab;
    [Tooltip("Punto di lancio, condiviso anche dall'attacco radiale (Cast_2): la sfera compare li' in entrambi i casi. Se vuoto, si usa la posizione di Vana con un offset in avanti e in alto (segnaposto finche' non c'e' un osso mano/bocca da assegnare).")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("Non spara se il player e' piu' vicino di cosi'. Deve restare oltre radialSlamMaxRange: sotto quella soglia ci pensa l'attacco radiale, i due range non devono sovrapporsi.")]
    [SerializeField] private float rangedAttackMinRange = 8f;
    [Tooltip("Non spara se il player e' piu' lontano di cosi'.")]
    [SerializeField] private float rangedAttackMaxRange = 30f;
    [Tooltip("Tempo minimo fra un lancio e il successivo.")]
    [SerializeField] private float rangedAttackCooldown = 4f;

    [Header("Attacco radiale (Cast_2)")]
    [Tooltip("Prefab della sfera 'in mano' che Vana sbatte a terra (nessuno script, solo un segnaposto visivo: VanaBoss ne controlla tutto il ciclo di vita). Lasciare vuoto disattiva l'attacco senza toccare il resto dell'albero.")]
    [SerializeField] private GameObject slamOrbPrefab;
    [Tooltip("Prefab dell'onda d'urto (Combat/RadialSlam), istanziato nel punto d'impatto a terra.")]
    [SerializeField] private RadialSlam radialSlamPrefab;
    [Tooltip("Non lo usa se il player e' piu' lontano di cosi': deve restare entro rangedAttackMinRange (default 8m su entrambi), altrimenti il cerchio non arriverebbe mai a toccarlo. Coerente col maxRadius del prefab RadialSlam.")]
    [SerializeField] private float radialSlamMaxRange = 8f;
    [Tooltip("Tempo minimo fra uno slam e il successivo.")]
    [SerializeField] private float radialSlamCooldown = 5f;

    [Header("Necromanzia (fase 2, riusa l'esatta clip/trigger di Cast_1)")]
    [Tooltip("Il DemonVale completo (DemonValeBT), gia' pronto a combattere. Lasciare vuoto disattiva la necromanzia senza toccare il resto dell'albero.")]
    [SerializeField] private DemonValeBT demonValePrefab;
    [Tooltip("Variante 'necromantica' del dardo (Combat/NecromancyBolt): stessa messa in scena di Projectile ma colore/VFX diversi e nessun danno — vola verso il cadavere invece che verso il player. Lasciare vuoto disattiva la necromanzia senza toccare il resto dell'albero.")]
    [SerializeField] private NecromancyBolt necromancyBoltPrefab;
    [Tooltip("Quanti DemonVale rievocati possono essere vivi in scena contemporaneamente. Oltre questo tetto Vana non rievoca altri cadaveri finche' uno non muore (il cadavere resta comunque in coda, verra' rievocato appena si libera uno slot).")]
    [SerializeField] private int maxConcurrentDemons = 3;

    private readonly List<ValerioBT> activeMinions = new();

    private Phase phase = Phase.Idle;
    private Transform player;
    private PlayerMovement playerMovement;
    private int totalSpawned;
    private bool budgetExhausted;
    private bool guardiansDeployed;
    // Condiviso da entrambi gli attacchi speciali: Vana non puo' mai avere
    // Cast_1 e Cast_2 in corso insieme, userebbero comunque lo stesso layer
    // dell'Animator.
    private bool castingSpecialAttack;
    private float nextRangedAttackTime;
    private bool hasRangedAttackParam;
    private Projectile pendingProjectile;
    private float nextSlamTime;
    private bool hasRadialSlamParam;
    private GameObject pendingSlamOrb;

    // Cadaveri di guardie non ancora rievocati: popolato via Health.OnDeath
    // sottoscritto in Awake (guardianSimps e' gia' valorizzato dall'Inspector
    // a quel punto, a differenza dei simp normali registrati a runtime).
    private readonly List<GuardianBT> deadGuardianCorpses = new();
    // DemonVale attualmente vivi, rievocati da Vana: serve solo per il tetto.
    private readonly List<DemonValeBT> activeDemons = new();
    // Bersaglio del cast di necromanzia in corso: letto da OnCastSpawn per
    // decidere se questo Cast_1 e' un attacco normale o una rievocazione.
    private GuardianBT pendingNecromancyTarget;
    private NecromancyBolt pendingNecromancyBolt;

    private static readonly int ShieldOrderHash = Animator.StringToHash("Shield_order");
    private static readonly int ReleaseOrderHash = Animator.StringToHash("Release_order");
    private static readonly int CallSimpHash = Animator.StringToHash("Call_simp");
    private static readonly int NoMoreSimpsHash = Animator.StringToHash("No_more_simps");
    private static readonly int DefeatIdleHash = Animator.StringToHash("Defeat_idle");
    private static readonly int RangedAttackHash = Animator.StringToHash("Ranged_attack");
    private static readonly int CastStateHash = Animator.StringToHash("Cast_1");
    private static readonly int RadialSlamHash = Animator.StringToHash("Radial_slam");
    private static readonly int Cast2StateHash = Animator.StringToHash("Cast_2");

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (animator == null) animator = GetComponent<Animator>();

        // Il danno normale e' sbloccato fin da subito (il pool enorme la rende
        // virtualmente inespugnabile per quella via): resta pero' immune alla
        // vera morte finche' non si passa in Defeat_idle, dove un colpo qualsiasi
        // la finisce all'istante indipendentemente dalla vita rimasta.
        health.AlwaysExecutable = false;
        health.OnDeath += () => phase = Phase.Dead;

        // I trigger 'Ranged_attack'/'Radial_slam' potrebbero non esistere ancora
        // nel controller (animazione non ancora collegata): senza questo
        // controllo SetTrigger stamperebbe un warning a ogni lancio finche' non
        // verra' aggiunto, stesso schema di Health.hasStunnedParam.
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == RangedAttackHash) hasRangedAttackParam = true;
            if (p.nameHash == RadialSlamHash) hasRadialSlamParam = true;
        }

        // Le guardie sono gia' in scena dall'inizio (roster fisso via
        // Inspector), non spawnate a runtime come i simp normali: la
        // sottoscrizione va fatta qui, non al momento della morte.
        // Health.OnDeath scatta prima che GuardianBT.OnDisable tolga la
        // guardia dal roster di GuardianFormation, quindi non c'e' corsa.
        foreach (var guardian in guardianSimps)
        {
            if (guardian == null) continue;
            var h = guardian.GetComponent<Health>();
            if (h != null) h.OnDeath += () => OnGuardianDeath(guardian);
        }
    }

    // BehaviourTreeBase aspetta gia' PlayerManager.Instance.PlayerTransform
    // prima di chiamare SetupTree(): qui sotto e' garantito non nullo, non
    // serve una propria coroutine di attesa come nelle versioni precedenti.
    protected override Node SetupTree()
    {
        player = PlayerManager.Instance.PlayerTransform;
        playerMovement = player.GetComponent<PlayerMovement>();

        // SpecialAttackGate avvolge tutto il resto: entrambi gli attacchi
        // devono poter scattare anche in fase 2 (guardie rilasciate), quando
        // FinalSequenceWatch ritorna Running per l'intera durata (aspetta la
        // morte dell'ultima guardia) e da solo bloccherebbe il Selector prima
        // che arrivi a valutare qualunque fratello messo li' dentro. Vedi
        // SpecialAttackGate.
        return new SpecialAttackGate(this, new Sequence(new List<Node>
        {
            new PhaseIsIdleCheck(this),
            new Selector(new List<Node>
            {
                new FinalSequenceWatch(this),
                new GuardianDeploymentWatch(this),
            })
        }));
    }

    // ---------------------------------------------------------------- nodi

    // Guardia comune a tutto il resto dell'albero: mentre Vana e' a meta' di
    // una transizione animata, nessuno degli altri nodi deve fare nulla.
    private class PhaseIsIdleCheck : Node
    {
        private readonly VanaBoss boss;
        public PhaseIsIdleCheck(VanaBoss boss) { this.boss = boss; }
        public override NodeState Evaluate() => boss.phase == Phase.Idle ? NodeState.Success : NodeState.Failure;
    }

    // Dopo il rilascio non c'e' piu' nessuna transizione da aspettare: solo
    // controllare se anche l'ultima guardia e' morta. Se non sono ancora state
    // rilasciate, lascia decidere agli altri nodi del Selector.
    private class FinalSequenceWatch : Node
    {
        private readonly VanaBoss boss;
        public FinalSequenceWatch(VanaBoss boss) { this.boss = boss; }
        public override NodeState Evaluate()
        {
            if (!GuardianFormation.ReleaseStarted) return NodeState.Failure;
            if (GuardianFormation.AliveCount == 0) boss.EnterFinalSequence();
            return NodeState.Running;
        }
    }

    // Scatta una sola volta per tutta la bossfight (UpdateProximity si
    // autoprotegge internamente a dispiegamento gia' avvenuto). Se in questo
    // stesso tick ha appena fatto scattare il dispiegamento, ferma qui il
    // Selector: il controllo sul richiamo dei simp non deve girare nello
    // stesso frame.
    private class GuardianDeploymentWatch : Node
    {
        private readonly VanaBoss boss;
        public GuardianDeploymentWatch(VanaBoss boss) { this.boss = boss; }
        public override NodeState Evaluate()
        {
            boss.UpdateProximity();
            return boss.phase != Phase.Idle ? NodeState.Running : NodeState.Failure;
        }
    }

    // Valuta il controllo di ENTRAMBI gli attacchi speciali ad OGNI frame,
    // PRIMA di delegare al figlio (il resto dell'albero, invariato) — e ne
    // ritorna il risultato cosi' com'e', senza aggiungere niente al proprio: a
    // BehaviourTreeBase.Update() non importa cosa torna root.Evaluate(), lo
    // scarta. Non e' un nodo del Selector proprio perche' li' dentro
    // FinalSequenceWatch puo' restare Running per tutta la fase 2 (aspetta la
    // morte dell'ultima guardia) e bloccherebbe in eterno qualunque fratello:
    // stando FUORI da quella catena, i lanci restano possibili sia prima del
    // dispiegamento sia dopo il rilascio delle guardie. Le condizioni vere e
    // proprie (fase Idle, guardie gia' dispiegate, range, cooldown, lock
    // condiviso) restano tutte dentro TryFireRangedAttack/TryFireRadialSlam,
    // ciascuno controlla castingSpecialAttack da solo: qui e' solo il punto
    // d'aggancio, non serve ripetere il controllo. Non serve nessuna logica di
    // scelta fra i due: i loro range sono partizionati apposta (vicino =
    // radiale, lontano = proiettile) e il lock condiviso impedisce comunque
    // che partano insieme.
    private class SpecialAttackGate : Node
    {
        private readonly VanaBoss boss;
        private readonly Node child;
        public SpecialAttackGate(VanaBoss boss, Node child) { this.boss = boss; this.child = child; }
        public override NodeState Evaluate()
        {
            boss.TryFireRangedAttack();
            boss.TryFireRadialSlam();
            // Ultimo dei tre: se piu' condizioni sono vere nello stesso
            // frame, la necromanzia cede il passo agli attacchi che
            // colpiscono davvero il player. E' un evento piu' raro e ad alto
            // impatto (necromancyCooldown alto), non deve competere per la
            // precedenza — il lock condiviso (castingSpecialAttack) impedisce
            // comunque le sovrapposizioni, l'ordine conta solo come
            // spareggio.
            boss.TryFireNecromancy();
            return child.Evaluate();
        }
    }

    // ---------------------------------------------------------------- dispiegamento

    private void UpdateProximity()
    {
        if (guardiansDeployed) return;   // scatta una sola volta per tutta la bossfight

        // orizzontale: Vana e' su un piccolo bimah rialzato, la distanza 3D
        // includerebbe il dislivello e falserebbe la soglia
        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude <= shieldTriggerDistance)
            EnterShieldOrder();
    }

    private void EnterShieldOrder()
    {
        phase = Phase.ShieldOrder;
        animator.SetTrigger(ShieldOrderHash);
        StartCoroutine(WaitForStateEnd(ShieldOrderHash, OnShieldOrderFinished));
    }

    private void OnShieldOrderFinished()
    {
        GuardianFormation.Deploy(transform, shieldRadius, shieldArcDegrees);
        guardiansDeployed = true;
        EndTransition();   // TryToProceed() vede zero simp vivi e fa scattare subito il primo richiamo
    }

    // ---------------------------------------------------------------- richiamo simp normali

    private void RegisterSimp(ValerioBT bt)
    {
        if (bt == null || activeMinions.Contains(bt)) return;
        activeMinions.Add(bt);

        var h = bt.GetComponent<Health>();
        if (h != null) h.OnDeath += () => OnSimpDeath(bt);
    }

    private void OnSimpDeath(ValerioBT dead)
    {
        activeMinions.Remove(dead);
        TryToProceed();
    }

    // Puo' scattare mentre Vana e' gia' a meta' di un'altra animazione (es. un
    // richiamo in corso per una morte precedente): in quel caso si riprova solo
    // al rientro in Idle, mai forzando un secondo trigger mentre una
    // transizione e' gia' attiva. Chiamato dopo ogni morte e dopo ogni
    // transizione animata: e' l'unico punto che decide se richiamare ancora,
    // niente piu' timer periodico — se sotto il tetto e c'e' budget, ne
    // richiama uno; ripetuto ad ogni morte tiene il conteggio vivo intorno al
    // tetto senza bisogno di un contatore di richiami "in coda".
    private void TryToProceed()
    {
        if (phase != Phase.Idle) return;

        if (budgetExhausted && activeMinions.Count == 0)
        {
            // Il rilascio avviene UNA VOLTA SOLA. Senza questo controllo,
            // rientrando in Idle a fine Release_order la diagnosi qui sopra
            // risulta di nuovo vera e si rilancia il rilascio all'infinito:
            // dato che WaitForStateEnd puo' completare in modo sincrono (vedi
            // sotto), la catena EnterReleaseOrder -> OnReleaseOrderFinished ->
            // EndTransition -> TryToProceed -> EnterReleaseOrder si richiude su
            // se stessa nello stesso stack e fa saltare Unity per stack
            // overflow, senza nemmeno lasciare un'eccezione nel log.
            if (GuardianFormation.ReleaseStarted)
            {
                // gia' rilasciate: da qui in poi decide solo FinalSequenceWatch,
                // che aspetta la morte dell'ultima guardia
                return;
            }

            // se il player e' riuscito a uccidere tutte le guardie prima ancora
            // che il budget dei simp normali finisse, non c'e' nessuno da
            // rilasciare: si salta dritto alla sequenza finale
            if (GuardianFormation.AliveCount > 0)
                EnterReleaseOrder();
            else
                EnterFinalSequence();
            return;
        }

        if (!budgetExhausted && guardiansDeployed && activeMinions.Count < normalSimpConcurrentCap)
            EnterRecallOrder();
    }

    private void EnterRecallOrder()
    {
        if (totalSpawned >= maxTotalSpawns)
        {
            budgetExhausted = true;
            return;
        }

        phase = Phase.RecallOrder;
        animator.SetTrigger(CallSimpHash);
        StartCoroutine(WaitForStateEnd(CallSimpHash, OnCallSimpFinished));
    }

    private void OnCallSimpFinished()
    {
        // il primissimo richiamo (nessun simp ancora vivo) ne fa arrivare un
        // gruppo intero fino al tetto in un colpo solo; i richiami successivi
        // (uno per ogni morte, vedi TryToProceed) ne rimpiazzano uno alla volta
        int toSpawn = totalSpawned == 0 ? Mathf.Min(normalSimpConcurrentCap, maxTotalSpawns) : 1;

        for (int i = 0; i < toSpawn && totalSpawned < maxTotalSpawns; i++)
            SpawnNewSimp();

        EndTransition();
    }

    private void SpawnNewSimp()
    {
        if (simpPrefab == null) return;

        Vector3 origin = spawnPoints != null && spawnPoints.Length > 0
            ? spawnPoints[Random.Range(0, spawnPoints.Length)].position
            : transform.position;

        Vector3 point = NavMesh.SamplePosition(origin, out var hit, 3f, NavMesh.AllAreas) ? hit.position : origin;

        Vector3 toVana = transform.position - point;
        toVana.y = 0f;
        Quaternion rot = toVana.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toVana.normalized) : Quaternion.identity;

        var simpObj = Instantiate(simpPrefab, point, rot);
        var bt = simpObj.GetComponent<ValerioBT>();

        totalSpawned++;
        if (totalSpawned >= maxTotalSpawns) budgetExhausted = true;

        RegisterSimp(bt);

        // i simp normali non vengono mai deputati a scudo in questo design: alla
        // difesa ci pensano solo le guardie speciali, sempre e solo attacco.
        // Punta oltre l'anello delle guardie, verso il player: puntare al
        // centro di Vana li faceva finire spesso dal lato opposto a dove
        // arriva il player (es. scendendo dalle scale), fermi dietro di lei
        // senza vederlo mai.
        var entrance = simpObj.AddComponent<MinionEntrance>();
        entrance.Configure(NormalSimpArrivalPoint(), 1.5f, 20f);
    }

    private Vector3 NormalSimpArrivalPoint()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        Vector3 towardPlayer = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;

        // sparso su un semicerchio davanti alle guardie, non un punto fisso
        // unico: piu' simp richiamati in rapida successione puntavano tutti
        // li' e si incastravano a vicenda e contro l'anello delle guardie.
        float angle = Random.Range(-normalSimpArrivalArc * 0.5f, normalSimpArrivalArc * 0.5f);
        Vector3 scatteredDir = Quaternion.AngleAxis(angle, Vector3.up) * towardPlayer;
        float radius = normalSimpArrivalOffset + Random.Range(0f, normalSimpArrivalSpread);
        Vector3 candidate = transform.position + scatteredDir * radius;

        // Vana e' su un piccolo bimah rialzato: 'candidate' erediterebbe la sua
        // quota (transform.position.y) invece del vero pavimento. Il punto
        // d'arrivo e' comunque oltre l'anello delle guardie, quindi fuori dal
        // bimah stesso: la superficie navigabile piu' vicina e' il pavimento vero.
        return NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas) ? hit.position : candidate;
    }

    // ---------------------------------------------------------------- rilascio guardie

    private void EnterReleaseOrder()
    {
        phase = Phase.ReleaseOrder;
        animator.SetTrigger(ReleaseOrderHash);
        StartCoroutine(WaitForStateEnd(ReleaseOrderHash, OnReleaseOrderFinished));
    }

    private void OnReleaseOrderFinished()
    {
        GuardianFormation.BeginRelease(releaseBatchSize);
        MinionCoordinator.MaxAttackers = releasedMaxAttackers;
        EndTransition();
    }

    // ---------------------------------------------------------------- attacco a distanza (Cast_1)

    // Chiamata ogni frame da SpecialAttackGate, PRIMA e indipendentemente dal
    // resto dell'albero: le condizioni che prima erano implicite nella
    // posizione nel Selector (dentro PhaseIsIdleCheck, dopo guardie non ancora
    // dispiegate) ora vanno controllate esplicitamente qui.
    //
    // - phase deve essere Idle: mentre Vana e' a meta' di una transizione
    //   scriptata (Shield/Recall/Release/Final) non deve tirare anche un
    //   trigger dell'Animator per il lancio, altrimenti i due si accavallano.
    // - guardiansDeployed: la bossfight non e' ancora davvero iniziata finche'
    //   non e' scattato l'ordine di formazione (vedi OnShieldOrderFinished) —
    //   Vana non spara prima che lo scontro sia partito sul serio.
    //
    // Non passa dalla macchina a fasi per la DURATA del lancio (Phase resta
    // Idle per tutta la coroutine): il lancio non e' una transizione scriptata
    // come Shield/Recall/Release, e farlo passare per WaitForStateEnd
    // bloccherebbe anche recall e dispiegamento per tutta la durata della
    // clip. castingSpecialAttack basta da solo a evitare lanci sovrapposti
    // (con l'attacco radiale compreso, vedi TryFireRadialSlam).
    private void TryFireRangedAttack()
    {
        if (castingSpecialAttack) return;
        if (phase != Phase.Idle) return;
        if (!guardiansDeployed) return;   // la fight non e' ancora iniziata
        if (projectilePrefab == null) return;   // attacco non ancora configurato
        if (Time.time < nextRangedAttackTime) return;

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist < rangedAttackMinRange || dist > rangedAttackMaxRange) return;

        nextRangedAttackTime = Time.time + rangedAttackCooldown;
        StartCoroutine(PerformRangedAttack());
    }

    // Spawn e lancio partono da due animation event distinti sulla clip Cast_1
    // (OnCastSpawn/OnCastRelease), non da qui: questa coroutine si limita a far
    // partire il trigger e ad aspettare che la clip finisca prima di liberare
    // castingSpecialAttack, cosi' Vana non puo' iniziare un secondo lancio (di
    // nessuno dei due tipi) a meta' del primo. Se il controller non ha ancora
    // il parametro (asset diverso o incompleto), spawna e lancia subito in
    // sequenza, senza animazione: l'attacco resta funzionante anche prima che
    // Cast_1 sia collegata.
    private IEnumerator PerformRangedAttack()
    {
        castingSpecialAttack = true;

        if (hasRangedAttackParam)
        {
            animator.SetTrigger(RangedAttackHash);
            yield return WaitForStateEnd(CastStateHash, null);
        }
        else
        {
            OnCastSpawn();
            OnCastRelease();
        }

        castingSpecialAttack = false;
    }

    // Animation event sulla clip Cast_1, al momento in cui il proiettile deve
    // COMPARIRE (stesso schema di OnRightHandStart/OnLeftHandStart in
    // CLAUDE.md: il metodo sta sullo stesso GameObject dell'Animator). Istanziato
    // come figlio del punto di lancio: resta fermo e segue la mano finche' non
    // arriva OnCastRelease, cosi' si vede comparire prima di partire davvero.
    // Il fotogramma esatto e' una stima iniziale, da verificare a occhio e
    // aggiustare in ModelImporter (tempo normalizzato, non in secondi).
    public void OnCastSpawn()
    {
        if (pendingNecromancyTarget != null)
        {
            if (necromancyBoltPrefab == null) return;

            Transform necroParent = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
            Vector3 necroOrigin = necroParent.position;

            Vector3 toCorpse = pendingNecromancyTarget.transform.position - necroOrigin;
            toCorpse.y = 0f;
            Quaternion necroRot = toCorpse.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toCorpse.normalized)
                : necroParent.rotation;

            var corpseTarget = pendingNecromancyTarget;   // copia per la closure
            pendingNecromancyBolt = Instantiate(necromancyBoltPrefab, necroOrigin, necroRot, necroParent);
            pendingNecromancyBolt.Prepare(corpseTarget.transform, () => ResurrectGuardian(corpseTarget));
            return;
        }

        if (projectilePrefab == null || player == null) return;

        Transform parent = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        Vector3 origin = parent.position;

        Vector3 toPlayer = player.position - origin;
        toPlayer.y = 0f;
        Quaternion rot = toPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toPlayer.normalized)
            : parent.rotation;

        pendingProjectile = Instantiate(projectilePrefab, origin, rot, parent);
        pendingProjectile.Prepare(player, health);
    }

    // Animation event sulla clip Cast_1, al momento in cui il proiettile PARTE
    // davvero. Se OnCastSpawn non e' mai scattato (es. tempi degli eventi da
    // rivedere) non c'e' niente da lanciare: nessun crash, il colpo semplicemente
    // non parte, ed e' un segnale chiaro che i due eventi vanno riallineati.
    public void OnCastRelease()
    {
        if (pendingNecromancyBolt != null)
        {
            pendingNecromancyBolt.Launch();
            pendingNecromancyBolt = null;
            return;
        }

        if (pendingProjectile == null) return;
        pendingProjectile.Launch();
        pendingProjectile = null;
    }

    // ---------------------------------------------------------------- attacco radiale (Cast_2)

    // Stesso schema di TryFireRangedAttack, stesso lock condiviso
    // (castingSpecialAttack): un solo controllo di distanza, nessun minimo —
    // a differenza del proiettile, non ha senso escludere il corpo a corpo per
    // questo attacco, anzi e' li' che colpisce meglio.
    private void TryFireRadialSlam()
    {
        if (castingSpecialAttack) return;
        if (phase != Phase.Idle) return;
        if (!guardiansDeployed) return;   // la fight non e' ancora iniziata
        if (slamOrbPrefab == null || radialSlamPrefab == null) return;   // attacco non ancora configurato
        if (Time.time < nextSlamTime) return;

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.magnitude > radialSlamMaxRange) return;

        nextSlamTime = Time.time + radialSlamCooldown;
        StartCoroutine(PerformRadialSlam());
    }

    // Stesso schema di PerformRangedAttack: gli animation event OnSlamSpawn/
    // OnSlamImpact sulla clip Cast_2 fanno il lavoro vero, questa coroutine
    // aspetta solo che la clip finisca prima di liberare castingSpecialAttack.
    private IEnumerator PerformRadialSlam()
    {
        castingSpecialAttack = true;

        if (hasRadialSlamParam)
        {
            animator.SetTrigger(RadialSlamHash);
            yield return WaitForStateEnd(Cast2StateHash, null);
        }
        else
        {
            OnSlamSpawn();
            OnSlamImpact();
        }

        castingSpecialAttack = false;
    }

    // Animation event sulla clip Cast_2, al momento in cui la sfera compare in
    // mano. A differenza del proiettile resta SEMPRE figlia della mano (mai
    // staccata): Vana la porta con se' fino a sbatterla a terra, non la lancia.
    public void OnSlamSpawn()
    {
        if (slamOrbPrefab == null) return;

        Transform parent = projectileSpawnPoint != null ? projectileSpawnPoint : transform;
        pendingSlamOrb = Instantiate(slamOrbPrefab, parent.position, parent.rotation, parent);
    }

    // Animation event sulla clip Cast_2, al momento dell'impatto a terra. Il
    // raycast verso il basso agganciano il punto d'origine dell'onda d'urto al
    // vero pavimento (stesso principio di NavMesh.SamplePosition usato altrove
    // in questo file): in quel fotogramma la mano potrebbe non essere
    // perfettamente a livello del suolo, e l'onda deve comunque nascere a terra.
    public void OnSlamImpact()
    {
        if (pendingSlamOrb == null || radialSlamPrefab == null) return;

        Vector3 origin = pendingSlamOrb.transform.position;
        if (Physics.Raycast(origin + Vector3.up * 0.5f, Vector3.down, out var hit, 5f))
            origin = hit.point;

        Destroy(pendingSlamOrb);
        pendingSlamOrb = null;

        RadialSlam slam = Instantiate(radialSlamPrefab, origin, Quaternion.identity);
        slam.Detonate(health);
    }

    // ---------------------------------------------------------------- necromanzia (fase 2)

    private void OnGuardianDeath(GuardianBT guardian)
    {
        if (guardian != null && !deadGuardianCorpses.Contains(guardian))
            deadGuardianCorpses.Add(guardian);
    }

    // Stesso schema di TryFireRangedAttack/TryFireRadialSlam (lock condiviso,
    // chiamata ogni frame da SpecialAttackGate), con un vincolo in piu':
    // GuardianFormation.ReleaseStarted deve essere vero (solo fase 2, vincolo
    // di design fisso, non tecnico).
    //
    // Deterministico, non probabilistico: appena una guardia muore (in fase 2)
    // viene rievocata, non e' piu' "puo' capitare o no". Nessun cooldown
    // separato — il ritmo lo detta solo quanto dura la clip Cast_1 (tramite
    // castingSpecialAttack, che impedisce due cast sovrapposti): se muoiono piu'
    // guardie vicine, Vana le rievoca una via l'altra appena libera, nell'ordine
    // in cui sono morte (FIFO), non a caso.
    private void TryFireNecromancy()
    {
        if (castingSpecialAttack) return;
        if (phase != Phase.Idle) return;
        if (!guardiansDeployed) return;
        if (!GuardianFormation.ReleaseStarted) return;
        if (demonValePrefab == null || necromancyBoltPrefab == null) return;

        activeDemons.RemoveAll(d => d == null);
        if (activeDemons.Count >= maxConcurrentDemons) return;

        deadGuardianCorpses.RemoveAll(c => c == null);
        if (deadGuardianCorpses.Count == 0) return;

        var corpse = deadGuardianCorpses[0];

        // Tolto SUBITO dal pool, non all'arrivo del dardo: castingSpecialAttack
        // si libera gia' a fine clip Cast_1, ma il dardo puo' restare in volo
        // oltre — senza questa riga lo stesso cadavere potrebbe essere
        // selezionato due volte.
        deadGuardianCorpses.RemoveAt(0);

        StartCoroutine(PerformNecromancy(corpse));
    }

    // Stesso schema esatto di PerformRangedAttack: riusa il trigger
    // 'Ranged_attack' e lo stato Cast_1 — stessa animazione, stessi
    // animation event OnCastSpawn/OnCastRelease. E' pendingNecromancyTarget
    // (letto da OnCastSpawn) a decidere se quel Cast_1 spawna un Projectile
    // normale o un NecromancyBolt: nessun nuovo parametro Animator, nessuna
    // nuova clip.
    private IEnumerator PerformNecromancy(GuardianBT corpse)
    {
        castingSpecialAttack = true;
        pendingNecromancyTarget = corpse;

        if (hasRangedAttackParam)
        {
            animator.SetTrigger(RangedAttackHash);
            yield return WaitForStateEnd(CastStateHash, null);
        }
        else
        {
            OnCastSpawn();
            OnCastRelease();
        }

        pendingNecromancyTarget = null;
        castingSpecialAttack = false;
    }

    // Chiamato dal dardo all'arrivo (NecromancyBolt callback onArrival):
    // distrugge il cadavere e istanzia il DemonVale al suo posto, gia'
    // allertato (nessun MinionEntrance: nasce esattamente dov'era il
    // cadavere).
    private void ResurrectGuardian(GuardianBT corpse)
    {
        if (corpse == null || demonValePrefab == null) return;

        Vector3 pos = corpse.transform.position;
        Quaternion rot = corpse.transform.rotation;

        Destroy(corpse.gameObject);

        var demon = Instantiate(demonValePrefab, pos, rot);
        demon.SetHome(pos, rot);
        demon.MarkAlerted();
        demon.SuppressCoordinatorOverride();

        activeDemons.Add(demon);
        var h = demon.GetComponent<Health>();
        if (h != null) h.OnDeath += () => activeDemons.Remove(demon);
    }

    // ---------------------------------------------------------------- fine bossfight

    private void EnterFinalSequence()
    {
        phase = Phase.FinalSequence;
        if (playerMovement != null) playerMovement.SetInputLocked(true);
        animator.SetTrigger(NoMoreSimpsHash);
        StartCoroutine(WaitForStateEnd(NoMoreSimpsHash, OnNoMoreSimpsFinished));
    }

    private void OnNoMoreSimpsFinished()
    {
        if (playerMovement != null) playerMovement.SetInputLocked(false);

        phase = Phase.DefeatIdle;
        animator.SetTrigger(DefeatIdleHash);

        // solo qui Vana diventa davvero colpibile: un attacco qualsiasi la
        // uccide, tramite lo stesso percorso di Hitbox.TryHit() usato per
        // l'esecuzione post-parry (vedi Health.AlwaysExecutable).
        health.IsInvincible = false;
        health.AlwaysExecutable = true;
    }

    private void EndTransition()
    {
        phase = Phase.Idle;
        TryToProceed();
    }

    // ---------------------------------------------------------------- animator

    private IEnumerator WaitForStateEnd(int stateHash, System.Action onFinished)
    {
        // Cede SEMPRE almeno un frame prima di qualunque controllo. StartCoroutine
        // esegue il primo tratto in modo sincrono, dentro lo stack del chiamante:
        // se l'Animator si trovasse gia' nello stato voluto e a fine clip,
        // entrambi i while qui sotto passerebbero senza mai cedere, e onFinished
        // verrebbe chiamato dentro EnterXxxOrder(). Ogni catena che da li' torna
        // a far partire una transizione si richiuderebbe su se stessa nello
        // stesso stack, fino allo stack overflow (che in Unity e' un crash
        // secco del processo, senza eccezione). Un frame di attesa la spezza.
        yield return null;

        // l'Animator ci mette un frame a entrare nello stato dopo il trigger
        while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != stateHash)
            yield return null;

        while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.98f)
            yield return null;

        onFinished?.Invoke();
    }

    // ---------------------------------------------------------------- gizmo

    // Sempre visibile (non solo da selezionato), per poter verificare a occhio
    // la soglia mentre si sposta il player in scena.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        DrawGizmoCircle(transform.position, shieldTriggerDistance);

        Gizmos.color = new Color(0.6f, 0.1f, 0.85f, 0.6f);
        DrawGizmoCircle(transform.position, rangedAttackMinRange);
        DrawGizmoCircle(transform.position, rangedAttackMaxRange);

        Gizmos.color = new Color(0.95f, 0.28f, 0.05f, 0.6f);
        DrawGizmoCircle(transform.position, radialSlamMaxRange);
    }

    private static void DrawGizmoCircle(Vector3 center, float radius)
    {
        const int segments = 64;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}

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
// Vana non attacca mai in questa fase: le fasi "di transizione animata" (ordine
// di scudo, richiamo, rilascio, sequenza finale) sospendono prossimita' e
// richiami finche' non si torna a Idle, cosi' un evento non interrompe mai
// un'animazione a meta'.
//
// E' un vero Behaviour Tree (BehaviourTreeBase, come ValerioBT/GuardianBT), ma
// la logica interna resta quasi tutta quella di prima: le coroutine che
// aspettano la fine di un'animazione (WaitForStateEnd) sono delicate e gia'
// testate, riscriverle da capo come nodi non avrebbe aggiunto nulla se non
// rischio. I nodi sotto sono wrapper sottili che chiamano dentro gli stessi
// metodi privati di prima: e' cosi' che in futuro si aggiunge un nodo fratello
// (es. un attacco speciale) senza toccare il collaudato.
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

    private readonly List<ValerioBT> activeMinions = new();

    private Phase phase = Phase.Idle;
    private Transform player;
    private PlayerMovement playerMovement;
    private int totalSpawned;
    private bool budgetExhausted;
    private bool guardiansDeployed;

    private static readonly int ShieldOrderHash = Animator.StringToHash("Shield_order");
    private static readonly int ReleaseOrderHash = Animator.StringToHash("Release_order");
    private static readonly int CallSimpHash = Animator.StringToHash("Call_simp");
    private static readonly int NoMoreSimpsHash = Animator.StringToHash("No_more_simps");
    private static readonly int DefeatIdleHash = Animator.StringToHash("Defeat_idle");

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
    }

    // BehaviourTreeBase aspetta gia' PlayerManager.Instance.PlayerTransform
    // prima di chiamare SetupTree(): qui sotto e' garantito non nullo, non
    // serve una propria coroutine di attesa come nelle versioni precedenti.
    protected override Node SetupTree()
    {
        player = PlayerManager.Instance.PlayerTransform;
        playerMovement = player.GetComponent<PlayerMovement>();

        return new Sequence(new List<Node>
        {
            new PhaseIsIdleCheck(this),
            new Selector(new List<Node>
            {
                new FinalSequenceWatch(this),
                new GuardianDeploymentWatch(this),
            })
        });
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

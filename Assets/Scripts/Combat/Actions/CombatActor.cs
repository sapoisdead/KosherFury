using UnityEngine;

// Macchina a stati del combattimento di un personaggio: decide quale mossa parte e
// quando, e la fa suonare all'ActionPlayer. Generica: riceve RICHIESTE (RequestLight,
// RequestParry, ...) e non sa chi le manda. Per il player le manda l'input (con il suo
// buffer, fuori da qui); per un nemico migrato le mandera' il behaviour tree.
//
// Una richiesta o viene accettata subito o viene rifiutata: niente coda qui dentro.
// Chi ha un buffer riprova a ogni frame finche' non scade, e cosi' un colpo premuto
// in anticipo parte esattamente quando si apre la finestra di combo, mentre uno
// premuto troppo presto non impegna a niente.
//
//   Locomotion -> Attack | Parry | Dodge       qualunque richiesta, se il moveset ha la mossa
//   Attack     -> Attack                       da comboFrame, se la mossa ha il collegamento
//   Attack     -> Parry | Dodge                da cancelFrame
//   Parry      -> Attack (riposta)             dopo una parata riuscita
//   Parry      -> Attack | Parry | Dodge       da cancelFrame (la parata di oggi e' annullabile subito)
//   Parry      -> (recovery a vuoto)           a fine mossa, se non ha parato niente
//   Dodge      -> qualunque                    da cancelFrame
//   *          -> HitReaction                  quando Health fa partire uno stagger
//   *          -> Dead                         alla morte
//
// Tocca Health solo nelle proprie finestre (parata e invulnerabilita'), e CombatState
// tramite l'ActionPlayer.
[DisallowMultipleComponent]
[RequireComponent(typeof(ActionPlayer))]
public class CombatActor : MonoBehaviour
{
    public enum State { Locomotion, Attack, Parry, Dodge, HitReaction, Dead }

    [Header("Lunge")]
    [Tooltip("Distanza fra i centri a cui il lunge si ferma davanti a un corpo. Il minimo fisico fra due capsule da 0.40 e' 0.80: sopra c'e' il margine.")]
    [SerializeField] private float lungeMinDistance = 0.9f;
    [Tooltip("Raggio della sfera con cui si cerca un corpo lungo la traiettoria del lunge quando non c'e' un bersaglio.")]
    [SerializeField] private float lungeProbeRadius = 0.35f;

    private ActionPlayer player;
    private Health health;
    private IActorMotor motor;

    private MoveData move;               // mossa in corso
    private bool parrySucceeded;
    private float whiffRecoveryLeft;     // secondi
    private bool writesBlocking, writesInvincible;

    // lunge della mossa in corso
    private float lungeLastFraction;
    private Vector3 lungeDirection;

    public State Current { get; private set; } = State.Locomotion;
    public MoveData CurrentMove => move;

    // Mosse dell'arma o del personaggio. Lo imposta chi lo possiede (il player dall'arma
    // in mano). Null = nessuna mossa del sistema nuovo.
    public Moveset Moveset { get; set; }

    // Bersaglio verso cui puntare il lunge e davanti a cui fermarsi. Null = avanti.
    public Transform Target { get; set; }

    // Impegnato in qualcosa: chi muove il personaggio non deve muoverlo.
    public bool IsBusy => Current != State.Locomotion;

    // Vero finche' questo componente governa i flag di Health (parata, invulnerabilita'):
    // chi altrimenti li riscriverebbe a ogni frame (il vecchio PlayerAnimator) deve astenersi.
    public bool OwnsHealthFlags => Current == State.Parry || Current == State.Dodge;

    private void Awake()
    {
        player = GetComponent<ActionPlayer>();
        health = GetComponent<Health>();
        motor = GetComponent<IActorMotor>();
        player.Finished += OnMoveFinished;
        if (health != null)
        {
            health.OnParried += OnParried;
            health.OnStaggered += OnStaggered;
            health.OnDeath += OnDeath;
        }
    }

    private void OnDestroy()
    {
        if (player != null) player.Finished -= OnMoveFinished;
        if (health != null)
        {
            health.OnParried -= OnParried;
            health.OnStaggered -= OnStaggered;
            health.OnDeath -= OnDeath;
        }
    }

    // ---------------------------------------------------------------- richieste

    public bool RequestLight()
    {
        if (Moveset == null) return false;

        switch (Current)
        {
            case State.Locomotion:
                return Begin(Moveset.light, State.Attack);
            case State.Attack:
                return ReachedFrame(move.comboFrame) && Begin(move.nextLight, State.Attack);
            case State.Parry:
                // dopo una parata riuscita: la riposta (o la catena normale se non c'e')
                if (whiffRecoveryLeft > 0f) return false;
                if (parrySucceeded)
                    return Begin(Moveset.riposte != null ? Moveset.riposte : Moveset.light, State.Attack);
                return ReachedFrame(move.cancelFrame) && Begin(Moveset.light, State.Attack);
            case State.Dodge:
                return ReachedFrame(move.cancelFrame) && Begin(Moveset.light, State.Attack);
            default:
                return false;
        }
    }

    public bool RequestHeavy()
    {
        if (Moveset == null) return false;

        switch (Current)
        {
            case State.Locomotion:
                return Begin(Moveset.heavy, State.Attack);
            case State.Attack:
                return ReachedFrame(move.comboFrame) && Begin(move.nextHeavy, State.Attack);
            case State.Parry:
                return whiffRecoveryLeft <= 0f && (parrySucceeded || ReachedFrame(move.cancelFrame)) && Begin(Moveset.heavy, State.Attack);
            case State.Dodge:
                return ReachedFrame(move.cancelFrame) && Begin(Moveset.heavy, State.Attack);
            default:
                return false;
        }
    }

    public bool RequestParry()
    {
        if (Moveset == null || !CanCancel()) return false;
        return Begin(Moveset.parry, State.Parry);
    }

    public bool RequestDodge(DodgeDirection direction)
    {
        if (Moveset == null || !CanCancel()) return false;
        return Begin(Moveset.Dodge(direction), State.Dodge);
    }

    // Da fermo si fa tutto; dentro una mossa solo da cancelFrame (la parata anche dopo
    // essere riuscita); mai durante la recovery a vuoto, la reazione o la morte.
    private bool CanCancel() => Current switch
    {
        State.Locomotion => true,
        State.Attack or State.Dodge => ReachedFrame(move.cancelFrame),
        State.Parry => whiffRecoveryLeft <= 0f && (parrySucceeded || ReachedFrame(move.cancelFrame)),
        _ => false,
    };

    private bool ReachedFrame(float frame) =>
        move != null && frame >= 0f && player.IsPlaying && player.Current == move && player.ElapsedFrames >= frame;

    private bool Begin(MoveData next, State state)
    {
        if (next == null || next.clip == null) return false;

        ReleaseHealthFlags();
        move = next;
        Current = state;
        parrySucceeded = false;
        whiffRecoveryLeft = 0f;
        lungeLastFraction = next.lungeCurve != null ? next.lungeCurve.Evaluate(0f) : 0f;
        lungeDirection = LungeDirection();
        player.Play(next);

        // Una finestra che parte dal frame 0 vale gia' da questo istante, non dal primo
        // LateUpdate: la parata di oggi e' sincrona col comando per lo stesso motivo
        // (un colpo nemico che arriva in quel frame verrebbe incassato).
        UpdateHealthFlags(0f);
        return true;
    }

    // ---------------------------------------------------------------- tempo

    // Dopo l'ActionPlayer (che aggiorna il tempo della mossa in LateUpdate): l'ordine
    // fra i LateUpdate non e' garantito, ma uno sfasamento di un frame sui flag di
    // Health e sul lunge non si vede.
    private void LateUpdate()
    {
        if (Current == State.Dead) return;

        if (whiffRecoveryLeft > 0f)
        {
            // HitStop ferma anche la penalita': e' tempo della mossa, non del mondo
            if (!HitStop.IsFrozen(gameObject)) whiffRecoveryLeft -= Time.deltaTime;
            if (whiffRecoveryLeft <= 0f) Return();
            return;
        }

        if (Current == State.HitReaction)
        {
            // senza clip di reazione nel moveset la durata e' quella dello stordimento
            if (!player.IsPlaying && (health == null || !health.IsStunned)) Return();
            return;
        }

        if (move == null || !player.IsPlaying || player.Current != move) return;

        float frame = player.ElapsedFrames;
        UpdateHealthFlags(frame);
        ApplyLunge();
    }

    private void UpdateHealthFlags(float frame)
    {
        if (health == null) return;

        if (Current == State.Parry)
        {
            bool inWindow = MoveData.InWindow(frame, move.parryStartFrame, move.parryEndFrame);
            health.IsBlocking = inWindow;
            writesBlocking = true;
        }
        if (Current == State.Dodge)
        {
            bool inWindow = MoveData.InWindow(frame, move.iframeStartFrame, move.iframeEndFrame);
            health.IsInvincible = inWindow;
            writesInvincible = true;
        }
    }

    private void ReleaseHealthFlags()
    {
        if (health == null) return;
        if (writesBlocking) health.IsBlocking = false;
        if (writesInvincible && !health.IsDead) health.IsInvincible = false;
        writesBlocking = writesInvincible = false;
    }

    private void OnMoveFinished(MoveData finished)
    {
        if (finished != move) return;     // sostituita da un'altra: non e' la fine

        ReleaseHealthFlags();

        if (Current == State.Parry && !parrySucceeded && finished.whiffRecoveryFrames > 0f)
        {
            // parata a vuoto: fermo per qualche frame, senza poter agire
            whiffRecoveryLeft = finished.whiffRecoveryFrames / MoveData.FrameRate;
            return;
        }

        if (Current == State.HitReaction) return;   // la chiude LateUpdate
        Return();
    }

    private void Return()
    {
        ReleaseHealthFlags();
        whiffRecoveryLeft = 0f;
        move = null;
        Current = State.Locomotion;
    }

    // ---------------------------------------------------------------- Health

    private void OnParried()
    {
        if (Current == State.Parry) parrySucceeded = true;
    }

    // Un colpo che fa partire lo stagger interrompe qualunque mossa: e' la punizione.
    // (Sui frame attivi con armor Health non chiama Stagger, quindi lo scambio regge.)
    private void OnStaggered(bool fromParry)
    {
        if (Current == State.Dead) return;
        ReleaseHealthFlags();
        whiffRecoveryLeft = 0f;

        MoveData reaction = Moveset != null ? Moveset.hitReaction : null;
        Current = State.HitReaction;
        if (reaction != null && reaction.clip != null)
        {
            move = reaction;
            player.Play(reaction);
        }
        else
        {
            move = null;
            player.Stop(0.05f);
        }
    }

    private void OnDeath()
    {
        ReleaseHealthFlags();
        Current = State.Dead;
        move = null;
        player.Stop(0.1f);
    }

    // ---------------------------------------------------------------- lunge

    // Spostamento in avanti dal MoveData: la differenza fra la frazione di curva di
    // adesso e quella del frame prima, per la distanza. Guidato dal tempo della mossa,
    // si ferma da solo con HitStop. La direzione e' congelata all'inizio della mossa:
    // il personaggio puo' continuare a girarsi verso il bersaglio, la traiettoria resta dritta.
    private void ApplyLunge()
    {
        if (motor == null || move.lungeDistance <= 0f || move.lungeCurve == null || lungeDirection == Vector3.zero) return;

        float fraction = move.lungeCurve.Evaluate(player.NormalizedTime);
        float step = (fraction - lungeLastFraction) * move.lungeDistance;
        lungeLastFraction = fraction;
        if (step <= 0f) return;

        step = Mathf.Min(step, LungeRoom(step));
        if (step > 0f) motor.MoveBy(lungeDirection * step);
    }

    private Vector3 LungeDirection()
    {
        Vector3 dir = Target != null ? Target.position - transform.position : transform.forward;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.zero;
    }

    // Quanto si puo' ancora avanzare prima di arrivare a lungeMinDistance dal corpo che
    // si ha davanti: il bersaglio se c'e', altrimenti il primo personaggio lungo la
    // traiettoria (il colpo puo' andare a segno anche senza bersaglio).
    private float LungeRoom(float step)
    {
        Transform body = Target != null ? Target : FirstBodyAhead(step);
        if (body == null) return step;

        Vector3 toBody = body.position - transform.position;
        toBody.y = 0f;
        return Mathf.Max(toBody.magnitude - lungeMinDistance, 0f);
    }

    private Transform FirstBodyAhead(float step)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        var hits = Physics.SphereCastAll(origin, lungeProbeRadius, lungeDirection, step + lungeMinDistance, ~0, QueryTriggerInteraction.Ignore);

        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var hit in hits)
        {
            Health other = hit.collider.GetComponentInParent<Health>();
            if (other == null || other == health || other.IsDead) continue;
            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearest = other.transform;
            }
        }
        return nearest;
    }
}

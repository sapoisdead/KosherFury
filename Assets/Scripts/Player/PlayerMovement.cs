using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour, IActorMotor
{
    [Header("Movement")]
    [Tooltip("Camminata libera, in m/s. Regolabile: la clip Walk (nativa 1.2 m/s) viene riprodotta a walkSpeed / 1.2, quindi i piedi non scivolano.")]
    [SerializeField] private float walkSpeed = 1.2f;
    [Tooltip("Corsa (sprint), in m/s. Regolabile: la clip Run (nativa 5.0 m/s) viene riprodotta a sprintSpeed / 5.0.")]
    [SerializeField] private float sprintSpeed = 5f;
    [Tooltip("Velocita' massima da agganciati (strafe), in m/s. Separata dalla camminata: gli strafe in guardia sono a passo accostato, piu' lenti. Regolabile: la clip viene riprodotta a velocita' / guardClipSpeed (PlayerAnimator).")]
    [SerializeField] private float lockMoveSpeed = 0.8f;
    [Tooltip("Velocita' con cui il player si porta avanti DURANTE il proprio colpo, solo se agganciato. Serve a recuperare la spinta che il colpo precedente ha impresso al bersaglio: senza, dopo due o tre colpi il nemico e' fuori portata e la combo si spezza. Non e' movimento libero, punta solo al bersaglio e si ferma a attackAdvanceMinDistance.")]
    [SerializeField] private float attackAdvanceSpeed = 1.2f;
    [Tooltip("Distanza a cui l'avanzamento durante il colpo si ferma. Sotto la portata del player (1.05-1.10 m) e sopra il minimo fisico fra i due corpi (0.80 m).")]
    [SerializeField] private float attackAdvanceMinDistance = 0.9f;

    [Header("Salto")]
    [Tooltip("Altezza del salto, in metri.")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [Tooltip("Jump_Start: la spinta verso l'alto parte al suo marker liftoff.")]
    [SerializeField] private MoveData jumpStartMove;
    [Tooltip("Jump_Land: fino al suo marker cancel si resta fermi e non si attacca; da li' il movimento interrompe la clip.")]
    [SerializeField] private MoveData jumpLandMove;
    [Tooltip("Secondi senza appoggio prima che una caduta senza salto passi a Jump_Air (e poi atterri con Jump_Land). Sotto, scalini e pendenze non interrompono la corsa.")]
    [SerializeField] private float fallGraceTime = 0.15f;

    [Header("Dash")]
    [Tooltip("Scatto laterale, disponibile solo sotto lock. Piu' corto e secco della capriola.")]
    [SerializeField] private float dashSpeed = 7f;
    [Tooltip("Deve combaciare con la velocita' degli stati Dash_Left/Right/Front/Back nell'Animator (tutti a 3.3, clip da 1.16s => 0.35s), o il movimento finisce prima o dopo l'animazione. Dash_Left/Right non erano mai stati sincronizzati bene: a velocita' 1.2 su una clip da 1.16s la finestra di 0.45s si chiudeva a meno di meta' animazione.")]
    [SerializeField] private float dashDuration = 0.35f;
    [Tooltip("Per quanto il tasto di schivata resta in attesa di una direzione. Serve a partire da fermo premendo schivata e direzione insieme.")]
    [SerializeField] private float dodgeBufferTime = 0.15f;
    [Tooltip("Se attivo, lo spostamento dello scatto viene dall'animazione stessa (root motion) invece che da dashSpeed. La distanza la decide chi ha animato la clip.")]
    [SerializeField] private bool useRootMotionForDash = true;
    [Tooltip("Moltiplica lo spostamento del root motion, conservando direzione e curva dell'animazione. Serve perche' le clip coprono ~0.87 m, meno della portata del player (1.10). Alzandolo troppo i piedi iniziano a slittare, perche' il passo animato non copre piu' la distanza percorsa.")]
    [SerializeField] private float dashRootMotionScale = 1.8f;

    private CharacterController controller;
    private PlayerAnimator animator;
    private Animator anim;
    private ActionPlayer actionPlayer;
    private CombatActor combatActor;
    private PlayerCombatInput combatInput;

    // L'arma in mano ha un Moveset: i comandi d'attacco, parata e schivata vanno al
    // sistema d'azioni nuovo invece che al PlayerAnimator vecchio.
    private bool UsesActions => combatInput != null && combatInput.HasMoveset;

    // Riferimento congelato del root motion della mossa in corso (come dashFrame)
    private MoveData rootMotionMove;
    private Quaternion rootMotionFrame = Quaternion.identity;
    private Vector2 moveInput;
    private bool isSprinting;
    private InputAction moveAction;

    // Vero quando il movimento arriva da uno stick, cioe' quando l'inclinazione ha
    // un valore intermedio da rispettare. Da tastiera resta falso e la velocita'
    // torna a essere quella fissa di camminata.
    private bool analogMove;

    private bool jumpRequested;
    private float jumpRequestTime;

    // Fase del salto, in tempo di gioco accumulato dall'inizio della fase. Start va dalla
    // pressione alla spinta (liftoff), Air fino al ritorno a terra, Land fino alla fine di
    // Jump_Land.
    private enum JumpPhase { None, Start, Air, Land }
    private JumpPhase jumpPhase;
    private float jumpPhaseTime;
    private float airTime;
    private Vector3 velocity;
    private Transform cameraTransform;

    private Vector3 dashDirection;
    private float dashTimeRemaining;
    private Quaternion dashFrame = Quaternion.identity;
    private float bufferedDodgeTime = -1f;
    private bool IsDashing => dashTimeRemaining > 0f;

    // Blocco scriptato dell'input (es. l'animazione lunga di sconfitta di Vana):
    // azzera anche l'input gia' letto, non solo quello futuro, o un movimento
    // impostato un istante prima del lock resterebbe applicato in HandleMovement.
    public bool InputLocked { get; private set; }

    public void SetInputLocked(bool locked)
    {
        InputLocked = locked;
        if (locked)
        {
            moveInput = Vector2.zero;
            isSprinting = false;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<PlayerAnimator>();
        anim = GetComponent<Animator>();
        actionPlayer = GetComponent<ActionPlayer>();
        combatActor = GetComponent<CombatActor>();
        combatInput = GetComponent<PlayerCombatInput>();

        if (Camera.main != null)
            cameraTransform = Camera.main.transform;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnMove(InputValue value)
    {
        if (InputLocked) return;

        // Muoversi NON azzera piu' la combo. Con un nemico che arretra e che il
        // knockback allontana, inseguirlo e' obbligatorio, e ogni correzione di
        // direzione riportava la combo al primo colpo. Ci pensa gia' comboResetTime
        // in PlayerAnimator ad azzerarla quando si smette davvero di attaccare.
        moveInput = value.Get<Vector2>();
        analogMove = IsAnalogSource();

        // la direzione puo' arrivare DOPO il tasto di schivata: e' il caso di chi
        // parte da fermo e preme i due tasti insieme
        TryConsumeDodge();
    }

    // Lo stesso valore puo' venire dallo stick o dalla tastiera, e il modulo da solo
    // non li distingue: WASD in diagonale vale esattamente 1, come lo stick a fondo
    // corsa. L'unico modo di saperlo e' chiedere da quale controllo e' partito.
    // L'azione si risolve alla prima chiamata: l'ordine degli Awake fra PlayerInput
    // e questo componente non e' garantito.
    private bool IsAnalogSource()
    {
        if (moveAction == null)
        {
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput == null || playerInput.actions == null) return false;
            moveAction = playerInput.actions.FindAction("Move");
            if (moveAction == null) return false;
        }

        return moveAction.activeControl != null && moveAction.activeControl.device is Gamepad;
    }

    // Quanto si va, dato il tetto di velocita' del contesto: libero si arriva alla
    // corsa, agganciati ci si ferma a lockMoveSpeed, perche' girare attorno al nemico a
    // stick pieno e' il gesto piu' comune del combattimento e farne partire la corsa
    // sgancerebbe il lock ogni volta. Da tastiera, senza sprint, la velocita' e' la
    // camminata, o il tetto del lock se e' piu' basso.
    private float MoveSpeed(float maxSpeed)
    {
        if (isSprinting) return sprintSpeed;
        if (!analogMove) return Mathf.Min(walkSpeed, maxSpeed);

        return Mathf.Clamp01(moveInput.magnitude) * maxSpeed;
    }

    private void OnSprint(InputValue value)
    {
        if (InputLocked) return;

        isSprinting = value.isPressed;

        // Correre disattiva il lock: non ha senso restare agganciati mentre si
        // scappa o si insegue a tutta velocita', ed e' cosi' che si passa dalla
        // camminata/guardia alla corsa libera.
        if (isSprinting && TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked)
            TargetLockSystem.Instance.Unlock();
    }

    private void OnJump(InputValue value)
    {
        if (InputLocked) return;

        if (value.isPressed)
        {
            jumpRequested = true;
            jumpRequestTime = Time.time;
        }
    }

    private void OnPunch(InputValue value)
    {
        if (InputLocked) return;
        if (!value.isPressed) return;
        if (UsesActions) { combatInput.Light(); return; }
        if (!CanAct) return;
        animator.TriggerPunch();
    }

    // Tasto dedicato (destro del mouse, ora che Lock e' su X): un click secco,
    // niente tenuta. Senza spada in mano non ha una clip corrispondente, quindi
    // non fa nulla.
    private void OnAreaSlash(InputValue value)
    {
        if (InputLocked) return;
        if (!value.isPressed) return;
        if (!animator.IsArmed) return;
        if (UsesActions) { combatInput.Heavy(); return; }
        if (!CanAct) return;
        animator.TriggerAreaSlash();
    }

    private void OnBlock(InputValue value)
    {
        if (InputLocked) return;
        if (!value.isPressed) return;
        if (UsesActions) { combatInput.Parry(); return; }
        if (!CanAct) return;
        animator.TriggerParry();
    }

    private void OnDodge(InputValue value)
    {
        if (InputLocked) return;
        if (!value.isPressed) return;

        // Il comando viene sempre registrato, anche senza direzione: partendo da
        // fermo, schivata e direzione si premono insieme e l'ordine con cui arrivano
        // le due callback non e' garantito. Senza questo, premere i due tasti nello
        // stesso istante perdeva la schivata perche' moveInput era ancora a zero.
        bufferedDodgeTime = Time.time;
        TryConsumeDodge();
    }

    private void TryConsumeDodge()
    {
        if (bufferedDodgeTime < 0f) return;

        // scaduto: meglio perderlo che vederlo partire con mezzo secondo di ritardo
        if (Time.time - bufferedDodgeTime > dodgeBufferTime)
        {
            bufferedDodgeTime = -1f;
            return;
        }

        if (IsDashing) return;                        // gia' in schivata: resta in coda
        if (!CanAct) return;                          // in aria o in atterraggio: resta in coda
        if (moveInput.sqrMagnitude <= 0.01f) return;  // manca la direzione: aspetta

        // La schivata esiste solo agganciati: fuori lock non c'e' un bersaglio
        // rispetto a cui abbia senso, e la capriola e' stata rimossa.
        bool locked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;
        if (!locked) return;

        bufferedDodgeTime = -1f;
        DoDash();
    }

    private void DoDash()
    {
        // La direzione si prende dall'asse del BERSAGLIO, non da quello della camera:
        // e' cosi' che lo scatto gira attorno al nemico invece che di traverso.
        Transform lockTarget = TargetLockSystem.Instance.LockedTarget;
        Vector3 toEnemy = lockTarget.position - transform.position;
        toEnemy.y = 0f;
        if (toEnemy.sqrMagnitude < 0.0001f) return;
        toEnemy.Normalize();

        Vector3 lateralAxis = new Vector3(toEnemy.z, 0f, -toEnemy.x);   // perpendicolare

        // Una sola delle quattro direzioni, quella dominante: uno scatto in diagonale
        // non avrebbe una clip che lo rappresenti, e si vedrebbe scivolare di lato.
        PlayerAnimator.DashDirection dir;
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            bool toRight = moveInput.x > 0f;
            dashDirection = (toRight ? lateralAxis : -lateralAxis).normalized;
            dir = toRight ? PlayerAnimator.DashDirection.Right : PlayerAnimator.DashDirection.Left;
        }
        else
        {
            bool forward = moveInput.y > 0f;
            dashDirection = (forward ? toEnemy : -toEnemy).normalized;
            dir = forward ? PlayerAnimator.DashDirection.Front : PlayerAnimator.DashDirection.Back;
        }

        // Con il sistema nuovo la schivata e' una mossa del Moveset: il root motion lo
        // applica OnAnimatorMove, nel riferimento congelato alla partenza.
        if (UsesActions)
        {
            combatInput.Dodge(dir switch
            {
                PlayerAnimator.DashDirection.Left  => DodgeDirection.Left,
                PlayerAnimator.DashDirection.Right => DodgeDirection.Right,
                PlayerAnimator.DashDirection.Front => DodgeDirection.Forward,
                _                                  => DodgeDirection.Back,
            });
            return;
        }

        // Niente rotazione: le clip di scatto sono animate come spostamenti rispetto
        // a chi guarda avanti, e girare il player le farebbe leggere di traverso.
        // Sotto lock resta comunque rivolto al nemico.
        animator.TriggerDash(dir);
        // orientamento congelato: e' il riferimento in cui verra' applicato il root
        // motion per tutta la durata dello scatto (vedi OnAnimatorMove)
        dashFrame = transform.rotation;
        dashTimeRemaining = dashDuration;
    }

    // Si puo' attaccare, parare e schivare: non in aria, non durante la spinta, e
    // all'atterraggio solo dal cancel di Jump_Land in poi.
    public bool CanAct => jumpPhase == JumpPhase.None
                          || (jumpPhase == JumpPhase.Land && jumpPhaseTime >= LandCancelTime);

    private bool LandLocked => jumpPhase == JumpPhase.Land && jumpPhaseTime < LandCancelTime;

    private float LiftoffTime => jumpStartMove != null && jumpStartMove.liftoffFrame >= 0f
        ? jumpStartMove.FrameToTime(jumpStartMove.liftoffFrame) : 0f;
    private float LandCancelTime => jumpLandMove != null && jumpLandMove.cancelFrame >= 0f
        ? jumpLandMove.FrameToTime(jumpLandMove.cancelFrame) : 0f;
    private float LandEndTime => jumpLandMove != null ? jumpLandMove.DurationAt() : 0f;

    private void Update()
    {
        // una schivata premuta mezzo istante troppo presto parte qui, appena si puo'
        TryConsumeDodge();

        HandleGravityAndJump();
        HandleMovement();
    }

    // Salto in tre parti (Jump_Start, Jump_Air, Jump_Land). La spinta parte al marker
    // liftoff, non alla pressione: fino a li' i piedi sono a terra. Anche una caduta senza
    // salto, oltre fallGraceTime, passa da Jump_Air e atterra con Jump_Land.
    private void HandleGravityAndJump()
    {
        if (jumpRequested && Time.time - jumpRequestTime > jumpBufferTime)
            jumpRequested = false;

        bool grounded = controller.isGrounded;
        airTime = grounded ? 0f : airTime + Time.deltaTime;
        jumpPhaseTime += Time.deltaTime;

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;

        switch (jumpPhase)
        {
            case JumpPhase.None:
                if (grounded && jumpRequested && CanStartJump())
                    StartJump();
                else if (!grounded && airTime > fallGraceTime)
                    EnterAir();
                break;

            case JumpPhase.Start:
                if (jumpPhaseTime >= LiftoffTime)
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    EnterAir();
                }
                break;

            case JumpPhase.Air:
                // in salita isGrounded puo' restare vero per il primo frame dopo la spinta
                if (grounded && velocity.y <= 0f)
                    SetJumpPhase(JumpPhase.Land);
                break;

            case JumpPhase.Land:
                if (jumpPhaseTime < LandCancelTime) break;
                if (jumpRequested && CanStartJump())
                {
                    StartJump();
                    break;
                }
                // dal cancel il movimento interrompe la clip; senza input finisce da sola
                if (moveInput.sqrMagnitude > 0.01f)
                {
                    animator.TriggerLandCancel();
                    SetJumpPhase(JumpPhase.None);
                }
                else if (jumpPhaseTime >= LandEndTime)
                    SetJumpPhase(JumpPhase.None);
                break;
        }

        velocity.y += gravity * Time.deltaTime;
    }

    // Non durante una mossa, uno scatto o un attacco del sistema vecchio
    private bool CanStartJump()
    {
        if (IsDashing) return false;
        if (actionPlayer != null && actionPlayer.IsPlaying) return false;
        if (combatActor != null && combatActor.IsBusy) return false;
        return animator == null || !animator.IsCommittedToAttack;
    }

    private void StartJump()
    {
        jumpRequested = false;
        animator.TriggerJump();
        SetJumpPhase(JumpPhase.Start);
    }

    private void EnterAir() => SetJumpPhase(JumpPhase.Air);

    private void SetJumpPhase(JumpPhase phase)
    {
        jumpPhase = phase;
        jumpPhaseTime = 0f;
        animator.SetGrounded(phase != JumpPhase.Air);
    }

    private void HandleMovement()
    {
        if (cameraTransform == null || !cameraTransform.gameObject)
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

        if (IsDashing)
        {
            dashTimeRemaining -= Time.deltaTime;

            // Il player resta rivolto al bersaglio anche mentre scatta di lato,
            // altrimenti perderebbe di vista il nemico proprio nel momento in cui
            // gli sta girando attorno.
            if (TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked)
                FaceLockTarget();

            // Con il root motion lo spostamento orizzontale arriva da OnAnimatorMove,
            // che gira dopo la valutazione dell'animazione: qui resta solo la gravita'.
            controller.Move(useRootMotionForDash
                ? velocity * Time.deltaTime
                : (dashDirection * dashSpeed + velocity) * Time.deltaTime);
            return;
        }

        // Atterraggio: fermi fino al cancel di Jump_Land, poi il movimento la interrompe
        if (LandLocked)
        {
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        bool locked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;

        // Il proprio colpo e' un impegno: finche' dura non ci si sposta. Restano
        // la gravita' e, da agganciati, la rotazione verso il bersaglio — senza
        // quella un nemico che si sposta di poco farebbe mancare un colpo gia'
        // partito. Lo scatto invece passa sopra a questo blocco (sta piu' in
        // alto): annullare l'attacco con una schivata resta possibile.
        // Anche una mossa del sistema nuovo (CombatActor) e' un impegno, comprese la
        // recovery a vuoto della parata e la reazione al colpo. Il suo spostamento, se
        // c'e', lo portano il lunge (MoveBy) e il root motion (OnAnimatorMove).
        // L'avanzamento agganciato di oggi resta solo per i colpi SENZA lunge (tutti
        // quelli migrati con le clip vecchie): con il lunge i due si sommerebbero.
        bool inAction = (actionPlayer != null && actionPlayer.IsPlaying) || (combatActor != null && combatActor.IsBusy);
        if ((animator != null && animator.IsCommittedToAttack) || inAction)
        {
            if (locked) FaceLockTarget();
            bool attackWithoutLunge = combatActor != null && combatActor.Current == CombatActor.State.Attack
                                      && (combatActor.CurrentMove == null || combatActor.CurrentMove.lungeDistance <= 0f);
            Vector3 advance = !inAction || attackWithoutLunge ? AttackAdvance(locked) : Vector3.zero;
            controller.Move((advance + velocity) * Time.deltaTime);
            return;
        }

        if (locked)
        {
            HandleLockedMovement();
        }
        else if (moveInput.sqrMagnitude > 0.01f)
        {
            if (cameraTransform == null) return;
            float speed = MoveSpeed(sprintSpeed);

            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;

            if (moveDir != Vector3.zero)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    Quaternion.LookRotation(moveDir),
                    360f * Time.deltaTime
                );
            }

            controller.Move((moveDir * speed + velocity) * Time.deltaTime);
        }
        else
        {
            controller.Move(velocity * Time.deltaTime);
        }
    }

    // Avanzamento durante il proprio colpo. Ogni colpo andato a segno spinge via
    // il bersaglio, e da quando il movimento e' bloccato in attacco il player non
    // ha piu' modo di recuperare quei centimetri: dopo due o tre colpi il nemico e'
    // fuori portata e la combo muore da sola. E' lo stesso accorgimento che usa il
    // simp, che avanza mentre concatena (vedi AttackPlayer.Striking).
    //
    // Non riapre il movimento libero: punta solo al bersaglio agganciato, si ferma
    // a distanza di colpo e non supera mai quella soglia in un frame.
    private Vector3 AttackAdvance(bool locked)
    {
        if (!locked || attackAdvanceSpeed <= 0f) return Vector3.zero;

        Transform target = TargetLockSystem.Instance.LockedTarget;
        if (target == null) return Vector3.zero;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float gap = toTarget.magnitude - attackAdvanceMinDistance;
        if (gap <= 0f) return Vector3.zero;

        // il passo non deve mai scavalcare la soglia: senza il clamp, a frame
        // lunghi il player finirebbe addosso al nemico invece che a tiro
        float speed = Mathf.Min(attackAdvanceSpeed, gap / Mathf.Max(Time.deltaTime, 0.0001f));
        return toTarget.normalized * speed;
    }

    private void FaceLockTarget()
    {
        Transform lockTarget = TargetLockSystem.Instance.LockedTarget;
        if (lockTarget == null) return;

        Vector3 toEnemy = lockTarget.position - transform.position;
        toEnemy.y = 0f;
        if (toEnemy == Vector3.zero) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(toEnemy),
            360f * Time.deltaTime
        );
    }

    private void HandleLockedMovement()
    {
        Transform lockTarget = TargetLockSystem.Instance.LockedTarget;

        FaceLockTarget();
        Vector3 toEnemy = lockTarget.position - transform.position;
        toEnemy.y = 0f;

        if (moveInput.sqrMagnitude < 0.01f)
        {
            controller.Move(velocity * Time.deltaTime);
            return;
        }

        float speed = MoveSpeed(lockMoveSpeed);

        // Forward = verso il nemico, right = perpendicolare
        Vector3 forward = toEnemy.normalized;
        Vector3 right = new Vector3(forward.z, 0f, -forward.x);

        Vector3 moveDir = (forward * moveInput.y + right * moveInput.x).normalized;
        controller.Move((moveDir * speed + velocity) * Time.deltaTime);
    }

    // Root motion. Implementare questo metodo fa si' che Unity NON sposti piu' il
    // Transform da se': ci passa il delta e lascia decidere a noi. E' l'unico modo
    // corretto con un CharacterController, perche' scrivere sul Transform
    // scavalcherebbe le collisioni e farebbe attraversare i muri.
    //
    // Lo applichiamo solo durante lo scatto: tutte le altre clip sono animate sul
    // posto, quindi il loro delta e' comunque nullo, ma restringere il campo evita
    // sorprese se un domani qualche clip venisse riesportata con la radice sbloccata.
    private void OnAnimatorMove()
    {
        // Root motion di una mossa dell'ActionPlayer: stesso trattamento dello scatto,
        // sul piano, nel riferimento congelato all'inizio della mossa (girandosi verso il
        // bersaglio la traiettoria non si incurva) e attraverso il CharacterController.
        if (actionPlayer != null && actionPlayer.WantsRootMotion && anim != null)
        {
            if (rootMotionMove != actionPlayer.Current)
            {
                rootMotionMove = actionPlayer.Current;
                rootMotionFrame = transform.rotation;
            }

            Vector3 actionDelta = anim.deltaPosition;
            actionDelta.y = 0f;
            Vector3 actionLocal = Quaternion.Inverse(transform.rotation) * actionDelta;
            controller.Move(rootMotionFrame * actionLocal * actionPlayer.RootMotionScale);
            return;
        }
        rootMotionMove = null;

        if (!useRootMotionForDash || !IsDashing || anim == null) return;

        Vector3 delta = anim.deltaPosition;
        delta.y = 0f;                       // la quota la governa la gravita'

        // Il delta esce nel riferimento CORRENTE del personaggio, che pero' sta
        // ruotando di continuo per restare rivolto al nemico: applicato cosi', la
        // direzione "destra" ruota insieme al player e la traiettoria si incurva in
        // un arco attorno al bersaglio, senza allontanarsene. Lo rimappo quindi
        // sull'orientamento congelato alla partenza: la traiettoria torna rettilinea,
        // mentre il personaggio continua a girarsi verso il nemico.
        Vector3 local = Quaternion.Inverse(transform.rotation) * delta;
        Vector3 straight = dashFrame * local;

        controller.Move(straight * dashRootMotionScale);
    }

    // Il lunge del CombatActor: passa dal CharacterController, cosi' rispetta le collisioni.
    public void MoveBy(Vector3 delta) => controller.Move(delta);

    // Velocita' di gioco in sola lettura: PlayerAnimator ci mette le soglie del blend
    // Walk/Run e ne ricava la velocita' di riproduzione delle clip.
    public float WalkSpeed => walkSpeed;
    public float SprintSpeed => sprintSpeed;

    public float GetSpeed()
    {
        Vector3 horizontal = controller.velocity;
        horizontal.y = 0f;
        return horizontal.magnitude;
    }

    // Velocita' orizzontale come vettore, non come modulo: serve all'animator per
    // sapere se ci si sta muovendo in avanti o all'indietro, cosa che il solo
    // modulo non puo' dire.
    public Vector3 GetHorizontalVelocity()
    {
        Vector3 v = controller.velocity;
        v.y = 0f;
        return v;
    }
}

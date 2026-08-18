using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 4.8f;
    [SerializeField] private float sprintSpeed = 9.6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpBufferTime = 0.15f;

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
    private Vector2 moveInput;
    private bool isSprinting;
    private InputAction moveAction;

    // Vero quando il movimento arriva da uno stick, cioe' quando l'inclinazione ha
    // un valore intermedio da rispettare. Da tastiera resta falso e la velocita'
    // torna a essere quella fissa di camminata.
    private bool analogMove;

    private bool elevatePlayer;
    private bool jumpRequested;
    private float jumpRequestTime;
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
    // corsa, agganciati ci si ferma alla camminata da combattimento, perche' girare
    // attorno al nemico a stick pieno e' il gesto piu' comune del combattimento e
    // farne partire la corsa sgancerebbe il lock ogni volta.
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
        animator.TriggerAreaSlash();
    }

    private void OnBlock(InputValue value)
    {
        if (InputLocked) return;
        if (!value.isPressed) return;
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

        // Niente rotazione: le clip di scatto sono animate come spostamenti rispetto
        // a chi guarda avanti, e girare il player le farebbe leggere di traverso.
        // Sotto lock resta comunque rivolto al nemico.
        animator.TriggerDash(dir);
        // orientamento congelato: e' il riferimento in cui verra' applicato il root
        // motion per tutta la durata dello scatto (vedi OnAnimatorMove)
        dashFrame = transform.rotation;
        dashTimeRemaining = dashDuration;
    }

    private void OnElevating()
    {
        elevatePlayer = true;
    }

    private void Update()
    {
        // una schivata premuta mezzo istante troppo presto parte qui, appena si puo'
        TryConsumeDodge();

        HandleGravityAndJump();
        HandleMovement();
    }

    private void HandleGravityAndJump()
    {
        if (jumpRequested && Time.time - jumpRequestTime > jumpBufferTime)
            jumpRequested = false;

        if (controller.isGrounded)
        {
            if (velocity.y < 0f)
                velocity.y = -2f;

            if (jumpRequested)
            {
                animator.TriggerJump();
                jumpRequested = false;
            }

            if (elevatePlayer)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                elevatePlayer = false;
            }
        }

        velocity.y += gravity * Time.deltaTime;
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

        bool locked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;

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

        float speed = MoveSpeed(walkSpeed);

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

    // Velocita' di camminata in sola lettura: e' anche il tetto a cui si fermano
    // le andature analogiche quando si e' agganciati.
    public float WalkSpeed => walkSpeed;

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

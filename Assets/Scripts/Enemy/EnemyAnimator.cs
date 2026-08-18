using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [SerializeField] private float smoothTime = 0.1f;
    [SerializeField] private Hitbox rightHandHitbox;
    [SerializeField] private Hitbox leftHandHitbox;
    [SerializeField] private Hitbox rightFootHitbox;
    [SerializeField] private Hitbox leftFootHitbox;

    private Animator animator;
    private NavMeshAgent agent;
    private EnemyWeaponManager weaponManager;
    private Health health;
    private CombatState combatState;
    private float currentSpeed;
    private float speedVelocity;
    private Vector2 currentMove;
    private Vector2 moveVelocity;
    private int lastAttackIndex = -1;

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int MoveXHash       = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash       = Animator.StringToHash("MoveZ");
    private static readonly int PunchRightHash  = Animator.StringToHash("Punch_right");
    private static readonly int PunchLeftHash   = Animator.StringToHash("Punch_left");
    private static readonly int KickRightHash   = Animator.StringToHash("Kick_right");
    private static readonly int KickLeftHash    = Animator.StringToHash("Kick_left");
    private static readonly int SlashHash       = Animator.StringToHash("Slash");
    private static readonly int TakeHitHash     = Animator.StringToHash("Take_hit");
    private static readonly int PraiseHash      = Animator.StringToHash("Praise");

    // Kick_right e' fuori rotazione: la sua hitbox risulta sovrapposta al player
    // durante i frame attivi (0.443 contro una soglia di 0.500) ma il colpo non
    // registra mai, a nessuna distanza. Finche' non si capisce perche', il simp
    // pesca fra gli altri tre. Lo stato e i suoi eventi restano al loro posto.
    private static readonly int[] AttackTriggers =
    {
        PunchRightHash, PunchLeftHash, KickLeftHash
    };

    // Nomi degli STATI dell'Animator, non dei trigger: servono a capire se il simp
    // e' dentro a un attacco, per ricavarne la fase.
    private static readonly int[] AttackStates =
    {
        Animator.StringToHash("Punch_Right"),
        Animator.StringToHash("Punch_Left"),
        Animator.StringToHash("Kick_Right"),
        Animator.StringToHash("Kick_Left"),
        Animator.StringToHash("Slash"),
    };

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent    = GetComponent<NavMeshAgent>();
        weaponManager = GetComponent<EnemyWeaponManager>();
        health = GetComponent<Health>();

        combatState = GetComponent<CombatState>();
        if (combatState == null) combatState = gameObject.AddComponent<CombatState>();

        foreach (var hb in GetComponentsInChildren<Hitbox>(true))
        {
            bool isRight = hb.name.Contains("Right");
            bool isFoot  = hb.name.Contains("Kick") || hb.name.Contains("Foot");

            if (isFoot && isRight)       { if (rightFootHitbox == null) rightFootHitbox = hb; }
            else if (isFoot && !isRight) { if (leftFootHitbox == null)  leftFootHitbox = hb; }
            else if (isRight)            { if (rightHandHitbox == null) rightHandHitbox = hb; }
            else                         { if (leftHandHitbox == null)  leftHandHitbox = hb; }
        }
    }

    private void Update()
    {
        Vector3 worldVelocity = agent != null ? agent.velocity : Vector3.zero;

        float targetSpeed = worldVelocity.magnitude;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedVelocity, smoothTime);
        animator.SetFloat(SpeedHash, currentSpeed);

        // Direzione di movimento RELATIVA a dove il simp sta guardando. Serve perche'
        // in combattimento resta rivolto al player mentre si sposta di lato: senza
        // questo camminerebbe di fianco con l'animazione di corsa frontale.
        Vector3 local = transform.InverseTransformDirection(worldVelocity);
        Vector2 targetMove = new Vector2(local.x, local.z);

        currentMove = Vector2.SmoothDamp(currentMove, targetMove, ref moveVelocity, smoothTime);
        animator.SetFloat(MoveXHash, currentMove.x);
        animator.SetFloat(MoveZHash, currentMove.y);

        // Fase dell'attacco in corso, ricavata come per il player
        int stateHash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        combatState.UpdatePhase(IsInAnyState(stateHash, AttackStates), stateHash);
    }

    private static bool IsInAnyState(int hash, int[] states)
    {
        for (int i = 0; i < states.Length; i++)
            if (states[i] == hash) return true;
        return false;
    }

    public bool IsBeingHit()
    {
        bool inTakeHitAnim = animator.GetCurrentAnimatorStateInfo(0).shortNameHash == TakeHitHash;
        return inTakeHitAnim || (health != null && health.IsStunned);
    }

    public void TriggerAttack()
    {
        if (weaponManager != null && weaponManager.CurrentWeapon != null && !weaponManager.CurrentWeapon.isFists)
        {
            animator.SetTrigger(SlashHash);
            return;
        }

        // Scelta casuale fra i 4 attacchi, evitando di ripetere subito lo stesso
        // (due colpi identici di fila si leggono come un glitch, non come una scelta)
        int index = Random.Range(0, AttackTriggers.Length);
        if (AttackTriggers.Length > 1 && index == lastAttackIndex)
            index = (index + 1 + Random.Range(0, AttackTriggers.Length - 1)) % AttackTriggers.Length;

        lastAttackIndex = index;
        animator.SetTrigger(AttackTriggers[index]);
    }

    // Usata da GuardianBT/PreDeploymentIdle: posa di venerazione rivolta verso
    // Vana prima del dispiegamento. Idle->Praise nel controller, e Praise ha le
    // stesse uscite di Idle (Speed/attacco), quindi appena l'agent riprende a
    // muoversi o attacca se ne esce da solo.
    public void TriggerPraise() => animator.SetTrigger(PraiseHash);

    // Vero solo quando lo stato Animator corrente e' davvero Praise. Serve a
    // PreDeploymentIdle per verificare che il trigger abbia attecchito: chiamare
    // SetTrigger nello stesso frame in cui l'Animator si abilita (prima che lo
    // state machine abbia inizializzato lo stato di default) puo' perdere il
    // trigger in silenzio — capitato su un sottoinsieme di guardie, non tutte,
    // a seconda di quando il loro SetupTree si risolveva.
    public bool IsPraising() => animator.GetCurrentAnimatorStateInfo(0).shortNameHash == PraiseHash;

    // I trigger dell'Animator restano ARMATI finche' una transizione non li
    // consuma. PreDeploymentIdle ripete TriggerPraise finche' non vede lo stato
    // raggiunto, ma durante la fusione Idle->Praise lo stato corrente e' ancora
    // Idle: le chiamate di quei frame riarmano il trigger DOPO che la
    // transizione ne ha gia' consumato uno, e l'ultimo resta armato per sempre
    // (Praise non ha uscite su quel trigger che possano consumarlo).
    // Risultato: a dispiegamento avvenuto, appena la guardia si fermava sul
    // proprio slot e rientrava in Idle, il trigger rimasto faceva scattare di
    // nuovo Idle->Praise — la guardia si inginocchiava in mezzo al
    // combattimento. Va disarmato appena la posa e' confermata.
    public void ResetPraise() => animator.ResetTrigger(PraiseHash);

    // Stessi nomi di evento del player, cosi' la convenzione e' una sola
    public void OnRightHandStart() => OpenWindow(rightHandHitbox, UnarmedDamage());
    public void OnRightHandEnd()   => CloseWindow(rightHandHitbox);
    public void OnLeftHandStart()  => OpenWindow(leftHandHitbox, UnarmedDamage());
    public void OnLeftHandEnd()    => CloseWindow(leftHandHitbox);

    public void OnRightFootStart() => OpenWindow(rightFootHitbox, UnarmedDamage());
    public void OnRightFootEnd()   => CloseWindow(rightFootHitbox);
    public void OnLeftFootStart()  => OpenWindow(leftFootHitbox, UnarmedDamage());
    public void OnLeftFootEnd()    => CloseWindow(leftFootHitbox);

    public void OnWeaponStart()    => OpenWindow(weaponManager?.CurrentHitbox, (weaponManager?.CurrentWeapon?.damage ?? 0f) * DamageMultiplier);
    public void OnWeaponEnd()      => CloseWindow(weaponManager?.CurrentHitbox);

    // Moltiplicatore esterno sul danno, 1 di default. Usato da GuardianBT per
    // alzarlo mentre la guardia difende Vana (e ancora di piu' una volta
    // rilasciata): deve essere un deterrente forte, non solo fisico (knockback
    // azzerato durante la difesa) ma anche punitivo.
    public float DamageMultiplier { get; set; } = 1f;

    // finestra di colpo e fase Active aperte e chiuse insieme, come per il player
    private void OpenWindow(Hitbox hitbox, float damage)
    {
        combatState.OpenActiveWindow();
        hitbox?.Activate(damage);
    }

    private void CloseWindow(Hitbox hitbox)
    {
        combatState.CloseActiveWindow();
        hitbox?.Deactivate();
    }

    private float UnarmedDamage()
    {
        var w = weaponManager?.CurrentWeapon;
        return (w != null && w.isFists ? w.damage : 0f) * DamageMultiplier;
    }
}

using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float smoothTime = 0.1f;
    [Tooltip("SmoothDamp e' simmetrico: con smoothTime tarato per una partenza morbida, fermarsi ci mette 5-6 volte tanto a scendere sotto la soglia di 0.1 che fa scattare Idle (misurato: ~0.6s), e si legge come 'continua a camminare'. Sotto questo tempo, in frenata, si usa questo valore molto piu' basso invece di smoothTime.")]
    [SerializeField] private float stopSmoothTime = 0.02f;
    [SerializeField] private Hitbox rightHandHitbox;
    [SerializeField] private Hitbox leftHandHitbox;
    [SerializeField] private Hitbox rightFootHitbox;
    [SerializeField] private Hitbox leftFootHitbox;
    [SerializeField] private float comboResetTime = 1f;
    [Tooltip("Per quanto un comando d'attacco resta in coda se non e' eseguibile subito. Senza, premere durante una parata o una reazione al colpo perdeva l'input.")]
    [SerializeField] private float inputBufferTime = 0.25f;
    [Tooltip("Durata massima della finestra di parata, in secondi, dal momento del comando. E' solo una rete di sicurezza: normalmente a chiuderla e' l'animation event OnParryEnd. Serve perche' se l'animazione viene interrotta prima dell'evento (es. parata annullata in attacco), senza questo tetto si resterebbe 'in parata' per sempre, che e' un exploit. Va tenuto sopra il tempo reale a cui arriva OnParryEnd, misurato in Play Mode, o taglierebbe la finestra prima del previsto.")]
    [SerializeField] private float parryMaxWindow = 0.8f;
    [Tooltip("Quanto velocemente si passa da postura pacifica a combat.")]
    [SerializeField] private float stanceBlendSpeed = 6f;
    [Tooltip("Velocita' in m/s a cui e' tarata la clip di camminata: e' la SOGLIA DEL BLEND TREE (4.8), non la velocita' del player, che puo' essere diversa. Sotto questo valore la riproduzione rallenta in proporzione per non far slittare i piedi.")]
    [SerializeField] private float walkClipSpeed = 4.8f;
    [Tooltip("Ritmo minimo di riproduzione della locomozione, sotto la velocita' di camminata. A zero il passo si congelerebbe alle andature lentissime: meglio un filo di scivolamento che un personaggio che avanza al rallentatore.")]
    [SerializeField] private float minLocomotionPlaybackRate = 0.35f;

    [Header("Moltiplicatori di danno per colpo (sul danno base dell'arma equipaggiata)")]
    [Tooltip("Nello stesso ordine della combo: Pugno destro, Calcio sinistro, Pugno sinistro, Calcio destro. Moltiplica il danno dell'arma a mani nude (fists) in WeaponData.")]
    [SerializeField] private float[] punchComboMultiplier = { 1f, 1.1f, 1f, 1.2f };

    [Tooltip("Nello stesso ordine della combo: Fendente 1, Fendente 2, Affondo, Finale. Moltiplica il danno dell'arma equipaggiata in WeaponData.")]
    [SerializeField] private float[] slashComboMultiplier = { 1f, 1f, 1.2f, 1.5f };
    [Tooltip("Sciabolata ad area (tasto dedicato, spada in pugno). Moltiplica il danno dell'arma equipaggiata.")]
    [SerializeField] private float areaSlashMultiplier = 1.8f;

    private Animator animator;
    private Health health;
    private CombatState combatState;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    private static readonly int StanceHash = Animator.StringToHash("Stance");
    private static readonly int IsLockedHash = Animator.StringToHash("IsLocked");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int SpeedMultiplierHash = Animator.StringToHash("SpeedMultiplier");

    // Layer "Weapon_Overlay": mascherato su busto/braccia, sovrascrive la posa a mani
    // nude con quella da spada. Indice 1 perche' e' il secondo layer del controller,
    // subito dopo la Base Layer.
    private const int WeaponOverlayLayerIndex = 1;
    private float currentSpeed;
    private float speedVelocity;
    private Vector2 currentMove;
    private Vector2 moveVelocity;
    private int punchComboStep;
    private float lastPunchTime;
    private bool punchQueued;
    private bool comboAdvancePending;
    private float bufferedPunchTime = -1f;
    private int pendingPunchStateHash;

    // Aperta SINCRONA nel frame del comando (vedi TriggerParry), non quando
    // l'Animator entra davvero nello stato Block: la transizione + il ritardo di
    // elaborazione del trigger costavano 200-300ms misurati, che sui pugni del
    // simp (startup 260-278ms) rendevano la parata di reazione impossibile in
    // pratica — la finestra si apriva dopo che la hitbox era gia' passata.
    private bool parryActive;
    private float parryCommandTime = -1f;

    // Combo corpo a corpo: pugno destro -> calcio sinistro -> pugno sinistro -> calcio destro
    private static readonly int[] PunchTriggers =
    {
        Animator.StringToHash("Punch_right"),
        Animator.StringToHash("Kick_left"),
        Animator.StringToHash("Punch_left"),
        Animator.StringToHash("Kick_right"),
    };
    private static readonly int[] PunchStates =
    {
        Animator.StringToHash("Punch_Right"),
        Animator.StringToHash("Kick_Left"),
        Animator.StringToHash("Punch_Left"),
        Animator.StringToHash("Kick_Right"),
    };

    // Combo di spada: slash 1 -> slash 2 -> pierce -> final
    private static readonly int[] SlashTriggers =
    {
        Animator.StringToHash("Slash"),
        Animator.StringToHash("Slash_2"),
        Animator.StringToHash("Sword_pierce"),
        Animator.StringToHash("Sword_final"),
    };
    private static readonly int[] SlashStates =
    {
        Animator.StringToHash("Slash"),
        Animator.StringToHash("Slash_2"),
        Animator.StringToHash("Sword_Pierce"),
        Animator.StringToHash("Sword_Final"),
    };

    private static readonly int TakeHitStateHash = Animator.StringToHash("Take_hit");
    private static readonly int AreaSlashHash = Animator.StringToHash("Area_slash");
    private static readonly int AreaSlashStateHash = Animator.StringToHash("Area_Slash");

    private static readonly int BlockHash      = Animator.StringToHash("Block");
    private static readonly int BlockStateHash = Animator.StringToHash("Block");

    public enum DashDirection { Left, Right, Front, Back }

    private static readonly int[] DashTriggers =
    {
        Animator.StringToHash("Dash_left"),
        Animator.StringToHash("Dash_right"),
        Animator.StringToHash("Dash_front"),
        Animator.StringToHash("Dash_back"),
    };
    private static readonly int[] DashStates =
    {
        Animator.StringToHash("Dash_Left"),
        Animator.StringToHash("Dash_Right"),
        Animator.StringToHash("Dash_Front"),
        Animator.StringToHash("Dash_Back"),
    };

    private int slashComboStep;
    private float lastSlashTime;
    private bool slashQueued;
    private bool slashComboAdvancePending;
    private int pendingSlashStateHash;
    private WeaponManager weaponManager;
    private Knockback knockback;

    // postura: 0 = combat (a mani nude), 1 = con l'arma in pugno.
    // La postura pacifica e' stata rimossa: il player e' sempre in guardia.
    private float currentStance;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        health = GetComponent<Health>();

        combatState = GetComponent<CombatState>();
        if (combatState == null) combatState = gameObject.AddComponent<CombatState>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        weaponManager = GetComponent<WeaponManager>();
        knockback = GetComponent<Knockback>();

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

    // Con lo stick la velocita' e' continua, ma le clip di locomozione no: sotto la
    // soglia di camminata il blend tree mescola idle e camminata e la clip continua a
    // girare al proprio ritmo, quindi i piedi slittano. Rallentando la riproduzione in
    // proporzione alla velocita' reale il passo torna attaccato a terra.
    //
    // Sopra quella soglia il moltiplicatore resta 1: li' ci pensano gia' le soglie del
    // blend tree, che sono in m/s veri, a tenere insieme passo e spostamento.
    //
    // Il riferimento e' la soglia del blend tree, NON walkSpeed del player: sono due
    // numeri diversi (4.8 contro 2.5 sul prefab) e usare il secondo terrebbe il passo
    // a ritmo pieno proprio dove la clip sta gia' correndo piu' del personaggio.
    //
    // Il valore va sugli stati di locomozione (vedi il parametro SpeedMultiplier nel
    // controller), non sull'Animator intero: moltiplicare tutto rallenterebbe anche
    // pugni e parate.
    private float LocomotionPlaybackRate()
    {
        if (walkClipSpeed <= 0f || currentSpeed >= walkClipSpeed) return 1f;

        return Mathf.Max(currentSpeed / walkClipSpeed, minLocomotionPlaybackRate);
    }

    private void Update()
    {
        // Durante il rinculo da knockback il CharacterController si muove (Knockback
        // chiama controller.Move()), e quel movimento finisce nello stesso
        // controller.velocity che leggiamo qui: senza questa esclusione il personaggio
        // resterebbe a camminare/correre mentre scivola indietro da un colpo, pur non
        // premendo nulla.
        bool inKnockback = knockback != null && knockback.IsActive;
        float targetSpeed = (!inKnockback && playerMovement != null) ? playerMovement.GetSpeed() : 0f;

        bool isDecelerating = targetSpeed < currentSpeed;
        currentSpeed = Mathf.SmoothDamp(
            currentSpeed,
            targetSpeed,
            ref speedVelocity,
            isDecelerating ? stopSmoothTime : smoothTime
        );

        animator.SetFloat(SpeedHash, currentSpeed);
        animator.SetFloat(SpeedMultiplierHash, LocomotionPlaybackRate());

        // Direzione di movimento RELATIVA a dove guarda il player. Serve sotto lock,
        // dove si resta rivolti al nemico e ci si sposta di lato o all'indietro: il
        // solo modulo della velocita' non distingue quei casi, e si camminerebbe
        // all'indietro con l'animazione della camminata in avanti.
        Vector3 localVelocity = playerMovement != null
            ? transform.InverseTransformDirection(playerMovement.GetHorizontalVelocity())
            : Vector3.zero;

        currentMove = Vector2.SmoothDamp(currentMove, new Vector2(localVelocity.x, localVelocity.z),
                                         ref moveVelocity, smoothTime);
        animator.SetFloat(MoveXHash, currentMove.x);
        animator.SetFloat(MoveZHash, currentMove.y);

        // Postura: 0 combat a mani nude, 1 con l'arma in mano. La sceglie l'arma
        // equipaggiata, non piu' un tasto.
        bool armed = weaponManager != null && weaponManager.CurrentWeapon != null && !weaponManager.CurrentWeapon.isFists;
        float targetStance = armed ? 1f : 0f;

        currentStance = Mathf.MoveTowards(currentStance, targetStance, stanceBlendSpeed * Time.deltaTime);
        animator.SetFloat(StanceHash, currentStance);

        // Locomozione libera (non agganciata) o in guardia: lo decide il lock, non
        // l'arma. Le vecchie animazioni restano quelle usate sotto lock.
        bool isLocked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;
        animator.SetBool(IsLockedHash, isLocked);

        var currentState = animator.GetCurrentAnimatorStateInfo(0);

        // Layer mascherato (solo busto/braccia): sceglie la posa delle braccia
        // (corpo a corpo o spada, blend interno su Stance) SOLO durante idle/
        // locomozione IN GUARDIA (agganciato). La libera ha gia' le braccia giuste
        // nelle proprie clip (Idle/Walk/Run/Walk_sword/Run_sword), quindi li' il
        // layer resta spento e non le tocca.
        bool inGuardLocomotionOrIdle = currentState.IsName("Idle") || currentState.IsName("Locomotion");
        animator.SetLayerWeight(WeaponOverlayLayerIndex, inGuardLocomotionOrIdle ? 1f : 0f);
        bool inPunchState = IsInAnyState(currentState.shortNameHash, PunchStates);
        bool inSlashState = IsInAnyState(currentState.shortNameHash, SlashStates);
        bool inHitState   = currentState.shortNameHash == TakeHitStateHash;
        // lo scatto ha fotogrammi di invulnerabilita': e' una schivata, se non
        // evitasse niente non sarebbe una scelta
        bool inDashState = IsInAnyState(currentState.shortNameHash, DashStates);
        // Fase dell'attacco in corso, letta dagli animation event delle hitbox.
        combatState.UpdatePhase(inPunchState || inSlashState, currentState.shortNameHash);

        // Rete di sicurezza per la parata: la chiude di norma OnParryEnd (animation
        // event su Hand_parry/Sword_parry), ma se l'animazione viene interrotta
        // prima che l'evento scatti (parata annullata in attacco, o un colpo
        // subito mentre si sta ancora entrando in Block) va chiusa comunque, o
        // resterebbe 'in parata' a oltranza.
        if (parryActive)
        {
            bool canceledIntoAction = inPunchState || inSlashState || inHitState || inDashState;
            bool timedOut = Time.time - parryCommandTime >= parryMaxWindow;
            if (canceledIntoAction || timedOut) EndParry();
        }

        if (health != null)
        {
            // Attaccare NON rende piu' invulnerabili: la protezione durante il colpo
            // e' ora limitata ai soli frame attivi (CombatState.IsArmored), dove si
            // incassa il danno senza essere interrotti. Startup e recovery sono
            // scoperti, ed e' quello che rende punibile un attacco tirato a caso.
            // Restano invulnerabili la capriola e la reazione al colpo: quest'ultima
            // e' cio' che impedisce a piu' simp di incatenare uno stunlock.
            health.IsInvincible = inHitState || inDashState;
            health.IsBlocking = parryActive;
        }

        if (slashComboAdvancePending && currentState.shortNameHash == pendingSlashStateHash)
            slashComboAdvancePending = false;

        if (slashQueued && !slashComboAdvancePending && !animator.IsInTransition(0))
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.shortNameHash == TakeHitStateHash)
            {
                slashQueued = false;
                return;
            }

            bool inSlash = IsInAnyState(state.shortNameHash, SlashStates);
            if (!inSlash || state.normalizedTime >= 0.5f)
            {
                slashQueued = false;
                FireSlash();
            }
        }

        if (comboAdvancePending && currentState.shortNameHash == pendingPunchStateHash)
            comboAdvancePending = false;

        // un comando dato mezzo istante troppo presto parte qui, appena si puo'
        ConsumePunchBuffer();

        if (punchQueued && !comboAdvancePending && !animator.IsInTransition(0))
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);

            if (state.shortNameHash == TakeHitStateHash)
            {
                punchQueued = false;
                return;
            }

            bool inPunch = IsInAnyState(state.shortNameHash, PunchStates);
            if (!inPunch || state.normalizedTime >= 0.5f)
            {
                punchQueued = false;
                FirePunch();
            }
        }
    }

    public void TriggerJump()
    {
        animator.SetTrigger(JumpHash);
    }

    // Scatto: la schivata rapida usata sotto lock, al posto della capriola.
    public void TriggerDash(DashDirection direction)
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        animator.SetTrigger(DashTriggers[(int)direction]);
    }

    public void TriggerParry()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        animator.SetTrigger(BlockHash);

        // Efficace da QUESTO istante, non da quando l'Animator entra davvero
        // nello stato Block: e' il punto di tutto il cambiamento, vedi il
        // commento sul campo parryActive.
        parryActive = true;
        parryCommandTime = Time.time;

        // Scritto anche qui, non solo in Update: passando per Update si perde
        // comunque un frame, che a 60fps sono 16ms ma con un calo di framerate
        // in mezzo alla mischia diventano molti di piu' — proprio quando la
        // parata serve. Cosi' e' sincrona col comando a qualunque framerate.
        if (health != null) health.IsBlocking = true;
    }

    // Animation event su Hand_parry/Sword_parry, nel punto in cui la guardia si
    // riabbassa: chiude la finestra di parata sui fotogrammi giusti invece che a
    // una percentuale fissa della clip, che si disallinea ogni volta che l'anim
    // viene rifatta con un ritmo diverso.
    public void OnParryEnd() => EndParry();

    private void EndParry()
    {
        parryActive = false;
        if (health != null) health.IsBlocking = false;
    }

    // Con l'arma in pugno, non a mani nude: e' PlayerMovement a doverlo sapere
    // per decidere se tenere premuto il tasto d'attacco deve accumulare verso la
    // sciabolata ad area invece che comportarsi come un tap normale.
    public bool IsArmed => weaponManager != null && weaponManager.CurrentWeapon != null && !weaponManager.CurrentWeapon.isFists;

    // Sciabolata ad area, sbloccata tenendo premuto il tasto d'attacco con la
    // spada in pugno. Trigger diretto come il parry, non entra nella coda dei
    // combo: e' un colpo a se', non una prosecuzione.
    public void TriggerAreaSlash()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        animator.SetTrigger(AreaSlashHash);
    }

    public void TriggerPunch()
    {
        if (weaponManager != null && weaponManager.CurrentWeapon != null && !weaponManager.CurrentWeapon.isFists)
        {
            TriggerSlash();
            return;
        }

        // Il comando viene sempre registrato, anche se in questo istante non e'
        // eseguibile: prima veniva scartato del tutto, ed era il motivo per cui
        // punire subito dopo una parata sembrava "non prendere l'input".
        bufferedPunchTime = Time.time;
        ConsumePunchBuffer();
    }

    // Prova a far partire il comando in coda. Richiamato anche da Update, cosi' un
    // input dato mezzo istante troppo presto parte da solo appena si puo'.
    private void ConsumePunchBuffer()
    {
        if (bufferedPunchTime < 0f) return;

        // scaduto: meglio perderlo che vederlo uscire con mezzo secondo di ritardo
        if (Time.time - bufferedPunchTime > inputBufferTime)
        {
            bufferedPunchTime = -1f;
            return;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);

        // Incassare un colpo non e' annullabile: e' la punizione. L'input resta pero'
        // in coda e parte appena si esce dalla reazione.
        // La parata invece SI: e' proprio quello il momento in cui si vuole punire.
        if (state.shortNameHash == TakeHitStateHash) return;

        bool inPunch = IsInAnyState(state.shortNameHash, PunchStates);

        // La combo si azzera solo se sei tornato fuori dagli attacchi da abbastanza
        // tempo. Confrontare solo il tempo trascorso non basta: le clip durano piu'
        // di comboResetTime, quindi la combo si sarebbe azzerata da sola a meta'
        // anche continuando a premere.
        if (!inPunch && Time.time - lastPunchTime > comboResetTime)
            punchComboStep = 0;

        if (!inPunch)
        {
            bufferedPunchTime = -1f;
            FirePunch();
        }
        else if (!comboAdvancePending)
        {
            bufferedPunchTime = -1f;
            punchQueued = true;
        }
        // se comboAdvancePending e' gia' true il comando resta in coda: c'e' gia' un
        // colpo non ancora confermato dall'Animator, non se ne impilano altri
    }

    private void TriggerSlash()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash == TakeHitStateHash) return;

        bool inSlash = IsInAnyState(state.shortNameHash, SlashStates);

        // stessa logica della combo corpo a corpo
        if (!inSlash && Time.time - lastSlashTime > comboResetTime)
            slashComboStep = 0;
        if (!inSlash)
            FireSlash();
        else if (!slashComboAdvancePending)
            slashQueued = true;
        // stessa logica della combo pugni: se c'e' gia' un colpo in coda non confermato, il click viene scartato
    }

    private static bool IsInAnyState(int hash, int[] states)
    {
        for (int i = 0; i < states.Length; i++)
            if (states[i] == hash) return true;
        return false;
    }

    private void FireSlash()
    {
        lastSlashTime = Time.time;
        pendingSlashStateHash = SlashStates[slashComboStep];
        slashComboAdvancePending = true;

        animator.SetTrigger(SlashTriggers[slashComboStep]);
        slashComboStep = (slashComboStep + 1) % SlashTriggers.Length;
    }

    // Aperture/chiusure della finestra di colpo, richiamate dagli animation event.
    // Sono nominate sull'ARTO e non sul tipo di colpo, cosi' un gomito o una
    // ginocchiata riuserebbero lo stesso evento senza toccare il codice - ma dato
    // che ogni arto e' comunque un solo colpo specifico della combo (vedi l'ordine
    // di PunchStates), il danno giusto lo troviamo guardando lo stato attivo.
    public void OnRightHandStart() => OpenWindow(rightHandHitbox, DamageForCurrentState(PunchStates, punchComboMultiplier));
    public void OnRightHandEnd()   => CloseWindow(rightHandHitbox);
    public void OnLeftHandStart()  => OpenWindow(leftHandHitbox, DamageForCurrentState(PunchStates, punchComboMultiplier));
    public void OnLeftHandEnd()    => CloseWindow(leftHandHitbox);

    public void OnRightFootStart() => OpenWindow(rightFootHitbox, DamageForCurrentState(PunchStates, punchComboMultiplier));
    public void OnRightFootEnd()   => CloseWindow(rightFootHitbox);
    public void OnLeftFootStart()  => OpenWindow(leftFootHitbox, DamageForCurrentState(PunchStates, punchComboMultiplier));
    public void OnLeftFootEnd()    => CloseWindow(leftFootHitbox);

    // OnWeaponStart invece e' davvero condiviso da 5 attacchi diversi (Slash,
    // Slash_2, Sword_Pierce, Sword_Final, Area_Slash): qui il controllo sullo
    // stato serve per forza, non e' solo un fallback.
    public void OnWeaponStart()
    {
        int hash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        float baseDamage = weaponManager?.CurrentWeapon?.damage ?? 0f;
        float multiplier = hash == AreaSlashStateHash ? areaSlashMultiplier : MultiplierForCurrentState(SlashStates, slashComboMultiplier);
        OpenWindow(weaponManager?.CurrentHitbox, multiplier * baseDamage);
    }
    public void OnWeaponEnd()      => CloseWindow(weaponManager?.CurrentHitbox);

    // Cerca lo stato attivo tra quelli elencati e moltiplica il danno base
    // dell'arma equipaggiata (WeaponData.damage) per il fattore nella stessa
    // posizione dell'array parallelo. I valori configurati qui sono fattori, non
    // danno assoluto: cosi' la stessa combo scala con qualunque arma equipaggiata,
    // non serve piu' un danno fisso duplicato per ogni arma.
    private float DamageForCurrentState(int[] stateHashes, float[] multipliers)
    {
        float baseDamage = weaponManager?.CurrentWeapon?.damage ?? 0f;
        return MultiplierForCurrentState(stateHashes, multipliers) * baseDamage;
    }

    private float MultiplierForCurrentState(int[] stateHashes, float[] multipliers)
    {
        int hash = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        for (int i = 0; i < stateHashes.Length; i++)
            if (stateHashes[i] == hash) return multipliers[i];
        return 0f;
    }

    // La finestra di colpo e la fase Active sono la stessa cosa vista da due lati:
    // vengono aperte e chiuse insieme, cosi' non possono divergere.
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

    private void FirePunch()
    {
        lastPunchTime = Time.time;
        pendingPunchStateHash = PunchStates[punchComboStep];
        comboAdvancePending = true;

        animator.SetTrigger(PunchTriggers[punchComboStep]);
        punchComboStep = (punchComboStep + 1) % PunchTriggers.Length;
    }

    public void ResetCombo()
    {
        punchQueued = false;
        comboAdvancePending = false;
        punchComboStep = 0;
        foreach (var t in PunchTriggers) animator.ResetTrigger(t);

        slashQueued = false;
        slashComboAdvancePending = false;
        slashComboStep = 0;
        foreach (var t in SlashTriggers) animator.ResetTrigger(t);
    }
}

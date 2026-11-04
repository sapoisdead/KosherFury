using UnityEngine;

// Lato player del sistema d'azioni: traduce i comandi in richieste al CombatActor e
// tiene le cose che sono solo del player, cioe' il buffer d'input, il bersaglio
// agganciato e la scelta del moveset dall'arma in mano.
//
// Buffer: il comando resta in coda per inputBufferTime e viene riproposto a ogni frame
// finche' il CombatActor non lo accetta. Cosi' il colpo successivo parte nel primo
// frame in cui si apre la finestra di combo, e uno premuto troppo presto scade da solo
// invece di impegnare il personaggio.
//
// I comandi arrivano da PlayerMovement, che riceve quelli del PlayerInput (Punch,
// AreaSlash, Block, Dodge) e li manda qui quando l'arma in mano ha un Moveset. Le regole
// che sono solo del player restano la': la schivata solo agganciati, il pesante solo armati.
[DisallowMultipleComponent]
[RequireComponent(typeof(CombatActor))]
public class PlayerCombatInput : MonoBehaviour
{
    private enum Command { None, Light, Heavy, Parry, Dodge }

    [Tooltip("Per quanto un comando resta in coda se non e' eseguibile subito.")]
    [SerializeField] private float inputBufferTime = 0.25f;

    private CombatActor actor;
    private PlayerMovement movement;
    private WeaponManager weapons;

    private Command buffered;
    private DodgeDirection bufferedDirection;
    private float bufferedTime;

    private void Awake()
    {
        actor = GetComponent<CombatActor>();
        movement = GetComponent<PlayerMovement>();
        weapons = GetComponent<WeaponManager>();
    }

    private void Update()
    {
        // contesto del player: mosse dell'arma in mano, bersaglio agganciato
        actor.Moveset = weapons != null && weapons.CurrentWeapon != null ? weapons.CurrentWeapon.moveset : null;
        actor.Target = TargetLockSystem.Instance != null ? TargetLockSystem.Instance.LockedTarget : null;

        if (movement != null && movement.InputLocked)
        {
            buffered = Command.None;
            return;
        }

        TryBuffered();
    }

    // L'arma in mano usa il sistema nuovo. Letto sull'arma e non sul CombatActor, perche'
    // il comando puo' arrivare prima del primo Update dopo un cambio d'arma.
    public bool HasMoveset => weapons != null && weapons.CurrentWeapon != null && weapons.CurrentWeapon.moveset != null;

    public void Light() => Buffer(Command.Light);
    public void Heavy() => Buffer(Command.Heavy);
    public void Parry() => Buffer(Command.Parry);

    public void Dodge(DodgeDirection direction)
    {
        bufferedDirection = direction;
        Buffer(Command.Dodge);
    }

    // Il comando prova a partire subito, nello stesso frame: passando dal solo Update
    // si perderebbe un frame, e per una parata di reazione conta.
    private void Buffer(Command command)
    {
        buffered = command;
        bufferedTime = Time.time;
        actor.Moveset = weapons != null && weapons.CurrentWeapon != null ? weapons.CurrentWeapon.moveset : null;
        TryBuffered();
    }

    private void TryBuffered()
    {
        if (buffered == Command.None) return;
        if (Time.time - bufferedTime > inputBufferTime)
        {
            buffered = Command.None;   // scaduto: meglio perderlo che vederlo uscire tardi
            return;
        }

        // in aria e nella prima parte dell'atterraggio il comando resta in coda
        if (movement != null && !movement.CanAct) return;

        bool accepted = buffered switch
        {
            Command.Light => actor.RequestLight(),
            Command.Heavy => actor.RequestHeavy(),
            Command.Parry => actor.RequestParry(),
            Command.Dodge => actor.RequestDodge(bufferedDirection),
            _ => false,
        };
        if (accepted) buffered = Command.None;
    }
}

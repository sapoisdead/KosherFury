using UnityEngine;
using UnityEngine.InputSystem;

// Lato player del sistema d'azioni: traduce i comandi in richieste al CombatActor e
// tiene le cose che sono solo del player, cioe' il buffer d'input, il bersaglio
// agganciato e la scelta del moveset dall'arma in mano.
//
// Buffer: il comando resta in coda per inputBufferTime e viene riproposto a ogni frame
// finche' il CombatActor non lo accetta. Cosi' il colpo successivo parte nel primo
// frame in cui si apre la finestra di combo, e uno premuto troppo presto scade da solo
// invece di impegnare il personaggio.
//
// FASE A.3: i comandi arrivano da tasti di prova, letti direttamente dai dispositivi,
// cosi' il sistema vecchio resta sui comandi veri. Funziona solo con un'arma che ha un
// Moveset (oggi la spada). In A.4 i comandi veri passano di qui.
//   P / Quadrato   colpo leggero         L / Triangolo   colpo pesante
//   K / L2         parata                O               schivata (direzione da WASD; ferma = indietro)
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

        ReadCommands();
        TryBuffered();
    }

    private void ReadCommands()
    {
        var keys = Keyboard.current;
        var pad = Gamepad.current;

        if ((keys != null && keys.pKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame)) Buffer(Command.Light);
        if ((keys != null && keys.lKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame)) Buffer(Command.Heavy);
        if ((keys != null && keys.kKey.wasPressedThisFrame) || (pad != null && pad.leftTrigger.wasPressedThisFrame)) Buffer(Command.Parry);
        if (keys != null && keys.oKey.wasPressedThisFrame)
        {
            bufferedDirection = DodgeFromKeys(keys);
            Buffer(Command.Dodge);
        }
    }

    private void Buffer(Command command)
    {
        buffered = command;
        bufferedTime = Time.time;
    }

    private void TryBuffered()
    {
        if (buffered == Command.None) return;
        if (Time.time - bufferedTime > inputBufferTime)
        {
            buffered = Command.None;   // scaduto: meglio perderlo che vederlo uscire tardi
            return;
        }

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

    // Direzione dominante, relativa a dove guarda il personaggio (sotto lock, il bersaglio)
    private static DodgeDirection DodgeFromKeys(Keyboard keys)
    {
        float x = (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f);
        float y = (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f);
        if (Mathf.Abs(x) > Mathf.Abs(y)) return x > 0f ? DodgeDirection.Right : DodgeDirection.Left;
        return y > 0f ? DodgeDirection.Forward : DodgeDirection.Back;
    }
}

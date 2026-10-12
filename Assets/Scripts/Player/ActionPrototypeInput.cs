using UnityEngine;
using UnityEngine.InputSystem;

// PROTOTIPO (Fase A.2), da togliere in A.3: due azioni di prova suonate dall'ActionPlayer
// su tasti liberi, senza passare dal sistema d'input di oggi.
//   Quadrato / P  -> colpo di prova (clip vecchia, finestre dai dati)
//   Triangolo / O -> scatto di prova (root motion attraverso l'ActionPlayer)
// Legge i dispositivi direttamente: le azioni dell'asset di input restano intatte.
[DisallowMultipleComponent]
public class ActionPrototypeInput : MonoBehaviour
{
    [SerializeField] private ActionSpec attack = new() { name = "Proto_Attack" };
    [SerializeField] private ActionSpec dash = new() { name = "Proto_Dash", isAttack = false, rootMotion = true };

    private ActionPlayer actionPlayer;
    private PlayerMovement movement;
    private CombatState combat;

    public ActionSpec Attack => attack;
    public ActionSpec Dash => dash;

    private void Awake()
    {
        actionPlayer = GetComponent<ActionPlayer>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<CombatState>();
    }

    private void Update()
    {
        if (actionPlayer == null || (movement != null && movement.InputLocked)) return;

        var pad = Gamepad.current;
        var keys = Keyboard.current;
        bool attackPressed = (pad != null && pad.buttonWest.wasPressedThisFrame) || (keys != null && keys.pKey.wasPressedThisFrame);
        bool dashPressed   = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (keys != null && keys.oKey.wasPressedThisFrame);

        if (attackPressed) TryPlay(attack);
        else if (dashPressed) TryPlay(dash);
    }

    // Parte da fermo, o in recovery dell'azione in corso: quella e' la catena di prova,
    // che passa per il secondo slot del mixer.
    public bool TryPlay(ActionSpec spec)
    {
        bool free = !actionPlayer.IsPlaying || combat == null || combat.Phase == AttackPhase.Recovery || !actionPlayer.Current.isAttack;
        if (!free) return false;
        actionPlayer.Play(spec);
        return true;
    }
}

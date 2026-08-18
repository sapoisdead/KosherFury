using UnityEngine;
using UnityEngine.InputSystem;

// Pausa secca: ferma il tempo di gioco e nient'altro, senza menu.
//
// Sta sul GameObject del player perche' PlayerInput consegna i comandi con
// SendMessages, che raggiunge solo il proprio GameObject: e' la stessa ragione per
// cui ci stanno TargetLockSystem e WeaponManager.
public class PauseController : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    private PlayerMovement movement;
    private bool inputLockedBeforePause;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();

        // La proprieta' e' statica: senza questo azzeramento, con il domain reload
        // disattivato la partita successiva ripartirebbe credendosi in pausa.
        IsPaused = false;
    }

    // Uscire dal Play Mode a gioco fermo lascerebbe Time.timeScale a 0 anche nella
    // sessione dopo.
    private void OnDisable()
    {
        if (IsPaused) SetPaused(false);
    }

    private void OnPause(InputValue value)
    {
        if (!value.isPressed) return;
        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        if (paused == IsPaused) return;
        IsPaused = paused;

        if (paused)
        {
            // Il rallentatore delle esecuzioni conta in secondi reali e si riporta a 1
            // da solo quando ha finito: lasciandolo vivo, dopo qualche secondo di pausa
            // spauserebbe il gioco. Annullarlo non costa nulla — dura una frazione di
            // secondo, e quando si riprende sarebbe comunque gia' finito.
            ExecutionSlowMo.Cancel();
            Time.timeScale = 0f;

            // I comandi continuano ad arrivare a tempo fermo, perche' l'Input System
            // non guarda timeScale: senza questo, un pugno premuto durante la pausa
            // partirebbe alla ripresa. Il valore precedente va conservato, o spausare
            // restituirebbe il controllo anche durante un blocco scriptato.
            if (movement != null)
            {
                inputLockedBeforePause = movement.InputLocked;
                movement.SetInputLocked(true);
            }
        }
        else
        {
            Time.timeScale = 1f;
            if (movement != null) movement.SetInputLocked(inputLockedBeforePause);
        }
    }
}

using UnityEngine;

// Variante di EnemyAnimator per DemonVale: niente pugni/calci, tre fendenti
// (Slash_1/Slash_2/Pierce) che si concatenano in combo tramite TriggerAttack(),
// come per Vale. Leap_attack e' invece una mossa speciale per chiudere la
// distanza: il behaviour tree la pilota a parte con TriggerLeapAttack(), non
// entra nella scelta casuale della combo.
public class DemonValeAnimator : EnemyAnimator
{
    private static readonly int Slash1Hash = Animator.StringToHash("Slash_1");
    private static readonly int Slash2Hash = Animator.StringToHash("Slash_2");
    private static readonly int PierceHash = Animator.StringToHash("Pierce");
    private static readonly int LeapAttackHash = Animator.StringToHash("Leap_attack");

    private static readonly int[] ComboTriggers = { Slash1Hash, Slash2Hash, PierceHash };

    // Stati (non trigger) dell'Animator di DemonVale: sostituiscono i Punch/Kick/Slash
    // di EnemyAnimator, cosi' CombatState traccia correttamente le fasi anche qui.
    private static readonly int[] DemonAttackStates =
    {
        Animator.StringToHash("Slash_1"),
        Animator.StringToHash("Slash_2"),
        Animator.StringToHash("Pierce"),
        Animator.StringToHash("Leap_attack"),
    };

    protected override int[] AttackStates => DemonAttackStates;

    private int lastComboIndex = -1;

    public override void TriggerAttack()
    {
        int index = Random.Range(0, ComboTriggers.Length);
        if (index == lastComboIndex)
            index = (index + 1 + Random.Range(0, ComboTriggers.Length - 1)) % ComboTriggers.Length;

        lastComboIndex = index;
        animator.SetTrigger(ComboTriggers[index]);
    }

    // Il caricamento prima dello stacco (e l'eventuale rinculo all'atterraggio)
    // non devono spostare il personaggio: LeapAttack aspetta questi due eventi
    // invece di muoversi da subito al trigger. Stessa convenzione degli
    // animation event sulle hitbox (nome sull'azione, metodo su questo
    // componente perche' sta sullo stesso GameObject dell'Animator).
    public bool LeapLaunched { get; private set; }
    public bool LeapLanded { get; private set; }

    public void TriggerLeapAttack()
    {
        LeapLaunched = false;
        LeapLanded = false;
        animator.SetTrigger(LeapAttackHash);
    }

    public void OnLeapLaunch() => LeapLaunched = true;
    public void OnLeapLand() => LeapLanded = true;
}

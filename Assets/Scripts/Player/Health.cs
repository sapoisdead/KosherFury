using System.Collections;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float hitStunDuration = 0.75f;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsInvincible { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsDead { get; private set; }
    public bool IsStunned => Time.time < stunnedUntil;

    // Come IsArmored ma esterno, non legato ai propri frame attivi: usato dalle
    // guardie di Vana mentre tengono la formazione, che incassano il danno ma
    // non devono flinchare/interrompersi (romperebbe la postazione fissa che
    // devono tenere). Il danno resta invariato, e' solo Stagger() a saltare.
    public bool IsUnstaggerable { get; set; }

    // Vero SOLO se lo stordimento in corso viene da una parata subita, non da una
    // semplice reazione da colpo normale (che chiama Stagger() allo stesso modo).
    // E' la condizione giusta per l'esecuzione: senza questa distinzione, il
    // secondo colpo di un qualunque combo normale si leggerebbe come "bersaglio
    // ancora stordito da parata" e ucciderebbe sul colpo.
    //
    // AlwaysExecutable copre il caso di un bersaglio che va ucciso al primo colpo
    // in una finestra scriptata (es. Vana in Defeat_idle), non legata a uno
    // stordimento da parata: il chiamante toglie IsInvincible e alza questo flag
    // solo per quella finestra, riusando lo stesso percorso di Hitbox.TryHit()
    // (hitstop/shake/slowmo da esecuzione) senza duplicarlo.
    public bool IsVulnerableToExecution => AlwaysExecutable || (IsStunned && stunnedFromParry);
    public bool AlwaysExecutable { get; set; }

    // Notifica la morte senza costringere chi ha bisogno di saperlo (es. VanaBoss,
    // che deve richiamare un nuovo simp) a fare polling su IsDead ogni frame.
    public event System.Action OnDeath;

    // Istante dell'ultimo colpo incassato (anche se parato). Serve all'IA per
    // ingaggiare chi la colpisce alle spalle, fuori dal cono visivo.
    public float LastDamagedTime { get; private set; } = -999f;

    // True nei soli frame attivi del proprio attacco: incassa il danno ma non viene
    // interrotto. Non e' cachato in Awake perche' i CombatState vengono aggiunti a
    // runtime dagli animator, e l'ordine degli Awake non e' garantito.
    public bool IsArmored => TryGetComponent(out CombatState combat) && combat.IsArmored;

    private float stunnedUntil = -1f;
    private bool stunnedFromParry;
    private Animator animator;
    private bool hasStunnedParam;
    private static readonly int TakeHitHash = Animator.StringToHash("Take_hit");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private static readonly int DeathStateHash = Animator.StringToHash("Death");
    private static readonly int StunnedHash = Animator.StringToHash("Stunned");

    private void Awake()
    {
        CurrentHealth = maxHealth;
        animator = GetComponent<Animator>();

        // Non tutti i controller hanno il parametro (il player non viene mai
        // parato: nessun nemico para). Senza questo controllo, SetBool su un
        // parametro inesistente stampa un warning a ogni frame.
        if (animator != null)
            foreach (var p in animator.parameters)
                if (p.nameHash == StunnedHash) { hasStunnedParam = true; break; }
    }

    // Tiene l'Animator allineato allo stordimento DA PARATA, che e' l'unico che
    // apre la finestra di risposta. E' la durata (hitStunDuration) a comandare,
    // l'animazione la segue: cosi' la posa di stordimento cicla esattamente
    // quanto dura la finestra, e le due non possono desincronizzarsi come
    // succedeva prima (clip da 0.60s reali contro 1.00s di stordimento).
    private void Update()
    {
        if (hasStunnedParam)
            animator.SetBool(StunnedHash, IsStunned && stunnedFromParry);
    }

    public void SetHealth(float amount)
    {
        CurrentHealth = Mathf.Clamp(amount, 0f, maxHealth);
    }

    // Ritorna true se il colpo e' stato parato (nessun danno, il chiamante deve
    // stordire l'attaccante), false in ogni altro caso (danno applicato, oppure
    // ignorato per morte/invincibilita' - nessuna reazione speciale richiesta).
    public bool TakeDamage(float amount)
    {
        if (IsDead) return false;
        if (IsInvincible) return false;

        // registrato prima del blocco: anche un colpo parato dice che qualcuno
        // ti sta attaccando, ed e' un buon motivo per girarti
        LastDamagedTime = Time.time;

        if (IsBlocking) return true;

        // Esecuzione: chi e' ancora stordito da una parata riuscita muore al colpo
        // successivo, qualunque sia il danno dell'arma. E' quello che rende la parata
        // la lettura piu' forte del gioco invece di un semplice "annulla il colpo".
        if (IsVulnerableToExecution)
        {
            CurrentHealth = 0f;
            Die();
            return false;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0f);

        if (CurrentHealth <= 0f)
            Die();
        else if (!IsArmored && !IsUnstaggerable)
            Stagger(fromParry: false);
        // Colpito sui propri frame attivi: incassa il danno ma il colpo esce lo
        // stesso. E' quello che permette gli scambi, invece del solo "chi parte
        // prima vince". Il feedback lo danno hitstop e VFX, che partono comunque.

        return false;
    }

    // Reazione da colpo subito o da parata riuscita contro l'attaccante: nessun
    // danno necessariamente coinvolto, solo l'animazione di reazione + una finestra
    // di stordimento durante la quale il chiamante (es. AttackPlayer) deve evitare
    // di far ripartire un nuovo attacco.
    public void Stagger(bool fromParry = false)
    {
        stunnedUntil = Time.time + hitStunDuration;
        stunnedFromParry = fromParry;
        animator?.SetTrigger(TakeHitHash);
    }

    private void Die()
    {
        IsDead = true;
        IsInvincible = true;
        OnDeath?.Invoke();

        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.enabled = false;

        var bt = GetComponent<BehaviourTree.BehaviourTreeBase>();
        if (bt != null) bt.enabled = false;

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        if (animator != null)
        {
            animator.SetTrigger(DeathHash);
            StartCoroutine(DespawnAfterDeathAnimation());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator DespawnAfterDeathAnimation()
    {
        float safety = 2f;
        while (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != DeathStateHash && safety > 0f)
        {
            safety -= Time.deltaTime;
            yield return null;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0);
        float remaining = state.shortNameHash == DeathStateHash
            ? Mathf.Max(0f, (1f - state.normalizedTime) * state.length)
            : 0f;

        yield return new WaitForSeconds(remaining);

        gameObject.SetActive(false);
    }
}

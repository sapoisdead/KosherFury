using BehaviourTree;
using UnityEngine;
using UnityEngine.AI;

// Strumento di prova, non di gioco: un nemico fermo che attacca il player a intervalli
// regolari con i suoi colpi veri, per allenare e tarare la parata. L'albero di
// comportamento viene spento: niente avvicinamenti, niente schivate, solo un colpo
// ogni 'interval' secondi, rivolto verso il player.
//
// Scrive nel log l'esito di ogni colpo (parato / preso / a vuoto), cosi' si vede
// subito se la finestra di parata e' troppo stretta o troppo larga.
//
// Lo crea il menu KosherFury/Combat/Parry Trainer (Play Mode).
[DisallowMultipleComponent]
public class ParryTrainer : MonoBehaviour
{
    [Tooltip("Secondi fra un attacco e il successivo.")]
    [SerializeField] private float interval = 2f;
    [Tooltip("Velocita' con cui si gira verso il player, in gradi al secondo.")]
    [SerializeField] private float turnSpeed = 720f;
    [Tooltip("Distanza fra i centri da cui attacca. A 1.0 m alcuni colpi del simp non arrivano (calcio sinistro 0.99 m): 0.9 sta dentro la portata di tutti e sopra il minimo fisico di 0.80. Prima di ogni colpo ci si riporta, perche' la spinta di un colpo andato a segno allontana il player.")]
    [SerializeField] private float attackDistance = 0.9f;
    [Tooltip("Velocita' con cui si riavvicina, in m/s.")]
    [SerializeField] private float stepSpeed = 3f;

    private EnemyAnimator enemyAnimator;
    private NavMeshAgent agent;
    private CombatState combat;
    private Health health;
    private Health playerHealth;
    private float nextAttack;

    // esito dell'attacco in corso
    private int trackedAttackId = -1;
    private bool parried;
    private float playerHealthAtStart;

    public void Setup(float attackInterval) => interval = attackInterval;

    private void Start()
    {
        enemyAnimator = GetComponent<EnemyAnimator>();
        combat = GetComponent<CombatState>();
        health = GetComponent<Health>();

        var bt = GetComponent<BehaviourTreeBase>();
        if (bt != null) bt.enabled = false;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.updateRotation = false;

        if (PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null)
            playerHealth = PlayerManager.Instance.PlayerTransform.GetComponent<Health>();
        if (playerHealth != null) playerHealth.OnParried += OnPlayerParried;

        nextAttack = Time.time + interval;
    }

    private void OnDestroy()
    {
        if (playerHealth != null) playerHealth.OnParried -= OnPlayerParried;
    }

    private void Update()
    {
        if (health == null || health.IsDead || playerHealth == null) return;

        FacePlayer();
        TrackOutcome();

        if (combat == null || combat.IsAttacking || enemyAnimator.IsBeingHit()) return;
        bool inPlace = StepToAttackDistance();

        if (Time.time < nextAttack || !inPlace) return;

        enemyAnimator.TriggerAttack();
        nextAttack = Time.time + interval;
    }

    // Si riporta a attackDistance lungo la linea verso il player. Vero quando e' arrivato.
    private bool StepToAttackDistance()
    {
        Vector3 toPlayer = Flat(playerHealth.transform.position) - Flat(transform.position);
        float gap = toPlayer.magnitude - attackDistance;
        if (Mathf.Abs(gap) < 0.03f) return true;

        Vector3 step = toPlayer.normalized * Mathf.Sign(gap) * Mathf.Min(Mathf.Abs(gap), stepSpeed * Time.deltaTime);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.Move(step);
        else transform.position += step;
        return false;
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = playerHealth.transform.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), turnSpeed * Time.deltaTime);
    }

    // Un attacco per volta: si apre quando CombatState cambia AttackId, si chiude quando
    // l'attacco finisce, e si scrive com'e' andato.
    private void TrackOutcome()
    {
        if (combat.IsAttacking && combat.AttackId != trackedAttackId)
        {
            trackedAttackId = combat.AttackId;
            parried = false;
            playerHealthAtStart = playerHealth.CurrentHealth;
            return;
        }

        if (trackedAttackId >= 0 && !combat.IsAttacking)
        {
            string outcome = parried ? "PARATO"
                           : playerHealth.CurrentHealth < playerHealthAtStart ? "preso"
                           : "a vuoto";
            float distance = Vector3.Distance(Flat(transform.position), Flat(playerHealth.transform.position));
            Debug.Log($"[ParryTrainer] colpo {outcome} (distanza {distance:0.00} m)");
            trackedAttackId = -1;
        }
    }

    private void OnPlayerParried()
    {
        if (trackedAttackId >= 0) parried = true;
    }

    private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}

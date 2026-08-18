using UnityEngine;
using UnityEngine.AI;

// Spinta da impatto: il bersaglio arretra nella direzione del colpo.
//
// Player e nemici si muovono con sistemi diversi (CharacterController contro
// NavMeshAgent) e non si possono spostare entrambi scrivendo su transform.position:
// il CharacterController ignorerebbe le collisioni e l'agent verrebbe strappato via
// dalla NavMesh. Questo componente rileva quale dei due c'e' e usa quello giusto.
[DisallowMultipleComponent]
public class Knockback : MonoBehaviour
{
    [Tooltip("Quanto in fretta si esaurisce la spinta. Piu' alto = arresto piu' secco.")]
    [SerializeField] private float decay = 9f;

    [Tooltip("Sotto questa velocita' la spinta si considera finita.")]
    [SerializeField] private float stopThreshold = 0.05f;

    private CharacterController controller;
    private NavMeshAgent agent;
    private Vector3 velocity;
    private bool agentWasStopped;
    private bool suspendedAgent;

    public bool IsActive => velocity.sqrMagnitude > stopThreshold * stopThreshold;

    // Moltiplicatore esterno sulla spinta, 1 di default. Serve a chi deve rendere
    // temporaneamente un personaggio impossibile da spostare a colpi (es. un simp
    // a scudo davanti a Vana: senza questo il player potrebbe aprirsi un varco a
    // furia di knockback invece che dovendo davvero passare oltre la formazione).
    public float ForceMultiplier { get; set; } = 1f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        agent = GetComponent<NavMeshAgent>();
    }

    /// direction va normalizzata sul piano orizzontale da chi chiama.
    public void Apply(Vector3 direction, float force)
    {
        force *= ForceMultiplier;
        if (force <= 0f) return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        velocity += direction.normalized * force;
    }

    private void Update()
    {
        if (!IsActive)
        {
            ReleaseAgent();
            velocity = Vector3.zero;
            return;
        }

        // durante il fermo immagine non ci si muove: la spinta parte dopo, ed e'
        // proprio quella sequenza (colpo -> pausa -> il corpo parte) a dare l'impatto
        if (HitStop.IsFrozen(gameObject)) return;

        Vector3 delta = velocity * Time.deltaTime;

        if (controller != null && controller.enabled)
        {
            controller.Move(delta);
        }
        else if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            // l'agent sta ancora inseguendo il player: senza sospenderlo la sua
            // guida contrasterebbe la spinta e il colpo non si vedrebbe
            SuspendAgent();
            agent.Move(delta);
        }
        else
        {
            transform.position += delta;
        }

        velocity = Vector3.MoveTowards(velocity, Vector3.zero, decay * Time.deltaTime);
    }

    private void SuspendAgent()
    {
        if (suspendedAgent) return;
        agentWasStopped = agent.isStopped;
        agent.isStopped = true;
        suspendedAgent = true;
    }

    private void ReleaseAgent()
    {
        if (!suspendedAgent) return;
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = agentWasStopped;
        suspendedAgent = false;
    }

    private void OnDisable()
    {
        // se veniamo disattivati a meta' spinta (per esempio alla morte) non lasciamo
        // l'agent fermo per sempre
        ReleaseAgent();
        velocity = Vector3.zero;
    }
}

using UnityEngine;
using UnityEngine.AI;

// Accompagna un minion appena generato dal punto di comparsa fino al punto in cui
// comincia davvero a combattere, poi si toglie di mezzo.
//
// Serve perche' il BehaviourTree ingaggia solo entro detectionRange: un minion che
// compare in cima alla scalinata e' troppo lontano dal player, quindi resterebbe
// fermo lassu' invece di scendere. Qui il tragitto d'ingresso e' imposto, e il BT
// viene acceso solo all'arrivo.
//
// L'albero resta spento per tutto il tragitto e non viene solo "ignorato": e' cosi'
// che ValerioBT.SetHome fa in tempo a essere raccolto, dato che i nodi ricevono la
// posizione di casa per copia nel momento in cui l'albero viene costruito.
[RequireComponent(typeof(NavMeshAgent))]
public class MinionEntrance : MonoBehaviour
{
    [Tooltip("Dove deve arrivare prima di iniziare a combattere.")]
    [SerializeField] private Vector3 entryPoint;
    [Tooltip("Entro quanto dal punto d'ingresso si considera arrivato.")]
    [SerializeField] private float arriveDistance = 1.5f;
    [Tooltip("Oltre questo tempo il BT viene acceso comunque: meglio un minion che combatte da un punto sbagliato di uno bloccato per sempre.")]
    [SerializeField] private float timeout = 20f;

    private NavMeshAgent agent;
    private ValerioBT brain;
    private float startedAt;

    public void Configure(Vector3 point, float arrive, float maxTime)
    {
        entryPoint = point;
        arriveDistance = arrive;
        timeout = maxTime;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        brain = GetComponent<ValerioBT>();

        // spento subito, prima che il suo Start possa costruire l'albero
        if (brain != null) brain.enabled = false;
    }

    private void Start()
    {
        startedAt = Time.time;

        if (agent.isOnNavMesh)
        {
            agent.updateRotation = true;      // durante il tragitto guarda dove va
            agent.stoppingDistance = 0.2f;
            agent.SetDestination(entryPoint);
        }
    }

    private void Update()
    {
        if (!agent.isOnNavMesh) return;

        Vector3 here = transform.position;
        Vector3 there = entryPoint;
        here.y = there.y = 0f;

        bool arrived = Vector3.Distance(here, there) <= arriveDistance;
        bool giveUp = Time.time - startedAt > timeout;

        if (!arrived && !giveUp) return;

        HandOver();
    }

    private void HandOver()
    {
        agent.ResetPath();

        if (brain != null)
        {
            // da qui in poi "casa" e' il punto d'ingresso, non la cima della scalinata
            brain.SetHome(transform.position, transform.rotation);
            brain.MarkAlerted();              // e' stato accompagnato apposta per raggiungere il player
            brain.enabled = true;             // ora l'albero viene costruito
        }

        Destroy(this);
    }
}

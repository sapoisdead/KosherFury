using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Genera ondate di nemici a intervalli regolari da un punto, e li avvia verso il
// punto d'ingresso dell'arena.
//
// Il tetto sui vivi non e' un dettaglio: senza, con ondate ogni 20 secondi e un
// player che non fa in tempo a ripulire, il numero cresce all'infinito e il
// combattimento smette di essere leggibile molto prima che il gioco rallenti.
public class WaveSpawner : MonoBehaviour
{
    [Header("Chi e quanti")]
    [SerializeField] private GameObject enemyPrefab;
    [Tooltip("Quanti nemici per ondata.")]
    [SerializeField] private int perWave = 2;
    [Tooltip("Secondi fra un'ondata e la successiva.")]
    [SerializeField] private float interval = 20f;
    [Tooltip("Ritardo prima della primissima ondata.")]
    [SerializeField] private float firstWaveDelay = 3f;
    [Tooltip("Massimo di nemici vivi contemporaneamente. 0 = nessun limite.")]
    [SerializeField] private int maxAlive = 8;

    [Header("Dove")]
    [Tooltip("Punto di comparsa. Se vuoto, si usa la posizione di questo oggetto.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Raggio entro cui distanziare i nemici di una stessa ondata, per non farli comparire uno dentro l'altro.")]
    [SerializeField] private float spawnSpread = 1.2f;
    [Tooltip("Punto verso cui si dirigono appena nati, prima di iniziare a combattere.")]
    [SerializeField] private Transform entryPoint;

    [Header("Contenitore")]
    [Tooltip("Sotto quale oggetto appendere i nemici generati. Solo ordine in gerarchia.")]
    [SerializeField] private Transform parent;

    private readonly List<GameObject> alive = new();
    private float nextWaveTime;
    private int waveNumber;

    public int AliveCount { get { Prune(); return alive.Count; } }
    public int WaveNumber => waveNumber;

    private void Start()
    {
        nextWaveTime = Time.time + firstWaveDelay;
    }

    private void Update()
    {
        if (Time.time < nextWaveTime) return;

        nextWaveTime = Time.time + interval;
        SpawnWave();
    }

    private void SpawnWave()
    {
        Prune();

        if (enemyPrefab == null) return;

        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 target = entryPoint != null ? entryPoint.position : origin;

        waveNumber++;
        int spawned = 0;

        for (int i = 0; i < perWave; i++)
        {
            if (maxAlive > 0 && alive.Count >= maxAlive) break;

            // distribuiti su un cerchio attorno al punto di comparsa: nascendo tutti
            // sullo stesso punto si spingerebbero a vicenda fuori dalla NavMesh
            float angle = (i / Mathf.Max(1f, perWave)) * Mathf.PI * 2f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnSpread;
            Vector3 wanted = origin + offset;

            if (!NavMesh.SamplePosition(wanted, out var hit, 3f, NavMesh.AllAreas))
                continue;                       // niente NavMesh li': salto, non lo lascio cadere

            var enemy = Instantiate(enemyPrefab, hit.position, Quaternion.LookRotation(
                new Vector3(target.x - hit.position.x, 0f, target.z - hit.position.z)));

            if (parent != null) enemy.transform.SetParent(parent, true);

            // L'accompagnatore va aggiunto PRIMA che il BT possa partire: il suo
            // Awake spegne l'albero, e Awake dei componenti aggiunti da script
            // gira prima del primo Update dell'oggetto appena istanziato.
            var entrance = enemy.AddComponent<MinionEntrance>();
            entrance.Configure(target, 1.5f, 20f);

            alive.Add(enemy);
            spawned++;
        }

        if (spawned > 0)
            Debug.Log($"[WaveSpawner] ondata {waveNumber}: {spawned} nemici, vivi ora {alive.Count}");
    }

    private void Prune()
    {
        alive.RemoveAll(e => e == null || !e.activeInHierarchy);
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;

        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(origin, spawnSpread);

        if (entryPoint != null)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(entryPoint.position, 1.5f);
            Gizmos.DrawLine(origin, entryPoint.position);
        }
    }
}

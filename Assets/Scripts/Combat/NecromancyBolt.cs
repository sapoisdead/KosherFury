using UnityEngine;

// Variante "necromantica" di Combat/Projectile: stessa messa in scena (carica in
// mano mentre sale, poi lancio), ma vola verso un punto FERMO (il cadavere
// bersaglio, non il player) e alla fine invoca una callback invece di infliggere
// danno. Tenuto separato da Projectile.cs apposta per non toccare codice di
// combattimento critico: qui non c'e' nessuna nozione di danno/hitstop/
// knockback/parata, solo volo + arrivo. Nessun homing sofisticato: il bersaglio
// non si muove, una correzione di rotta lineare verso il punto e' sufficiente.
public class NecromancyBolt : MonoBehaviour
{
    [Header("Carica (fra Prepare e Launch)")]
    [Tooltip("Velocita' di salita mentre e' ancora 'in mano', in m/s. 0 per staccarsi subito senza salire.")]
    [SerializeField] private float riseSpeed = 8f;
    [Tooltip("Quanto sale in totale prima di fermarsi e staccarsi dalla mano.")]
    [SerializeField] private float maxRiseHeight = 3f;

    [Header("Movimento")]
    [Tooltip("Velocita' di volo verso il cadavere, in m/s.")]
    [SerializeField] private float speed = 10f;
    [Tooltip("Entro questa distanza dal bersaglio si considera arrivato: la trasformazione scatta e il dardo si autodistrugge.")]
    [SerializeField] private float arriveDistance = 0.3f;
    [Tooltip("Offset verticale del punto mirato sul cadavere: punta un po' piu' in alto del piede per non infilarsi nel pavimento.")]
    [SerializeField] private float targetHeightOffset = 0.8f;

    [Header("Durata")]
    [Tooltip("Dopo quanto si autodistrugge se non e' mai arrivato (es. il cadavere e' sparito nel frattempo). Nessuna trasformazione in quel caso, nessun errore.")]
    [SerializeField] private float lifetime = 5f;

    private Transform target;
    private System.Action onArrival;
    private float elapsed;
    private bool launched;
    private bool detachedFromHand;
    private float risenSoFar;

    // Chiamato subito dopo Instantiate, mentre e' ancora "in mano": memorizza
    // bersaglio e callback ma non lo fa muovere. Stesso schema di
    // Projectile.Prepare, con la callback al posto del proprietario/danno.
    public void Prepare(Transform target, System.Action onArrival)
    {
        this.target = target;
        this.onArrival = onArrival;
    }

    public void Launch()
    {
        if (launched) return;
        launched = true;
        if (!detachedFromHand) DetachFromHand();
    }

    private void Update()
    {
        if (!launched)
        {
            if (detachedFromHand) return;

            if (riseSpeed > 0f && risenSoFar < maxRiseHeight)
            {
                float step = Mathf.Min(riseSpeed * Time.deltaTime, maxRiseHeight - risenSoFar);
                transform.position += Vector3.up * step;
                risenSoFar += step;
            }

            if (riseSpeed <= 0f || risenSoFar >= maxRiseHeight)
                DetachFromHand();

            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) { Destroy(gameObject); return; }

        // il cadavere e' sparito nel frattempo: nessuna trasformazione, nessun crash
        if (target == null) { Destroy(gameObject); return; }

        Vector3 aimPoint = target.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = aimPoint - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= arriveDistance)
        {
            onArrival?.Invoke();
            onArrival = null;
            Destroy(gameObject);
            return;
        }

        Vector3 dir = toTarget / dist;
        transform.rotation = Quaternion.LookRotation(dir);
        transform.position += dir * Mathf.Min(speed * Time.deltaTime, dist);
    }

    // Mantiene la posa mondiale attuale, cosi' non salta ne' di posizione ne' di
    // rotazione nel momento in cui perde il parent. Identico a Projectile.
    private void DetachFromHand()
    {
        detachedFromHand = true;
        transform.SetParent(null, true);
    }
}

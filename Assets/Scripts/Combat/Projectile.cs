using UnityEngine;

// Proiettile omingante generico: compare in un punto (di solito in mano, come
// figlio dell'osso durante la carica), insegue un bersaglio restando puntato su
// di lui, vola a velocita' costante e si autodistrugge alla scadenza del tempo
// di vita anche se non ha mai colpito nulla. Non e' legato a nessun nemico in
// particolare: chi lo spawna passa bersaglio e proprietario, tutto il resto
// (danno, velocita', durata) sta sui campi Inspector del prefab, come in Hitbox.
//
// Spawn e lancio sono due momenti separati (Prepare/Launch), non uno solo:
// mentre e' "in mano" segue il parent (l'osso) e sale lentamente in verticale
// (carica), cosi' si vede comparire prima di partire davvero. E' il chiamante
// (VanaBoss, due animation event distinti) a decidere quando passare dall'uno
// all'altro.
//
// Il controllo di collisione e' manuale (sweep + overlap), non un Collider +
// OnTriggerEnter: stesso motivo di Hitbox, un oggetto veloce puo' scavalcare il
// bersaglio fra due step della fisica se il controllo e' solo puntuale.
public class Projectile : MonoBehaviour
{
    [Header("Carica (fra Prepare e Launch)")]
    [Tooltip("Velocita' di salita mentre e' ancora 'in mano', in m/s. 0 per staccarsi subito senza salire.")]
    [SerializeField] private float riseSpeed = 2.5f;
    [Tooltip("Quanto sale in totale prima di fermarsi e staccarsi dalla mano, indipendentemente da quanto dura la carica: se OnCastRelease tarda, il proiettile smette di salire invece di scappare verso il soffitto.")]
    [SerializeField] private float maxRiseHeight = 2f;

    [Header("Movimento")]
    [Tooltip("Velocita' di volo, in m/s.")]
    [SerializeField] private float speed = 12f;
    [Tooltip("Quanto in fretta corregge la rotta verso il bersaglio, in gradi/secondo. Alto = insegue quasi in modo perfetto (missile a ricerca); piu' basso lo rende schivabile con una virata stretta, per quando servira' bilanciare.")]
    [SerializeField] private float turnRateDegPerSec = 720f;
    [Tooltip("Offset verticale del punto mirato sul bersaglio: il Transform del player e' a livello del CharacterController (i piedi), non del petto.")]
    [SerializeField] private float targetHeightOffset = 1.0f;

    [Header("Durata")]
    [Tooltip("Dopo quanto si autodistrugge se non ha ancora colpito nulla.")]
    [SerializeField] private float lifetime = 5f;

    [Header("Impatto")]
    [SerializeField] private float damage = 40f;
    [Tooltip("Raggio della sfera usata per il controllo di collisione.")]
    [SerializeField] private float hitRadius = 0.18f;
    [SerializeField] private float hitStop = 0.08f;
    [SerializeField, Range(0f, 1f)] private float cameraShake = 0.4f;
    [Tooltip("Spinta impressa al bersaglio colpito, in m/s. 0 per non spingere.")]
    [SerializeField] private float knockback = 4f;
    [Tooltip("Effetto istanziato nel punto di contatto. Lasciare vuoto per nessun effetto.")]
    [SerializeField] private GameObject impactVfx;
    [SerializeField] private float impactVfxLifetime = 1.5f;

    [Header("Debug")]
    [Tooltip("Solo per il disegno del gizmo: non influisce sui controlli.")]
    [SerializeField] private Color gizmoColor = new Color(0.6f, 0.1f, 0.85f, 0.5f);

    private Transform target;
    private Health ownerHealth;
    private float elapsed;
    private bool launched;
    private bool detachedFromHand;
    private float risenSoFar;

    // Chiamato subito dopo Instantiate, mentre il proiettile e' ancora "in
    // mano": memorizza bersaglio e proprietario ma non lo fa muovere. Se e'
    // stato istanziato come figlio di un osso, resta agganciato e lo segue
    // finche' non finisce di salire (vedi Update).
    public void Prepare(Transform target, Health owner)
    {
        this.target = target;
        ownerHealth = owner;
    }

    // Il lancio vero e proprio: si assicura di essere gia' staccato dal parent
    // (normalmente lo e' gia', vedi Update — questo e' solo un fallback se
    // Launch arriva prima che la salita sia finita) e da qui in poi Update() lo
    // fa volare.
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
            if (detachedFromHand) return;   // gia' staccato, resta fermo ad aspettare Launch

            // Sale in mondiale mentre e' ancora figlio della mano: la componente
            // orizzontale la da' il parent (segue il gesto), quella verticale
            // questa qui. Il conto e' su risenSoFar, non sulla Y assoluta: la
            // mano stessa puo' muoversi in verticale durante il gesto, e non
            // deve confondersi con quanto il proiettile e' salito per conto suo.
            if (riseSpeed > 0f && risenSoFar < maxRiseHeight)
            {
                float step = Mathf.Min(riseSpeed * Time.deltaTime, maxRiseHeight - risenSoFar);
                transform.position += Vector3.up * step;
                risenSoFar += step;
            }

            // Finita la salita si stacca SUBITO dalla mano, non solo al Launch:
            // altrimenti resterebbe agganciato per tutto il resto della carica e
            // seguirebbe qualunque altro movimento dell'osso (es. il braccio che
            // si riporta indietro per il lancio), finendo schiacciato a terra o
            // in pose assurde invece di restare a mezz'aria ad aspettare.
            if (riseSpeed <= 0f || risenSoFar >= maxRiseHeight)
                DetachFromHand();

            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) { Destroy(gameObject); return; }
        if (target == null) { Destroy(gameObject); return; }

        Vector3 aimPoint = target.position + Vector3.up * targetHeightOffset;
        Vector3 toTarget = aimPoint - transform.position;
        Vector3 desiredDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward;

        Vector3 newDir = Vector3.RotateTowards(transform.forward, desiredDir,
            turnRateDegPerSec * Mathf.Deg2Rad * Time.deltaTime, 0f);
        transform.rotation = Quaternion.LookRotation(newDir);

        Vector3 prevPos = transform.position;
        Vector3 nextPos = prevPos + newDir * speed * Time.deltaTime;

        if (CheckHit(prevPos, nextPos)) return;   // TryHit ha gia' distrutto l'oggetto

        transform.position = nextPos;
    }

    // Mantiene la posa mondiale attuale, cosi' non salta ne' di posizione ne' di
    // rotazione nel momento in cui perde il parent.
    private void DetachFromHand()
    {
        detachedFromHand = true;
        transform.SetParent(null, true);
    }

    // ---------------------------------------------------------------- collisione

    private bool CheckHit(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;

        if (dist > 0.0001f)
        {
            var sweepHits = Physics.SphereCastAll(from, hitRadius, delta / dist, dist, ~0, QueryTriggerInteraction.Collide);
            foreach (var h in sweepHits)
                if (TryHit(h.collider)) return true;
        }

        // il punto d'arrivo va controllato anche a parte: lo sweep non rileva un
        // collider in cui ci si trova gia' immersi in partenza
        var overlap = Physics.OverlapSphere(to, hitRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var c in overlap)
            if (TryHit(c)) return true;

        return false;
    }

    private bool TryHit(Collider col)
    {
        Health victim = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
        if (victim == null) return false;
        if (ownerHealth != null && victim == ownerHealth) return false;   // non colpisce chi l'ha lanciato

        bool wasParried = victim.TakeDamage(damage);
        if (wasParried)
            ownerHealth?.Stagger(fromParry: true);

        GameObject attackerGo = ownerHealth != null ? ownerHealth.gameObject : gameObject;
        HitStop.Freeze(attackerGo, victim.gameObject, hitStop);

        if (InvolvesPlayer(victim))
            CameraShake.Add(cameraShake);

        if (knockback > 0f)
        {
            var kb = victim.GetComponentInChildren<Knockback>();
            if (kb != null)
                kb.Apply(victim.transform.position - transform.position, knockback);
        }

        SpawnImpact(transform.position);
        Destroy(gameObject);
        return true;
    }

    private bool InvolvesPlayer(Health victim)
    {
        Transform playerRoot = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerTransform : null;
        return playerRoot != null && victim.transform == playerRoot;
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVfx == null) return;
        var fx = Instantiate(impactVfx, point, transform.rotation);
        Destroy(fx, impactVfxLifetime);
    }

    // ---------------------------------------------------------------- gizmo

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
}

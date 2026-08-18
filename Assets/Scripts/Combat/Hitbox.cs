using System.Collections.Generic;
using UnityEngine;

// Hitbox unica per pugni, calci e armi.
//
// Il Collider serve SOLO come descrittore di forma: viene tenuto come trigger e
// disabilitato, cosi' non interferisce con la fisica. I controlli sono query
// manuali fatte con la forma reale del collider (raggio/altezza/centro), non con
// i suoi 'bounds': i bounds sono un box allineato agli assi che contiene la forma
// ruotata, quindi usarli renderebbe la hitbox piu' grande e disallineata.
//
// Il colpo non viene testato in un singolo istante ma lungo una FINESTRA (Activate
// -> Deactivate, guidata dagli animation event) e lungo il PERCORSO compiuto fra
// un frame e l'altro: con hitbox piccole e colpi veloci, un controllo puntuale
// puo' mancare il bersaglio passandoci attraverso fra due step della fisica.
[RequireComponent(typeof(Collider))]
public class Hitbox : MonoBehaviour
{
    [Header("Impatto")]
    [Tooltip("Durata del congelamento all'impatto, in secondi. 0 per disattivarlo su questa hitbox.")]
    [SerializeField] private float hitStop = 0.07f;
    [Tooltip("Congelamento quando il colpo viene parato: di solito conviene piu' lungo, la parata deve premiare.")]
    [SerializeField] private float hitStopOnParry = 0.12f;
    [Tooltip("Congelamento su un'esecuzione (bersaglio ancora stordito da una parata riuscita). Il piu' lungo dei tre.")]
    [SerializeField] private float hitStopOnExecution = 0.18f;

    [Tooltip("Effetto istanziato nel punto di contatto. Lasciare vuoto per nessun effetto.")]
    [SerializeField] private GameObject impactVfx;
    [Tooltip("Dopo quanto viene distrutto l'effetto. Deve coprire tutta la sua durata.")]
    [SerializeField] private float impactVfxLifetime = 1.5f;

    [Tooltip("Scuotimento della camera all'impatto (0-1). Parte solo se il player e' coinvolto.")]
    [SerializeField, Range(0f, 1f)] private float cameraShake = 0.35f;
    [Tooltip("Scuotimento quando il colpo viene parato.")]
    [SerializeField, Range(0f, 1f)] private float cameraShakeOnParry = 0.55f;
    [Tooltip("Scuotimento quando il colpo e' un'esecuzione (bersaglio ancora stordito da una parata riuscita). Il piu' forte dei tre, per far sentire la punizione.")]
    [SerializeField, Range(0f, 1f)] private float cameraShakeOnExecution = 0.85f;

    [Tooltip("Time.timeScale durante il rallentamento su esecuzione. Deliberatamente globale, a differenza di HitStop: vedi ExecutionSlowMo.")]
    [SerializeField, Range(0.05f, 1f)] private float executionSlowMoScale = 0.15f;
    [Tooltip("Durata REALE (non scalata) del rallentamento su esecuzione.")]
    [SerializeField] private float executionSlowMoDuration = 0.2f;

    [Tooltip("Spinta impressa al bersaglio, in m/s. 0 per non spingere.")]
    [SerializeField] private float knockback = 3.5f;
    [Tooltip("Spinta impressa a CHI ATTACCA quando il colpo viene parato: e' la parata che lo respinge.")]
    [SerializeField] private float knockbackOnParry = 4.5f;

    [Header("Debug")]
    [Tooltip("Solo per il disegno del gizmo: non influisce sui controlli.")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.4f, 0f, 0.35f);

    private Collider shape;
    private readonly HashSet<GameObject> hitThisSwing = new();

    private bool active;
    private float damage;
    private Vector3 lastCenter;
    private bool hasLastCenter;
    private Health ownerHealth;

    public bool IsActive => active;

    // Chi sta tirando il colpo: il PRIMO antenato con una Health, cioe' il
    // personaggio, non transform.root.
    //
    // Con transform.root si prendeva la cima della gerarchia, che per un
    // personaggio istanziato a runtime coincide col personaggio stesso, ma per
    // uno piazzato in scena sotto un contenitore (es. le guardie sotto 'Simps')
    // e' IL CONTENITORE. Da li' GetComponentInChildren<Health>() restituiva
    // sempre la prima guardia del gruppo, chiunque avesse davvero colpito: la
    // parata stordiva la guardia sbagliata (e knockback, hitstop e direzione
    // del VFX puntavano al contenitore invece che a chi tirava il colpo).
    private Health OwnerHealth
    {
        get
        {
            if (ownerHealth == null) ownerHealth = GetComponentInParent<Health>();
            return ownerHealth;
        }
    }

    private Transform OwnerRoot => OwnerHealth != null ? OwnerHealth.transform : transform.root;

    private void Awake()
    {
        shape = GetComponent<Collider>();
        shape.isTrigger = true;
        shape.enabled = false;   // solo forma, niente fisica
    }

    // Apre la finestra di colpo. Il danno lo decide chi chiama (arma equipaggiata,
    // dati del nemico, ecc.): la hitbox resta ignara di cosa la sta usando.
    public void Activate(float damage)
    {
        this.damage = damage;
        hitThisSwing.Clear();
        active = true;
        hasLastCenter = false;

        // ririsolto a ogni swing: l'hitbox di un'arma puo' cambiare padrone
        // quando l'arma passa di mano, e una cache di Awake resterebbe vecchia
        ownerHealth = GetComponentInParent<Health>();

        // Un controllo SUBITO, senza aspettare il prossimo FixedUpdate. Con finestre
        // molto brevi (poche decine di ms) l'apertura e la chiusura possono cadere
        // fra due step della fisica, e il colpo non verrebbe mai testato: cosi' ogni
        // attivazione ha comunque almeno una lettura, qualunque sia la sua durata.
        Physics.SyncTransforms();
        Vector3 center = WorldCenter();
        foreach (var hit in StaticOverlap(center))
            TryHit(hit);

        lastCenter = center;
        hasLastCenter = true;
    }

    public void Deactivate()
    {
        active = false;
    }

    private void FixedUpdate()
    {
        if (!active) return;

        Physics.SyncTransforms();
        Vector3 center = WorldCenter();

        // percorso coperto da questo frame: senza questo, un colpo veloce puo'
        // scavalcare il bersaglio fra uno step e l'altro
        Collider[] hits = hasLastCenter
            ? Sweep(lastCenter, center)
            : StaticOverlap(center);

        foreach (var hit in hits)
            TryHit(hit);

        lastCenter = center;
        hasLastCenter = true;
    }

    // ---------------------------------------------------------------- query

    private Collider[] StaticOverlap(Vector3 center)
    {
        if (shape is CapsuleCollider capsule)
        {
            GetCapsulePoints(capsule, out Vector3 p0, out Vector3 p1, out float r);
            return Physics.OverlapCapsule(p0, p1, r, ~0, QueryTriggerInteraction.Collide);
        }

        return Physics.OverlapSphere(center, WorldRadius(), ~0, QueryTriggerInteraction.Collide);
    }

    private Collider[] Sweep(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;

        if (dist < 0.0001f)
            return StaticOverlap(to);

        Vector3 dir = delta / dist;
        RaycastHit[] sweepHits;

        if (shape is CapsuleCollider capsule)
        {
            GetCapsulePoints(capsule, out Vector3 p0, out Vector3 p1, out float r);
            // la capsula viene spazzata a partire dalla posizione precedente
            sweepHits = Physics.CapsuleCastAll(p0 - delta, p1 - delta, r, dir, dist, ~0, QueryTriggerInteraction.Collide);
        }
        else
        {
            sweepHits = Physics.SphereCastAll(from, WorldRadius(), dir, dist, ~0, QueryTriggerInteraction.Collide);
        }

        // lo sweep non rileva i collider in cui si e' gia' immersi in partenza,
        // quindi va unito al controllo statico sulla posizione finale
        var result = new List<Collider>(StaticOverlap(to));
        foreach (var h in sweepHits)
            if (!result.Contains(h.collider)) result.Add(h.collider);

        return result.ToArray();
    }

    // ---------------------------------------------------------------- forma

    private Vector3 WorldCenter()
    {
        if (shape is SphereCollider s)  return transform.TransformPoint(s.center);
        if (shape is CapsuleCollider c) return transform.TransformPoint(c.center);
        if (shape is BoxCollider b)     return transform.TransformPoint(b.center);
        return transform.position;
    }

    private float WorldRadius()
    {
        Vector3 sc = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(sc.x), Mathf.Abs(sc.y), Mathf.Abs(sc.z));

        if (shape is SphereCollider s)  return s.radius * maxScale;
        if (shape is CapsuleCollider c) return c.radius * maxScale;
        if (shape is BoxCollider b)     return Mathf.Max(b.size.x, b.size.y, b.size.z) * 0.5f * maxScale;
        return 0.1f;
    }

    private void GetCapsulePoints(CapsuleCollider c, out Vector3 p0, out Vector3 p1, out float radius)
    {
        Vector3 axis = c.direction == 0 ? Vector3.right
                     : c.direction == 1 ? Vector3.up
                     : Vector3.forward;

        Vector3 sc = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(sc.x), Mathf.Abs(sc.y), Mathf.Abs(sc.z));
        radius = c.radius * maxScale;

        float halfSegment = Mathf.Max(c.height * 0.5f * maxScale - radius, 0f);
        Vector3 worldCenter = transform.TransformPoint(c.center);
        Vector3 worldAxis = transform.TransformDirection(axis).normalized;

        p0 = worldCenter - worldAxis * halfSegment;
        p1 = worldCenter + worldAxis * halfSegment;
    }

    // ---------------------------------------------------------------- danno

    private void TryHit(Collider hit)
    {
        if (hit == shape) return;

        Health health = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
        if (health == null) return;

        // Identita' per PERSONAGGIO, non per radice della gerarchia: le guardie
        // condividono la radice 'Simps', quindi confrontando le radici
        // risultavano tutte lo stesso soggetto — nessuna poteva colpire una
        // compagna, e il dedup per swing le trattava come un bersaglio solo.
        Transform victimRoot = health.transform;
        if (victimRoot == OwnerRoot) return;                // non colpire chi lo sta tirando

        GameObject root = victimRoot.gameObject;
        if (hitThisSwing.Contains(root)) return;            // una volta sola per swing

        hitThisSwing.Add(root);

        bool victimArmored = health.IsArmored;
        // catturato PRIMA di TakeDamage: se il bersaglio e' vulnerabile a
        // esecuzione (stordito SPECIFICAMENTE da una parata, non da una reazione
        // da colpo normale), questo colpo lo uccide sul colpo (vedi Health.TakeDamage).
        bool isExecution = health.IsVulnerableToExecution && !health.IsBlocking;
        bool wasParried = health.TakeDamage(damage);
        if (wasParried)
            OwnerHealth?.Stagger(fromParry: true);

        float appliedHitStop = wasParried ? hitStopOnParry : (isExecution ? hitStopOnExecution : hitStop);
        HitStop.Freeze(OwnerRoot.gameObject, health.gameObject, appliedHitStop);

        // Solo se c'e' di mezzo il player: due nemici che si colpiscono lontano dalla
        // camera non devono muovere l'inquadratura.
        if (InvolvesPlayer(health))
        {
            float appliedShake = wasParried ? cameraShakeOnParry : (isExecution ? cameraShakeOnExecution : cameraShake);
            CameraShake.Add(appliedShake);

            if (isExecution)
                ExecutionSlowMo.Trigger(executionSlowMoScale, executionSlowMoDuration);
        }

        // Chi e' sui propri frame attivi non viene spinto: la spinta lo porterebbe
        // fuori dalla portata del colpo che sta gia' tirando, facendoglielo mancare
        // pur avendo "vinto" lo scambio. Su parata invece si spinge comunque,
        // perche' li' a essere respinto e' l'attaccante.
        if (wasParried || !victimArmored)
            ApplyKnockback(health, wasParried);

        SpawnImpact(ImpactPoint(hit));
    }

    private void ApplyKnockback(Health victim, bool wasParried)
    {
        Transform attacker = OwnerRoot;

        // colpo andato a segno: arretra il bersaglio, allontanandosi da chi ha colpito.
        // colpo parato: arretra chi ha attaccato, respinto dalla parata.
        // Sono i personaggi, non le radici: spingere transform.root avrebbe
        // spostato il contenitore 'Simps' con dentro tutte le guardie.
        Transform pushed = wasParried ? attacker : victim.transform;
        Transform origin = wasParried ? victim.transform : attacker;
        float force = wasParried ? knockbackOnParry : knockback;

        if (force <= 0f || pushed == null || origin == null) return;

        var kb = pushed.GetComponentInChildren<Knockback>();
        if (kb == null) return;

        kb.Apply(pushed.position - origin.position, force);
    }

    private bool InvolvesPlayer(Health victim)
    {
        Transform playerRoot = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerTransform : null;
        if (playerRoot == null) return false;

        return OwnerRoot == playerRoot || victim.transform == playerRoot;
    }

    // Le query di overlap restituiscono solo i collider colpiti, non un punto di
    // contatto. ClosestPoint da' il punto sulla superficie del bersaglio piu' vicino
    // alla hitbox: non e' il contatto fisicamente esatto, ma visivamente e'
    // indistinguibile e funziona con qualsiasi forma di collider.
    private Vector3 ImpactPoint(Collider hit)
    {
        Vector3 from = WorldCenter();
        Vector3 point = hit.ClosestPoint(from);

        // Se la hitbox e' gia' dentro al collider, ClosestPoint restituisce il punto
        // di partenza cosi' com'e' e l'effetto nascerebbe dentro al corpo, nascosto
        // dalla mesh. In quel caso lo riportiamo sulla superficie, dal lato da cui
        // e' arrivato il colpo.
        if ((point - from).sqrMagnitude > 0.000001f)
            return point;

        Vector3 outward = from - hit.bounds.center;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.000001f) outward = -transform.forward;
        outward.Normalize();

        // si interroga da un punto sicuramente esterno, cosi' ClosestPoint torna
        // a comportarsi come previsto
        Vector3 outside = hit.bounds.center + outward * (hit.bounds.extents.magnitude + 1f);
        Vector3 surface = hit.ClosestPoint(outside);

        // manteniamo l'altezza del colpo: senza questo un pugno al volto e un calcio
        // agli stinchi produrrebbero l'effetto alla stessa quota
        surface.y = from.y;
        return surface;
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVfx == null) return;

        // orientato verso chi ha colpito, cosi' l'effetto "legge" la direzione del
        // colpo invece di essere una macchia simmetrica
        Vector3 toAttacker = OwnerRoot.position - point;
        toAttacker.y = 0f;

        Quaternion rot = toAttacker.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toAttacker.normalized)
            : Quaternion.identity;

        var fx = Instantiate(impactVfx, point, rot);
        Destroy(fx, impactVfxLifetime);
    }

    // ---------------------------------------------------------------- gizmo

    private void OnDrawGizmos()
    {
        var col = shape != null ? shape : GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = Application.isPlaying && active
            ? new Color(1f, 0f, 0f, 0.5f)
            : gizmoColor;

        if (col is CapsuleCollider c)
        {
            GetCapsulePoints(c, out Vector3 p0, out Vector3 p1, out float r);
            Gizmos.DrawWireSphere(p0, r);
            Gizmos.DrawWireSphere(p1, r);
            Gizmos.DrawLine(p0, p1);
        }
        else
        {
            Gizmos.DrawWireSphere(WorldCenter(), WorldRadius());
        }
    }
}

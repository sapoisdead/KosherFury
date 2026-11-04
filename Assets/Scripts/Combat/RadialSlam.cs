using System.Collections.Generic;
using UnityEngine;

// Onda d'urto radiale: non si sposta, il suo raggio di danno cresce da 0 a
// maxRadius nel tempo (expandSpeed, non un timer fisso, cosi' resta coerente
// se in futuro cambia la scala dell'effetto). Pensata per Cast_2 di Vana (il
// cerchio che si apre dopo lo slam a terra), ma non legata a nessun nemico in
// particolare: chi la spawna passa solo il proprietario con Detonate().
//
// Stesso pattern di query di Hitbox/Projectile (Physics.OverlapSphere), e
// stesso schema di dedup per evitare colpi ripetuti mentre il cerchio
// continua a crescere sopra un bersaglio gia' colpito (hitThisSwing in
// Hitbox). Il knockback e' radiale "gratis": la direzione passata a
// Knockback.Apply e' vittima.position - transform.position, e siccome questo
// oggetto non si sposta mai, il risultato e' sempre "via dal centro"
// qualunque sia l'angolo del bersaglio.
public class RadialSlam : MonoBehaviour
{
    [Header("Espansione")]
    [Tooltip("Raggio massimo raggiunto dal cerchio.")]
    [SerializeField] private float maxRadius = 8f;
    [Tooltip("Velocita' di espansione, in m/s.")]
    [SerializeField] private float expandSpeed = 10f;
    [Tooltip("Quanto resta visibile a raggio pieno prima di autodistruggersi.")]
    [SerializeField] private float lingerTime = 0.3f;

    [Header("Impatto")]
    [SerializeField] private float damage = 50f;
    [SerializeField] private float hitStop = 0.1f;
    [SerializeField, Range(0f, 1f)] private float cameraShake = 0.5f;
    [Tooltip("Spinta impressa al bersaglio colpito, in m/s. 0 per non spingere.")]
    [SerializeField] private float knockback = 6f;
    [Tooltip("Effetto istanziato nel punto di contatto. Lasciare vuoto per nessun effetto.")]
    [SerializeField] private GameObject impactVfx;
    [SerializeField] private float impactVfxLifetime = 1.5f;

    [Header("Visuale")]
    [Tooltip("Trasform del disco piatto da scalare in XZ per seguire il raggio corrente. Puo' restare vuoto: in quel caso l'effetto fa comunque danno, solo senza segnaposto visivo (il gizmo resta, ma solo in editor).")]
    [SerializeField] private Transform visualDisc;
    [Tooltip("Diametro 'naturale' del disco a scala 1 (es. 1 per un cilindro/piano di default con diametro 1m): serve per calcolare il fattore di scala giusto dato il raggio corrente.")]
    [SerializeField] private float visualDiscBaseDiameter = 1f;

    [Header("Debug")]
    [Tooltip("Solo per il disegno del gizmo: non influisce sui controlli.")]
    [SerializeField] private Color gizmoColor = new Color(0.9f, 0.3f, 0.05f, 0.4f);

    private Health ownerHealth;
    private float currentRadius;
    private float lingerElapsed;
    private bool fullyExpanded;
    private readonly HashSet<GameObject> alreadyHit = new();

    // Chiamato subito dopo Instantiate, nel punto d'impatto gia' calcolato da
    // chi spawna (VanaBoss fa il proprio raycast verso il pavimento prima di
    // istanziare): questo componente non si sposta mai da li'.
    public void Detonate(Health owner)
    {
        ownerHealth = owner;
    }

    private void Update()
    {
        if (fullyExpanded)
        {
            lingerElapsed += Time.deltaTime;
            if (lingerElapsed >= lingerTime) Destroy(gameObject);
            return;
        }

        float previousRadius = currentRadius;
        currentRadius = Mathf.Min(currentRadius + expandSpeed * Time.deltaTime, maxRadius);

        if (currentRadius > previousRadius)
        {
            var overlap = Physics.OverlapSphere(transform.position, currentRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (var c in overlap)
                TryHit(c);
        }

        UpdateVisual();

        if (currentRadius >= maxRadius)
            fullyExpanded = true;
    }

    // Scala solo in XZ (il disco resta piatto, y invariata): il fattore e'
    // (diametro voluto) / (diametro a scala 1), cosi' funziona con qualunque
    // mesh di partenza purche' visualDiscBaseDiameter sia impostato giusto.
    private void UpdateVisual()
    {
        if (visualDisc == null || visualDiscBaseDiameter <= 0f) return;

        float scale = currentRadius * 2f / visualDiscBaseDiameter;
        Vector3 s = visualDisc.localScale;
        visualDisc.localScale = new Vector3(scale, s.y, scale);
    }

    private void TryHit(Collider col)
    {
        Health victim = col.GetComponent<Health>() ?? col.GetComponentInParent<Health>();
        if (victim == null) return;
        if (ownerHealth != null && victim == ownerHealth) return;   // non colpisce chi l'ha generata

        GameObject root = victim.gameObject;
        if (alreadyHit.Contains(root)) return;   // una volta sola, anche se il cerchio continua a crescere sopra di lui
        alreadyHit.Add(root);

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

        SpawnImpact(victim.transform.position);
    }

    private bool InvolvesPlayer(Health victim)
    {
        Transform playerRoot = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerTransform : null;
        return playerRoot != null && victim.transform == playerRoot;
    }

    private void SpawnImpact(Vector3 point)
    {
        if (impactVfx == null) return;
        var fx = Instantiate(impactVfx, point, Quaternion.identity);
        Destroy(fx, impactVfxLifetime);
    }

    // ---------------------------------------------------------------- gizmo

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(transform.position, Application.isPlaying ? currentRadius : maxRadius);
    }
}

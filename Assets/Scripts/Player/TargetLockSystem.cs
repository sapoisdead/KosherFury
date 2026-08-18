using UnityEngine;
using UnityEngine.InputSystem;

public class TargetLockSystem : MonoBehaviour
{
    public static TargetLockSystem Instance { get; private set; }

    [SerializeField] private float lockRange = 20f;

    public Transform LockedTarget { get; private set; }
    public bool IsLocked => LockedTarget != null;

    private void Awake()
    {
        Instance = this;
    }

    private void OnLock(InputValue value)
    {
        if (value.isPressed)
            ToggleLock();
    }

    private void Update()
    {
        if (!IsLocked) return;

        // Bersaglio morto: si passa da soli al prossimo, come se si ripremesse
        // il tasto. Se non ne restano, si sblocca e basta.
        if (IsDeadOrGone(LockedTarget))
        {
            LockedTarget = FindNearestTarget(exclude: LockedTarget);
            return;
        }

        // Uscito dal range: si sblocca soltanto, NON si passa al prossimo.
        // Allontanarsi e' una scelta del player: ritrovarsi agganciati a
        // qualcun altro sarebbe una sorpresa, non un aiuto.
        if (Vector3.Distance(transform.position, LockedTarget.position) > lockRange * 1.5f)
            LockedTarget = null;
    }

    // Health.IsDead e' vero nell'istante del colpo fatale, mentre l'oggetto
    // resta ATTIVO per tutta l'animazione di morte prima di sparire (vedi
    // Health.Die/DespawnAfterDeathAnimation): basarsi su activeSelf ritarderebbe
    // il cambio di bersaglio di quasi un secondo dopo il colpo che uccide.
    private static bool IsDeadOrGone(Transform target)
    {
        if (target == null || !target.gameObject.activeSelf) return true;

        Health health = target.GetComponent<Health>();
        return health != null && health.IsDead;
    }

    public void Unlock()
    {
        LockedTarget = null;
    }

    private void ToggleLock()
    {
        if (IsLocked)
        {
            LockedTarget = null;
            return;
        }

        LockedTarget = FindNearestTarget();
    }

    // Il piu' vicino entro lockRange. 'exclude' serve al cambio automatico alla
    // morte del bersaglio, per non riagganciare quello appena ucciso.
    private Transform FindNearestTarget(Transform exclude = null)
    {
        Health[] candidates = FindObjectsByType<Health>(FindObjectsInactive.Exclude);
        float minDist = lockRange;
        Transform nearest = null;

        foreach (Health h in candidates)
        {
            if (h.gameObject == gameObject) continue; // escludi il player
            if (!h.gameObject.activeSelf) continue;

            // Un nemico ucciso resta attivo per tutta l'animazione di morte:
            // senza questo, il "prossimo bersaglio" poteva essere un cadavere
            // (o il morto stesso da cui si stava passando).
            if (h.IsDead) continue;
            if (exclude != null && h.transform == exclude) continue;

            float dist = Vector3.Distance(transform.position, h.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = h.transform;
            }
        }

        return nearest;
    }
}

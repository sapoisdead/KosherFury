using UnityEngine;

public class PunchHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 0.35f;

    private float _gizmoTimer;

    public void CheckHit(float? overrideDamage = null)
    {
        _gizmoTimer = 0.3f;
        float dmg = overrideDamage ?? damage;

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        foreach (Collider hit in hits)
        {
            if (hit.transform.root == transform.root) continue;

            Health health = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
            if (health == null) continue;

            health.TakeDamage(dmg);
            return;
        }
    }

    private void Update()
    {
        if (_gizmoTimer > 0f)
            _gizmoTimer -= Time.deltaTime;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);

        if (_gizmoTimer > 0f)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

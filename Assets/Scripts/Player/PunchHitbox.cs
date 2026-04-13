using UnityEngine;

public class PunchHitbox : MonoBehaviour
{
    [SerializeField] private float damage = 25f;
    [SerializeField] private float radius = 1.0f;
    [SerializeField] private float forwardOffset = 0.8f;
    [SerializeField] private float heightOffset = 1.0f;

    public void CheckHit()
    {
        Transform root = transform.root;
        Vector3 center = root.position + root.forward * forwardOffset + Vector3.up * heightOffset;

        Collider[] hits = Physics.OverlapSphere(center, radius);
        foreach (Collider hit in hits)
        {
            if (hit.transform.root == root) continue;

            Health health = hit.GetComponent<Health>() ?? hit.GetComponentInParent<Health>();
            if (health == null) continue;

            health.TakeDamage(damage);
            return;
        }
    }
}

using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    private Collider hitCollider;
    private WeaponManager weaponManager;
    private readonly HashSet<GameObject> hitThisSwing = new();

    private void Awake()
    {
        hitCollider = GetComponent<Collider>();
        hitCollider.isTrigger = true;
        hitCollider.enabled = false;
        weaponManager = GetComponentInParent<WeaponManager>();

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void EnableHitbox()
    {
        hitThisSwing.Clear();
        hitCollider.enabled = true;
    }

    public void DisableHitbox()
    {
        hitCollider.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.root == transform.root) return;

        GameObject root = other.transform.root.gameObject;
        if (hitThisSwing.Contains(root)) return;

        Health health = other.GetComponent<Health>() ?? other.GetComponentInParent<Health>();
        if (health == null) return;

        hitThisSwing.Add(root);

        float damage = weaponManager?.CurrentWeapon?.damage ?? 0f;
        health.TakeDamage(damage);
    }

    private void OnDrawGizmos()
    {
        if (hitCollider == null) return;
        Gizmos.color = hitCollider.enabled
            ? new Color(1f, 0f, 0f, 0.4f)
            : new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, 0.15f);
    }
}

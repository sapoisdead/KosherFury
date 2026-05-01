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
        // Sblocca se il target è morto o uscito dal range
        if (IsLocked && (!LockedTarget.gameObject.activeSelf ||
            Vector3.Distance(transform.position, LockedTarget.position) > lockRange * 1.5f))
        {
            LockedTarget = null;
        }
    }

    private void ToggleLock()
    {
        if (IsLocked)
        {
            LockedTarget = null;
            return;
        }

        Health[] candidates = FindObjectsByType<Health>(FindObjectsInactive.Exclude);
        float minDist = lockRange;
        Transform nearest = null;

        foreach (Health h in candidates)
        {
            if (h.gameObject == gameObject) continue; // escludi il player
            if (!h.gameObject.activeSelf) continue;

            float dist = Vector3.Distance(transform.position, h.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = h.transform;
            }
        }

        LockedTarget = nearest;
    }
}

using UnityEngine;
using UnityEngine.UI;

public class LockOnIndicator : MonoBehaviour
{
    [SerializeField] private RectTransform indicator;
    [SerializeField] private float worldHeightOffset = 2.5f;

    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
        if (indicator != null)
            indicator.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (cam == null)
            cam = Camera.main;

        bool locked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;

        if (!locked)
        {
            indicator?.gameObject.SetActive(false);
            return;
        }

        indicator.gameObject.SetActive(true);

        Vector3 worldPos = TargetLockSystem.Instance.LockedTarget.position + Vector3.up * worldHeightOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        // Nasconde se il target è dietro la camera
        if (screenPos.z < 0f)
        {
            indicator.gameObject.SetActive(false);
            return;
        }

        indicator.position = screenPos;
    }
}

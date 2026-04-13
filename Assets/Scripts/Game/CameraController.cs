using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Distance")]
    [SerializeField] private float distance = 4f;

    [Header("Sensitivity")]
    [SerializeField] private float sensitivityX = 3f;
    [SerializeField] private float sensitivityY = 2f;

    [Header("Vertical Limits")]
    [SerializeField] private float minVerticalAngle = -20f;
    [SerializeField] private float maxVerticalAngle = 60f;

    [Header("Height Offset")]
    [SerializeField] private float heightOffset = 1.6f;

    [Header("Lock On")]
    [SerializeField] private float lockOnRotationSpeed = 8f;

    private float yaw;
    private float pitch = 20f;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (target == null)
            Debug.LogWarning("CameraController: nessun target assegnato.");

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        if (TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked)
        {
            // Yaw automatico verso il nemico lockato
            Vector3 toEnemy = TargetLockSystem.Instance.LockedTarget.position - target.position;
            toEnemy.y = 0f;
            float targetYaw = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, targetYaw, lockOnRotationSpeed * Time.deltaTime);

            // Pitch ancora manuale
            pitch -= mouseDelta.y * sensitivityY * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }
        else
        {
            yaw += mouseDelta.x * sensitivityX * Time.deltaTime;
            pitch -= mouseDelta.y * sensitivityY * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = target.position + Vector3.up * heightOffset;
        Vector3 cameraPosition = targetPosition - rotation * Vector3.forward * distance;

        transform.position = cameraPosition;
        transform.rotation = rotation;
    }
}

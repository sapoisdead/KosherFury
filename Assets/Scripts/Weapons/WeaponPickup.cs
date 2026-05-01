using UnityEngine;

public class WeaponPickup : MonoBehaviour
{
    [SerializeField] private WeaponData weaponData;

    [Header("Float & Rotate")]
    [SerializeField] private Vector3 rotationAxis = Vector3.up;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private float bounceHeight = 0.1f;
    [SerializeField] private float bounceSpeed = 2f;
    [SerializeField] private float hoverHeight = 0.5f;

    private Vector3 startPosition;
    private Transform visual;

    private void Start()
    {
        // Se l'arma è già stata raccolta, non comparire
        if (PlayerManager.Instance != null)
        {
            WeaponManager wm = PlayerManager.Instance.PlayerTransform?.GetComponent<WeaponManager>();
            if (wm != null && wm.HasWeapon(weaponData.weaponName))
            {
                Destroy(gameObject);
                return;
            }
        }

        visual = transform.GetChild(0);
        startPosition = transform.position + Vector3.up * hoverHeight;
        transform.position = startPosition;
    }

    private void Update()
    {
        if (visual != null)
            visual.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);

        float newY = startPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        WeaponManager wm = other.GetComponent<WeaponManager>();
        if (wm == null) return;

        wm.AddWeapon(weaponData);
        Destroy(gameObject);
    }
}

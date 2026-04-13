using UnityEngine;

public class MenorahCandle : MonoBehaviour
{
    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f;

    [Header("Bounce")]
    [SerializeField] private float bounceHeight = 0.1f;
    [SerializeField] private float bounceSpeed = 2f;
    [SerializeField] private float hoverHeight = 0.5f;

    private Vector3 startPosition;
    private Transform visual; // MenorahCandleCilinder

    private void Start()
    {
        // Trova il figlio visivo da ruotare
        visual = transform.GetChild(0);

        // Solleva il root e salva la posizione base per il bounce
        startPosition = transform.position + Vector3.up * hoverHeight;
        transform.position = startPosition;
    }

    private void Update()
    {
        // Ruota solo il visual (pivot centrato sul mesh) — non il root
        if (visual != null)
            visual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);

        // Bounce sinusoidale sul root
        float newY = startPosition.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        transform.position = new Vector3(startPosition.x, newY, startPosition.z);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        MenorahManager.Instance?.OnCandleCollected();
        Destroy(gameObject);
    }
}

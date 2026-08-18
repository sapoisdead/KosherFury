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

    [Header("Pad Sensitivity")]
    [Tooltip("Gradi al secondo a stick completamente inclinato. Non c'entra nulla con la sensibilita' del mouse: quella moltiplica pixel, questa moltiplica un valore fermo fra -1 e 1.")]
    [SerializeField] private float stickSensitivityX = 180f;
    [SerializeField] private float stickSensitivityY = 120f;

    [Header("Vertical Limits")]
    [SerializeField] private float minVerticalAngle = -20f;
    [SerializeField] private float maxVerticalAngle = 60f;
    [Tooltip("Inclinazione iniziale della camera. Piu' alto = piu' dall'alto, si vede meglio il combattimento.")]
    [SerializeField] private float defaultPitch = 28f;

    [Header("Height Offset")]
    [SerializeField] private float heightOffset = 1.6f;

    [Header("Lock On")]
    [SerializeField] private float lockOnRotationSpeed = 8f;
    [Tooltip("Scostamento laterale della camera sotto lock, in metri. Senza, camera-player-nemico sono allineati e il player copre completamente il nemico. Negativo = spalla sinistra.")]
    [SerializeField] private float lockShoulderOffset = 0.75f;
    [Tooltip("Quanto in fretta la camera scivola sopra la spalla quando agganci, e torna al centro quando sganci.")]
    [SerializeField] private float shoulderBlendSpeed = 6f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.05f;

    private float yaw;
    private float pitch;
    private Vector3 positionVelocity;

    // Posizione "pulita", senza scuotimento. Va tenuta separata da transform.position:
    // se lo SmoothDamp leggesse la posizione gia' scossa come punto di partenza, lo
    // scuotimento rientrerebbe nel calcolo del frame dopo e si auto-alimenterebbe.
    private Vector3 smoothedPosition;
    private bool hasSmoothedPosition;
    private float currentShoulder;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Start()
    {
        if (target == null)
            Debug.LogWarning("CameraController: nessun target assegnato.");

        // Il pitch parte da defaultPitch, non dalla rotazione del prefab: quella e'
        // (0,0,0) e lasciava la camera perfettamente orizzontale dietro le spalle.
        yaw = transform.eulerAngles.y;
        pitch = Mathf.Clamp(defaultPitch, minVerticalAngle, maxVerticalAngle);
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // A gioco fermo la camera deve stare ferma: qui si legge il mouse in
        // LateUpdate, e timeScale non ha alcun effetto su quella lettura.
        if (PauseController.IsPaused) return;

        // Mouse e stick destro non sono la stessa grandezza: il mouse da' i pixel
        // percorsi nel frame, lo stick una posizione ferma fra -1 e 1. Con una sola
        // sensibilita' per entrambi la camera sarebbe immobile col pad o impazzita col
        // mouse, quindi si sommano gia' convertiti in gradi.
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 stickInput = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;

        float yawSpeed = mouseDelta.x * sensitivityX + stickInput.x * stickSensitivityX;
        float pitchSpeed = mouseDelta.y * sensitivityY + stickInput.y * stickSensitivityY;

        bool locked = TargetLockSystem.Instance != null && TargetLockSystem.Instance.IsLocked;

        // Sfumato invece che istantaneo: agganciare non deve far saltare l'inquadratura di lato
        currentShoulder = Mathf.MoveTowards(currentShoulder, locked ? lockShoulderOffset : 0f,
                                            Mathf.Abs(lockShoulderOffset) * shoulderBlendSpeed * Time.deltaTime);

        if (locked)
        {
            // Yaw automatico verso il nemico lockato
            Vector3 toEnemy = TargetLockSystem.Instance.LockedTarget.position - target.position;
            toEnemy.y = 0f;
            float targetYaw = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
            yaw = Mathf.LerpAngle(yaw, targetYaw, lockOnRotationSpeed * Time.deltaTime);

            // Pitch ancora manuale
            pitch -= pitchSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }
        else
        {
            yaw += yawSpeed * Time.deltaTime;
            pitch -= pitchSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = target.position + Vector3.up * heightOffset;

        // Lo scostamento sposta la CAMERA, non il punto guardato: e' cosi' che il
        // player esce dalla retta camera-nemico e smette di coprirlo. Spostare invece
        // il punto guardato li terrebbe allineati, limitandosi a spostare tutto.
        Vector3 cameraPosition = targetPosition
                               - rotation * Vector3.forward * distance
                               + rotation * Vector3.right * currentShoulder;

        if (!hasSmoothedPosition)
        {
            smoothedPosition = cameraPosition;
            hasSmoothedPosition = true;
        }

        smoothedPosition = Vector3.SmoothDamp(smoothedPosition, cameraPosition, ref positionVelocity, positionSmoothTime);

        // lo scuotimento si somma DOPO lo smoothing, altrimenti verrebbe smorzato
        // proprio dallo smoothing che dovrebbe solo seguire il personaggio
        CameraShake.Evaluate(Time.unscaledDeltaTime, out Vector3 shakeOffset, out float shakeRoll);

        transform.rotation = rotation * Quaternion.Euler(0f, 0f, shakeRoll);
        transform.position = smoothedPosition
                           + transform.right * shakeOffset.x
                           + transform.up * shakeOffset.y;
    }
}

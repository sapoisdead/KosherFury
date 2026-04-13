using UnityEngine;
using TMPro;

public class EnemySpeech : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private TextMeshProUGUI speechText;

    [Header("Impostazioni")]
    [SerializeField] private float triggerDistance = 5f;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float cooldown = 3f;

    [TextArea]
    [SerializeField] private string[] phrases = {
        "Ciao, hai pregiudizi contro gli ex carcerati?",
        "Sono dell'ente distribuzione nazionale",
        "Dammi i soldi ebreo"
    };

    private float displayTimer;
    private float cooldownTimer;
    private bool isShowing;

    private void Start()
    {
        if (player == null && PlayerManager.Instance != null)
            player = PlayerManager.Instance.PlayerTransform;

        speechBubble.SetActive(false);
    }

    private void Update()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (isShowing)
        {
            displayTimer -= Time.deltaTime;

            // Ruota la bolla verso la camera
            speechBubble.transform.LookAt(Camera.main.transform);
            speechBubble.transform.Rotate(0f, 180f, 0f);

            if (displayTimer <= 0f)
            {
                speechBubble.SetActive(false);
                isShowing = false;
                cooldownTimer = cooldown;
            }
        }
        else
        {
            cooldownTimer -= Time.deltaTime;

            if (distanceToPlayer <= triggerDistance && cooldownTimer <= 0f)
            {
                ShowPhrase();
            }
        }
    }

    private void ShowPhrase()
    {
        speechText.text = phrases[Random.Range(0, phrases.Length)];
        speechBubble.SetActive(true);
        displayTimer = displayDuration;
        isShowing = true;
    }
}

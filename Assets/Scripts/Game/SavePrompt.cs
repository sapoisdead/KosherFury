using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;

public class SavePrompt : MonoBehaviour
{
    [SerializeField] private Text feedbackText;
    [SerializeField] private float feedbackDuration = 2f;

    private Coroutine feedbackCoroutine;

    private void Update()
    {
        if (!Keyboard.current.fKey.wasPressedThisFrame) return;

        if (MenorahManager.Instance == null || !MenorahManager.Instance.CanSave)
        {
            ShowFeedback("Raccogli tutte le candele per salvare.");
            return;
        }

        if (PersistenceManager.Instance == null) { Debug.LogWarning("[SavePrompt] PersistenceManager non trovato."); return; }
        if (PlayerManager.Instance == null) { Debug.LogWarning("[SavePrompt] PlayerManager non trovato."); return; }
        PersistenceManager.Instance.Save(PlayerManager.Instance.PlayerTransform.position);
        MenorahManager.Instance?.OnSaved();
        ShowFeedback("Partita salvata.");
    }

    private void ShowFeedback(string message)
    {
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
    }

    private IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
    }
}

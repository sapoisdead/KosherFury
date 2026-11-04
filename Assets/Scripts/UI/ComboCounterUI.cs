using TMPro;
using UnityEngine;

// Mostra la catena di colpi consecutivi.
//
// Legge ComboCounter a ogni frame invece di iscriversi a un evento: il contatore
// e' statico e con scadenza lazy (nessun Update proprio), quindi non ha un
// momento in cui "scade" da notificare. Interrogarlo e' l'unico modo per vedere
// la catena spegnersi da sola.
public class ComboCounterUI : MonoBehaviour
{
    [System.Serializable]
    private struct Tier
    {
        [Tooltip("Da questo numero di colpi in su vale questo colore.")]
        public int minCount;
        public Color color;
    }

    [SerializeField] private TextMeshProUGUI counterText;

    [Tooltip("Colori per fascia di combo, dal piu' basso al piu' alto. Vale l'ultima soglia raggiunta, quindi vanno tenute in ordine crescente.")]
    [SerializeField] private Tier[] tiers =
    {
        new Tier { minCount = 2,  color = new Color(1.00f, 0.86f, 0.45f) },   // oro caldo
        new Tier { minCount = 10, color = new Color(1.00f, 0.62f, 0.22f) },   // arancio
        new Tier { minCount = 25, color = new Color(1.00f, 0.34f, 0.20f) },   // rosso
        new Tier { minCount = 50, color = new Color(1.00f, 0.97f, 0.90f) },   // bianco incandescente
    };

    [Tooltip("Sotto questo numero la catena non si mostra: un '1' a ogni singolo colpo e' rumore, non informazione.")]
    [SerializeField] private int minToShow = 2;

    [Tooltip("Di quanto il numero 'batte' a ogni colpo nuovo.")]
    [SerializeField] private float popScale = 1.35f;
    [Tooltip("In quanto tempo il battito rientra, in secondi.")]
    [SerializeField] private float popRecovery = 0.12f;

    [Tooltip("Opacita' a catena quasi scaduta: sbiadendo si vede che sta per finire, senza bisogno di una barra.")]
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.35f;

    private int lastCount;
    private float pop;
    private Color baseColor;

    private void Awake()
    {
        if (counterText == null) counterText = GetComponent<TextMeshProUGUI>();
        // usato solo se le fasce non sono configurate: il colore non va mai
        // riletto dal testo a ogni frame, o la dissolvenza si accumulerebbe su
        // se stessa e il numero sparirebbe per non tornare piu'
        if (counterText != null) baseColor = counterText.color;
    }

    // Vale l'ultima soglia raggiunta. Scorre dalla piu' alta alla piu' bassa cosi'
    // l'ordine dell'array conta ma un buco non rompe niente.
    private Color ColorFor(int count)
    {
        if (tiers == null || tiers.Length == 0) return baseColor;

        Color chosen = tiers[0].color;
        for (int i = 0; i < tiers.Length; i++)
            if (count >= tiers[i].minCount) chosen = tiers[i].color;

        return chosen;
    }

    private void Update()
    {
        if (counterText == null) return;

        int count = ComboCounter.Count;

        if (count < minToShow)
        {
            if (counterText.enabled) counterText.enabled = false;
            lastCount = count;
            return;
        }

        if (!counterText.enabled) counterText.enabled = true;

        if (count != lastCount)
        {
            counterText.text = count.ToString();
            pop = 1f;
            lastCount = count;
        }

        pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime / Mathf.Max(0.01f, popRecovery));
        float s = 1f + (popScale - 1f) * pop;
        counterText.rectTransform.localScale = new Vector3(s, s, 1f);

        var c = ColorFor(count);
        c.a = Mathf.Lerp(minAlpha, 1f, ComboCounter.NormalizedTimeLeft);
        counterText.color = c;
    }
}

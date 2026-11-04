using TMPro;
using UnityEngine;

// Numeri di danno che compaiono sopra chi viene colpito.
//
// Il popup viene COSTRUITO DA CODICE invece che istanziato da un prefab. Non e'
// per eleganza: un riferimento a prefab va assegnato in scena, ed e' esattamente
// il punto in cui questo progetto si e' gia' bruciato una sessione (vedi
// VANA_BOSSFIGHT.md: set_property scriveva il riferimento solo in memoria e su
// disco il campo restava vuoto, senza errori). Senza asset da collegare, non c'e'
// niente che possa restare scollegato in silenzio.
public static class DamagePopup
{
    [System.Serializable]
    public struct Settings
    {
        public float duration;      // quanto resta visibile, in secondi
        public float riseSpeed;     // di quanto sale al secondo, in metri
        public float lateralSpread; // scostamento casuale ai lati, per non sovrapporli
        public float heightOffset;  // quanto sopra l'origine del personaggio nasce
        public float fontSize;
    }

    public static Settings settings = new Settings
    {
        duration = 0.8f,
        riseSpeed = 1.1f,
        lateralSpread = 0.35f,
        heightOffset = 1.7f,   // poco sopra la testa: i personaggi sono alti ~1.64 m
        fontSize = 25f,
    };

    // Colori per tipo di colpo, tutti da un posto solo.
    public static Color enemyHitColor   = new Color(1f, 0.95f, 0.85f);   // danno inflitto dal player
    public static Color playerHurtColor = new Color(1f, 0.30f, 0.25f);   // danno subito dal player
    public static Color executionColor  = new Color(1f, 0.82f, 0.25f);   // esecuzione dopo parata

    // Un canvas in world space misura in unita' di mondo: senza rimpicciolirlo, un
    // font da 36 sarebbe alto 36 metri.
    private const float WorldScale = 0.01f;

    /// amount viene arrotondato: i decimali su un numero che dura mezzo secondo
    /// non si leggono, e fanno solo rumore.
    public static void Show(Vector3 worldPosition, float amount, Color color, float scale = 1f)
    {
        if (amount <= 0f) return;
        if (Camera.main == null) return;   // niente camera, niente da mostrare

        var go = new GameObject("DamagePopup");
        go.transform.position = worldPosition
            + Vector3.up * settings.heightOffset
            + new Vector3(Random.Range(-settings.lateralSpread, settings.lateralSpread), 0f,
                          Random.Range(-settings.lateralSpread, settings.lateralSpread));
        go.transform.localScale = Vector3.one * WorldScale;

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = canvas.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200f, 80f);

        var text = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = Mathf.RoundToInt(amount).ToString();
        text.color = color;
        text.fontSize = settings.fontSize * scale;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.fontStyle = FontStyles.Bold;

        var rise = go.AddComponent<Riser>();
        rise.Init(text, settings.duration, settings.riseSpeed);
    }

    // Salita + dissolvenza. Vive in LateUpdate come HitImpactReaction, cosi' il
    // billboard usa la posizione della camera gia' aggiornata per questo frame e
    // il numero non insegue l'inquadratura con un frame di ritardo.
    private class Riser : MonoBehaviour
    {
        private TextMeshProUGUI text;
        private float duration;
        private float riseSpeed;
        private float elapsed;
        private Color baseColor;

        public void Init(TextMeshProUGUI text, float duration, float riseSpeed)
        {
            this.text = text;
            this.duration = Mathf.Max(0.01f, duration);
            this.riseSpeed = riseSpeed;
            baseColor = text.color;
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            float k = elapsed / duration;

            if (k >= 1f) { Destroy(gameObject); return; }

            transform.position += Vector3.up * riseSpeed * Time.deltaTime;

            // billboard: stesso idioma di EnemySpeech (guarda la camera, poi si
            // gira di 180 perche' LookAt punta l'asse +Z verso l'oggetto, mentre
            // il testo si legge dal lato opposto)
            var cam = Camera.main;
            if (cam != null)
            {
                transform.LookAt(cam.transform);
                transform.Rotate(0f, 180f, 0f);
            }

            // dissolvenza quadratica: resta leggibile per la prima meta' e poi
            // sparisce in fretta, invece di restare a mezz'aria mezzo trasparente
            var c = baseColor;
            c.a = 1f - k * k;
            text.color = c;
        }
    }
}

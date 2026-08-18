using UnityEngine;

// Scuotimento della camera all'impatto.
//
// Modello a "trauma": ogni colpo ne aggiunge un po', il valore decade nel tempo e
// l'ampiezza effettiva e' il quadrato del trauma. Il quadrato serve perche' con una
// relazione lineare i colpi leggeri scuotono troppo e quelli pesanti troppo poco;
// cosi' invece i colpi deboli restano appena percettibili e solo quelli forti
// muovono davvero l'inquadratura.
//
// Il trauma si SOMMA: due colpi ravvicinati scuotono piu' di uno solo, senza che
// serva gestire esplicitamente le sovrapposizioni.
public static class CameraShake
{
    [System.Serializable]
    public struct Settings
    {
        public float maxOffset;      // spostamento massimo in metri
        public float maxAngle;       // rollio massimo in gradi
        public float frequency;      // velocita' dell'oscillazione
        public float decayPerSecond; // quanto in fretta si spegne
    }

    public static Settings settings = new Settings
    {
        maxOffset = 0.18f,
        maxAngle = 1.6f,
        frequency = 22f,
        decayPerSecond = 2.2f,
    };

    private static float trauma;
    private static readonly float seed = Random.value * 100f;

    /// Aggiunge scuotimento. amount tipicamente fra 0.2 (colpo leggero) e 1 (colpo pesante).
    public static void Add(float amount)
    {
        if (amount <= 0f) return;
        trauma = Mathf.Clamp01(trauma + amount);
    }

    /// Restituisce lo scostamento da applicare in questo frame. Va chiamata una sola
    /// volta per frame: fa decadere il trauma.
    public static void Evaluate(float deltaTime, out Vector3 localOffset, out float roll)
    {
        if (trauma <= 0f)
        {
            localOffset = Vector3.zero;
            roll = 0f;
            return;
        }

        float shake = trauma * trauma;
        float t = Time.unscaledTime * settings.frequency;

        // Perlin invece di Random: da' un movimento continuo invece che sgranato.
        // Campionato su righe diverse del rumore, gli assi restano indipendenti.
        float x = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float y = Mathf.PerlinNoise(seed + 11f, t) * 2f - 1f;
        float r = Mathf.PerlinNoise(seed + 23f, t) * 2f - 1f;

        localOffset = new Vector3(x, y, 0f) * settings.maxOffset * shake;
        roll = r * settings.maxAngle * shake;

        // tempo non scalato: lo scuotimento deve proseguire anche durante l'hitstop,
        // ed e' proprio la combinazione dei due a dare l'impatto
        trauma = Mathf.Max(trauma - settings.decayPerSecond * deltaTime, 0f);
    }

    public static void Reset() => trauma = 0f;

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnEnterPlayMode() => trauma = 0f;
#endif
}

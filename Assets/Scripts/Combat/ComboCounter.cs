using UnityEngine;

// Contatore dei colpi consecutivi del player.
//
// Stesso schema degli altri helper di combattimento (CameraShake, HitStop,
// ExecutionSlowMo): stato statico, nessun oggetto da collegare in scena.
//
// La scadenza e' LAZY: non c'e' nessun Update che decrementa un timer, il
// conteggio si considera scaduto leggendo Time.time al momento in cui lo si
// chiede. Cosi' il contatore non dipende dall'esistenza della UI che lo mostra,
// e non resta appeso se quella UI non c'e'.
public static class ComboCounter
{
    [Tooltip("Secondi senza colpi dopo i quali la catena si azzera.")]
    public static float ResetTime = 2.5f;

    private static int count;
    private static float lastHitTime = float.NegativeInfinity;

    public static int Count => Expired ? 0 : count;

    // Quanto manca alla scadenza, per chi vuole disegnare una barra che si svuota.
    public static float TimeLeft => Expired ? 0f : ResetTime - (Time.time - lastHitTime);
    public static float NormalizedTimeLeft => ResetTime <= 0f ? 0f : Mathf.Clamp01(TimeLeft / ResetTime);

    private static bool Expired => Time.time - lastHitTime > ResetTime;

    // Un colpo andato a segno, oppure una parata riuscita: la parata fa parte
    // della catena perche' e' una lettura, non una pausa. E' quello che permette
    // di tenere viva una combo difendendosi, invece di doverla per forza
    // interrompere per non farsi colpire.
    public static void Register()
    {
        count = Expired ? 1 : count + 1;
        lastHitTime = Time.time;
    }

    public static void Reset()
    {
        count = 0;
        lastHitTime = float.NegativeInfinity;
    }

    // In questo progetto il Play Mode gira con "Reload Domain" DISATTIVATO, quindi
    // lo stato statico sopravvive fra una sessione e l'altra (e' la stessa trappola
    // gia' documentata per GuardianFormation e MinionCoordinator). Senza questo
    // azzeramento il contatore ripartirebbe col valore della sessione precedente, e
    // per giunta non risulterebbe scaduto: Time.time riparte da zero, quindi
    // Time.time - lastHitTime verrebbe NEGATIVO e la catena vecchia sembrerebbe
    // appena fatta.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay() => Reset();
}

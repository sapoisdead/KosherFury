using System.Collections;
using UnityEngine;

// Rallentamento globale breve, riservato alle esecuzioni (colpo su un bersaglio
// ancora stordito da una parata riuscita). A differenza di HitStop, che congela
// i singoli personaggi coinvolti apposta per non fermare tutta la scena, qui si
// tocca davvero Time.timeScale: e' un'eccezione deliberata, perche' l'esecuzione
// e' rara e deve leggersi come un momento a se' anche se un altro nemico nei
// paraggi rallenta pure lui per un istante.
public static class ExecutionSlowMo
{
    private class Runner : MonoBehaviour { }

    private static Runner runner;
    private static Coroutine active;

    public static void Trigger(float timeScale, float realDuration)
    {
        if (!Application.isPlaying || realDuration <= 0f) return;
        EnsureRunner();

        if (active != null) runner.StopCoroutine(active);
        active = runner.StartCoroutine(Run(timeScale, realDuration));
    }

    // Interrompe il rallentamento e riporta subito la velocita' piena. Serve alla
    // pausa: la coroutine conta in secondi REALI e a tempo fermo continuerebbe a
    // scorrere, riportando timeScale a 1 e spausando il gioco da sola.
    public static void Cancel()
    {
        if (active != null && runner != null) runner.StopCoroutine(active);
        active = null;
        Time.timeScale = 1f;
    }

    private static IEnumerator Run(float timeScale, float realDuration)
    {
        Time.timeScale = timeScale;

        yield return new WaitForSecondsRealtime(realDuration);

        // Si riporta sempre a 1, non a un "valore originale" catturato a inizio
        // coroutine: se due esecuzioni si accavallano, quel valore sarebbe gia'
        // quello rallentato della prima, e non si tornerebbe mai alla velocita' vera.
        Time.timeScale = 1f;
        active = null;
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;

        var go = new GameObject("[ExecutionSlowMo]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        runner = go.AddComponent<Runner>();
    }

#if UNITY_EDITOR
    // sopravvive al domain reload: senza questo, alla partita successiva il
    // riferimento al runner della sessione precedente sarebbe gia' distrutto
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnEnterPlayMode()
    {
        Time.timeScale = 1f;
        runner = null;
        active = null;
    }
#endif
}

using UnityEngine;
using UnityEngine.InputSystem;

// Vibrazione del pad sugli impatti. Stesso modello a "trauma" di CameraShake:
// ogni colpo ne aggiunge un po', il valore decade nel tempo invece di essere
// una durata fissa, cosi' colpi ravvicinati si sommano senza dover gestire
// esplicitamente le sovrapposizioni.
//
// Nessun effetto se non c'e' un pad collegato (Gamepad.current nullo): da
// tastiera e mouse la vibrazione e' semplicemente irrilevante, non un errore.
public static class GamepadRumble
{
    private class Runner : MonoBehaviour
    {
        private void Update() => Tick(Time.unscaledDeltaTime);

        // OnApplicationQuit scatta anche fermando il Play Mode nell'Editor (non solo
        // in build): senza fermare qui i motori, un pad fisico continuerebbe a
        // vibrare dopo lo stop, perche' l'hardware non si resetta da solo quando
        // gli oggetti C# vengono distrutti.
        private void OnDisable() => StopMotors();
        private void OnApplicationQuit() => StopMotors();
    }

    [System.Serializable]
    public struct Settings
    {
        public float decayPerSecond; // quanto in fretta si spegne
    }

    public static Settings settings = new Settings
    {
        decayPerSecond = 3.5f,
    };

    // motore lento/pesante (il "corpo" del colpo) e motore veloce/leggero (l'accento)
    private static float lowTrauma;
    private static float highTrauma;
    private static Runner runner;

    /// low/high tipicamente fra 0.2 (colpo leggero) e 1 (colpo pesante o parata),
    /// stessa scala gia' usata per CameraShake.Add.
    public static void Add(float low, float high)
    {
        if (Gamepad.current == null) return;
        EnsureRunner();

        lowTrauma = Mathf.Clamp01(lowTrauma + low);
        highTrauma = Mathf.Clamp01(highTrauma + high);
    }

    private static void Tick(float deltaTime)
    {
        var pad = Gamepad.current;
        if (pad == null) return;

        pad.SetMotorSpeeds(lowTrauma, highTrauma);

        lowTrauma = Mathf.Max(lowTrauma - settings.decayPerSecond * deltaTime, 0f);
        highTrauma = Mathf.Max(highTrauma - settings.decayPerSecond * deltaTime, 0f);
    }

    private static void StopMotors()
    {
        lowTrauma = 0f;
        highTrauma = 0f;
        Gamepad.current?.SetMotorSpeeds(0f, 0f);
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;

        var go = new GameObject("[GamepadRumble]");
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
        lowTrauma = 0f;
        highTrauma = 0f;
        runner = null;
    }
#endif
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Congelamento breve all'impatto: e' quello che fa "sentire" il contatto invece
// di vedere due modelli che si compenetrano.
//
// Congela i singoli personaggi coinvolti, non Time.timeScale: con piu' nemici
// addosso, fermare tutto il mondo perche' uno ha tirato un pugno si nota come un
// difetto. Attaccante e bersaglio si bloccano, il resto della scena prosegue.
//
// Oltre all'Animator viene fermato anche il NavMeshAgent: bloccare solo
// l'animazione lascerebbe il nemico scivolare da fermo, che e' peggio del
// problema che stiamo risolvendo.
public static class HitStop
{
    private class Entry
    {
        public Animator animator;
        public float originalAnimatorSpeed;

        public NavMeshAgent agent;
        public bool agentWasStopped;

        public float endTime;
    }

    private class Runner : MonoBehaviour
    {
        private readonly List<GameObject> finished = new();

        private void Update()
        {
            if (frozen.Count == 0) return;

            finished.Clear();

            foreach (var kv in frozen)
            {
                if (Time.unscaledTime < kv.Value.endTime && kv.Key != null)
                    continue;

                Release(kv.Value);
                finished.Add(kv.Key);
            }

            foreach (var go in finished)
                frozen.Remove(go);
        }

        private void OnDestroy()
        {
            // se il runner muore (cambio scena, uscita dal play mode) non lasciamo
            // nessuno congelato per sempre
            foreach (var kv in frozen) Release(kv.Value);
            frozen.Clear();
        }
    }

    private static readonly Dictionary<GameObject, Entry> frozen = new();
    private static Runner runner;

    /// Congela un personaggio per la durata indicata.
    public static void Freeze(GameObject character, float duration)
    {
        if (character == null || duration <= 0f) return;
        if (!Application.isPlaying) return;

        EnsureRunner();

        GameObject root = character.transform.root.gameObject;
        float end = Time.unscaledTime + duration;

        if (frozen.TryGetValue(root, out Entry existing))
        {
            // gia' congelato: prolunghiamo soltanto. Risalvare adesso lo stato
            // significherebbe memorizzare "velocita' zero" come valore originale
            // e lasciarlo bloccato per sempre.
            if (end > existing.endTime) existing.endTime = end;
            return;
        }

        var entry = new Entry { endTime = end };

        entry.animator = root.GetComponentInChildren<Animator>();
        if (entry.animator != null)
        {
            entry.originalAnimatorSpeed = entry.animator.speed;
            entry.animator.speed = 0f;
        }

        entry.agent = root.GetComponent<NavMeshAgent>();
        if (entry.agent != null && entry.agent.enabled && entry.agent.isOnNavMesh)
        {
            entry.agentWasStopped = entry.agent.isStopped;
            entry.agent.isStopped = true;
        }
        else
        {
            entry.agent = null;   // non toccarlo al rilascio
        }

        frozen[root] = entry;
    }

    /// Congela entrambi i lati dell'impatto.
    public static void Freeze(GameObject attacker, GameObject victim, float duration)
    {
        Freeze(attacker, duration);
        Freeze(victim, duration);
    }

    /// Vero se il personaggio e' congelato in questo momento. Serve a chi deve
    /// sospendersi durante il fermo immagine (per esempio la spinta da impatto,
    /// che altrimenti farebbe scivolare un corpo con l'animazione bloccata).
    public static bool IsFrozen(GameObject character)
    {
        if (character == null) return false;
        return frozen.ContainsKey(character.transform.root.gameObject);
    }

    private static void Release(Entry e)
    {
        if (e.animator != null)
            e.animator.speed = e.originalAnimatorSpeed;

        // l'agent puo' essere stato disabilitato nel frattempo (per esempio se il
        // colpo lo ha ucciso): toccarlo in quello stato genera un'eccezione
        if (e.agent != null && e.agent.enabled && e.agent.isOnNavMesh)
            e.agent.isStopped = e.agentWasStopped;
    }

    private static void EnsureRunner()
    {
        if (runner != null) return;

        var go = new GameObject("[HitStop]");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        runner = go.AddComponent<Runner>();
    }

#if UNITY_EDITOR
    // I campi statici sopravvivono al domain reload: senza questo, alla partita
    // successiva il dizionario resterebbe sporco della sessione precedente
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnEnterPlayMode()
    {
        frozen.Clear();
        runner = null;
    }
#endif
}

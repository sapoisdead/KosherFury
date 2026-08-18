using System.Collections.Generic;
using UnityEngine;

// Coordina piu' minion che ingaggiano lo stesso player: limita quanti possono
// attaccare in contemporanea e assegna ai restanti una posizione di formazione
// attorno al player, cosi' non si accalcano tutti sullo stesso punto.
public static class MinionCoordinator
{
    public static int MaxAttackers = 2;

    // Dopo un turno d'attacco il minion deve lasciar passare gli altri per questo
    // tempo. Senza, il primo che acquisisce lo slot se lo riprende subito e gli
    // altri non attaccano mai: orbitano soltanto, finche' lui non muore.
    public static float SlotCooldown = 1.2f;

    private static readonly List<Transform> engaged = new();
    private static readonly List<Transform> attackers = new();
    private static readonly Dictionary<Transform, float> lastReleased = new();

    public static void Register(Transform minion)
    {
        Prune();
        if (!engaged.Contains(minion))
            engaged.Add(minion);
    }

    public static void Unregister(Transform minion)
    {
        engaged.Remove(minion);
        attackers.Remove(minion);
    }

    public static bool TryAcquireAttackSlot(Transform minion)
    {
        Prune();
        if (attackers.Contains(minion)) return true;
        if (attackers.Count >= MaxAttackers) return false;

        // Turno appena finito: lascia passare gli altri. Il vincolo vale solo se c'e'
        // davvero qualcuno in attesa, altrimenti con pochi minion si creerebbero pause
        // in cui nessuno attacca pur essendoci slot liberi.
        if (engaged.Count > MaxAttackers
            && lastReleased.TryGetValue(minion, out float released)
            && Time.time - released < SlotCooldown)
            return false;

        attackers.Add(minion);
        return true;
    }

    public static void ReleaseAttackSlot(Transform minion)
    {
        if (attackers.Remove(minion))
            lastReleased[minion] = Time.time;
    }

    // Offset di formazione attorno al player.
    //
    // Il minion mantiene la direzione da cui sta gia' arrivando, limitata all'arco
    // consentito davanti al player. Cosi' non gli gira intorno (e non lo attraversa)
    // solo per raggiungere uno slot prestabilito: si limita a non finirgli alle
    // spalle se l'arco e' minore di 360.
    //
    // arcDegrees: ampiezza totale dell'arco, centrato sulla direzione in cui guarda
    //             il player. 360 = puo' stare ovunque intorno a lui.
    public static Vector3 GetFormationOffset(Transform minion, float radius, Transform player, float arcDegrees)
    {
        Prune();

        Vector3 center = player != null ? player.forward : Vector3.forward;
        center.y = 0f;
        if (center.sqrMagnitude < 0.001f) center = Vector3.forward;
        center.Normalize();

        // direzione attuale player -> minion, in gradi rispetto al fronte del player
        Vector3 toMinion = minion.position - player.position;
        toMinion.y = 0f;

        float angle = toMinion.sqrMagnitude > 0.001f
            ? Vector3.SignedAngle(center, toMinion.normalized, Vector3.up)
            : 0f;

        // vincola l'angolo all'arco consentito
        float half = Mathf.Clamp(arcDegrees, 0f, 360f) * 0.5f;
        angle = Mathf.Clamp(angle, -half, half);

        // separa i minion che si sovrappongono, spingendoli ai lati opposti
        int index = engaged.IndexOf(minion);
        int count = engaged.Count;
        if (index >= 0 && count > 1)
        {
            float spread = Mathf.Min(half, 45f);
            float bias = (index % 2 == 0 ? 1f : -1f) * spread * ((index / 2 + 1f) / count);
            angle = Mathf.Clamp(angle + bias, -half, half);
        }

        float drift = Mathf.Sin(Time.time * 0.6f + Mathf.Max(index, 0) * 2.4f) * 6f;

        return Quaternion.Euler(0f, angle + drift, 0f) * center * radius;
    }

    private static void Prune()
    {
        engaged.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
        attackers.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);

        // il dizionario e' statico e sopravvive ai minion: senza questo tratterrebbe
        // riferimenti a oggetti distrutti per tutta la sessione
        if (lastReleased.Count > 0)
        {
            var stale = new List<Transform>();
            foreach (var kv in lastReleased)
                if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy) stale.Add(kv.Key);
            foreach (var s in stale) lastReleased.Remove(s);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Coordina la squadra fissa di guardie attorno a Vana: stato condiviso statico,
// sullo schema di MinionCoordinator ma per il ruolo di scudo invece che per lo
// slot d'attacco.
//
// Postazioni FISSE: al dispiegamento ogni guardia riceve uno slot, equidistanziato
// dagli altri, e ci resta finche' non viene rilasciata o uccisa. Nessuna guardia
// insegue il player: e' il player che deve venire a tiro di UNA di loro, non il
// contrario. Con arcDegrees=360 (default) e' un anello completo attorno a Vana,
// non un semicerchio: un semicerchio rivolto verso il player al momento del
// dispiegamento lascia tutto il retro scoperto, ed e' esattamente li' che il
// player puo' entrare indisturbato se cambia direzione d'attacco. Con un arco
// piu' stretto la direzione (dove punta il centro) resta comunque congelata al
// momento del dispiegamento, non ricalcolata mentre il player si sposta.
//
// Roster fisso: chi muore viene tolto (Unregister) e la formazione si stringe
// sui sopravvissuti (gli slot si ridistribuiscono sugli indici rimasti), nessun
// rimpiazzo.
public static class GuardianFormation
{
    // tutte le guardie vive, in difesa o gia' rilasciate
    private static readonly List<GuardianBT> guardians = new();
    // sottoinsieme di 'guardians' che e' gia' passato all'attacco
    private static readonly List<GuardianBT> released = new();

    private static Transform origin;
    private static Vector3 facingDirection;
    private static float radius;
    private static float arcDegrees;
    private static int releaseBatchSize = 3;

    public static bool IsDeployed { get; private set; }

    // La FASE di rilascio e' cominciata. Non vuol dire che tutte siano gia'
    // aggressive: il rilascio e' graduale, un gruppo per volta (vedi
    // BeginRelease). Per sapere se una singola guardia e' gia' passata
    // all'attacco serve IsReleased(guardia).
    public static bool ReleaseStarted { get; private set; }

    // Usato da VanaBoss per sapere quando passare alla sequenza finale: non
    // serve un evento per-guardia, basta interrogare questo ogni tanto.
    public static int AliveCount => guardians.Count;

    // Quante sono aggressive in questo momento, per diagnostica e test.
    public static int ReleasedCount => released.Count;

    public static void Register(GuardianBT guardian)
    {
        if (guardian != null && !guardians.Contains(guardian))
            guardians.Add(guardian);
    }

    public static void Unregister(GuardianBT guardian)
    {
        guardians.Remove(guardian);
        released.Remove(guardian);

        // Se era l'ultima dell'ondata in corso, ne parte una nuova. No-op
        // finche' restano aggressive in vita o finche' la fase di rilascio
        // non e' cominciata.
        ReleaseNextWaveIfCleared();
    }

    // Chiamato una sola volta da VanaBoss al primo superamento della soglia.
    // A questo punto tutte le guardie sono gia' registrate (Awake gira prima
    // di qualunque Start/albero), quindi il giro su EnterGuardDuty le prende tutte.
    public static void Deploy(Transform vanaOrigin, float shieldRadius, float shieldArcDegrees)
    {
        origin = vanaOrigin;
        radius = shieldRadius;
        arcDegrees = shieldArcDegrees;
        facingDirection = FacingPlayerXZ();   // congelata qui, non piu' ricalcolata
        IsDeployed = true;

        foreach (var guardian in guardians.ToArray())
            guardian?.EnterGuardDuty();
    }

    // Chiamato una sola volta da VanaBoss a budget dei simp normali esaurito.
    // Non le rilascia tutte insieme: ne manda all'attacco un'ondata di
    // batchSize, e la successiva parte solo quando l'ondata in corso e' stata
    // spazzata via del tutto (vedi ReleaseNextWaveIfCleared). Le altre restano
    // in formazione, con tutti i vantaggi della difesa, e l'anello si stringe
    // sui rimasti.
    public static void BeginRelease(int batchSize)
    {
        if (ReleaseStarted) return;

        ReleaseStarted = true;
        releaseBatchSize = Mathf.Max(1, batchSize);
        ReleaseNextWaveIfCleared();
    }

    // Manda all'attacco una nuova ondata di releaseBatchSize guardie, ma SOLO
    // se non ne resta viva nessuna della precedente: uccidere una delle tre non
    // ne fa arrivare subito un'altra, si devono finire tutte e tre prima che
    // l'anello ne ceda altre. Se in difesa ne restano meno di batchSize,
    // rilascia quel che c'e'.
    private static void ReleaseNextWaveIfCleared()
    {
        if (!ReleaseStarted) return;
        if (released.Count > 0) return;   // ondata in corso non ancora esaurita

        // copia difensiva: ReleaseToAggressive potrebbe innescare side-effect,
        // meglio non iterare la lista live mentre viene eventualmente toccata
        foreach (var guardian in guardians.ToArray())
        {
            if (released.Count >= releaseBatchSize) break;
            if (guardian == null || released.Contains(guardian)) continue;

            released.Add(guardian);
            guardian.ReleaseToAggressive();
        }
    }

    // Vero solo per le guardie gia' passate all'attacco. Quelle ancora in
    // difesa continuano a ricevere il proprio slot da TryGetAssignment.
    public static bool IsReleased(GuardianBT guardian) => released.Contains(guardian);

    // Restituisce lo slot fisso di questa guardia, posizione E orientamento:
    // 'facing' punta verso l'esterno del semicerchio lungo il proprio raggio,
    // cosi' la guardia sta sempre di spalle a Vana, mai voltata a inseguire il
    // player con lo sguardo. Il controllo di attacco resta compito del
    // chiamante sulla posizione reale del player: qui si decide solo dove sta
    // di posto e come e' rivolta.
    public static bool TryGetAssignment(GuardianBT self, out Vector3 targetPosition, out Quaternion facing)
    {
        targetPosition = default;
        facing = Quaternion.identity;
        if (!IsDeployed || origin == null) return false;

        // gia' all'attacco: non ha piu' un posto da tenere, il suo GuardDuty
        // deve fallire e lasciare il turno al ramo di combattimento
        if (released.Contains(self)) return false;

        // Gli slot si dividono solo fra i DIFENSORI: chi viene rilasciato esce
        // dal conteggio e i rimasti ricompattano l'anello, esattamente come
        // gia' accade quando una compagna muore.
        int index = DefenderIndexOf(self);
        if (index < 0) return false;

        Vector3 dir = SlotDirection(index, DefenderCount());
        Vector3 candidate = origin.position + dir * radius;

        // Vana sta su un piccolo bimah rialzato: 'candidate' erediterebbe la sua
        // quota invece del vero pavimento (il raggio della formazione e' scelto
        // apposta per restare fuori dall'ingombro del bimah, quindi il punto e'
        // gia' su suolo normale, non sopra il bimah stesso).
        targetPosition = NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas) ? hit.position : candidate;
        facing = Quaternion.LookRotation(dir, Vector3.up);
        return true;
    }

    // Quante guardie stanno ancora tenendo la formazione (esclude le rilasciate).
    private static int DefenderCount()
    {
        int count = 0;
        for (int i = 0; i < guardians.Count; i++)
            if (!released.Contains(guardians[i])) count++;
        return count;
    }

    // Posizione di 'self' fra i soli difensori, cioe' il suo indice di slot
    // sull'anello ricompattato. -1 se non e' un difensore.
    private static int DefenderIndexOf(GuardianBT self)
    {
        int index = 0;
        for (int i = 0; i < guardians.Count; i++)
        {
            var guardian = guardians[i];
            if (released.Contains(guardian)) continue;
            if (guardian == self) return index;
            index++;
        }
        return -1;
    }

    // Con un arco chiuso (360, il default) gli slot si dividono l'intero giro
    // (angleStep = 360/total): l'ultimo slot NON deve coincidere col primo, a
    // differenza di un arco aperto dove gli estremi sono i due capi distinti di
    // uno spicchio. Con un arco parziale (<360) resta la divisione classica su
    // (total-1) intervalli, capi compresi.
    private static Vector3 SlotDirection(int index, int total)
    {
        bool fullCircle = arcDegrees >= 360f;
        float angleStep = total > 1 ? arcDegrees / (fullCircle ? total : total - 1) : 0f;
        float angle = -arcDegrees / 2f + angleStep * index;
        return Quaternion.AngleAxis(angle, Vector3.up) * facingDirection;
    }

    private static Vector3 FacingPlayerXZ()
    {
        Transform player = PlayerManager.Instance != null ? PlayerManager.Instance.PlayerTransform : null;
        if (player == null || origin == null) return Vector3.forward;

        Vector3 dir = player.position - origin.position;
        dir.y = 0f;
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : origin.forward;
    }
}


# Reazioni fisiche ai colpi — contesto per la prossima sessione

**Stato: solo design, nessuna riga di codice scritta.** Questo file nasce da una
sessione di sole domande (niente Unity aperto, niente modifiche al progetto):
riassume le due opzioni discusse per dare più "peso" fisico ai colpi senza dover
animare a mano ogni reazione, e i punti di aggancio reali nel codice esistente.

**Piano dell'utente**: provare prima l'**Opzione 1**. Se non soddisfa, passare
all'**Opzione 2** nella stessa sessione o in una successiva — sono indipendenti,
la seconda non richiede di disfare la prima.

---

## Obiettivo

I colpi oggi fanno partire una clip di hit reaction fissa (`Take_hit` sul simp,
l'equivalente sul player) più `Knockback` (spinta rigida sul `CharacterController`/
`NavMeshAgent`). Funziona ma è sempre uguale a se stesso: un pugno leggero e uno
pesante producono la stessa reazione, solo con un knockback più lungo.

L'idea è aggiungere un **layer fisico additivo sopra l'animazione esistente**,
che reagisca a direzione/forza del colpo — senza toccare le clip, gli Animator
Controller o `CombatState` già collaudati.

---

## Punto di aggancio comune

Entrambe le opzioni si agganciano nello stesso punto: `Hitbox.TryHit()` in
[Assets/Scripts/Combat/Hitbox.cs](Assets/Scripts/Combat/Hitbox.cs), subito dopo
la chiamata esistente a `ApplyKnockback()` (riga ~274-275):

```csharp
if (wasParried || !victimArmored)
    ApplyKnockback(health, wasParried);
```

Questa condizione (`wasParried || !victimArmored`) è **esattamente** la gating
già corretta per il knockback: chi è su frame `Active` (armor, vedi
`CombatState.IsArmored`) non viene spinto, quindi non deve nemmeno flinchare
fisicamente — stesso principio, va riusato tale e quale per non reintrodurre un
caso che il codice attuale già gestisce bene.

`ApplyKnockback` calcola già `attacker`/`victim`/`force` — la direzione e la
forza servono identiche anche al nuovo layer, quindi conviene passarle di lì
invece di ricalcolarle.

Per la **scoperta del componente sul bersaglio**, replicare lo stesso pattern già
usato per `Knockback`:

```csharp
var kb = pushed.GetComponentInChildren<Knockback>();
```

cioè un `GetComponentInChildren<T>()` sul personaggio colpito, componente
opzionale (se assente, semplicemente non succede nulla — come già succede oggi
se manca `Knockback`).

---

## Opzione 1 — "Bone kick" procedurale

Nessun `Rigidbody`, nessun `Collider` per osso, nessun ragdoll setup. Un
componente nuovo, es. `Assets/Scripts/Combat/HitImpactReaction.cs`, sullo stesso
GameObject dell'`Animator` del personaggio (stessa convenzione già in uso per gli
animation event `OnRightHandStart` ecc., vedi `CLAUDE.md`).

**Meccanica**:
- Un riferimento Inspector a uno o più bone del rig Mixamo (es.
  `mixamorig:Spine1` per un flinch sul busto, `mixamorig:Head` per uno sulla
  testa — nomi da verificare sul rig effettivo, non assunti).
- Un metodo pubblico, es. `Kick(Vector3 direction, float force)`, chiamato dal
  punto di aggancio sopra.
- Internamente si tiene un **offset di rotazione che decade nel tempo** (non una
  rotazione assoluta): all'impatto l'offset salta a un valore proporzionale a
  `force`, poi si riassorbe verso zero in ~0.15-0.3s (`Mathf.MoveTowards` o una
  curva di decadimento).
- L'offset va **applicato in `LateUpdate`**, dopo che l'`Animator` ha scritto la
  posa del frame (ordine di default in Unity: Animator aggiorna prima delle
  `LateUpdate` degli script, a meno che il progetto non usi `Animate Physics` —
  da verificare sul componente `Animator` dei prefab coinvolti prima di
  assumerlo). Se l'offset è zero il risultato è identico a oggi: nessuna
  differenza quando non c'è impatto.

**Da verificare/decidere in implementazione**:
- Quali bone Mixamo usare (probabilmente `Spine1` per i colpi al corpo, `Head`
  per un accento in più sui colpi forti).
- Se un solo bone basta o ne servono 2-3 con pesi diversi per un effetto più
  "a catena" (colpo che parte dal busto e si smorza verso le estremità).
- Se scalare l'impulso su `force` in modo lineare o con una curva (un colpo
  debole dovrebbe restare quasi invisibile, non è detto che lineare sia giusto).

**Verifica** (seguendo le regole di test del progetto in `CLAUDE.md`):
- Va provato in Play Mode, non a colpi di frame contati — per giudicare "quanto
  dura il decadimento" serve accumulare `Time.deltaTime`, non contare frame a
  260fps.
- Uscire e rientrare dal Play Mode dopo ogni modifica allo script (ricarica
  dominio, altrimenti lo stato del componente resta vecchio).
- Prova pratica: un colpo leggero (pugno) e uno pesante (pesante/area) sullo
  stesso simp, confrontando visivamente l'ampiezza della reazione.

---

## Opzione 2 — Ragdoll fisico, dip temporaneo (non blend continuo per-osso)

**Attenzione, correzione rispetto a come è stata descritta a parole in
conversazione**: un vero blend continuo per-singolo-osso (fisica e Animator
attivi *insieme* sullo stesso bone, mescolati in tempo reale) **non è banale in
Unity stock** — l'`Animator` su un rig Humanoid riscrive l'intero scheletro ogni
frame tramite i muscoli, quindi non "lascia spazio" a un singolo bone guidato
dalla fisica senza un plugin dedicato (es. PuppetMaster/Final IK). La versione
realisticamente implementabile senza asset esterni è un **tuffo temporaneo in
ragdoll a corpo intero**, poi un ritorno all'animazione — non un blend per-osso.

**Setup** (va fatto **a mano nell'Editor**, non è scriptabile via Coplay):
`GameObject > 3D Object > Ragdoll...` sul rig del personaggio. Funziona perché i
rig sono importati **Humanoid** (`Human + Create From This Model`, vedi
`CLAUDE.md`) — il wizard mappa da solo bacino/colonna/arti e genera
`Rigidbody` + `CharacterJoint` + `Collider` per ogni osso.

**Meccanica** (`Assets/Scripts/Combat/PartialRagdollReaction.cs` o simile, stesso
GameObject dell'Animator):
1. Alla creazione, raccoglie tutti i `Rigidbody` del ragdoll
   (`GetComponentsInChildren<Rigidbody>()`) e li tiene `isKinematic = true`
   finché non serve la reazione — così normalmente è l'`Animator` a guidare
   tutto, come oggi.
2. Su `Kick(Vector3 direction, float force, Vector3 hitPoint)`:
   - disattiva l'`Animator` (e il `NavMeshAgent`/`CharacterController` se
     presenti, altrimenti litigano con la fisica);
   - porta tutti i rigidbody a `isKinematic = false`;
   - applica l'impulso con `AddForceAtPosition` sul rigidbody più vicino a
     `hitPoint` (che `Hitbox.ImpactPoint()` calcola già, riusabile);
   - dopo una finestra breve (~0.2-0.4s reali, misurati con `Time.deltaTime`
     accumulato, non a frame) **richiude** la sequenza: o snap secco alla posa
     che l'Animator produrrebbe in quel momento (più economico, un piccolo
     "pop" visivo accettabile per un solo frame), o un blend graduale
     interpolando ogni bone dalla rotazione fisica finale a quella animata su
     ~0.15-0.2s prima di riattivare l'Animator per intero (più curato, più
     lavoro: serve una passata che legga entrambe le pose e le interpoli
     bone-per-bone).
3. Riattiva `Animator`/`NavMeshAgent`/`CharacterController`.

**Layer fisico dedicato**: i collider del ragdoll vanno su un layer che collide
con la geometria statica ma **non** con altri personaggi — altrimenti il dip di
un simp durante un combattimento affollato spintona i vicini in modo assurdo.
Stesso tipo di accorgimento di cui si è parlato per i cadaveri, ma qui più
importante perché il personaggio è ancora vivo e in mezzo alla mischia, non
fermo in un angolo.

**Quando usarlo**: riservato a colpi pesanti, non a ogni pugno — altrimenti il
personaggio perde il controllo (Animator/NavMeshAgent spenti) troppo spesso.
Un modo pulito per deciderlo: un campo booleano o una soglia di `force` su
`Hitbox` stesso (accanto ai tooltip già presenti per `hitStop`,
`cameraShakeOnExecution` ecc. — stesso pattern di tunable per-hitbox), così solo
gli attacchi pesanti del player o gli attacchi di un boss lo fanno scattare.

**Verifica**: stessa procedura dell'Opzione 1 (Play Mode, `Time.deltaTime`
accumulato, uscire/rientrare dopo modifiche script), più un controllo esplicito
che `NavMeshAgent`/`Animator` tornino correttamente attivi a fine sequenza — un
simp lasciato con l'Animator spento resterebbe fermo per sempre.

---

## Convenzioni del progetto da rispettare (da `CLAUDE.md`)

- **Nomi in inglese** (classi, metodi, campi, variabili), **commenti in
  italiano** dove il *perché* non è ovvio dal codice.
- Test via **Coplay MCP** (`execute_script`, `play_game`, `check_compile_errors`
  ecc.), non a occhio nell'Editor.
- Il gioco gira a ~260fps: **misurare il tempo accumulando `Time.deltaTime`**,
  mai contando i frame.
- **Uscire e rientrare dal Play Mode** dopo ogni modifica agli script (ricarica
  dominio).
- Se serve isolare la prova, disattivare il `PlayerInput` per iniettare valori
  per riflessione senza interferenze.
- `get_unity_logs` è rumoroso: far scrivere al test un blocco con titolo
  riconoscibile e rileggerlo da `~/Library/Logs/Unity/Editor.log`.

---

## Prompt suggerito per la prossima sessione

> Leggi `HIT_REACTIONS.md` nella root del progetto: è il contesto di design
> (non ancora implementato) per aggiungere reazioni fisiche ai colpi senza
> rifare le animazioni esistenti. Comincia dall'**Opzione 1** ("bone kick"
> procedurale) agganciandola in `Hitbox.TryHit()` come descritto. Prova in Play
> Mode su un simp normale con colpi leggeri e pesanti. Se il risultato non
> convince, passa all'**Opzione 2** (ragdoll a dip temporaneo) seguendo la
> correzione già segnata nel file: è un tuffo a corpo intero con eventuale
> blend in uscita, non un blend continuo per-singolo-osso.

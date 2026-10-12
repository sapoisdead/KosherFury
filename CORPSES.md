# Corpi a terra — contesto per la prossima sessione

**Stato: solo design, nessuna riga di codice scritta.** Sessione di sole domande
(niente Unity aperto): riassume le due opzioni discusse per lasciare i nemici
morti visibili in scena invece di farli sparire, e un vincolo reale scoperto
leggendo il codice — non un dettaglio, una precondizione da risolvere prima di
scegliere l'opzione.

---

## Punto di partenza reale (letto da `Health.cs`, non assunto)

**Oggi non esistono corpi a terra: i nemici spariscono.** In
[Assets/Scripts/Player/Health.cs](Assets/Scripts/Player/Health.cs), `Die()` fa
partire il trigger `Death` sull'Animator e avvia `DespawnAfterDeathAnimation()`,
che aspetta la fine della clip e poi chiama **`gameObject.SetActive(false)`**.
Non è "i corpi restano troppo poco": non restano **per niente**, il
GameObject si disattiva del tutto a fine animazione.

Qualunque opzione si scelga, il primo cambio è in quella coroutine: non deve più
disattivare l'oggetto, deve lasciarlo visibile e passare alla gestione del corpo.

---

## ⚠️ Vincolo reale: due sistemi contano i nemici tramite `activeInHierarchy`

Prima di scegliere l'opzione, questo va risolto — altrimenti lasciare il corpo
attivo in scena **rompe silenziosamente** due sistemi già collaudati:

```
Assets/Scripts/Enemy/WaveSpawner.cs:102
    alive.RemoveAll(e => e == null || !e.activeInHierarchy);

Assets/Scripts/Enemy/MinionCoordinator.cs:104-105, 113
    engaged.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
    attackers.RemoveAll(t => t == null || !t.gameObject.activeInHierarchy);
    ...!kv.Key.gameObject.activeInHierarchy) stale.Add(kv.Key);
```

Entrambi usano "non più attivo" come proxy di "morto/sparito" per liberare lo
slot d'attacco (`MinionCoordinator`) o il posto nel tetto di nemici vivi
(`WaveSpawner`). Se il corpo resta `SetActive(true)` per essere visibile, questi
continuano a contarlo come vivo: lo slot non si libera mai, `WaveSpawner` pensa
che il nemico sia ancora in campo e non ne fa arrivare uno di rimpiazzo.

**Nota**: `TargetLockSystem` questo problema **non ce l'ha già** — il codice
attuale esclude i cadaveri dalla ricerca leggendo `Health.IsDead`, apposta per
non dipendere da `activeSelf` (la morte lascia l'oggetto attivo per tutta la
durata dell'animazione, quindi guardare `activeSelf` avrebbe ritardato il cambio
bersaglio di quasi un secondo — è già scritto così). Gli altri due sistemi non
hanno mai avuto motivo di preoccuparsene finora, perché finora "non più attivo"
e "morto" coincidevano sempre.

### Due strade, da decidere in implementazione

**A — Cambiare la condizione di pruning in `IsDead`** (stesso principio già
usato in `TargetLockSystem`): `WaveSpawner`/`MinionCoordinator` controllano
`Health.IsDead` invece di `activeInHierarchy`. Più coerente col resto del
codice, ma tocca due file con una storia di bug delicati (`MinionCoordinator`
ha `static` condivisi fra tutte le istanze, `WaveSpawner` gestisce le ondate) —
va reverificato con attenzione, non solo compilato.

**B — Corpo come oggetto separato, disaccoppiato dal nemico tracciato**: alla
morte si instanzia un GameObject "solo visivo" (un clone del modello, o un
prefab leggero dedicato) nella posa/posizione finale, e l'oggetto originale
tracciato da `WaveSpawner`/`MinionCoordinator` continua a disattivarsi **esattamente
come oggi**. Zero modifiche ai due sistemi esistenti; il costo è un
`Instantiate` in più a ogni morte e una gestione del ciclo di vita del corpo
completamente separata.

La B è meno rischiosa (non tocca codice già verificato), la A è più pulita e
coerente con come il progetto ha già risolto lo stesso tipo di problema altrove.
**Non è stata presa una decisione** — va scelta nella sessione di implementazione,
eventualmente chiedendo all'utente se preferisce prudenza (B) o coerenza (A).

---

## Opzione A — Corpo congelato in posa (nessuna fisica)

- `DespawnAfterDeathAnimation()` non disattiva più l'oggetto: a fine clip
  **disabilita l'`Animator`** (`animator.enabled = false`), lasciando il
  modello fermo sull'ultima posa.
- Costo pressoché nullo. Rischio noto (segnalato anche nella sessione di
  domande): su un dislivello — vedi la superficie irregolare vicino a
  `Small_bimah` già documentata in `CLAUDE.md`/`VANA_BOSSFIGHT.md` — la posa
  autorata può compenetrare la geometria o restare sospesa, perché nessuna
  correzione fisica adagia il corpo al terreno.

## Opzione B — Ragdoll vero alla morte

Stesso strumento discusso in [HIT_REACTIONS.md](HIT_REACTIONS.md) per
l'Opzione 2 delle reazioni ai colpi: **Ragdoll Wizard**
(`GameObject > 3D Object > Ragdoll...`), fatto a mano in Editor, funziona sui
rig Humanoid Mixamo già in uso. **Se quella feature viene implementata prima,
il rig del ragdoll è riusabile pari pari anche qui** — stesso setup, due usi
diversi (dip temporaneo durante il combattimento vs presa di controllo
definitiva alla morte). Se invece si parte da qui, il setup del ragdoll va
fatto comunque, e resterà pronto per l'altra feature in futuro.

Flusso in `Die()`:
1. Disabilita `Animator` e `NavMeshAgent` (già fatto oggi per l'agent, riga 147
   di `Health.cs`).
2. Porta i `Rigidbody` del ragdoll da cinematici a `isKinematic = false`.
3. **Continuità con l'ultimo colpo**: per far "volare" il corpo nella direzione
   giusta invece di afflosciarsi sul posto serve la direzione dell'ultimo
   knockback. `Knockback.cs` oggi tiene `velocity` privato, **senza getter
   pubblico** — va aggiunta una proprietà di sola lettura (es.
   `public Vector3 CurrentVelocity => velocity;`) prima di poterla leggere da
   `Die()`. Piccola modifica, ma necessaria: oggi quel dato non è accessibile
   dall'esterno.
4. Aspetta che i rigidbody scendano sotto una soglia di velocità (qualche
   frame), poi li rimette `isKinematic = true`: da lì in poi il corpo è
   statico, costa quanto l'opzione A.

---

## Ciclo di vita del corpo (comune a entrambe le opzioni)

Indipendentemente da A o B, senza gestione i cadaveri si accumulano
all'infinito — rilevante soprattutto con `WaveSpawner` che fa arrivare ondate:

- **Layer fisico dedicato** per i collider del corpo (solo per l'Opzione B, che
  ne ha): deve collidere con la geometria statica per adagiarsi, **non** con
  altri personaggi vivi, altrimenti un cadavere che cade contro un simp ancora
  vivo lo spinge in modo assurdo.
- **Tetto massimo di corpi simultanei** (es. 8-10), con dissolvenza/destroy del
  più vecchio quando si supera — altrimenti dopo un combattimento lungo la
  scena si riempie senza motivo visivo.
- **NavMesh**: non serve fare nulla apposta. `NavMeshAgent` ignora del tutto i
  collider fisici (lo dice esplicitamente `CLAUDE.md` a proposito dei bimah) —
  un cadavere **non blocca** i simp vivi automaticamente, resta solo
  cosmetico. Carvare la NavMesh con un `NavMeshObstacle` per farli inciampare
  sui corpi è possibile ma introduce un ricalcolo continuo: probabilmente non
  vale la candela per un dettaglio estetico, a meno che non lo si voglia
  esplicitamente come feature.

---

## Convenzioni del progetto da rispettare (da `CLAUDE.md`)

- Nomi in **inglese**, commenti in **italiano** dove il *perché* non è ovvio.
- Test via **Coplay MCP**, non a occhio: uscire e rientrare dal Play Mode dopo
  ogni modifica agli script (ricarica dominio, altrimenti gli stati statici
  come quelli di `MinionCoordinator` non si azzerano fra una sessione e
  l'altra — vedi le note tecniche già raccolte in `VANA_BOSSFIGHT.md`).
- Misurare i tempi (durata prima del despawn, finestra di assestamento del
  ragdoll) accumulando `Time.deltaTime`, mai contando i frame a ~260fps.
- **Prova end-to-end obbligatoria dopo la scelta fra strada A/B del vincolo
  sopra**: far morire più nemici in sequenza durante un'ondata di
  `WaveSpawner` e verificare che gli slot si liberino comunque e che arrivino
  i rimpiazzi — è l'unico modo per sapere se il fix al pruning (o l'oggetto
  separato) funziona davvero, non basta vedere il corpo a terra.

---

## Prompt suggerito per la prossima sessione

> Leggi `CORPSES.md` nella root del progetto: è il contesto di design (non
> ancora implementato) per lasciare i nemici morti visibili come corpi invece
> di farli sparire — oggi `Health.Die()` li disattiva del tutto a fine
> animazione. **Prima di tutto** risolvi il vincolo descritto nel file:
> `WaveSpawner` e `MinionCoordinator` liberano slot/conteggi controllando
> `activeInHierarchy`, quindi un corpo lasciato attivo li confonde — scegli fra
> le due strade proposte (cambiare il pruning a `Health.IsDead`, o instanziare
> un corpo visivo separato) e verificalo con un'ondata vera di `WaveSpawner`,
> non solo guardando il corpo a terra. Poi implementa l'**Opzione A** (posa
> congelata) o la **B** (ragdoll, riusando il setup di `HIT_REACTIONS.md` se
> quella feature è già stata fatta) secondo quanto preferisce l'utente.

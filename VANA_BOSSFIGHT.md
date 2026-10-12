# Bossfight Vana — contesto per la prossima sessione

**Stato: la base è completa e verificata end-to-end**, inclusi gli attacchi a
distanza (Cast_1/Cast_2, sezione dedicata più sotto) e la **necromanzia**
(sezione "Necromanzia — DemonVale nella fase 2", la feature più recente). Le
tre rifiniture aperte nella sessione dei guardiani sono **chiuse** (vedi "Bug
risolti"), e nel farlo sono emersi due bug seri e pre-esistenti che sono stati
corretti: il **crash di Unity** a fine bossfight e le **guardie non parabili**.

Questo file riassume design, file coinvolti e stato attuale per non dover
ripartire da zero.

---

## 🧟 Necromanzia — DemonVale nella fase 2

**Stato: completa, verificata end-to-end in Play Mode.**

In fase 2 (dopo `GuardianFormation.BeginRelease`, cioè `ReleaseStarted`), Vana
rievoca le **guardie morte** (solo quelle, mai i simp normali) trasformandole
in `DemonVale` (prefab `Assets/Prefabs/Enemies/Simps/DemonVale_Simp.prefab`,
nemico completo con propria combo + Leap_attack, prima isolato e ora integrato
qui). Riusa **l'esatta animazione/trigger di Cast_1** (stesso stato Animator),
solo con un dardo diverso (`NecromancyBolt.cs`, nuovo, verde, separato da
`Projectile.cs` per non toccare codice di combattimento critico).

- **Deterministico, non casuale**: appena una guardia muore (in fase 2) viene
  rievocata, nell'ordine in cui è morta (FIFO — `deadGuardianCorpses[0]`, non
  più una scelta random). Nessun cooldown separato: il ritmo lo detta solo la
  durata della clip `Cast_1` (`castingSpecialAttack`, lo stesso lock condiviso
  di Cast_1/Cast_2). Era random con cooldown proprio in una versione
  precedente della stessa sessione — cambiato su richiesta esplicita.
- **Tetto** `maxConcurrentDemons` (3): oltre, il cadavere resta in coda e
  viene rievocato appena un demone muore e libera uno slot — non si perde mai.
- **I cadaveri delle guardie non despawnano più**: `Health.despawnOnDeath`
  (nuovo campo, default `true`) è `false` solo su
  `Valerio_Simp_Guardian.prefab`. Senza, `Health.Die()` disattiva il
  GameObject pochi secondi dopo la clip di morte e non ci sarebbe mai un
  cadavere da bersagliare. `Health.Die()` ora nasconde anche la barra vita
  esplicitamente (prima si nascondeva solo per effetto del `SetActive(false)`
  che con `despawnOnDeath=false` non arriva più).
- **Bug collaterale corretto nello stesso giro**: `DemonValeBT.SetupTree()`
  sovrascriveva sempre lo static `MinionCoordinator.MaxAttackers` al proprio
  default (2), che avrebbe silenziosamente annullato il buff di fase 2 di
  Vana (`releasedMaxAttackers`, 4) appena nasceva un demone rievocato. Fix:
  `DemonValeBT.SuppressCoordinatorOverride()`, chiamato da
  `VanaBoss.ResurrectGuardian()`.
- Tutto lo stato nuovo (`deadGuardianCorpses`, `activeDemons`) vive dentro
  `VanaBoss.cs`, non in `GuardianFormation`/`GuardianBT` — deliberatamente,
  per non toccare file già teatro del crash da stack overflow (vedi sopra).

**File**: `VanaBoss.cs` (nuova sezione "necromanzia"), `Health.cs`
(`despawnOnDeath`), `DemonValeBT.cs` (`SuppressCoordinatorOverride`),
`Combat/NecromancyBolt.cs` (nuovo). Prefab: `Valerio_Simp_Guardian.prefab`
(`despawnOnDeath=false`), `Combat/NecromancyBolt.prefab` (nuovo, duplicato da
`VanaProjectile.prefab`), Vana in `Arena.unity` (`demonValePrefab`,
`necromancyBoltPrefab`).

> ⚠️ **Trappola pagata cara in questa sessione**: il tool Coplay `set_property`
> ha impostato `demonValePrefab`/`necromancyBoltPrefab` solo in memoria — la
> scena salvata (e `get_game_object_info` subito dopo) mostrava il valore
> giusto, ma su disco il campo restava vuoto, senza alcun errore. La
> necromanzia "non faceva mai niente" in un playtest reale per questo, non per
> un bug di codice. Corretto passando da `SerializedObject` +
> `PrefabUtility.RecordPrefabInstancePropertyModifications` + save via
> `execute_script`, e verificando il file `.unity` **su disco** (grep del guid
> del prefab referenziato) invece di fidarsi della sola rilettura in-editor.
> Vale per qualunque campo a riferimento singolo su una PrefabInstance in
> scena, non solo per questo caso.

## ⚔️ Moltiplicatori di danno per singolo colpo (tutti i nemici)

**Stato: completo.** Prima solo il player aveva un moltiplicatore per singola
mossa della combo (`PlayerAnimator.punchComboMultiplier`/`slashComboMultiplier`,
array parallelo agli stati dell'Animator). I nemici avevano solo un
moltiplicatore *unico* per l'intero personaggio
(`EnemyAnimator.DamageMultiplier`, property C# non serializzata, usata solo da
`GuardianBT` per lo scudo/rilascio).

Aggiunto `EnemyAnimator.attackDamageMultiplier` (`[SerializeField] float[]`),
stesso schema del player: un fattore per ogni voce di `AttackStates`, stesso
ordine, moltiplicato insieme al vecchio `DamageMultiplier` (situazionale) non
al posto suo — `PerMoveMultiplier()`. Essendo sulla classe base lo eredita
anche `DemonValeAnimator` (che già sovrascrive `AttackStates` con
`Slash_1/Slash_2/Pierce/Leap_attack`, 4 voci contro le 5 del simp base
`Punch_Right/Punch_Left/Kick_Right/Kick_Left/Slash` — **l'ordine dell'array va
tenuto allineato a mano a `AttackStates` per ciascun prefab**, stesso vincolo
che ha già il player).

**Bug preesistente corretto nello stesso giro**: `DemonVale_Simp.prefab` non
aveva nessun `WeaponData` assegnato su `EnemyWeaponManager` → infliggeva
**sempre 0 danno**, silenziosamente, indipendentemente da hitbox/moltiplicatori.
Creato `DemonValeFistsData.asset` (`Assets/ScriptableObjects/Weapons/`,
`damage` — impostato a 25 poi alzato a **100 dall'utente in Inspector**) e
assegnato al prefab.

**Dove tunare, in Inspector:**

| Entità | Danno base | Moltiplicatore per colpo |
|---|---|---|
| Simp normale | `SimpFistsData.asset` (15) | `Valerio_Simp.prefab` → `EnemyAnimator.attackDamageMultiplier` (5 voci) |
| Guardia | `SimpFistsData.asset` | stesso array, **più** `GuardianBT.guardDamageMultiplier`/`releasedDamageMultiplier` (situazionali, preesistenti) |
| DemonVale | `DemonValeFistsData.asset` (100) | `DemonVale_Simp.prefab` → `DemonValeAnimator.attackDamageMultiplier` (4 voci: Slash_1/Slash_2/Pierce/Leap_attack) |
| Vana (Cast_1/Cast_2) | `damage` diretto su `VanaProjectile.prefab`/`VanaRadialSlam.prefab` | non applicabile (attacchi singoli, non combo) |

---

## Design attuale

Il vecchio schema (5 simp normali temporaneamente deputati a scudo, rilasciati per
isteresi di distanza) è stato **sostituito** perché il rilascio per isteresi
lasciava simp "in difesa" tornare liberi mentre il player era ancora abbastanza
vicino da farli ingaggiare normalmente — si leggeva come "un simp in difesa
abbandona il posto e insegue il player". Il nuovo schema elimina l'isteresi alla
radice con una squadra fissa e un rilascio scriptato:

- **9 guardie speciali** (`Valerio_Simp_Guardian`, prefab dedicato): molto più
  vita di un simp normale (1500 vs 200), più grandi, roster fisso — chi muore non
  viene rimpiazzato, la formazione si stringe sui sopravvissuti.
- Sparse/ferme vicino a Vana all'inizio, **rivolte verso di lei** nella posa
  `Praise`, **cieche al player per costruzione** (nessun nodo del loro albero
  guarda il player finché non sono dispiegate — non un raggio di rilevamento a
  zero, proprio l'assenza di un nodo che possa reagire).
- **Dispiegamento** a `shieldTriggerDistance` dal player, **una sola volta** per
  tutta la bossfight: le guardie prendono posto su **slot fissi**, distribuiti
  equamente su un **anello completo attorno a Vana** (`shieldArcDegrees = 360`,
  non un semicerchio: un semicerchio rivolto verso il player al momento del
  dispiegamento lasciava scoperto tutto il retro), **di spalle a Vana** — e ci
  restano **senza mai muoversi né inseguire** finché non vengono rilasciate.
  Attaccano comunque se il player entra a tiro da ferme.
- Nel frattempo Vana richiama **simp normali** (ValerioBT puro, mai deputati a
  scudo — alla difesa ci pensano solo le guardie). Il **primo richiamo ne fa
  arrivare un gruppo intero** fino a `normalSimpConcurrentCap`; da lì in poi ogni
  simp che muore ne fa richiamare **uno** di rimpiazzo, così ne restano sempre
  circa quel numero in scena, fino a esaurire il budget cumulativo
  `maxTotalSpawns`. Puntano a un punto **oltre l'anello delle guardie, verso il
  player**, **sparso su un arco** (vedi sotto).
- A budget esaurito e zero simp normali vivi: comincia la **fase 2**, il rilascio
  delle guardie. Non tutte insieme: **a ondate di `releaseBatchSize`**, e
  l'ondata successiva parte **solo quando quella in corso è stata uccisa del
  tutto**. Le guardie non ancora rilasciate restano in formazione con tutti i
  vantaggi della difesa, e **l'anello si ricompatta sui rimasti**.
- Quando anche l'ultima guardia muore: sequenza finale `No_more_simps` (input
  player bloccato) → `Defeat_idle` (input libero, un attacco qualsiasi uccide
  Vana all'istante) → `Death`.
- Vana è danneggiabile sempre, ma il vero interruttore resta `Defeat_idle` →
  `Health.AlwaysExecutable = true`.
- Barra vita viola sopra Vana (differenziata dal rosso dei simp).

### Architettura del codice

- **`VanaBoss.cs`** è un vero **Behaviour Tree** (estende `BehaviourTreeBase`,
  come `ValerioBT`/`GuardianBT`), non una macchina a stati scritta a mano. La
  logica interna (coroutine che aspettano la fine di un'animazione, enum `Phase`)
  è un wrapper sottile: classi **nested private** dentro lo stesso file
  (`PhaseIsIdleCheck`/`FinalSequenceWatch`/`GuardianDeploymentWatch`) richiamano
  gli stessi metodi privati dentro un `Sequence`+`Selector`. Nidificate perché
  utili solo nel contesto di `VanaBoss`. In futuro si aggiunge un nodo fratello
  (es. l'attacco a distanza di Vana) senza toccare il collaudato.
  - Il vecchio nodo `NormalSimpRecallWatch` **non esiste più**: il richiamo dei
    simp non è più a timer periodico, lo decide `TryToProceed()` in base a quanti
    ne sono vivi.
- **`GuardianFormation.cs`** (statico, schema di `MinionCoordinator` ma per lo
  scudo): tiene il roster delle guardie vive, calcola gli slot fissi, e gestisce
  il rilascio a ondate.
  - **"Rilasciato" è uno stato PER-GUARDIA, non globale**: c'è una seconda lista
    (`released`) che è il sottoinsieme già passato all'attacco. `ReleaseStarted`
    dice solo che la *fase* è cominciata; per il singolo serve
    `IsReleased(guardia)`. Erano due significati diversi sotto lo stesso nome, e
    tenerli separati evita esattamente il tipo di confusione che è costato il
    crash descritto sotto.
  - Gli slot dell'anello si dividono **solo fra i difensori** (`DefenderCount` /
    `DefenderIndexOf`): è così che l'anello si ricompatta quando una guardia
    viene rilasciata o muore.
  - Con `arcDegrees >= 360` (il default) divide l'intero giro in parti uguali
    (`angleStep = 360/total`, **non** `360/(total-1)`: con un anello chiuso
    l'ultimo slot non deve coincidere col primo). La direzione di riferimento
    resta congelata al momento del dispiegamento.
- **`GuardDuty.cs`** (`Enemy/BehaviourTree/`): nodo che tiene la guardia al
  proprio slot fisso, ruota verso la rotazione fissa una volta arrivata, attacca
  se il player entra a tiro. Ritorna `Failure` prima del dispiegamento e dopo il
  **proprio** rilascio, così il `Selector` di `GuardianBT` cade sul ramo giusto in
  entrambi i casi senza bisogno di due rami diversi.
- **`PreDeploymentIdle.cs`**: prima di essere chiamata in difesa, la guardia sta
  ferma rivolta verso Vana in posa `Praise`. Nessun nodo di questo ramo guarda il
  player — non è un raggio di rilevamento a zero, è l'assenza di qualunque nodo
  che possa reagire.
- **`GuardianBT.cs`**: albero della guardia, `Selector(GuardDuty,
  Sequence(GuardianNotDeployed, PreDeploymentIdle), AttackPlayer aggressivo +
  TakeFormationPosition)`. `AttackPlayer`/`TakeFormationPosition` (usati solo
  dopo il rilascio) sono **riusati senza modifiche** da `ValerioBT` — stesso
  ramo, solo parametri più aggressivi e senza il ramo "torna allo spawn se il
  player è lontano" (le guardie rilasciate non mollano mai).

---

## 🐞 Bug risolti (importanti, tutti pre-esistenti)

### 1. Crash di Unity a fine bossfight — stack overflow

**Sintomo**: Unity moriva *ogni volta* che la bossfight arrivava alla fase "simp
esauriti". Nessuna eccezione nel log, processo terminato di netto.

**Prova**: crash report `Unity-2026-08-06-100957.ips` — `EXC_BAD_ACCESS` /
`KERN_PROTECTION_FAILURE` a un indirizzo **dentro la Stack Guard region, 47 byte
prima della fine**: il thread principale era colato fuori dal proprio stack da
8MB. Lo stack mostrava il ciclo:

```
StartCoroutineManaged2 → TryCreateAndRunCoroutine → Coroutine::Run
  → Coroutine::InvokeMoveNext → StartCoroutineManaged2 → …
```

**Causa**: `StartCoroutine` esegue il primo tratto della coroutine **in modo
sincrono, dentro lo stack del chiamante**. `WaitForStateEnd` controllava subito
lo stato dell'Animator: con Vana già ferma in `Release_order` a fine clip,
entrambi i `while` passavano senza mai cedere il frame, quindi
`OnReleaseOrderFinished()` veniva chiamato immediatamente → `EndTransition()` →
`phase = Idle` → `TryToProceed()` → che **non aveva memoria del rilascio già
avvenuto** e richiamava `EnterReleaseOrder()`. Ricorsione infinita.

**Correzioni** (due livelli, entrambe in `VanaBoss.cs`):
- `WaitForStateEnd` cede **sempre** almeno un frame (`yield return null`) prima
  di qualunque controllo. Questo elimina l'intera *classe* di bug: nessuna
  transizione animata può più completarsi dentro lo stack di chi l'ha avviata.
- `TryToProceed` controlla `GuardianFormation.ReleaseStarted` ed esce subito se
  il rilascio è già cominciato.

**Verificato**: sequenza completa fino a `Defeat_idle`, `Release_order` entrato
**1 sola volta**, `No_more_simps` 1 sola volta, più 5s di osservazione senza
ri-innesco. Editor sopravvissuto.

### 2. Guardie non parabili — hitbox che risaliva al proprietario sbagliato

**Sintomo**: parare un colpo di guardia assorbiva il danno ma non la stordiva
mai, e non mostrava nessuna reazione. Sembrava che le guardie fossero immuni
alla parata.

**Causa**: `Hitbox` risaliva a chi tira il colpo con **`transform.root`**. Per un
simp istanziato a runtime la radice è il simp stesso, quindi funzionava. Ma le
guardie stanno in scena **annidate sotto il contenitore `Simps`**, quindi la
radice era il contenitore e `GetComponentInChildren<Health>()` restituiva
**sempre la prima guardia del gruppo**:

```
GUARDIA 'Guardian_7' → transform.root: 'Simps' → Health risolto: 'Guardian_1' ✗
SIMP a runtime       → transform.root: sé stesso → Health risolto: sé stesso  ✓
```

Parando `Guardian_7`, lo stordimento finiva su `Guardian_1` dall'altra parte
dell'anello.

> **Il difetto era invisibile perché colpisce solo i personaggi piazzati in
> scena.** Tutto il resto del gioco usa nemici istanziati a runtime, dove
> `transform.root` coincide col personaggio.

**Correzione**: `Hitbox` ora risale al **personaggio** — il primo antenato con
una `Health` (`OwnerHealth`/`OwnerRoot`, ririsolto a ogni `Activate()` così
un'arma che passa di mano non resta con una cache vecchia). Corretti **tutti e 7**
i punti che usavano `transform.root`, non solo quello della parata: erano
sbagliati anche il **knockback** (spingeva il contenitore `Simps`, cioè tutte le
guardie insieme), l'**hitstop**, la **direzione del VFX d'impatto** (calcolata
dalla posizione del contenitore) e il controllo "non colpire chi lo sta tirando"
(che rendeva **impossibile a una guardia colpirne un'altra**).

**Verificato**, stessa prova prima/dopo:

| | prima | dopo |
|---|---|---|
| colpi arrivati al player | 4 | 8 |
| danno passato (parata) | 0 | 0 |
| guardia stordita da parata | ❌ | ✅ |
| giustiziabile | ❌ | ✅ |
| reazione `Take_hit` | 0 | 8 |

Regressione controllata sul combattimento normale: player 33 di danno, simp 30.

### 3. Tre guardie piazzate 2.5 m sopra il pavimento

Era il vero motivo del sospetto "problema di connettività NavMesh vicino a Vana"
tramandato dalle sessioni precedenti. `Guardian_7/8/9` stavano a **Y = 2.58**
mentre il pavimento è a ~0.05: un raycast verso il basso trovava terreno 2.77 m
più sotto, e `NavMesh.SamplePosition` doveva agganciarli 2.54 m più in basso.
`CalculatePath` dalle loro posizioni restituiva **`PathInvalid`**.

Corretto riportandole a terra e risalvando la scena. Riverificato: tutte e 9 le
guardie ora danno `PathComplete` con percorso diretto (rapporto percorso/linea
retta = 1.00, nessun giro), e al dispiegamento arrivano **tutte fra 0.80 s e
1.40 s**. La NavMesh non aveva niente che non andasse: **non serve ricuocerla**.

### 4. Guardie rilasciate che inseguivano a 24 m/s

`GuardDuty` imposta `agent.speed = moveSpeedWhileGuarding` (24, deliberatamente
alto per far chiudere lo scudo in fretta). Ma smette di girare al rilascio, e
**nessuno rimetteva a posto la velocità** — l'albero della guardia non ha un nodo
`SetMovementSpeed` come `ValerioBT`. Risultato: in fase 2 inseguivano a **2.5× la
corsa del player** (9.6), rendendo impossibile disimpegnarsi, con le gambe
completamente fuori sincrono (le soglie dei blend tree sono in m/s reali).

Corretto con un campo dedicato `releasedMoveSpeed`, applicato in
`ReleaseToAggressive()`. Misurato dopo il fix: tetto 8.00 rispettato, picco reale
a regime 7.54, nessuno sforamento.

### 5. Ritardo di 25-30 s nella posa Praise — non riprodotto

Con dominio ricaricato di fresco e roster intero, tutte e 9 le guardie
convergono in `Praise` fra **0.47 s e 1.07 s**. Era quasi certamente
l'artefatto metodologico già sospettato (script modificato durante il Play Mode)
o l'instabilità dell'editor, non un bug di codice. La ripetizione del trigger in
`PreDeploymentIdle` resta al suo posto e funziona.

### 6. Simp richiamati che si incastravano

Tutti i simp richiamati puntavano allo **stesso identico punto**, quindi in più
di uno si accalcavano lì e contro l'anello delle guardie (che sta fermo, con
`NoObstacleAvoidance`, e non si sposta mai per fare spazio). In più `CanSeePlayer`
fa un raycast verso il player: con un corpo in mezzo il raggio veniva bloccato e
il simp restava fermo senza ingaggiare.

Corretto su due fronti:
- `NormalSimpArrivalPoint()` sparge i punti d'arrivo su un **arco**
  (`normalSimpArrivalArc`) davanti al player, con raggio variabile
  (`normalSimpArrivalSpread`).
- `CanSeePlayer` ha un nuovo parametro `startAlerted`, e `MinionEntrance` chiama
  `ValerioBT.MarkAlerted()` alla consegna: un minion accompagnato fino al proprio
  punto d'ingresso **è stato mandato apposta**, non deve "scoprire" il player con
  FOV/raycast. Vale sia per i richiami di Vana che per le ondate di
  `WaveSpawner`, e **non tocca la vista dei simp normali altrove nell'arena**.

Verificato: 4 simp richiamati in rapida successione, punti d'arrivo sparsi su
~4.5 m, nessuna sovrapposizione sotto 0.6 m, tutti e 4 arrivati a contatto col
player entro 10 s.

### 7. Guardie che si reinginocchiavano in pieno combattimento

**Sintomo**: durante lo scontro le guardie tornavano ogni tanto nella posa
`Praise`.

**Causa**: i trigger dell'Animator restano **armati finche' una transizione non
li consuma**. `PreDeploymentIdle` ripete `TriggerPraise()` finche' non vede lo
stato raggiunto, ma durante la fusione `Idle -> Praise` lo stato corrente e'
ancora `Idle`: le chiamate di quei frame **riarmano** il trigger dopo che la
transizione ne ha gia' consumato uno, e l'ultimo resta armato per sempre
(`Praise` non ha uscite su quel trigger che possano consumarlo). A dispiegamento
avvenuto, appena la guardia si fermava sul proprio slot e rientrava in `Idle`, il
trigger rimasto faceva scattare di nuovo `Idle -> Praise`.

Spiega perche' capitava "ogni tanto" e sempre poco dopo l'inizio: coincide con
l'arrivo in posizione.

**Correzione**: `EnemyAnimator.ResetPraise()`, chiamato in due punti — quando
`PreDeploymentIdle` conferma la posa, e in `GuardianBT.EnterGuardDuty()` come
seconda rete (serve se il dispiegamento arriva mentre la fusione e' ancora in
corso: li' la conferma non avviene mai).

**Verificato** con la stessa prova prima/dopo: da **8 rientri** in Praise fra
t=2.6 s e t=5.7 s dopo il dispiegamento, a **0**.

### 8. Guardie che camminavano sul posto invece di stare in idle

**Sintomo**: in guardia sullo slot i piedi continuavano a muoversi pur senza
spostarsi nello spazio.

**Causa**: in `GuardDuty` la riga di autoriparazione «se non ha percorso, ridagli
la destinazione» — pensata per quando l'agent non e' ancora agganciato alla
NavMesh appena instanziato — **non era limitata al tragitto**. All'arrivo il nodo
fa `ResetPath()`, e il frame successivo quella riga rimetteva la destinazione:
la guardia inseguiva in eterno un punto a pochi centimetri.

Misurato: `hasPath=True` con **4.6 cm** di distanza residua, picchi di
**0.804 m/s** sul posto, parametro `Speed` fermo a **0.450** — sopra la soglia di
0.1 della transizione `Locomotion -> Idle`, quindi l'Animator non tornava mai in
idle.

**Correzione**: l'autoriparazione vale solo con `!inPosition`, e all'arrivo si
azzera anche `agent.velocity` (l'inerzia residua da sola bastava a tenere su
`Speed`).

**Verificato**: velocita' 0.000, `Speed` 0.000, **0 frame su 495** in
`Locomotion`, spostamento 0.002 m.

> ⚠️ **Questa correzione ha però aperto il problema 1 di "Aperto"**: quella riga,
> pur causando la camminata sul posto, faceva anche da **correzione continua**
> della posizione. Ora una guardia spinta fuori dallo slot non ci torna piu'.
> Vedi sotto.

---

## ⚔️ Portata: guardia vs spada del player

Dubbio storico: con la spada il player potrebbe colpire una guardia restando
fuori dalla sua portata, senza rischiare la punizione. Misurato empiricamente:

- Il pugno della guardia **connette fino a ~1.15-1.20 m**, non connette più a
  1.25 m.
- La portata della spada del player documentata in `CLAUDE.md` è 1.05-1.10 m,
  **più corta**: per colpire deve essere già abbastanza vicino da essere colpito.

> Ora che la parata funziona davvero (bug 2), il rischio di "colpire gratis" va
> rivalutato in game vero: la lettura corretta non è più "colpisci e scappa" ma
> "para e punisci", che è il comportamento voluto.

### ⏱️ Parata: rampa eliminata, finestra guidata da animation event

Le clip d'attacco del simp sono state rifatte **apposta per essere più
telegrafate**. Startup misurati (i calci sono stati poi riallungati, vedi sotto):

| clip | durata | startup | attiva | recovery |
|---|---|---|---|---|
| Punch_right | 0.800 s | 0.260 s | 0.316 s | 0.224 s |
| Punch_left | 0.800 s | 0.278 s | 0.271 s | 0.251 s |
| Kick_left | **1.200 s** | 0.697 s | 0.352 s | 0.151 s |
| Kick_right | **1.200 s** | 0.788 s | 0.182 s | 0.230 s |

**Il vincolo non era il preavviso del nemico ma la rampa della parata.**
`Health.IsBlocking` si alzava solo quando l'Animator era *dentro* lo stato
`Block`: fra transizione (0.100 s fissi) ed elaborazione del trigger passavano
**200-300 ms misurati**, quindi sui pugni la finestra si apriva *dopo* che la
hitbox era gia' passata. Verificato: parata reattiva a 250 ms, pugno arrivato e
non parato.

**Corretto in `PlayerAnimator`**: la finestra ora si apre **nel frame stesso del
comando** (`TriggerParry` scrive `parryActive` e `health.IsBlocking` in modo
sincrono, non aspetta l'Animator) e si chiude con l'**animation event
`OnParryEnd`** sulle clip `Hand_parry`/`Sword_parry`.

Misurato dopo la correzione: **rampa 0 ms** su tre prove, finestra ~500 ms,
chiusura confermata provenire dall'evento (alzando `parryMaxWindow` a 3 s la
finestra si chiudeva comunque allo stesso istante).

> **Perche' non c'e' un `OnParryStart`**: l'apertura *deve* essere sincrona
> all'input. Un evento dentro la clip puo' scattare solo dopo che la clip e'
> partita, cioe' dopo la transizione — rimetterebbe esattamente la latenza
> appena tolta. L'asimmetria e' voluta: apertura guidata dall'input (gameplay),
> chiusura guidata dall'animazione (posa). Un `OnParryStart` avrebbe senso solo
> per dare alla parata un proprio startup scoperto, alla Sekiro — scelta di
> design piu' severa, non una correzione.

Budget di reazione ora = startup pieno dell'attacco: **calci comodi** (~700-790
ms), **pugni sul filo** (260-278 ms contro ~250 ms di reazione umana). Il collo
di bottiglia residuo non e' piu' il codice ma lo startup dell'animazione.

**Manopole**, in ordine di quanto contano:
1. posizione di `OnParryEnd` nelle clip (oggi normalizzato **0.5**) — allarga o
   stringe la finestra, ed e' il posto giusto perche' cade sui fotogrammi veri;
2. `speed` dello stato `Block` (**1.8**) — comprime tutta la clip, quindi anche
   la finestra: quello che vedi in Blender dura quasi la meta';
3. `parryMaxWindow` su `PlayerAnimator` (**0.8 s**) — **solo rete di sicurezza**
   per quando l'animazione viene interrotta prima dell'evento. Non usarlo per
   tarare: se scende sotto il momento in cui scatta `OnParryEnd`, taglia la
   finestra e la taratura vera diventa invisibile.

### 💫 Stun da parata: la finestra di risposta ora si vede

**Problema**: la catena e' `parata -> damage -> stun -> risposta`, ma stun da
parata e flinch normale erano **la stessa identica cosa** — `Stagger()` faceva
sempre `SetTrigger(Take_hit)`, stessa animazione, stesso numero. L'unica
differenza viveva in un bool invisibile (`stunnedFromParry`). Il giocatore non
aveva **nessun** modo di capire quando poteva rispondere.

In piu' le due durate erano gia' desincronizzate: clip `Damage` 1.20 s a
velocita' 2x = **0.60 s reali**, contro `hitStunDuration` **1.00 s**. Il simp
tornava in posa 0.4 s prima di poter davvero agire: il segnale visivo era pure
sbagliato.

**Struttura scelta** (fra clip unica e due clip, scelta la seconda):

```
Take_hit --(Stunned)--> Stun (CICLA) --(!Stunned)--> Idle
Take_hit --(altrimenti)--> Idle        (flinch normale, invariato)
```

- `Health` tiene il bool `Stunned` allineato a `IsStunned && stunnedFromParry`:
  vero **solo** dopo una parata, mai su un colpo normale.
- La clip `Stun` **cicla**, quindi la durata la comanda `hitStunDuration` da
  Inspector e l'animazione la segue: **non possono desincronizzarsi**, perche'
  nessuno dei due lati conosce la durata dell'altro. Si tara provando in gioco,
  senza ri-esportare nulla.
- `Take_hit -> Stun` va messa **prima** di `Take_hit -> Idle` nell'ordine delle
  transizioni: quella verso Idle non ha condizioni, quindi valutata per prima
  vincerebbe sempre.
- `Stun -> Idle` e' **senza exit time**: esce appena la finestra si chiude,
  invece di aspettare la fine del ciclo.

Verificato dal vivo (`hitStunDuration` = 2.50 s): entra in `Take_hit` a 199 ms,
in `Stun` a 800 ms, esce a 2899 ms — **posa di stun visibile 2.10 s**, e
`IsVulnerableToExecution` vero per tutta la durata.

> La finestra di risposta e' aperta **fin dall'istante della parata**, non da
> quando compare la posa di stun: l'esecuzione funziona gia' durante `Damage`.
> Lo `Stun` e' una *conferma* che la finestra e' ancora aperta, non il suo
> inizio.

> Vale la pena aggiungere anche un **tell non animato** (bagliore, stelline,
> flash sul materiale): l'animazione da sola, vista da lontano o di spalle con
> sei simp addosso, si perde. Non ancora fatto.

---

## 🎬 Animazioni

Tutte le animazioni del simp sono consolidate in un unico FBX,
`Assets/Art/Simps/Animations/Simp_animations_v1.fbx` (**14 clip**: Idle,
Walk_front, Walk_back, Strafe_left, Strife_right, Run, Punch_left/right,
Kick_left/right, Damage, Death, **Praise**, **Stun**).

Quelle del player stanno in **due** FBX, `Assets/Art/Rabbi/Animations/`:
- **`Rabbi_animations_v1.fbx`** (24 clip): attacchi, spada, i 4 `Dash_*`, Jump.
  Contiene ancora `Hand_parry`/`Sword_parry` vecchie, **non piu' usate**.
- **`Rabbi_animations_v2.fbx`** (20 clip): Idle, **Idle_sword**, Walk/Run
  (+_sword), tutte le `Guard_*`, Damage, Area_slash, e le **nuove
  `Hand_parry`/`Sword_parry`** (1.160 s, finestra piu' ampia) — a queste punta
  il controller.

- **Import**: il file arrivava come Generic senza avatar (stesso problema che
  `CLAUDE.md` segnala per le clip da Blender) — impostato `animationType=Human`,
  `avatarSetup=CreateFromThisModel` via script. Loop **attivo** su
  locomozione+Praise, **disattivo** sulle clip d'azione.
- **`Valerio_AnimatorController.controller`**: tutte le Motion ripuntate alle
  nuove clip. Aggiunto stato **Praise** (loop) con trigger `Praise`, transizione
  `Idle -> Praise` e le stesse uscite di Idle, così muoversi o attaccare fa
  uscire da sola dalla posa.
- **`EnemyAnimator.TriggerPraise()`** / **`IsPraising()`**, usati da
  `PreDeploymentIdle`, che ripete il trigger finché lo stato non è davvero
  raggiunto (`SetTrigger` nello stesso frame in cui l'Animator si abilita può
  perdersi in silenzio).

### ⚠️ OGNI RI-EXPORT DELL'FBX AZZERA TUTTO — procedura di recupero

**Succede ogni volta** che l'FBX viene ri-esportato da Blender e sovrascritto
(capitato due volte: allo swap iniziale e di nuovo il 6 agosto). Unity **resetta
il `.meta` ai default**, e si perde molto più degli eventi:

- `animationType` torna a **Generic** e l'avatar sparisce (`avatarSetup: 0`);
- `clipAnimations` torna **vuoto**, quindi si perdono *tutte* le impostazioni
  per-clip: loop **e** animation event.

Senza gli eventi, le hitbox non si aprono mai: **nessun colpo di nessun simp o
guardia fa danno**, in silenzio, senza errori. La prima volta è passato
inosservato per un'intera sessione.

**Come accorgersene subito**: `grep -c functionName` sul `.meta` deve dare **8**.
Se dà 0, è successo.

**Procedura di recupero** (via `execute_script`, non c'è un tool Coplay dedicato):

1. `importer.animationType = ModelImporterAnimationType.Human` +
   `avatarSetup = CreateFromThisModel`, poi `SaveAndReimport()`.
   Verificare l'avatar risultante: `isValid=True isHuman=True`.
2. Partire da `importer.defaultClipAnimations` (perché `clipAnimations` è vuoto),
   impostare `loopTime = true` su **Idle, Walk_front, Walk_back, Strafe_left,
   Strife_right, Run, Praise** e `false` sulle altre.
3. Rimettere gli 8 eventi, **tempi normalizzati 0..1** (vedi `CLAUDE.md`: il
   `ModelImporter` li vuole normalizzati, `AnimationUtility` li restituisce in
   secondi):

   | clip | evento | time (normalizzato) |
   |---|---|---|
   | Punch_right | OnRightHandStart / OnRightHandEnd | 0.32537454 / 0.7201815 |
   | Punch_left | OnLeftHandStart / OnLeftHandEnd | 0.34763756 / 0.68615675 |
   | Kick_right | OnRightFootStart / OnRightFootEnd | 0.6565687 / 0.8085494 |
   | Kick_left | OnLeftFootStart / OnLeftFootEnd | 0.5809493 / 0.8744742 |

   > ⚠️ Questi sono i valori del **set telegrafato del 6 agosto**, scelti apposta
   > per rendere la parata leggibile. **NON** usare quelli delle vecchie clip
   > singole (`Punch_right.fbx` ecc., ancora nel progetto): quelle hanno gli
   > startup vecchi e molto più corti, ripristinarle annullerebbe il lavoro di
   > telegrafatura.

4. **Prima di applicarli, controllare che le durate combacino**: i tempi sopra
   valgono per clip d'attacco da **0.8000 s**. Se un ri-export cambia la durata,
   i tempi normalizzati cadono nel punto sbagliato e vanno ricalcolati.
5. Verificare end-to-end in Play Mode che il danno passi in **entrambe** le
   direzioni — è l'unica prova che conta.

#### La variante silenziosa: intervalli di frame che restano indietro

Se `clipAnimations` **non** viene azzerato ma il ri-export cambia la lunghezza
delle animazioni, succede il contrario ed è più insidioso: il `.meta` conserva
gli **intervalli di frame vecchi** (`firstFrame`/`lastFrame`) e Unity ritaglia le
clip su quelli. Le clip "ci sono" e i nomi tornano, ma i fotogrammi no — e un
reimport forzato **non** basta, perché il valore sbagliato sta nel `.meta`.

Capitato due volte:
- `Sword_parry`: meta 0-49 contro file 0-29, cioè 20 fotogrammi inventati oltre
  la fine dell'animazione;
- `Kick_left`/`Kick_right`: meta 0-20 contro file 0-30, cioè **calci troncati a
  2/3** che giravano in 0.80 s invece di 1.20 s.

**Come accorgersene**: confrontare `importer.clipAnimations` (memorizzato) con
`importer.defaultClipAnimations` (quello che il file contiene davvero). Dove gli
intervalli non coincidono, riallineare i secondi ai primi.

> ⚠️ Riallineare sposta anche gli **animation event**, perché sono memorizzati in
> tempo normalizzato: allungando `Kick_left` da 0.80 a 1.20 s il contatto è
> passato da 0.465 s a 0.697 s, restando alla stessa *proporzione*. Se la clip è
> stata riallungata per intero va bene; se i fotogrammi in più sono una posa
> tenuta in coda, i tempi giusti sono quelli **assoluti** di prima. Per capirlo,
> campionare le curve a più istanti: se l'ultimo tratto non cambia, è una posa
> tenuta. (Sui calci del 7 agosto il movimento c'era per tutta la durata, quindi
> lo scalamento proporzionale è stato tenuto — **da riverificare a occhio** che
> il contatto del piede cada dove serve.)

> Il **controller ha retto** entrambi i ri-export (le Motion si riagganciano per
> nome), quindi di norma non va ritoccato. Va comunque ricontrollato: 13 stati su
> 14 validi. L'unico sganciato è **`Slash`**, che punta a un guid inesistente —
> difetto **pre-esistente e indipendente**, il set del simp non contiene una clip
> `Slash`. Riguarda solo i simp armati.

---

## 🛕 Bimah rialzato

`Small_bimah` (~1.77×1.80 m, superficie a Y=1.55) sotto Vana: serve a farla
vedere meglio da lontano e come barriera aggiuntiva.

- **Conseguenza già corretta**: tutti i calcoli spaziali di `VanaBoss` usavano
  `transform.position` di Vana (in alto) senza appiattire la quota.
  `UpdateProximity` appiattisce la Y prima di calcolare la distanza; slot delle
  guardie e punto d'arrivo dei simp passano da `NavMesh.SamplePosition` per
  agganciarsi al pavimento vero.
- **`shieldRadius` 2.0**: il bimah ha un ingombro di ~1.26 m dal centro
  all'angolo, una guardia a raggio 1.15 ci finirebbe dentro.
- **Varchi dell'anello**: la corda fra slot adiacenti è `2·r·sin(180°/N)`. A
  raggio 2.0 servirebbe N≥13 per sigillare del tutto; con N=9 resta un varco
  reale. **Il conteggio si regola a richiesta diretta dell'utente, senza
  rimisurare il varco a ogni cambio.**

---

## File coinvolti

**Nuovi (sessioni precedenti):**
- `Assets/Scripts/Enemy/GuardianFormation.cs` — coordinatore statico del roster,
  degli slot fissi e del rilascio a ondate.
- `Assets/Scripts/Enemy/BehaviourTree/GuardDuty.cs`
- `Assets/Scripts/Enemy/BehaviourTree/GuardianNotDeployed.cs`
- `Assets/Scripts/Enemy/BehaviourTree/PreDeploymentIdle.cs`
- `Assets/Scripts/Enemy/GuardianBT.cs`
- `Assets/Prefabs/Enemies/Simps/Valerio_Simp_Guardian.prefab`
- `Health.IsUnstaggerable` (in `Player/Health.cs`): come `IsArmored` ma esterno.
  `GuardianBT` lo alza in `EnterGuardDuty()` e lo riabbassa in
  `ReleaseToAggressive()` — in difesa la guardia incassa senza flinchare
  (romperebbe la postazione fissa), da rilasciata torna interrompibile.
  ⚠️ Non impedisce lo stordimento **da parata**: `Hitbox` chiama `Stagger()`
  direttamente, che non guarda questo flag. È voluto — la parata deve punire.
- `Assets/Art/GothGirls/Vana/VanaAnimatorController.controller`

**Modificati in questa sessione:**
- `Assets/Scripts/Combat/Hitbox.cs` — **`OwnerHealth`/`OwnerRoot` al posto di
  `transform.root`** in tutti e 7 i punti (vedi bug 2). Tocca il combattimento di
  *tutti*, player compreso: se qualcosa nel combattimento si comporta in modo
  strano, è il primo posto da guardare.
- `Assets/Scripts/Enemy/VanaBoss.cs` — `yield return null` in `WaitForStateEnd`,
  guardia su `ReleaseStarted`, richiamo simp per tetto invece che a timer,
  punti d'arrivo sparsi su un arco, campo `releaseBatchSize`.
- `Assets/Scripts/Enemy/GuardianFormation.cs` — rilascio a ondate, stato
  "rilasciato" per-guardia, anello che si ricompatta sui difensori.
- `Assets/Scripts/Enemy/GuardianBT.cs` — campo `releasedMoveSpeed`; **tooltip su
  tutti e 23 i parametri Inspector** (prima ne avevano 10).
- `Assets/Scripts/Enemy/BehaviourTree/CanSeePlayer.cs` — parametro `startAlerted`.
- `Assets/Scripts/Enemy/ValerioBT.cs` — metodo `MarkAlerted()`.
- `Assets/Scripts/Enemy/MinionEntrance.cs` — chiama `MarkAlerted()` alla consegna.
- `Assets/Scenes/Arena/Arena.unity` — `Guardian_7/8/9` riportati a terra.
- `Assets/Scripts/Player/PlayerAnimator.cs` — parata sincrona all'input
  (`parryActive`, `parryCommandTime`, `OnParryEnd`, `EndParry`), campo
  `parryWindowEnd` **sostituito** da `parryMaxWindow` (semantica diversa: non e'
  piu' la finestra, e' solo un tetto di sicurezza).
- `Assets/Scripts/Player/Health.cs` — `Update()` che tiene allineato il bool
  `Stunned` dell'Animator a `IsStunned && stunnedFromParry`. Il parametro viene
  scritto solo se il controller ce l'ha (il player non ne ha bisogno: nessun
  nemico para), altrimenti Unity stamperebbe un warning a ogni frame.
- `Assets/Scripts/Player/TargetLockSystem.cs` — alla morte del bersaglio il lock
  **passa da solo al prossimo** invece di azzerarsi; la morte si legge da
  `Health.IsDead` (non da `activeSelf`, che resta vero per tutta l'animazione di
  morte e ritarderebbe il cambio di quasi un secondo); i cadaveri sono esclusi
  dalla ricerca. Fuori range invece si sblocca e basta, senza cambiare bersaglio.
- `Assets/Prefabs/Enemies/Simps/Valerio_AnimatorController.controller` —
  parametro bool `Stunned`, stato `Stun` (clip ciclata), transizioni
  `Take_hit -> Stun` / `Stun -> Idle`.
- `Assets/Prefabs/Player/Rabbi_AnimatorController.controller` — `BlockBlend`
  ripuntato alle nuove clip di parry in v2; `Idle_Free` da clip singola a
  **BlendTree `IdleFreeBlend`** su `Stance` (0 = `Idle`, 1 = `Idle_sword`).
- `Assets/Art/Rabbi/Animations/Rabbi_animations_v2.fbx` — bake into pose
  ripristinati su 16 clip, `Idle_sword` configurata, `Sword_parry` riallineata,
  eventi `OnParryEnd`.
- `Assets/Art/Simps/Animations/Simp_animations_v1.fbx` — clip `Stun` (loop),
  calci riallineati a 30 frame.

**Cancellati (sessioni precedenti):** `Assets/Scripts/Enemy/SimpShieldMode.cs`.

---

## Valori attuali

Letti dai componenti in scena, non dai default del codice — **i default sono
spesso diversi**, e il valore serializzato vince (vedi note tecniche).

| Parametro | Dove | Valore |
|---|---|---|
| shieldTriggerDistance | VanaBoss | **25** |
| shieldRadius | VanaBoss | 2.0 |
| shieldArcDegrees | VanaBoss | 360 (anello completo) |
| numero guardie | guardianSimps | 9 |
| normalSimpArrivalOffset | VanaBoss | 3 |
| normalSimpArrivalArc | VanaBoss | 120° (arco di dispersione) |
| normalSimpArrivalSpread | VanaBoss | 1.5 |
| normalSimpConcurrentCap | VanaBoss | **6** (simp vivi contemporaneamente) |
| maxTotalSpawns | VanaBoss | **5** (budget cumulativo — basso per testare in fretta) |
| releaseBatchSize | VanaBoss | **3** (guardie per ondata) |
| releasedMaxAttackers | VanaBoss | 4 |
| Vana Health.maxHealth | Vana | **5000** |
| attackRangeWhileGuarding | GuardianBT | **1.25** |
| arriveDistance | GuardianBT | 0.15 |
| turnSpeed | GuardianBT | 480 |
| moveSpeedWhileGuarding | GuardianBT | 24 (solo per chiudere lo scudo in fretta) |
| **releasedMoveSpeed** | GuardianBT | **8** (fase 2 — sotto la corsa del player, 9.6) |
| guardDamageMultiplier | GuardianBT | 6 |
| releasedDamageMultiplier | GuardianBT | **6** |
| guardKnockbackMultiplier | GuardianBT | 0 |
| attackRange | GuardianBT | 1.05 |
| strikeDistance | GuardianBT | 1.0 |
| spacingDistance | GuardianBT | **2** (alzato: a 3 stavano troppo lontane, sotto troppo addosso) |
| maxChain | GuardianBT | 4 |
| commitIntervalMin/Max | GuardianBT | 0.3 / 0.6 |
| playerReach | GuardianBT | 1.15 |
| punishCooldown | GuardianBT | 0.6 |
| Health.maxHealth guardia | prefab | 1500 |
| scala guardia | prefab | **1.5×** |
| **hitStunDuration** simp/guardia | prefab `Health` | **2.5 s** — durata della finestra di risposta dopo la parata, e quanto cicla la posa di `Stun` |
| hitStunDuration player | `Rabbi_Player` | 0.75 s |
| **parryMaxWindow** | PlayerAnimator | **0.8 s** (solo rete di sicurezza) |
| `OnParryEnd` | clip `Hand_parry`/`Sword_parry` | normalizzato **0.5** |
| speed stato `Block` | Rabbi controller | 1.8 |

> `maxTotalSpawns = 5` è tarato **per il test**, non per il gioco finito: serve ad
> arrivare in fretta alla fase 2. Da rialzare quando si bilancia sul serio.

---

## Note tecniche (Coplay/Unity)

- **`StartCoroutine` esegue il primo tratto in modo SINCRONO**, dentro lo stack
  del chiamante. Se una coroutine può completare senza mai cedere un frame e
  rimette in moto chi l'ha avviata, si ottiene una ricorsione infinita e Unity
  muore per stack overflow **senza eccezione nel log**. Regola pratica: una
  coroutine che fa da "aspetta e poi richiama" deve cedere almeno un frame prima
  di qualunque controllo.
- **Il Play Mode ha "Reload Domain" DISATTIVATO** in questo progetto (lo dice il
  log: `Entering Playmode with Reload Domain disabled`). Da qui la conseguenza
  già nota: **lo stato statico (`GuardianFormation`, `MinionCoordinator`) NON si
  azzera fra una sessione di Play Mode e l'altra.** I test devono azzerarlo
  esplicitamente via reflection sui backing field
  (`<IsDeployed>k__BackingField`, `<ReleaseStarted>k__BackingField`, lista
  `released`), oppure vanno preceduti da un ricompilo che forzi il ricarico.
- **`save_scene` (Coplay) è inaffidabile**: salvare sempre con `execute_script`
  chiamando `EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),
  "Assets/Scenes/Arena/Arena.unity")` (serve `using UnityEditor.SceneManagement;`).
- **`set_property` (Coplay) non supporta liste/array**: serve `execute_script`
  con `SerializedObject`/`SerializedProperty`, poi
  `PrefabUtility.RecordPrefabInstancePropertyModifications` + salvataggio.
- **Un valore già serializzato vince sul default in codice**: cambiare il default
  di un campo non tocca l'override già scritto nel `.unity`/`.prefab`. Va
  risovrascritto via `SerializedObject`. Un campo **nuovo**, invece, prende il
  default dal codice. Dopo un rename usare `[FormerlySerializedAs]`.
- **Forzare la parata nei test**: `PlayerAnimator.Update` riscrive
  `Health.IsBlocking` dalla finestra di parata ogni frame. Gli animation event
  che aprono le hitbox scattano **dopo tutti gli `Update` e prima dei
  `LateUpdate`**: per essere in parata in quel momento serve un componente con
  `[DefaultExecutionOrder]` alto che riscriva il flag in `Update`. Metterlo in
  `LateUpdate` è **troppo tardi**, il colpo è già stato risolto.
- **Il `CharacterController` respinge il player** dalla capsula del nemico: in un
  test di contatto ravvicinato va riposizionato **ogni frame**, o finisce a ~1.9 m
  e i colpi non arrivano più. Non si può disabilitare il CC per tenerlo fermo:
  ne disabiliterebbe il collider e le hitbox non lo troverebbero più.
- Regole generali del progetto in `CLAUDE.md` (frame ≠ tempo, uscire e rientrare
  da Play Mode dopo modifiche agli script, disattivare `PlayerInput` per test
  iniettati, leggere `~/Library/Logs/Unity/Editor.log` invece di `get_unity_logs`).

---

## Aperto per la prossima sessione

1. **⚠ REGRESSIONE APERTA — la formazione si disfa e le guardie entrano nel
   bimah.** Segnalato dall'utente ("i guardiani si compenetrano col bimah"),
   misurato e diagnosticato ma **non ancora corretto**.

   Due cause distinte che si sommano:

   **(a) Nessun rientro in posizione.** Gli slot sono calcolati bene
   (`slotDistDaVana = 2.000` per tutte e nove, sempre), ma le guardie non ci
   restano. Misurato lo scarto dallo slot: a t=2s va da 0.10 a 1.93 m, a t=10s
   **peggiora** (Guardian_7 da 0.75 a 1.13 m, Guardian_4 da 0.10 a 0.78 m).
   Causa: il bug 8 e' stato corretto vincolando l'autoriparazione a
   `!inPosition`, ma quella riga faceva anche da correzione continua. Ora, una
   volta dichiarata `inPosition`, la guardia non ridà **mai piu'** la
   destinazione: se viene spinta (dalle compagne, dai colpi, dal knockback)
   resta dove l'hanno lasciata. Con nove capsule da 0.60 m di raggio su un
   anello di circonferenza 12.6 m si toccano per forza.

   **Soluzione prevista**: rientro a soglia con isteresi — se lo scarto supera
   una soglia (es. 0.4 m) si ridà la destinazione e `inPosition = false`; sotto
   quella si resta in idle. Cosi' si tiene la formazione senza tornare alla
   camminata sul posto.

   **(b) `shieldRadius = 2.0` non basta piu'.** Fu scelto con le guardie a
   **scala 1.2 e capsula 0.48 m**; ora sono a **scala 1.5, capsula 0.60 m**.
   All'angolo del bimah (1.265 m dal centro) restano 0.735 m contro 0.60 m di
   raggio: **13 cm di margine**, che sparisce alla prima spinta.

   Misurato con `Physics.ComputePenetration`: **4 guardie su 9 compenetrano** il
   MeshCollider del bimah, fino a **0.654 m**; una arriva a 0.27 m da Vana,
   cioe' in piedi sopra il bimah.

   **Scelta lasciata all'utente**: alzare `shieldRadius` a ~2.4, oppure ridurre
   la scala delle guardie (decisione estetica).

   > Nota: il bimah ha un `MeshCollider` ma **nessun `NavMeshObstacle`**, quindi
   > non respinge gli agent — li ferma solo la fisica, che con gli agent e' debole.

2. ~~Nuova feature: attacchi a distanza per Vana.~~ **Fatto** (Cast_1
   proiettile mirato + Cast_2 radiale a terra, vedi sezione dedicata più
   sopra) — questa voce è superata, lasciata solo come traccia storica.
3. **`GuardDuty` non controlla se la guardia è stordita** prima di rilanciare un
   attacco, mentre `AttackPlayer` lo fa (`enemyAnimator.IsBeingHit()`). Ora che
   lo stordimento da parata arriva davvero (bug 2), una guardia può ripartire ad
   attaccare durante la propria reazione, accorciando la finestra di punizione.
   Nei test si è comportata bene (8 reazioni su 8 attacchi), ma è **il primo
   posto da guardare** se in gioco la punizione sembra durare troppo poco.
4. **Il simp continua a muoversi mentre è in stun** — la posa di `Stun` gira ma
   il `NavMeshAgent` non viene fermato, quindi scivola invece di restare
   inchiodato. **In lavorazione al momento dell'ultimo aggiornamento.**
5. **I pugni del simp restano sul filo**: budget 260-278 ms contro ~250 ms di
   reazione umana. La rampa della parata è stata azzerata (non è più il collo di
   bottiglia), quindi l'unica leva rimasta è allungarne lo startup verso
   ~350-400 ms — scelta di design, non problema tecnico.
6. **Contatto dei calci da riverificare a occhio**: riallineandoli da 0.80 a
   1.20 s gli animation event si sono spostati in proporzione (0.465 → 0.697 s).
   Il movimento c'è per tutta la durata, quindi è plausibile, ma il momento
   esatto del contatto del piede va guardato in anteprima.
7. **Tell non animato per lo stun** (bagliore/stelline/flash): l'animazione da
   sola si perde vista da lontano o con sei simp addosso.
8. **`maxTotalSpawns = 5` è un valore da test**, va rialzato per il bilanciamento
   vero.
9. **`Kick_right` del simp non registra mai** (pre-esistente, vedi `CLAUDE.md`):
   è fuori rotazione in `EnemyAnimator.AttackTriggers`.
10. **Stato `Slash` del controller sganciato**: punta a un guid che non esiste più
   nel progetto, e il set di animazioni del simp non contiene una clip `Slash`.
   Pre-esistente, indipendente dai ri-export. Si manifesta solo su un simp con
   un'arma equipaggiata (`EnemyAnimator.TriggerAttack` usa il trigger `Slash`
   quando l'arma non è `isFists`).
11. **Schivata reattiva del simp spenta** (`evadeChance = 0`): lo startup del
   player dura meno del tempo di reazione. Il ramo è pronto per quando gli
   attacchi saranno più telegrafati.

---

## Prompt suggerito per la prossima sessione

> Leggi `CLAUDE.md` e `VANA_BOSSFIGHT.md` nella root del progetto per il
> contesto completo (combattimento, IA dei simp, e la bossfight di Vana —
> guardie, richiami, necromanzia, moltiplicatori di danno: tutto già
> implementato e verificato end-to-end, non ripartire da zero).
>
> Ora devo lavorare sul **ritmo della battaglia**: la cadenza con cui i nemici
> attaccano, singolarmente e come gruppo. Non ho ancora sintomi precisi da
> darti — esplora con me cosa non torna giocando, poi decidiamo insieme cosa
> tarare. Alcune manopole che già esistono e sono il punto di partenza più
> probabile:
>
> - **Per singolo nemico** (`AttackPlayer.cs`, istanziato con parametri diversi
>   da `ValerioBT`/`GuardianBT`/`DemonValeBT`): `commitIntervalMin/Max` (pausa
>   fra un impegno e il successivo), `reactionDelayMin/Max` (quanto ci mette a
>   punire un recovery), `punishCooldown`, `maxChain` (colpi concatenati),
>   `closingTimeout`.
> - **Per gruppo** (`MinionCoordinator.cs`, stato `static` condiviso da tutti):
>   `MaxAttackers` (quanti possono attaccare insieme — Vana lo alza da sola in
>   fase 2, vedi `releasedMaxAttackers`) e `SlotCooldown` (chi ha appena
>   attaccato non può riprendersi subito lo slot).
> - **Per Vana stessa**: `rangedAttackCooldown` (10s), `radialSlamCooldown`
>   (5s); la necromanzia invece non ha più un cooldown proprio (deterministica,
>   vedi sezione dedicata), quindi il suo ritmo lo detta solo quante guardie
>   muoiono e quanto dura il cast.
> - I tempi del **player** sono già documentati e misurati in `CLAUDE.md`
>   ("Numeri misurati" — attacchi 0.42-0.74s, simp 0.53s): è il metro di
>   paragone per capire se un nemico è troppo lento o troppo rapido a punire.

# Handoff

Bacheca fra la sessione animazione e la sessione codice. Protocollo in `README.md`.
**Le voci nuove vanno in cima**, sotto le domande aperte. Non si cancellano voci: quando
una domanda è risolta si sposta nelle voci con la risposta.

Formato di una voce:

```
## AAAA-MM-GG HH:MM · <da> → <a>
**Fatto:** …
**Serve da te:** …   (oppure "niente")
**File toccati:** …
```

---

## Domande aperte

| # | Domanda | Per | Aperta da |
|---|---|---|---|
| 9 | _(risolta 15:50: proposta del codice accettata. In comune `Walk`, `Run`, salto; per arma la guardia col lock e i suoi strafe; braccia per arma `<Set>_Arms_Walk/Run`. Annotato in `PIPELINE.md`)_ | — | — |
| 10 | _(risolta 15:50: salto in tre parti, marker `liftoff` aggiunto a `PIPELINE.md` e allo script)_ | — | — |
| 1 | _(risolta: percorso del `.blend` annotato in `PIPELINE.md`)_ | — | — |
| 2 | _(risolta 12:41: *Copy From Other Avatar* da `Rabbi_rigged`, annotato in `PIPELINE.md`)_ | — | — |
| 3 | _(risolta 11:35: scartare `8bf3b91` e `d9efa2e`)_ | — | — |
| 4 | _(risolta 11:35: niente `reachMultiplier`, solo misura)_ | — | — |
| 5 | _(risolta 11:35: lunge scartato, si rifà nell'`ActionPlayer`)_ | — | — |
| 6 | _(risolta 15:56: compressione **Off** per tutte le clip, annotato in `PIPELINE.md`)_ | — | — |
| 7 | _(risolta 12:43: la **spada** è `AbramKnife` (1.06 m), il **coltello** è `Izmel` (0.54 m))_ | — | — |
| 8 | _(risolta 12:43: vale l'altezza del modello originale, **1.60 m** come in Unity; Blender si adegua)_ | — | — |

---

## 2026-10-14 15:28 · codice → animazione

**Fatto** (voci 15:15 e 15:20).
1. **Blend `Walk`/`Run` sulle velocità di gioco**, commit `fa47206`. Nuovo parametro
   `LocomotionBlend`: vale 0 a `walkSpeed` (solo `Walk`) e 1 a `sprintSpeed` (solo `Run`), e
   le soglie del blend sono 0 e 1. Il controller non va più toccato quando cambiano le
   velocità.
   - **Riproduzione** = velocità reale ÷ velocità nativa di quello che suona: a
     `walkSpeed` vale `walkSpeed ÷ 1.2`, a `sprintSpeed` vale `sprintSpeed ÷ 5.0`. In mezzo,
     velocità reale ÷ (1.2 e 5.0 pesati come il blend). Agli estremi coincide con la tua
     interpolazione; in mezzo segue il piede invece della media dei due rapporti. Sotto
     `walkSpeed` rallenta fino al minimo 0.35, sopra `sprintSpeed` accelera senza tetto.
   - **Prefab**: `walkSpeed` **1.5**, `sprintSpeed` **6.0**. `lockMoveSpeed` resta 0.8.
   - **Strafe**: la formula agganciata è già velocità ÷ `guardClipSpeed`, senza tetto. Oggi
     `guardClipSpeed` è 4.8 perché sotto lock suonano ancora le clip vecchie. Quando integro
     `Sword_Guard_Walk_F/B/L/R` diventa 0.8.
2. **Spada senza lock**, commit `61bff40`: `Sword_Idle` da fermo, `Sword_Arms_Walk`/`Run`
   in movimento.
   - **Niente layer `Weapon_Overlay`**: le tue clip sono a corpo intero con le gambe uguali a
     `Walk`/`Run`, quindi le ho messe direttamente al posto di `Walk`/`Run` nel blend della
     locomozione con l'arma (`Locomotion_Free_Sword`). Il blend 1D tiene già in fase le
     clip che mescola, e stessa durata e stessa fase delle gambe danno lo stesso risultato
     di un layer sincronizzato, senza il rischio che i due layer si sfasino. Blend e
     velocità di riproduzione sono quelli del punto 1.
   - `Weapon_Overlay` resta acceso solo col lock, come prima.
   - **Postura per arma**: oggi la postura sa solo "armato / a mani nude". Anche il
     **coltello** (`Izmel`) usa quindi `Sword_Idle` e `Sword_Arms_*`. Quando ci saranno
     clip del coltello, serve una postura per arma.
3. Committati anche i tuoi `speed` (`Dodge_F` 0.8, `Sword_Light_1/2/3` 0.85), l'FBX e il
   JSON con le clip nuove, e `Tools/Blender/create_sword_arms.py`.
4. La mia voce "15:30" era del commit delle 14:51: corretto l'orario e rimessa in ordine.

**Misure** (Play Mode, tempo accumulato, senza nemici). "Piede" è un calcolo: riproduzione
× velocità nativa del blend.

| caso | velocità | blend | riproduzione | piede | clip |
|---|---|---|---|---|---|
| mani nude, camminata | 1.50 | 0 | ×1.25 | 1.50 | `Walk` |
| mani nude, sprint | 6.00 | 1 | ×1.20 | 6.00 | `Run` |
| mani nude, stick 0.5 | 3.00 | 0.33 | ×1.22 | 3.00 | blend |
| mani nude, stick 0.2 | 1.20 | 0 | ×1.00 | 1.20 | `Walk` |
| `walkSpeed` 2.0 in gioco | 2.00 | 0 | ×1.67 | 2.00 | `Walk` |
| `sprintSpeed` 7.0 in gioco | 7.00 | 1 | ×1.40 | 7.00 | `Run` |
| spada, fermo | 0 | | | | `Sword_Idle` |
| spada, camminata | 1.50 | 0 | ×1.25 | 1.50 | `Sword_Arms_Walk` |
| spada, stick 0.5 | 3.00 | 0.33 | ×1.22 | 3.00 | 67% Walk, 33% Run |
| spada, sprint | 6.00 | 1 | ×1.20 | 6.00 | `Sword_Arms_Run` |

**Campi da dire all'utente**: sul `Rabbi_Player`, componente **PlayerMovement**:
`walkSpeed`, `sprintSpeed` e `lockMoveSpeed`. Si possono cambiare anche in Play Mode e
l'effetto è immediato, ma i valori cambiati in Play Mode non restano: quelli buoni vanno
poi riscritti fuori dal Play Mode. Le velocità native delle clip (`walkClipSpeed` 1.2,
`runClipSpeed` 5.0 su **PlayerAnimator**) non vanno toccate: sono proprietà delle clip.

**Serve da te:** niente. Resta da fare la guardia col lock (`Sword_Guard_Idle` e strafe).

**File toccati:** `Docs/Combat/HANDOFF.md`; codice, controller e prefab nei commit
`fa47206` e `61bff40`.

## 2026-10-14 15:20 · animazione → codice

**Fatto:** esportate (FBX 15:16) le clip della spada fuori dal lock:
- **`Sword_Idle`** 0–72, ciclica, corpo intero: fermo con la spada abbassata di lato
  (mano destra lungo il fianco, lama in avanti verso il basso).
- **`Sword_Arms_Walk`** 0–30 e **`Sword_Arms_Run`** 0–24, cicliche, per il layer
  `Weapon_Overlay`. Sono clip a corpo intero: gambe, bacino, busto, testa e braccio
  sinistro identici a `Walk`/`Run` (entro 1 cm), braccio destro e spada dalla posa di
  `Sword_Idle` con una piccola oscillazione. Stessa durata e stessa fase delle gambe:
  vanno sincronizzate a `Walk`/`Run` (stesso tempo normalizzato), altrimenti la torsione
  del busto non coincide con i passi.

**Serve da te:**
1. Import come `Walk` (Loop Time on, Bake Into Pose, compressione Off).
2. Con la spada in mano e senza lock: `Sword_Idle` da fermo; sopra `Walk`/`Run` il layer
   `Weapon_Overlay` con `Sword_Arms_Walk`/`Sword_Arms_Run`, stesso blend e stessa velocità
   di riproduzione delle gambe (anche dopo la modifica della voce 15:15).
3. Scrivimi quando è giocabile.

**File toccati:** `Rabbi_anim.fbx`, `Rabbi_anim_events.json`,
`Tools/Blender/create_sword_arms.py` (nuovo), `Docs/Combat/HANDOFF.md`.

## 2026-10-14 15:15 · animazione → codice

**Prova in gioco dell'utente:** colpi, parata e salto vanno bene. **Camminata e corsa
troppo lente**, **`Dodge_F` troppo veloce**, attacchi forse un po' veloci.

**Fatto (solo dati, niente codice):** `speed` nei MoveData, così l'utente prova subito:
- `Dodge_F` **0.8** (24 → 30 frame, stessa distanza 2.5 m);
- `Sword_Light_1`, `_2`, `_3` **0.85**.

Valori di partenza, l'utente li ritocca dall'Inspector. Committali tu insieme al resto.

**Serve da te:**
1. **Velocità di locomozione regolabili senza far scivolare i piedi.** Oggi il blend
   `Walk`/`Run` ha le soglie sulle velocità native (1.2 e 5.0) e la riproduzione resta a ×1
   fra le due. Se l'utente alza `walkSpeed` a 1.5, finisce dentro il blend (92% `Walk` a
   ×1) e i piedi scivolano. Serve che:
   - la camminata a `walkSpeed` suoni `Walk` pura a velocità `walkSpeed ÷ 1.2`;
   - la corsa a `sprintSpeed` suoni `Run` pura a `sprintSpeed ÷ 5.0`;
   - in mezzo, blend con la velocità di riproduzione interpolata fra le due;
   - lo stesso per gli strafe: `lockMoveSpeed ÷ 0.8`.
   In pratica le soglie del blend sulle velocità *di gioco*, non su quelle native.
2. Poi metti sul prefab **`walkSpeed` 1.5** e **`sprintSpeed` 6.0** come primo tentativo
   (clip a ×1.25 e ×1.2). L'utente li regola dall'Inspector finché non gli piacciono.
3. Scrivimi quando è pronto e quali campi toccare, così lo dico all'utente.

**File toccati:** `Assets/ScriptableObjects/Moves/Dodge_F.asset`, `Sword_Light_1/2/3.asset`
(campo `speed`), `Docs/Combat/HANDOFF.md`.

## 2026-10-14 14:51 · codice → animazione

_(Orario corretto: era scritto 15:30, ma la voce è del commit `654828f`, 14:51.)_

**Fatto** (voci 14:40 e 14:46).
1. **Script Blender committati così com'erano sul disco**, commit `d5ef6b8`. Nel reflog non
   c'è nessun checkout, stash o reset mio dopo `6bec077`: solo commit. L'unico stash del
   repo è di GitHub Desktop ed è di luglio. Non so cosa li abbia riportati indietro, ma
   d'ora in poi non tocco `Tools/Blender/` e `Docs/Combat/` con operazioni git che
   riscrivono file.
2. **Salto in tre parti giocabile**, commit `068f810`, per tutte le armi:
   - `Jump_Start` parte al tasto (fusione d'ingresso 0.05 s). La spinta verso l'alto
     arriva al marker `liftoff`, letto dall'importer come i tempi delle mosse (nuovo
     MoveData `Jump_Start`, tipo `Locomotion`: solo tempi, la clip la suona l'Animator).
     A fine clip si passa a `Jump_Air` senza fusione: il frame 10 è il frame 0 di Air.
   - `Jump_Air` resta in loop finché si è in aria.
   - `Jump_Land` parte al contatto. Fino al `cancel` (MoveData `Jump_Land`) si resta
     fermi e non si attacca, non si para e non si schiva; un comando dato in quel tempo
     resta in coda e parte al cancel. Dal cancel il movimento interrompe la clip,
     altrimenti finisce da sola. Si può anche risaltare.
   - In aria non si attacca, non si para e non si schiva. Il movimento orizzontale resta
     quello di terra: chi salta correndo continua a 5 m/s.
   - Anche **cadere senza saltare** passa da `Jump_Air` e atterra con `Jump_Land`, ma solo
     dopo 0.15 s senza appoggio, così scalini e pendenze non interrompono la corsa.
   - Altezza invariata (`jumpHeight` 1.5 m, gravità −20), del sistema vecchio.

**Misure** (Play Mode, tempo accumulato, passo fisso 1/60):

| | misura | calcolo |
|---|---|---|
| spinta dopo la pressione | 0.117 s | 6/60 = 0.100 (+1 frame di gioco) |
| apice | 1.44 m | 1.5 |
| tempo in aria | 0.77 s | 2·√(2·1.5/20) = 0.77 |
| dal contatto al cancel | 0.200 s | 12/60 = 0.200 |
| salto in corsa, distanza in volo | 3.67 m | 5 × 0.75 = 3.75 |
| attacco premuto in aria | non parte | |
| attacco premuto 0.1 s prima del cancel | parte al cancel | |

**Da provare in gioco (utente):**
- **Reattività in corsa** (tua domanda 2): la spinta arriva a ~0.12 s dalla pressione. In
  corsa sono 0.6 m percorsi ancora a terra. Se sembra in ritardo, anticipare `liftoff`
  basta: l'importer lo aggiorna da solo.
- **Atterraggio dalla corsa**: col blocco fino al cancel, chi atterra a 5 m/s si ferma
  di colpo per 0.2 s. È quello che chiede la voce 15:50, ma da fermo a fermo e in corsa
  si sente diverso. Se stona, si può lasciar scorrere la velocità orizzontale durante
  il blocco, o accorciare il cancel per la corsa.
- Il volo dura 0.77 s; `Jump_Air` ne copre 0.67 per ciclo, quindi si vede ripartire il
  loop per un istante prima dell'atterraggio.

**Serve da te:** niente.

**File toccati:** `Docs/Combat/HANDOFF.md`; codice, controller, prefab e MoveData nel commit
`068f810`.

## 2026-10-14 14:46 · animazione → codice

**Fatto:** salto in tre parti esportato in `Rabbi_anim.fbx` (14:44), comune a tutte le armi,
tutto **sul posto** (Bake Into Pose come `Walk`, compressione Off). Verificato reimportando:
- **`Jump_Start`** 0–10, **`liftoff` al 6** (gambe in piena spinta, talloni su), non ciclica.
  Il frame 10 coincide con il frame 0 di `Jump_Air` (scarto 0).
- **`Jump_Air`** 0–40, **ciclica** (Loop Time on), movimento piccolo: regge da pochi frame
  a cadute lunghe.
- **`Jump_Land`** 0–24, **`cancel` al 12**, non ciclica. Frame 0 già a terra (piedi in
  posa di riposo, bacino −4 cm), ammortizza al 4, piedi fermi per tutta la clip. Il frame 24
  coincide con il frame 0 di `Jump_Start` (posa neutra in piedi).

**Serve da te:**
1. Collegarle come da voce 15:50 del 13: `Jump_Start` come mossa con spinta al marker
   `liftoff`; `Jump_Air` in loop finché `isGrounded` è falso; `Jump_Land` quando torna vero,
   interrompibile da movimento e attacchi dal `cancel` in poi.
2. Se il giocatore salta mentre corre, prova se `Jump_Start` (10 frame, spinta a 0.1 s) è
   abbastanza reattivo; se serve, la spinta si può anticipare spostando `liftoff`, dimmelo.
3. Dimmi com'è in gioco: l'utente lo prova.

**File toccati:** `Rabbi_anim.fbx`, `Rabbi_anim_events.json`, `Docs/Combat/HANDOFF.md`.

## 2026-10-14 14:40 · animazione → codice

**Attenzione agli script Blender in git.** Alle 14:36 l'export dei marker ha rifiutato
`liftoff`: in `Tools/Blender/` c'erano versioni vecchie di `export_move_markers.py`,
`create_run.py`, `create_walk.py`, `setup_leg_ik.py` e `setup_reference_scene.py`, uguali a
quelle di HEAD (`6bec077`). Le mie versioni nuove sul disco erano state sovrascritte, credo
da un checkout/stash/reset fatto da te. Le ho rimesse adesso.

**Serve da te:**
1. Committa `Tools/Blender/` **così com'è sul disco** (5 file modificati).
2. D'ora in poi non ripristinare né fare checkout di `Tools/Blender/` e `Docs/Combat/`:
   li scrive anche la sessione animazione, senza git. Se ti servono stash o reset, escludi
   quelle cartelle oppure committale prima.

**In arrivo:** `Jump_Start` (marker `liftoff` al 6), `Jump_Air` (ciclica, 40 frame),
`Jump_Land` (`cancel` al 12, 24 frame), tutte sul posto.

**File toccati:** `Tools/Blender/*.py` (ripristinati), `Docs/Combat/HANDOFF.md`.

## 2026-10-14 13:15 · codice → animazione

**Fatto** (voci 10:15–12:50), commit `679f820`. **`Walk` e `Run` sono giocabili.**
1. **Velocità sul prefab**: `walkSpeed` **1.2**, `sprintSpeed` **5.0**, nuovo `lockMoveSpeed`
   **0.8**, che è il tetto da agganciati, separato dalla camminata.
2. **Locomozione libera** (a mani nude e con l'arma): blend su `Speed` in m/s veri, **`Walk`
   a 1.2 e `Run` a 5.0**, velocità dello stato 1 (era 2). Con l'arma, per ora, le stesse clip
   intere: le braccia oscillano finché non arrivano `<Set>_Arms_Walk/Run`.
3. **Velocità di riproduzione = velocità reale ÷ velocità nativa**: sotto 1.2 rallenta (fino
   al minimo 0.35), fra 1.2 e 5.0 resta 1, perché le soglie del blend coincidono con le
   velocità native e il blend combacia da solo; sopra 5.0 accelera, **senza tetto**. Se
   l'utente alza `sprintSpeed`, la corsa accelera in proporzione.
4. **Locomozione agganciata**: parametro proprio (`GuardSpeedMultiplier`, velocità nativa
   `guardClipSpeed`). Oggi gira ancora sulle clip vecchie a 4.8, quindi a 0.8 m/s sono al
   minimo di 0.35 e **i piedi scivolano**. Si sistema con gli strafe: `guardClipSpeed` 0.8 e
   un blend 2D sulle quattro direzioni.

**Misure** (Play Mode, tempo accumulato):

| Caso | Velocità | Riproduzione |
|---|---|---|
| tastiera, camminata | 1.22 m/s | ×1.00 |
| tastiera, sprint | 5.08 m/s | ×1.00 |
| stick a metà (0.5) | 2.54 m/s | ×1.00, blend camminata/corsa |
| stick a 0.2 | 1.02 m/s | ×0.83 |
| agganciato, tastiera o stick pieno | 0.81 m/s | guardia ×0.35 (clip vecchie) |

**Come si passa oggi da camminata a corsa** (domanda 3 delle 12:50):
- **Tastiera**: WASD è sempre camminata (1.2). La corsa è **Shift sinistro tenuto** (sprint,
  5.0). Non c'è niente in mezzo: il `Speed` dell'Animator passa da 1.2 a 5.0 con uno
  smorzamento di circa 0.1 s, e il blend attraversa la zona intermedia in quel tempo.
- **Pad**: la velocità è **inclinazione dello stick × 5.0** senza lock (stick pieno = corsa),
  × 0.8 col lock. **L3 tenuto** forza la corsa. Ogni inclinazione intermedia dà una velocità
  intermedia: a metà stick 2.5 m/s, cioè il blend 66% `Walk` + 34% `Run`.
- Lo sprint **sgancia il lock** (come oggi).
- **Da provare in gioco**: con 1.2 e 5.0 così lontani, le andature intermedie col pad sono un
  impasto di due passi diversi (4 passi/s e 2.5 passi/s). Se non regge, posso mappare lo
  stick a due sole andature (sotto una soglia camminata, sopra corsa) invece che in continuo.

**IA e lock col player più lento** (domanda 4):
- **Lock**: aggancio entro 20 m, sgancio oltre 30 m. Sono distanze, quindi non cambiano.
- **Simp**: corre a **4.2** in avvicinamento e cammina a **1.8** in combattimento (sotto i 5 m,
  `combatRange`). Quindi:
  - **camminando (1.2) o agganciato (0.8) il player è più lento del simp in combattimento
    (1.8)**: indietreggiando non si sfugge, chiude sempre la distanza. Per staccarsi
    restano la schivata (2.5 m) e lo sprint, che però sgancia il lock;
  - **in sprint (5.0) il player è più veloce del simp in corsa (4.2)**: si stacca di 0.8 m/s,
    per arrivare dai 5 m del combattimento ai 10 m di `abandonRange` servono circa 6 s.
- Lo spacing del simp (`spacingDistance` 1.3, ritirata 0.35 s) e i suoi tempi di reazione non
  dipendono dalla velocità del player. Cambia invece il bilancio: prima il player
  agganciato (2.8) era più veloce del simp (1.8), ora è più lento. **Da provare in gioco**:
  se il simp diventa troppo appiccicoso, si abbassa il suo 1.8.
- L'avanzamento sotto lock durante i colpi (1.2 m/s finché il bersaglio è oltre 0.9 m) resta
  uguale, e vale anche per i colpi nuovi senza lunge.

**Ancora da fare** (voce 15:50 del 13): layer delle braccia per arma sulla locomozione
libera, salto in tre parti, guardia e strafe per arma col lock (`Sword_Guard_Idle` è ancora
solo in anteprima). Li preparo così le clip si provano appena arrivano.

**Serve da te:** niente. Il giudizio sulle andature lo dà l'utente.

**File toccati:** `Docs/Combat/HANDOFF.md`; codice, controller e prefab nel commit `679f820`;
gli script Blender `create_run/walk/strafe.py` (tuoi) nel commit docs.

## 2026-10-14 12:50 · animazione → codice

**Decisione dell'utente: la camminata scende a `walkSpeed` 1.2 m/s** (sostituisce i 2.4 delle
voci 10:20 e 10:35). Con le gambe del Rabbi (45 cm) il passo di una camminata vera arriva a
30 cm: a 2.4 m/s sarebbero serviti ~7 passi al secondo. Una corsetta a 2.4 m/s è stata
provata e scartata. La clip `Walk` (**esportata alle 12:50**, take `Armature|Walk`, nel JSON 0–30 `cyclic: true`, loop chiuso, sul posto; import come `Run`: Loop Time on, Bake Into Pose per rotazione e posizione, compressione Off) è di **30 frame**, 4 passi al secondo,
sempre almeno un piede a terra, sul posto.

**Serve da te:**
1. `walkSpeed` = 1.2 sul prefab. **Col lock la velocità scende a 0.8 m/s** (decisione
   dell'utente, 12:55): gli strafe `Sword_Guard_Walk_F/B/L/R` sono a passo accostato,
   ciclo di 24 frame, velocità nativa 0.8 m/s in tutte e quattro le direzioni, sul posto,
   **esportati alle 12:59** (take `Armature|Sword_Guard_Walk_F/B/L/R`, nel JSON 0–24
   `cyclic: true`, loop chiusi; import come `Walk`). Approvati dall'utente in Blender. Serve un parametro separato (es. `lockMoveSpeed` 0.8) e la velocità di
   riproduzione degli strafe calcolata su 0.8, come per `Walk` e `Run`. Il blend fra le
   quattro direzioni è il tuo (2D sulla direzione dell'input rispetto al bersaglio).
2. Velocità nativa di `Walk` **1.2 m/s** nella compensazione della velocità di riproduzione
   (voce 10:35), senza tetto.
3. Con la camminata a 1.2 e la corsa a 5.0 c'è un salto grande: se la locomozione libera
   sfuma fra le due in base all'input analogico, la soglia/blend va ricalcolata su questi
   valori. Dimmi come funziona oggi il passaggio camminata → corsa (input analogico o tasto).
4. Controlla l'IA e il lock (distanze di aggancio, inseguimento) con il player più lento a
   camminare, e segnala cosa cambia.

**File toccati:** `Rabbi_anim.fbx`, `Rabbi_anim_events.json`, `Docs/Combat/HANDOFF.md`, `MOVES.md`,
`Tools/Blender/create_walk.py` e `create_strafe.py` (nuovi).

## 2026-10-14 11:30 · animazione → codice

**Fatto:** `Run` esportata in `Rabbi_anim.fbx` (take `Armature|Run`), ciclo di **24 frame**
a 60 fps, loop chiuso (posa del frame 24 = frame 0, scarto 0 mm), **sul posto**: il piede a
terra scorre all'indietro a circa 5 m/s, il bacino non avanza. Nel JSON: `Run`, 0–24,
`cyclic: true`, nessun marker. Clip condivisa da tutte le armi.

**Serve da te:**
1. Import: **Loop Time** on (Loop Pose off: la chiusura è già nella clip), Root Transform
   Rotation e Position Y/XZ **Bake Into Pose** (Based Upon: Original), compressione Off.
2. Velocità nativa della clip **5.0 m/s**: velocità di riproduzione = velocità reale ÷ 5.0,
   senza tetto (voce 10:35). `sprintSpeed` 5.0.
3. Nella locomozione libera con la spada, `Run` va sotto il layer braccia per arma, quando
   c'è; finché manca `Sword_Arms_Run` va bene la clip intera (braccia che oscillano).
4. Scrivimi qui quando è giocabile: l'utente la prova in gioco. Dalla clip il ciclo appare
   un po' rigido ("clunky"), lo rifiniamo dopo averla vista in movimento.

**File toccati:** `Assets/Art/Rabbi/Animations/Rabbi_anim.fbx`, `Rabbi_anim_events.json`,
`Tools/Blender/create_run.py` (nuovo: genera la `Run` di base), `Docs/Combat/HANDOFF.md`.

## 2026-10-14 10:35 · animazione → codice

**Correzione alla voce 10:20, decisione dell'utente:** la corsa scende a **`sprintSpeed`
5.0 m/s** (non 6.2). Con le gambe del Rabbi (~50 cm), 6.2 m/s avrebbe chiesto un ciclo di
18 frame, cioè 6–7 passi al secondo: troppo. La clip `Run` sarà di **24 frame** (0.40 s),
2.0 m per ciclo, passo 1.0 m. La camminata resta 2.4 m/s per ora (si rivede quando tocca a
`Walk`).

**Serve da te**, oltre alla voce 10:20:
1. `sprintSpeed` = 5.0 sul prefab.
2. Velocità di riproduzione della locomozione = velocità reale ÷ velocità nativa della clip
   (5.0 per `Run`, 2.4 per `Walk`), **anche sopra 1**, senza tetto. Così, se l'utente in gioco
   trova la corsa lenta e alza `sprintSpeed`, la clip accelera in proporzione e i piedi non
   scivolano. Oggi `SpeedMultiplier` vale solo sotto la camminata.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`.

## 2026-10-14 10:20 · animazione → codice

**Decisione dell'utente sulle velocità di locomozione** (sostituisce la domanda della voce
10:15): una via di mezzo fra quelle attuali e una versione più lenta.
- **Camminata `walkSpeed` 2.4 m/s** (era 2.8). Vale anche come tetto col lock.
- **Corsa `sprintSpeed` 6.2 m/s** (era 7.0).

Le clip nuove saranno animate esattamente su queste velocità:
- `Run`: ciclo di **18 frame** (0.30 s), 1.86 m per ciclo, passo 0.93 m;
- `Walk` e `<Set>_Guard_Walk_F/B/L/R`: ciclo di **26 frame** (0.433 s), 1.04 m per ciclo,
  passo 0.52 m. Le quattro direzioni con la stessa durata.

**Serve da te:**
1. Imposta le due velocità sul prefab del player.
2. Nella locomozione nuova (controller ridotto o blend tree) le soglie e la compensazione
   della velocità di riproduzione (oggi `walkClipSpeed` / `SpeedMultiplier`) devono usare
   **2.4 e 6.2**, che sono le velocità reali delle clip nuove, non più 4.8 e 9.6.
3. Controlla se qualcosa nell'IA dei simp o nel lock dipende dalle velocità vecchie
   (distanze di aggancio, inseguimento) e segnalalo.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`.

## 2026-10-14 10:15 · animazione → codice

**Fatto:** l'utente ha provato la fetta verticale. **Colpi, catena e parata approvati**
(`chiusa` in `MOVES.md`). `Dodge_F` forse troppo veloce: resta in prova, si rivede più avanti.
Prossimo: locomozione (corsa per prima), poi strafe e salto.

**Serve da te:** quando hai pronto il layer delle braccia sulla locomozione libera e il
salto in tre parti (voce 15:50 del 13), scrivilo qui. Intanto confermami le velocità che
la locomozione deve rispettare: dal prefab leggo `walkSpeed` 2.8 e `sprintSpeed` 7.0 m/s,
con il lock che limita alla camminata. Sono quelle definitive? Lo stride delle clip si
ricava da lì.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`.

## 2026-10-14 10:10 · codice → animazione

**Fatto** (voce 09:58):
1. **`Dodge_F` importata** con XZ libero (root motion), senza loop, e **collegata come
   schivata in avanti per spada, coltello e pugni** al posto di `Dash_Front`, con root
   motion **senza scala**. Tempi dall'importer: invulnerabilità 3–14, cancel 18, fine 24.
   Le altre direzioni restano quelle vecchie (con il loro ×4).
2. **Misure**:
   - nella clip in Unity: spostamento finale **2.500 m**, massimo 2.588 m al frame 18,
     quindi **rinculo di 8.8 cm** nei frame 18–24;
   - in gioco (tempo accumulato, 60 Hz): **2.518 m** in avanti, 0.6 cm di lato;
     **invulnerabile dal frame 3 al 13** (11 frame, come 3–14 con fine esclusa). Il rinculo
     in gioco è **4.6 cm**, ridotto dal crossfade d'uscita: c'è, ma è più piccolo;
   - a ×0.7: stessa distanza (2.523 m) in 35 frame invece di 24, invulnerabilità di 15 frame.
3. Avvisi d'import di scala su gambe e piedi di `Dodge_F` (e già di `Sword_Light_1` e
   `_3`): è il rumore del bake dell'IK visto il 13, da ignorare.

**Serve da te:** niente. Ora la prova l'utente.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; asset `Dodge_F` e moveset.

## 2026-10-14 09:58 · animazione → codice

**Fatto:** letta la voce delle 09:50 (tempi in frame della clip e moltiplicatore: perfetto).
Esportata **`Dodge_F`** (FBX delle 09:50, sei take; JSON delle 09:50: `iframe_start` 3,
`iframe_end` 14, `cancel` 18, fine 24). Verificato reimportando:
- **root motion nella clip**: il bacino avanza di **2.50 m esatti** in −Y di Blender (il
  davanti del Rabbi), nessuno spostamento laterale o verticale netto;
- frame 0 = guardia; frame 24 = guardia traslata di 2.5 m (scarto 0.0 cm, 0.0°);
- due passi, bacino fino a 15–16 m/s ai frame 4–5 e 11–12; invulnerabilità 3–14 copre
  quasi tutto lo spostamento (2.35 m su 2.5);
- nessuna scala ≠ 1. Comune a tutte le armi: braccia raccolte.
- Da guardare: il bacino arriva a 2.60 m al frame 18 e **torna indietro di 10 cm** fino al
  24; nello stesso tratto i piedi strisciano a terra (il destro di 33 cm) per tornare
  nella posizione della guardia. L'ho segnalato all'utente.

**Serve da te:**
1. Import come da `PIPELINE.md`: **XZ non in bake into pose** (root motion), loop spento.
2. Collegala come schivata in avanti per tutte le armi, al posto dello scatto vecchio
   (`Dash_Front`), con root motion **senza scala** (la distanza è già nella clip; il
   vecchio ×4 di `dashRootMotionScale` non va applicato). Le altre direzioni restano
   quelle vecchie finché non arrivano `Dodge_B/L/R`.
2. Misura in gioco: distanza percorsa (attesa 2.5 m), invulnerabilità 3–14, e se a fine
   schivata si vede il rinculo di 10 cm. Ricorda che con `speed` < 1 la distanza resta la
   stessa ma ci mette di più.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; export dell'utente.

## 2026-10-14 09:50 · codice → animazione

**Fatto** (voce 09:45), commit `8b590e4`:
1. **I tempi seguono `speed`, anche a mano dopo l'import.** Avevi ragione: l'importer
   divideva i marker per `speed` al momento dell'import, quindi cambiando `speed` dopo le
   finestre restavano al ritmo vecchio. Ora **tutti i tempi del `MoveData` sono frame della
   clip** (i frame di Blender) e l'`ActionPlayer` li confronta col tempo della clip:
   finestre attive, `combo`, `cancel`, parata, invulnerabilità e fine si spostano insieme
   alla lama a qualunque velocità. L'importer scrive i marker così come sono. Il lunge
   seguiva già (usa il tempo normalizzato). `liftoff` seguirà la stessa regola quando ci
   sarà il salto. Unica eccezione voluta: il **recupero a vuoto** della parata resta in
   tempo di gioco, perché è una penalità e non un punto della clip.
   I `Legacy_*` (tempi in frame di gioco) sono stati convertiti una volta: stesse durate in
   gioco di prima.
2. **Per provare**:
   - **moltiplicatore globale** sull'`ActionPlayer` del player (Inspector, *Speed
     Multiplier*), regolabile in Play Mode;
   - menu **`KosherFury/Combat/Velocità mosse/1.0 · 0.85 · 0.7`**;
   - il valore scelto si fissa poi nello `speed` della singola mossa.
   **Collaudo** (Play Mode, tempo di gioco accumulato): `Sword_Light_1` a ×1.0 / ×0.85 / ×0.7
   → Active ai frame 18.0 / 21.2 / 25.7, Recovery a 24.0 / 28.2 / 34.5, fine a 54.0 / 63.8 /
   77.3 (attesi 18 / 21.2 / 25.7, 24 / 28.2 / 34.3, 54 / 63.5 / 77.1). Il pugno vecchio ha
   gli stessi tempi di prima (24.5 / 33.2 / 60).
3. **Durate delle mosse nuove** (calcolo dai marker; tempo di gioco):

   | Mossa | Frame | ×1.0 | ×0.85 | ×0.7 |
   |---|---|---|---|---|
   | `Sword_Light_1` totale | 54 | 0.900 s | 1.059 s | 1.286 s |
   | hit_start | 18 | 0.300 s | 0.353 s | 0.429 s |
   | hit_end | 24 | 0.400 s | 0.471 s | 0.571 s |
   | combo | 28 | 0.467 s | 0.549 s | 0.667 s |
   | cancel | 34 | 0.567 s | 0.667 s | 0.810 s |
   | `Sword_Light_2` totale | 50 | 0.833 s | 0.980 s | 1.190 s |
   | hit_start | 14 | 0.233 s | 0.275 s | 0.333 s |
   | hit_end | 20 | 0.333 s | 0.392 s | 0.476 s |
   | combo | 26 | 0.433 s | 0.510 s | 0.619 s |
   | cancel | 30 | 0.500 s | 0.588 s | 0.714 s |
   | `Sword_Light_3` totale | 60 | 1.000 s | 1.176 s | 1.429 s |
   | hit_start | 18 | 0.300 s | 0.353 s | 0.429 s |
   | hit_end | 24 | 0.400 s | 0.471 s | 0.571 s |
   | cancel | 36 | 0.600 s | 0.706 s | 0.857 s |
   | `Sword_Parry` totale | 30 | 0.500 s | 0.588 s | 0.714 s |
   | parry_start | 2 | 0.033 s | 0.039 s | 0.048 s |
   | parry_end | 14 | 0.233 s | 0.275 s | 0.333 s |

   Per confronto, i colpi di oggi durano 1.0–1.1 s con startup 0.33–0.54 s (voce 11:23 del
   12 ottobre). Nota: rallentando la parata si allarga anche la sua finestra (0.20 s a ×1.0,
   0.29 s a ×0.7).

**Serve da te:** niente. Il ritmo lo sceglie l'utente in gioco.

**File toccati:** `Docs/Combat/HANDOFF.md`; codice e asset nel commit `8b590e4`.

## 2026-10-14 09:45 · animazione → codice

**Fatto:** letta la voce delle 16:53 (parata collegata, `Parry Trainer`: ottimo).

**L'utente trova le animazioni nuove troppo veloci** e vuole provare a **rallentarle da
Unity** prima di rifarle. Serve per trovare il ritmo giusto mossa per mossa; se la durata
trovata funziona ma lo scatto si ammorbidisce, la rifaremo in Blender allungando solo le
parti lente.

**Serve da te:**
1. **Verifica che `MoveData.speed` sposti anche le finestre.** Il commento in `MoveData`
   dice che i tempi sono "frame a 60 Hz di tempo di GIOCO". L'importer però scrive i frame
   della **clip** (i marker di Blender). Con `speed` 0.8 il marker `hit_start` 18 cade al
   frame di gioco 22.5: finestre attive, `combo`, `cancel`, parata, invulnerabilità,
   `liftoff`, lunge e fine mossa devono scalare insieme alla clip, altrimenti la hitbox si
   apre prima della lama. Scegli tu dove farlo (importer che scrive frame / speed, o
   conversione a runtime), ma deve restare vero anche se l'utente cambia `speed` a mano
   dopo l'import.
2. **Un modo rapido per provare**: un moltiplicatore globale di velocità delle mosse del
   player regolabile in Play Mode (per esempio un campo nell'Inspector di `CombatActor` o
   un menu `KosherFury/Combat/...`), più il campo `speed` per mossa per fissare il valore
   scelto. Valori da provare: 1.0, 0.85, 0.7.
3. Dopo, riporta per ogni mossa nuova la durata in secondi a 1.0 / 0.85 / 0.7, così la
   specifica delle prossime mosse parte dal ritmo scelto dall'utente.

**File toccati:** `Docs/Combat/HANDOFF.md`.

## 2026-10-13 16:53 · codice → animazione

**Fatto** (voci 16:20 e 16:25):
1. **`Sword_Parry` è la parata della spada** (commit `0eecfb5`), al posto di quella
   vecchia. Tempi dall'importer: finestra **2–14**, fine 30. Recupero a vuoto **20 frame**
   come da `MOVES.md`. **Niente cancel** (non c'è il marker), quindi la parata impegna; dopo
   una parata riuscita si può subito attaccare (riposta: la catena leggera, finché non
   c'è `Sword_Riposte`).
2. **Prova contro colpi veri** (commit `2b4ab15`): menu **`KosherFury/Combat/Parry Trainer
   (Play Mode)`**. Ferma le ondate, spegne gli altri nemici e mette un simp davanti al
   player, con il comportamento spento. Il simp si riporta a 0.9 m prima di ogni colpo
   (la spinta dei colpi presi allontana il player) e attacca ogni 2 s con i suoi colpi.
   **Ogni colpo scrive nel log** `[ParryTrainer] colpo PARATO / preso / a vuoto`.
   Primo collaudo:
   - con la parata comandata 0.1 s prima che il colpo del simp apra la hitbox: **4 parate
     su 4**, il simp resta stordito ogni volta;
   - parata a vuoto: il primo comando passa **19.5 frame** dopo la fine della mossa (20
     attesi, risoluzione del passo di prova 0.5 frame);
   - a 1.0 m alcuni colpi del simp **non arrivano** (calcio sinistro 0.99 m, e il calcio
     destro che non registra mai, vedi CLAUDE.md); per questo l'allenatore sta a 0.9 m.
3. **Torsione dell'avambraccio**: presa nota della decisione (problema noto, si riprende se
   diventa visibile). La segnalo se una misura futura supera di molto 2.6.

**Serve da te:** niente. Per giudicare la finestra in gioco: Play Mode, menu sopra,
spada in mano (AbramKnife), parata con L1 / Q.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; commit `2b4ab15`, `0eecfb5`;
`Tools/Blender/check_arm_twist.py` (della sessione animazione) messo nel commit docs.

## 2026-10-13 16:25 · animazione → codice

**Decisione dell'utente sulla torsione dell'avambraccio (voce 16:05):** ha provato i tre
fendenti in gioco e **non vede scatti**. Non si corregge per ora: resta come problema noto,
da riprendere solo se diventa visibile (colpi più lenti, altre clip). Il ritocco in Blender
proposto alle 16:20 è sospeso; `check_arm_twist.py` resta in `Tools/Blender/` per allora.

**Serve da te:** niente di nuovo. Se in qualche misura futura la torsione supera di molto i
valori di oggi (2.6 su `Sword_Light_3`), segnalalo.

**File toccati:** `Docs/Combat/HANDOFF.md`.

## 2026-10-13 16:20 · animazione → codice

**Fatto:**
- Letta la voce delle 16:05. Sulla **torsione dell'avambraccio** concordo: si corregge in
  Blender. Ho aggiunto `Tools/Blender/check_arm_twist.py`, che per ogni action con Fake
  User segnala i frame in cui braccio o avambraccio ruotano oltre 80° sul proprio asse
  rispetto alla rest pose. L'utente lo usa per trovare i frame e ritoccarli: meno rotazione
  sull'avambraccio, compensata da spalla e polso. La misura di Blender (rispetto alla
  A-pose) non coincide al grado con i muscoli di Unity: dopo la correzione serve la tua
  rimisura.
- Esportata **`Sword_Parry`** (FBX delle 16:10): 30 frame, `parry_start` 2, `parry_end` 14.
  Verificato reimportando: raccordi con la guardia a zero (inizio e fine), piedi fermi,
  nessuna scala; la spada sale in 2 frame (11 m/s). Durante la finestra però la mano
  destra **scende** da 0.92 a 0.66 m, sotto la guardia (0.72): l'ho segnalato all'utente.
- Confermato anche il raccordo `Sword_Light_2` (26) → `Sword_Light_3` (0): 0.0 cm.

**Serve da te:**
1. Collega `Sword_Parry` al moveset della spada come parata (finestra dal JSON, recupero a
   vuoto 20 frame come da `MOVES.md`), al posto di quella vecchia.
2. Fammi sapere se riesci a dare all'utente un modo per provare la parata **contro un
   colpo vero**: un simp che attacca, o un bersaglio che colpisce a intervalli.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; `Tools/Blender/check_arm_twist.py`;
export dell'utente.

## 2026-10-13 16:05 · codice → animazione

**Fatto** (voce 15:45):
1. **`Sword_Light_3` in catena**: `Sword_Light_1` → `_2` → **`_3`**. Tempi dall'importer:
   finestra 18–24, nessun combo, cancel 36, fine 60. Impatto come da `MOVES.md` (hit stop
   0.10, shake 0.45, knockback 2.0, Heavy), danno ×2.5. **`combo` di `Sword_Light_2` a 26**:
   l'importer l'ha già preso dal JSON delle 15:43.
   Visto che `Sword_Light_3` non ha `combo`, **la catena si ferma lì**: il quarto fendente
   vecchio è ancora collegato ma non si raggiunge. Se la catena diventa di 4 colpi, basta
   un `combo` su `_3`.
2. **Misure** (bersaglio con la capsula del simp):
   - **portata 1.463 m**;
   - contatto ai frame 19–21, con il centro della hitbox a 0.73–1.09 m da terra (scende
     col bersaglio più lontano);
   - punta: frame 20 a 1.03 m dal centro e 1.19 m d'altezza, poi **scende avanti**: 1.11 m
     al 21, **1.15 m al 22**, 1.13 m al 24, fino a 0.51 m d'altezza. Supera di 11–15 cm
     l'anello di 1.00 m che chiedeva la specifica, ma la portata resta come quella degli
     altri due.
3. **Raccordo 2 → 3**: nei dati delle clip `_2` al frame 26 e `_3` al frame 0 coincidono
   (0.0°, 0.0 cm su tutte le ossa, dita comprese), e anche 1 → 2 e la fine di `_3` sulla
   guardia. **In gioco però c'era uno scatto di 100–120° sull'avambraccio**: era un difetto
   dell'`ActionPlayer` (una clip collegata a un ingresso del mixer già usato usciva con la
   posa sbagliata per due frame), **corretto** (commit `42a9af7`). Dopo: 4–9° al cambio.

**Da guardare in Blender: torsione dell'avambraccio destro fuori dai limiti dell'avatar.**
Il rig Humanoid rappresenta ogni rotazione come un muscolo fra −1 e 1, dove 1 è il limite
di quell'articolazione. Misura sulle curve importate:

| Clip | Muscolo | Valore | Dove |
|---|---|---|---|
| `Sword_Light_3` | Right Forearm Twist | **fino a 2.61** | frame 16; passa da 2.46 a 1.69 fra i frame 17 e 18 |
| `Sword_Light_1` | Right Forearm Twist | 1.84 | frame 18 |
| `Sword_Light_2` | Right Arm Twist | −1.25 | frame 15 |

Oltre 1 Unity estrapola, e il risultato è instabile: **al frame 18 di `Sword_Light_3`
(l'ingresso) l'avambraccio ruota di 165° in un solo frame**, mentre braccio e mano restano
sui 50°. In gioco si vede come uno scatto dell'avambraccio proprio sul colpo. Due strade:
- **in Blender**: tenere la rotazione del polso sul solo osso della mano, o comunque meno
  torsione sull'avambraccio (sul rig Mixamo la torsione sta tutta su `RightForeArm`);
- **in Unity**: allargare il limite del muscolo nell'avatar di `Rabbi_rigged`. Si può fare,
  ma cambia la retarget di tutte le clip, vecchie comprese.
Decide l'utente; io consiglierei la prima.

**Prossimo mio** (voce 15:50): predisporre locomozione comune + layer braccia per arma e il
salto in tre parti, così le clip si provano appena arrivano.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; commit `42a9af7`, `c0b06e7`.

## 2026-10-13 15:50 · animazione → codice

**Fatto:** decisioni dell'utente sulle domande 9 e 10.
- **Locomozione: la tua proposta.** In comune a tutte le armi `Walk`, `Run`, `Jump_Start`,
  `Jump_Air`, `Jump_Land`. Per arma la guardia col lock (`<Set>_Guard_Idle`,
  `<Set>_Guard_Walk_F/B/L/R`) e l'idle libero `<Set>_Idle`. Braccia per arma sopra la
  locomozione libera: `<Set>_Arms_Walk`, `<Set>_Arms_Run`, della stessa durata delle gambe.
- **Salto in tre parti con `liftoff`**: aggiunto ai marker di `PIPELINE.md` e allo script
  Blender (obbligatorio su `Jump_Start`).

**Serve da te:** predisporre nel sistema nuovo locomozione comune + layer braccia per arma
e il salto in tre parti (spinta a `liftoff`, `Jump_Air` finché non è a terra,
`Jump_Land` all'atterraggio), così le clip si possono provare appena arrivano. Nessuna
fretta: prima arrivano `Sword_Parry` e `Dodge_F`.

**File toccati:** `Docs/Combat/HANDOFF.md`, `PIPELINE.md`, `PLAN.md`, `MOVES.md`,
`Tools/Blender/export_move_markers.py`.

## 2026-10-13 15:45 · animazione → codice

**Fatto:** letta la voce delle 15:10 (grazie per le misure e per il frame fermo corretto).
Esportata **`Sword_Light_3`** (finisher verticale), FBX delle 15:37 con quattro take; JSON
delle 15:37: `hit_start` 18, `hit_end` 24, `cancel` 36, fine 60, niente `combo`.
Verificato reimportando:
- fine = `Sword_Guard_Idle` al frame 0 (0.0 cm, 0.0°); nessuna scala ≠ 1;
- velocità media della mano: carica tenuta (8–14) 1.2 m/s, **scatto (14–20) 13.8 m/s con
  picco 20.9 al frame 18**, impatto 3.7, recupero 1.1–1.4;
- **raccordo con `Sword_Light_2`: il frame 0 coincide con il frame 26 di `Sword_Light_2`,
  non con il 24 (`combo`).** Al 24 lo scarto è 7.5 cm / 9° (mani e dita). Ho proposto
  all'utente di spostare `combo` di `Sword_Light_2` da 24 a 26: coincidenza esatta, con un
  solo marker.

**Serve da te:**
1. Metti `Sword_Light_3` in catena al posto del terzo fendente vecchio. Fine catena: il
   quarto vecchio resta finché non decidiamo se la catena della spada è di 3 o 4 colpi.
2. Misura portata e contatto come per gli altri due. Impatto proposto in `MOVES.md`
   (hit stop 0.10, shake 0.45, knockback 2.0, reazione Heavy).
3. Se l'utente sposta il marker, controlla che l'importer porti `combo` di
   `Sword_Light_2` a 26 e che il passaggio 2 → 3 sia pulito.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`; export dell'utente.

## 2026-10-13 15:10 · codice → animazione

**Fatto:**
1. **`Sword_Light_2` in catena**: `Sword_Light_1` → **`Sword_Light_2`** → fendenti vecchi 3 e 4.
   `MoveData` nuovo (`Assets/ScriptableObjects/Moves/Sword_Light_2.asset`): impatto come
   `Sword_Light_1`, danno ×2, niente lunge. I tempi li ha scritti l'importer: finestra
   14–20, combo 24, cancel 30, fine 50. La copia del secondo fendente vecchio è cancellata.
2. **Misure** (Play Mode, bersaglio con la capsula del simp, spada con hitbox sottile):
   - **portata 1.456 m** fra i centri (`Sword_Light_1`: 1.594);
   - contatto a 1.0 / 1.2 / 1.4 m ai frame 14.0 / 16.25 / 17.75, con il centro della
     hitbox a 0.87–0.88 m da terra;
   - punta nella finestra: arco orizzontale a **1.01–1.04 m** dal centro, alto **0.95–0.97
     m**, da sinistra (frame 14, 0.83 m di lato) a destra (frame 20, 0.40 m). È il rovescio
     laterale previsto, e infatti non allunga la portata.
3. **Passaggio 1 → 2 in gioco (catena vera, a 60 Hz): nessuno scatto.** Al cambio la
   rotazione massima delle ossa è 14.7° in un frame, contro i 22° dei frame subito prima
   dentro `Sword_Light_1`, e il crossfade fa il resto.
   Due cose viste:
   - **Un frame fermo al cambio**: la mossa nuova resta al tempo 0 per due frame, cioè
     16 ms di pausa. È un ritardo di un frame che l'`ActionPlayer` aggiunge all'inizio di
     **ogni** mossa: è lato codice, lo correggo io (vedi sotto).
   - **Dita**: ai frame 2–3 di `Sword_Light_2` l'anulare destro (`RightHandRing3`) ruota di
     **48° in un frame**. Forse un cambio di presa troppo secco, da guardare.
4. **Avviso sulla scala del piede (voce 14:20)**: c'è ancora, ma non è una chiave vera.
   Importando l'FBX come Generic, le scale dei piedi sono fra 0.9999955 e 1.000005 in tutte
   e tre le clip: è rumore numerico del bake dell'IK. Unity lo segnala solo per
   `Sword_Light_1` e comunque lo scarta. **Non serve fare niente.**

**Domanda 9 (gambe in comune, braccia per arma): fattibile.** Il controller di oggi lo fa
già: il layer `Weapon_Overlay` ha una maschera su busto e braccia e sovrascrive la posa
delle braccia sopra la locomozione. Le mosse (colpi, parate, schivate) restano a corpo
intero: le suona l'`ActionPlayer`, sopra a tutto.
Proposta su cosa tenere per arma:
- **In comune:** locomozione libera (`Walk`, `Run`) e salto. Le gambe sono uguali.
- **Per arma:** la **guardia** (`<Set>_Guard_Idle` e `<Set>_Guard_Walk_F/B/L/R`). È la
  guardia a definire lo stance, e lo stance passa da bacino e piedi: `Sword_Guard_Idle` ha il
  bacino girato di 30°, che una guardia a mani nude non avrà. Con le gambe in comune, gli
  strafe della spada camminerebbero frontali.
- **Braccia per arma sulla locomozione libera:** pose ferme o cicli della **stessa durata**
  della clip di gambe, così non vanno fuori fase (vale la stessa regola delle 4 direzioni).
- Conto: circa 5 comuni + 5 di guardia per arma + 1–2 pose di braccia per arma, invece di
  circa 3×9.

**Domanda 10 (salto in tre parti): fattibile, e meglio di oggi.** Come funziona adesso
(misura sui file):
- `jumpHeight` **1.5 m**, `gravity` **−20 m/s²** (prefab). Velocità di stacco
  √(2·1.5·20) = **7.75 m/s**, apice dopo **0.39 s**, in aria **0.77 s** su terreno piano
  (calcolo).
- La spinta arriva con l'evento `OnElevating` al tempo 0 della clip `Jump` (v1, speed 2):
  in pratica al primo frame.
- **La clip dura 0.52 s** (esce a 0.9), quindi **finisce 0.25 s prima dell'atterraggio**:
  oggi il personaggio torna alla locomozione mentre è ancora in aria. Non c'è nessun
  riconoscimento dell'atterraggio.
- **Controllo in aria pieno**: in aria ci si muove alla stessa velocità che a terra
  (camminata 2.8, corsa 7.0), anche agganciati. Il buffer del comando è 0.15 s.
- Col sistema nuovo: `Jump_Start` suonata come mossa, spinta al marker **`liftoff`** al
  posto dell'evento; `Jump_Air` ciclica finché `CharacterController.isGrounded` è falso;
  `Jump_Land` quando torna vero. Così la durata in aria la decide la fisica, non la clip.
  Per `PIPELINE.md` serve solo il marker `liftoff`, ma va prima l'ok dell'utente.
- Per le clip: lo stacco deve arrivare a `liftoff` con le gambe già in spinta, e
  `Jump_Air` deve reggere da 0.1 a ~0.8 s di volo (e oltre, per le cadute).

**Serve da te:** guardare le dita a inizio `Sword_Light_2`. Le domande 9 e 10 passano
all'utente.

**Prossimo mio:** correggere il frame fermo all'inizio di ogni mossa dell'`ActionPlayer`.
_Aggiunta 15:20: corretto (commit successivo a `288943d`). La mossa nuova ora avanza
subito, frame 0 → 1 → 2 → 3, anche nel passaggio `Sword_Light_1` → `Sword_Light_2`._

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; asset `Sword_Light_1`/`_2`; export
importato.

## 2026-10-13 15:05 · animazione → codice

**Fatto:** l'utente ricorda che servono **almeno corsa, strafe e salto**. Messi subito dopo
la fetta verticale della spada (ordine in `MOVES.md`, priorità in `PLAN.md`). Specifica di
`Sword_Light_3` rivista: 60 frame, `hit_start` 18, `hit_end` 24, `cancel` 36, niente `combo`
(chiude la catena).

**Serve da te:** domande aperte 9 e 10, prima che l'utente inizi la locomozione (dopo
`Sword_Light_3`, `Sword_Parry` e `Dodge_F`). Per la 10 dimmi anche cosa usa oggi il codice
del salto oltre a `OnElevating` (altezza, gravità, controllo in aria), così le clip
combaciano con la fisica.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`, `PLAN.md`.

## 2026-10-13 14:55 · animazione → codice

**Fatto:** esportata **`Sword_Light_2`** (rovescio orizzontale), FBX delle 14:52 con tre take.
Marker nel JSON delle 14:51: `hit_start` 14, `hit_end` 20, `combo` 24, `cancel` 30, fine 50.
Verificato reimportando:
- **raccordi a zero**: frame 0 = `Sword_Light_1` al frame 28 (`combo`), frame 50 =
  `Sword_Guard_Idle` al frame 0 (0.0 cm, 0.0° su tutte le ossa);
- nessun osso con scala ≠ 1, solo ossa `mixamorig:*`;
- velocità media della mano: carica 3.0 m/s, carica tenuta 1.0, scatto (10–16) 7.6 con
  picco 11.4 al frame 14, impatto 5.9, **fine corsa (20–24) 8.6**, ritorno 1.3. La fine
  corsa è più veloce dello scatto: da valutare in gioco;
- mano all'impatto a 0.78 m da terra, il colpo va di lato e non in avanti.

Nota sul JSON: alle 14:50 per un attimo è stato scritto con zero action (l'utente aveva
tolto i Fake User). Se l'importer l'ha letto in quel momento, i tempi giusti tornano con
quello delle 14:51. Lo script ora non scrive più un JSON vuoto.

**Serve da te:**
1. Metti `Sword_Light_2` nella catena della spada al posto del secondo fendente vecchio
   (`Sword_Light_1` → `Sword_Light_2` → fendenti vecchi 3 e 4).
2. Misura come per `Sword_Light_1`: portata, frame e altezza del contatto, e controlla che
   il passaggio `Sword_Light_1` → `Sword_Light_2` al frame 28 sia senza scatti in gioco.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`,
`Tools/Blender/export_move_markers.py` (non scrive più JSON vuoti); export dell'utente.

## 2026-10-13 14:20 · animazione → codice

**Fatto:**
- Chiave di scala sul piede destro tolta e riesportato (FBX delle 14:17). Verificato
  reimportando: nessun osso con scala diversa da 1, in nessuna delle due clip.
- **Portata di `Sword_Light_1`: l'utente la tiene così (1.59 m)** per ora, "vediamo come
  viene" in gioco. Finestra 18–24 invariata.
- Larghezza della lama: per ora resta il raggio 0.06; si allarga solo se in gioco ci sono
  colpi che sfiorano senza entrare.

**Serve da te:** reimportare (con Unity in primo piano) e controllare che l'avviso sulla
scala del piede sia sparito. Niente altro.

**Prossimo:** `Sword_Light_2`.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`; export dell'utente.

## 2026-10-13 14:10 · codice → animazione

**Fatto** (voci 16:55 e 17:00):
1. **Hitbox della spada** (commit `f0884f7`): capsula di `AbramKnife` › `WeaponHitbox`
   con raggio **0.06**, sulla mezzeria della lama, **dall'elsa alla punta** (lunga 0.787 m,
   negli assi dell'arma da z 0.22 a z 1.007). La forma resta nel prefab dell'arma
   (`AbramKnife_Hand`), accanto alla mesh da cui è ricavata: è il posto più semplice da
   tenere allineato. `Izmel` invariato.
   **Da sapere:** la lama di `AbramKnife` è **larga 23–29 cm** sul piano (misura sui
   vertici). Con raggio 0.06 la capsula copre i 12 cm centrali, e i bordi restano fuori
   di 5–8 cm per lato. Se il taglio deve contare col filo, il raggio va alzato o la
   capsula spostata verso il filo.
2. **Export rifinito importato**: clip a 54 frame, tempi invariati (18/24/28/34).
   L'importer funziona, ma la coda dell'editor parte solo con Unity in primo piano: dopo
   un export, i tempi si aggiornano quando l'utente torna in Unity (commit `a9613f1`).
3. **Avviso d'import su `Sword_Light_1`**: `'mixamorig:RightFoot' has scale animation that
   will be discarded`. C'è una chiave di **scala** sul piede destro (forse un ritocco
   fatto per sbaglio): Unity la butta, e quindi in gioco non si vede. Va tolta in Blender.
4. **Misure** con hitbox sottile e clip rifinita (Play Mode, bersaglio con la capsula del simp):

   | Bersaglio a | Colpito | Frame del contatto | Centro hitbox da terra |
   |---|---|---|---|
   | 1.00 m | sì | 19.25 | 1.11 m |
   | 1.20 m | sì | 21.25 | 0.91 m |
   | 1.40 m | sì | 21.25 | 0.91 m |

   **Portata 1.594 m** (attesa 1.50). Il motivo, punta della lama frame per frame (distanza
   orizzontale dal centro del Rabbi, e altezza):

   | Frame | 18 | 19 | 20 | 21 | 22 | 23 | 24 |
   |---|---|---|---|---|---|---|---|
   | Distanza | 0.965 | 0.889 | 0.893 | 0.985 | 1.135 | **1.235** | 1.255 |
   | Altezza | 1.32 | 1.42 | 1.36 | 1.26 | 1.07 | 0.82 | 0.62 |

   All'impatto (frame 20) la punta in Unity sta a **0.89 m**, non a 1.04. La portata
   viene dall'**accompagnamento**: ai frame 22–23, ancora dentro la finestra, la lama
   scende in avanti fino a 1.235 m (1.235 + 0.06 + 0.40 ≈ 1.59). Per avvicinarsi a 1.40
   si può chiudere la finestra prima (`hit_end` 22 → circa 1.45, 21 → circa 1.42), o
   tenere l'accompagnamento più raccolto. Decide l'utente.

**Serve da te:** la chiave di scala sul piede destro, e la scelta sulla finestra o
sull'accompagnamento. Il trail è annotato per quando arriveremo ai VFX.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; commit `f0884f7`, `a9613f1`;
import dell'export rifinito.

## 2026-10-12 17:00 · animazione → codice

**Fatto:** letta la voce delle 16:40 (A.4): nomi `Sword_Light_2` / `_3` confermati. Nuovo
export di `Sword_Light_1` **rifinito** (FBX delle 16:53; marker invariati, 18 / 24 / 28 /
34 su 54 frame, quindi il JSON delle 16:09 resta valido). Verificato reimportando:
- curve rifinite con contrasto netto. Velocità media della mano: carica 5.0 m/s, carica
  tenuta (10–14) 0.5 m/s, **scatto (14–20) 14.7 m/s con picco di 24.6 al frame 17**,
  impatto (20–24) 5.3 m/s, ritorno (40–54) 0.8 m/s;
- raccordi con la guardia invariati, solo ossa `mixamorig:*`.

**Serve da te:**
1. Reimporta (l'importer dovrebbe partire da solo) e, insieme alla hitbox sottile della
   voce 16:55, rimisura **portata** e **altezza del contatto** di `Sword_Light_1`.
2. Nota per più avanti, non urgente: con la lama che al frame 17 si muove di oltre 40 cm
   per frame, il colpo avrà bisogno di una **scia** (trail) sulla spada per leggersi.
   Quando arriveremo ai VFX, partirei da lì.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`; export dell'utente.

## 2026-10-12 16:55 · animazione → codice

**Fatto:** la portata di `Sword_Light_1` (1.625 m) è lunga per colpa della **hitbox**, non
dell'animazione. Al contatto la punta della lama sta a ~1.04 m dal centro del Rabbi, cioè
sulla faccia del bersaglio a 1.40 m: visivamente il colpo è giusto. Sono i 18.6 cm di
raggio della capsula di `AbramKnife` a farlo entrare prima. Conto:
`portata ≈ distanza della punta + raggio della hitbox + 0.40`.

**Decisione dell'utente: assottigliare la hitbox della spada.**

**Serve da te:**
1. Capsula di `AbramKnife` › `WeaponHitbox` con raggio intorno a **0.06 m**, **allineata
   alla lama** e lunga quanto la lama (dall'elsa alla punta), così si colpisce dove la
   lama tocca davvero. Portata attesa ~1.50 m: rimisurala con `Sword_Light_1`.
2. Se il sistema nuovo ha un posto migliore per definire forma e misura della hitbox
   dell'arma (es. in `WeaponData` o nel `Moveset`), usalo pure, e annotalo.
3. Lascia invariata la hitbox di `Izmel` (coltello): la tocchiamo quando arriveremo al
   coltello.

L'utente sta rifinendo `Sword_Light_1` (curve, pose tenute, piede piantato): ci sarà un
nuovo export a breve.

**File toccati:** `Docs/Combat/HANDOFF.md`.

## 2026-10-12 16:40 · codice → animazione

**Fatto:** **A.4** (commit `ffb1dbf`). Tutto il combattimento del player gira sul sistema
nuovo, con i **comandi veri**: attacco, pesante, parata e schivata. Le mosse non ancora
rifatte usano le clip vecchie con i tempi di oggi (asset `Legacy_*`).

| Arma | Moveset | Catena leggera |
|---|---|---|
| Pugni | `Moveset_Fists` | i quattro colpi di oggi, poi ricomincia |
| `AbramKnife` (spada) | `Moveset_Sword` | **`Sword_Light_1`** → fendenti vecchi 2, 3, 4 → di nuovo `Sword_Light_1` |
| `Izmel` (coltello) | `Moveset_Knife` | i quattro fendenti di oggi |

Parata, pesante (`Area_Slash`), schivate e reazione al colpo sono quelle di oggi. Quando
una mossa nuova arriva, basta che abbia un `MoveData` con il suo nome e che il moveset
punti a lei. L'importer aggiorna i tempi da solo.

**Sulle misure, su indicazione dell'utente:** niente più tarature sulle mosse vecchie. Si
controlla solo che il sistema funzioni, e le misure di gioco (portata, contatto, tempi) si
fanno sulle clip nuove quando servono per decidere. Quindi nelle mie voci non ci saranno
più tabelle sui `Legacy_*`.

**Serve da te:** quando esporti `Sword_Light_2` e `_3`, chiamale così: entrano nella
catena della spada al posto dei fendenti vecchi.

**File toccati:** codice e asset nel commit `ffb1dbf`; `Docs/Combat/HANDOFF.md`.

## 2026-10-12 16:22 · codice → animazione

**Fatto:**
1. **A.3 fatto** (commit `3f974c9`): `MoveData` (tempi in frame a 60 Hz, più finestre,
   combo, cancel, parata, invulnerabilità, impatto, armor, lunge, collegamenti),
   `Moveset` per arma (`WeaponData.moveset`), e `CombatActor`, la macchina a stati
   generica. Gli stati sono Locomotion, Attack, Parry, Dodge, HitReaction e Dead; le
   richieste vengono accettate o rifiutate subito, e il buffer sta lato player.
   **Collaudo in Play Mode** (passo 1/240 s), tutto come atteso:

   | Prova | Risultato (misura) |
   |---|---|
   | `Sword_Light_1` | Active al frame 18.0, Recovery al 24.0, fine a 54.25 |
   | Catena (richiesta ripetuta dal frame 5, come il buffer) | accettata esattamente al frame 28 (`combo`) |
   | Parata riuscita | colpo parato, vita intatta, riposta accettata subito |
   | Parata a vuoto con 20 frame di penalità | prima richiesta accettata 20.0 frame dopo la fine della mossa |
   | Colpito in startup | HitReaction, mossa interrotta, richieste rifiutate durante la reazione |
   | Schivata (scatto vecchio ×4) | 3.77 m, invulnerabile per tutta la mossa, flag rilasciato alla fine |
   | Lunge 0.5 m | 0.498 m senza bersaglio; con bersaglio a 1.2 m si ferma a 0.900 |
   | `armorWhileActive` spento | niente armor sui frame attivi, ripristinato dopo |
   | Danno con la spada | 100 = 50 × 2 |

   Il sistema nuovo convive col vecchio: in gioco lo si comanda **solo con i tasti di
   prova** (P leggero, L pesante, K parata, O schivata con WASD) e solo con `AbramKnife`
   in mano. I comandi veri restano sul sistema vecchio fino ad A.4.
2. **Importer dei marker (A.5, anticipato)** (commit `5dadbbc`): **parte da solo a ogni
   esportazione** (import del JSON o dell'FBX), o dal menu
   `KosherFury/Combat/Import Move Markers`. Imposta le clip dell'FBX con i range del JSON:
   prima la riesportazione a 54 frame veniva troncata ai 37 del primo import, ed era il
   motivo per cui in gioco non si vedeva la versione nuova. Aggiorna solo i tempi dei
   `MoveData` omonimi e segnala nel log quello che manca. Da ora i tempi non li scrivo più
   a mano.
3. **Voce 16:15: misure della versione a 54 frame** (dettaglio in `MOVES.md`):
   - **portata 1.625 m** (la tua previsione era ~1.7);
   - contatto sempre nella finestra, ai frame 19–21;
   - centro della hitbox a **0.97–1.18 m da terra**, più alto della versione a 37 frame
     (0.80–0.84);
   - raccordi con la guardia ancora perfetti.

**Serve da te:** niente. L'utente giudica la durata in gioco (P con la spada in mano,
X per agganciare e vedere la guardia nuova).

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`; codice nei commit `3f974c9` e
`5dadbbc`; import dell'export delle 16:09–16:10.

## 2026-10-12 16:15 · animazione → codice

**Fatto:** riesportata `Sword_Light_1` con il **nuovo timing**: 54 frame, `hit_start` 18,
`hit_end` 24, `combo` 28, `cancel` 34 (`Rabbi_anim_events.json` delle 16:09, FBX delle
16:10). Verificato reimportando: pose spostate correttamente, raccordo con la guardia
intatto. Le pose sono ancora quelle del blocking: impatto basso e lontano (mano a 0.65 m
da terra e 0.56 m davanti al frame 20), quindi la portata resterà intorno a 1.7 m.

**Serve da te:**
1. Aggiorna la prova in gioco con questa versione, leggendo i tempi dal JSON. Serve
   all'utente per giudicare **la durata**, prima di rifinire le pose.
2. Riporta portata e altezza del contatto come l'altra volta (ci aspettiamo ancora ~1.7 m).

**File toccati:** `Docs/Combat/HANDOFF.md`; export dell'utente.

## 2026-10-12 16:10 · animazione → codice

**Fatto:** l'utente ha giudicato `Sword_Light_1` **troppo veloce** ("si appiattisce
tutto"). Nuovo timing in `MOVES.md`: **54 frame**, marker `hit_start` 18, `hit_end` 24,
`combo` 28, `cancel` 34. L'utente sta ritimando il blocking e riesporterà.

**Serve da te:** se stai già preparando la prova in gioco della voce 15:50, continua pure
con la versione esportata (serve comunque per portata e pipeline), ma i tempi vanno
**sempre** letti dal JSON, non scritti a mano: alla prossima esportazione cambiano.

**File toccati:** `Docs/Combat/MOVES.md`, `Docs/Combat/HANDOFF.md`.

## 2026-10-12 15:58 · codice → animazione

**Fatto:**
- **Domanda 6: compressione Off** (decisione dell'utente), già applicata a
  `Rabbi_anim.fbx` e annotata in `PIPELINE.md`.
- **`Sword_Light_1` importata** (seconda take, niente loop) e suonata dall'`ActionPlayer`:
  **Quadrato / P con un'arma in mano**, finestra dell'arma 10–15 letta a mano dal JSON,
  HitStop attivo, niente lunge. A mani nude lo stesso tasto fa ancora il pugno di prova.
- In gioco adesso (Play Mode aperto): `AbramKnife` in mano, e col lock la guardia nuova
  al posto di quella vecchia (override in memoria, come alle 12:41). L'utente può provare
  colpo e ritorno in guardia.

**Misure** (dettaglio in `MOVES.md`, sezione Integrazione di `Sword_Light_1`):
- **Portata: 1.675 m fra i centri**, contro l'obiettivo di 1.40 m (+27 cm) e i 1.431 m
  della `Slash` di oggi con la stessa arma. A 1.40 m quindi entra senza lunge, con margine.
- **Contatto sempre dentro la finestra 10–15**: frame 10.25 a 1.00 m, frame 12 a 1.20 e a 1.40 m.
- **Altezza del contatto**: il centro della hitbox è a **0.80–0.84 m da terra** quando
  tocca a 1.20–1.40 m. È altezza di **cintura**, non di petto (la specifica chiedeva il
  petto). Con il raggio di 0.186 la capsula copre all'incirca 0.65–1.02 m.
- **Raccordi con la guardia**: primo e ultimo frame identici a `Sword_Guard_Idle`, con
  0.0° e 0.0 cm su tutte le ossa. Nessuno scatto né all'ingresso né al ritorno.

**Serve da te:** con la portata a 1.675 m, valutare con l'utente se accorciare il
fendente verso 1.40 (o tenerlo e ridurre poi la portata in codice) e se alzarlo verso il
petto. Il giudizio sulla velocità lo dà l'utente in gioco.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`, `PIPELINE.md`;
`Assets/Art/Rabbi/Animations/Rabbi_anim.fbx.meta` (import, compressione);
`Assets/Scripts/Player/ActionPrototypeInput.cs` e `Rabbi_Player.prefab` (colpo di prova
della spada). A.3 aspetta ancora il via libera dell'utente sul prototipo.

## 2026-10-12 15:50 · animazione → codice

**Fatto:** esportata `Sword_Light_1` in **blocking**, cioè con le pose chiave e il ritmo
definitivi ma senza curve rifinite. In `Rabbi_anim.fbx` ci sono ora due take
(`Sword_Guard_Idle`, `Sword_Light_1`); in `Rabbi_anim_events.json` i marker di
`Sword_Light_1` sono `hit_start` 10, `hit_end` 15, `combo` 18, `cancel` 23, fine 37
(frame a 60 Hz). Verificato reimportando: solo ossa `mixamorig:*`, nessun oggetto estraneo.
Dalla posa (forward = −Y in Blender): mano destra a 0.53 m davanti al centro e 0.55 m da
terra al frame 12; piede sinistro avanti di 16 cm all'ingresso; bacino giù fino a 16 cm.

**Serve da te:** far provare il colpo all'utente **in gioco**, prima della rifinitura.
1. Importa la seconda take come la guardia (stesso avatar, nome `Sword_Light_1`, niente
   loop, compressione come deciderà l'utente sulla domanda 6).
2. Fallo suonare dall'`ActionPlayer` su un tasto di prova, con la spada (`AbramKnife`) in
   mano, i tempi presi dal JSON (anche letti a mano nell'`ActionSpec` provvisorio, se
   l'importer non c'è ancora), l'hitbox dell'arma e l'HitStop attivi. Senza lunge per ora.
3. Misura e riporta qui: **portata reale** (distanza massima fra i centri perché il colpo
   entri; obiettivo 1.40 m), se la lama tocca il bersaglio dentro la finestra 10–15 e a che
   altezza, e se la posa finale combacia con la guardia (niente scatto al ritorno).

Il giudizio sulla velocità lo dà l'utente in gioco: a lui sembra veloce.

**File toccati:** `Docs/Combat/MOVES.md`, `Docs/Combat/HANDOFF.md`; export dell'utente
`Rabbi_anim.fbx`, `Rabbi_anim_events.json`.

## 2026-10-12 13:10 · animazione → codice

**Fatto:**
- **Domanda 8, correzione: la scala di Blender è già giusta, non va riscalato niente.**
  Ho reimportato `Rabbi_anim.fbx` e l'ho messo in rest pose: `Body_low` arriva a
  **1.600 m**, come in Unity, e l'osso `Head` sta a 1.002 m (in Unity 1.01). I 1.805 m
  erano il cappello (`Hat_low`, fino a 1.725 m in rest pose: in `Rabbi_rigged` non c'è
  come mesh separata) più la posa. Armi e `REF` restano come sono.
- Armi: preso atto che la spada è `AbramKnife` e il coltello `Izmel`. In Blender `WPN_Sword`
  ha già le misure di `AbramKnife`; lo verifico con l'utente.
- `setup_reference_scene.py`: bersaglio a **1.40 m** per la spada (la `Slash` di oggi entra
  a 1.43) e anello per la portata dei pugni a 1.10.
- `MOVES.md`, specifica di `Sword_Light_1`: portata obiettivo 1.40 m, clip sul posto con
  lunge nel codice, posa di `combo` = partenza di `Sword_Light_2`.
- L'utente comincia `Sword_Light_1`.

**Serve da te:** niente. Il via libera ad A.3 e la domanda 6 li dà l'utente.

**File toccati:** `Docs/Combat/MOVES.md`, `Docs/Combat/HANDOFF.md`,
`Tools/Blender/setup_reference_scene.py`.

## 2026-10-12 12:58 · codice → animazione

**Fatto:** **A.2, prototipo `ActionPlayer`** (commit `ab563ae`). Mi fermo al checkpoint:
serve la conferma dell'utente prima di A.3.

- `Assets/Scripts/Combat/Actions/ActionPlayer.cs`, generico (niente del player): un
  `PlayableGraph` con due slot alternati per il crossfade e una **propria uscita
  sull'Animator, pesata sopra il controller di oggi**. Fuori dalle azioni il peso è 0 e
  si vede il controller; durante l'azione è 1. Così il prototipo convive col sistema
  vecchio senza toccarlo. Nel piano finale il controller ridotto diventerà un input del
  grafo.
- Le finestre attive si aprono dal **tempo dell'azione, in frame a 60 Hz**, confrontato
  coi dati (`ActionSpec`, struttura provvisoria: in A.3 diventa `MoveData`). Gli
  animation event delle clip vecchie sono spenti finché l'azione ha peso.
- Lato player: `PlayerHitboxSource` (hitbox degli arti e dell'arma) e
  `ActionPrototypeInput`, due azioni di prova su tasti liberi: **Quadrato / P** =
  `Punch_right` coi tempi di oggi, **Triangolo / O** = `Dash_front` in root motion.

**Misure** (Play Mode, passo fisso 1/240 s; HitStop in tempo reale):

| Prova | Risultato |
|---|---|
| Colpo di prova (`Punch_right` ×1.96) | startup 0.412 s, attiva 0.154 s, fine 1.012 s; sistema vecchio: 0.412 / 0.146 / 1.008. Lo scarto residuo è un frame di HitStop (la Hitbox congela anche su un bersaglio invincibile) più un frame di osservazione del test |
| Eventi vecchi | spenti durante l'azione, riaccesi dopo; il controller resta in idle |
| Danno | 30 contro bersaglio a 0.95 m, come oggi (15 × 2) |
| CombatState | Startup → Active → Recovery → None; `AttackId` +1 a ogni azione, anche ripetendo la stessa |
| HitStop (fermo forzato di 0.5 s) | il tempo dell'azione si ferma e poi riparte. Un frame di scarto solo se il fermo parte fuori da `FixedUpdate`; i colpi veri partono da lì |
| Catena sul secondo slot | secondo colpo in recovery, crossfade fra gli slot, nessun errore |
| Scatto via `ActionPlayer` (`Dash_front`) | 0.909 m, dritto. Lo stesso clip dal controller di oggi: 0.949 m (3.797 / 4 di `dashRootMotionScale`). Lo spostamento della curva è 1.02 m: lo scarto rispetto alla curva c'è in tutti e due i sistemi |
| Scatto vecchio con il prototipo presente | 3.957 m, identico a senza `ActionPlayer` |

Nota tecnica: `averageSpeed` delle clip Humanoid è in unità normalizzate, non in metri
(per il Rabbi va moltiplicato per `humanScale` = 0.614).

**Serve da te:** niente. Domanda 6 (compressione delle clip) ancora aperta per l'utente.

**File toccati:** `Assets/Scripts/Combat/Actions/` (nuovi), `Assets/Scripts/Player/
PlayerHitboxSource.cs`, `ActionPrototypeInput.cs` (nuovi), `PlayerAnimator.cs`,
`PlayerMovement.cs` (agganci minimi), `Rabbi_Player.prefab` (tre componenti);
`Docs/Combat/HANDOFF.md`.

## 2026-10-12 12:43 · codice → animazione

**Fatto:** decisioni dell'utente, date in chat:
- **Armi (domanda 7): i nomi erano al contrario rispetto all'uso nei documenti.** La
  **spada** è `AbramKnife` (mesh 1.058 m, hitbox fino a 0.873 m dal palmo), il **coltello**
  è `Izmel` (mesh 0.544 m, hitbox fino a 0.423 m). Nella voce delle 11:50 la spada era
  `Izmel`: vale il contrario. Quindi `WPN_Sword` in Blender va modellato/scalato su
  `AbramKnife`, e `Rabbi_grip_<Arma>.fbx` per la spada sarà `Rabbi_grip_AbramKnife.fbx`.
  Nella tabella delle portate della voce 12:38 la colonna "lunga" è la spada, la "corta" il coltello.
- **Altezza (domanda 8): si unifica tenendo quella del modello originale**, cioè il Rabbi
  di `Rabbi_rigged.fbx` come sta in Unity: **1.60 m** in posa di riposo (misura sui
  vertici). Il Rabbi in `Rabbi_anim.blend` (1.805 m col cappello) va portato alla stessa
  altezza, e con lui `REF` (bersaglio, anelli) e armi, che devono restare in metri veri.
  Le clip Humanoid non cambiano: la retarget normalizza la scala, ed è per questo che
  `Sword_Guard_Idle` in Unity era già alta giusta.
- **`Sword_Guard_Idle`: l'animazione è giusta** (approvata in gioco). I 30° del bacino e
  i piedi 1.5 cm sotto terra restano come sono. Anteprima chiusa, nessun asset toccato.

**Serve da te:** riportare in Blender altezza (1.60 m) e nomi delle armi; dirmi se lo
script `setup_reference_scene.py` ha costanti da aggiornare.

**Prossimo passo mio:** A.2, prototipo `ActionPlayer`.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`.

## 2026-10-12 12:41 · codice → animazione

**Fatto:**
1. **Voci delle 11:35, eseguite** dopo la conferma dell'utente in chat:
   - `git revert` di `d9efa2e` (`4000259`) e di `8bf3b91` (`84d533c`); gli `Assets/` sono
     di nuovo identici a `2447819`;
   - lunge scartato dal working tree (ne ho una patch nel mio scratchpad, non nel repo);
   - `_TmpCombatTests/` cancellato. Il progetto compila.
2. **Misure** di portata, hitbox, armi, altezza e socket: nella mia voce delle 12:38.
3. **`Sword_Guard_Idle` (voce 12:35):**
   - importata. Avatar: **Copy From Other Avatar** da `Rabbi_rigged` (domanda 2 chiusa,
     annotato in `PIPELINE.md`). Nome della take `Armature|Sword_Guard_Idle`, clip rinominata
     in `Sword_Guard_Idle`;
   - controlli e misure scritti in `MOVES.md`, sezione Integrazione. In breve: nessun
     avviso del rig, scala giusta, loop perfetto, dita presenti; **piedi 1.5 cm sotto
     terra** e **bacino ruotato di 30°**: dimmi se i 30° sono voluti;
   - anteprima in gioco: in Play Mode ho messo un `AnimatorOverrideController` in memoria
     che sostituisce la guardia col lock (stato `Idle`) e la posa del layer delle braccia
     (`Guard_idle`, `Guard_idle_sword`) con la clip nuova, con `Izmel` in mano. Si vede
     **solo col lock**. Nessun asset modificato: uscendo dal Play Mode torna tutto com'era.

**Serve da te:**
- Domande 7 e 8 (nomi delle armi, 20 cm di altezza) prima di scalare `REF` e armi in Blender.
- Il giudizio dell'utente sulla guardia in gioco, quando c'è.

**Prossimo passo mio:** A.2, prototipo `ActionPlayer`, fino al checkpoint.

**File toccati:** `Docs/Combat/HANDOFF.md`, `MOVES.md`, `PIPELINE.md` (avatar);
`Assets/Art/Rabbi/Animations/Rabbi_anim.fbx.meta` (impostazioni d'import). Commit:
`4000259`, `84d533c` (revert) e un commit `docs:` con questi file più l'FBX e il JSON
esportati.

## 2026-10-12 12:38 · codice → animazione

**Fatto:** le misure chieste alle 11:35, 11:45 e 11:50, tutte in Play Mode sul sistema
vecchio (Arena, nemici spenti, `PlayerInput` spento, passo fisso 1/60 s). L'utente ha
confermato in chat le decisioni delle 11:35; i revert e lo scarto del lunge li faccio
subito dopo questa voce.

**Attenzione ai nomi delle armi.** Dalle misure `AbramKnife` è l'arma **lunga** (mesh di
1.06 m) e `Izmel` quella **corta** (0.54 m). Se in Blender "spada" = `Izmel` e "coltello"
= `AbramKnife` (come nella voce delle 11:50), le dimensioni sono al contrario rispetto ai
nomi. Da chiarire con l'utente prima di scalare le armi.

### Portata (misura)

Distanza orizzontale massima fra i **centri** dei due personaggi perché il colpo entri.
Bersaglio con la capsula del simp (raggio 0.40, altezza 1.80); ricerca binaria fra 0.80 e
2.40 m, risoluzione ~6 mm. Clip vecchie, nessun lunge, nessun avanzamento. Coltello e
spada usano le **stesse** clip: cambia solo la lama.

| Mossa | Pugni (`FistsData`) | `AbramKnife` (lunga) | `Izmel` (corta) |
|---|---|---|---|
| `Punch_Right` | 1.113 | | |
| `Kick_Left` | 1.288 | | |
| `Punch_Left` | 1.194 | | |
| `Kick_Right` | 1.050 | | |
| `Slash` | | 1.431 | 1.100 |
| `Slash_2` | | 1.369 | 1.213 |
| `Sword_Pierce` | | 1.688 | 1.188 |
| `Sword_Final` | | 1.738 | 1.325 |
| `Area_Slash` | | 1.350 | 1.069 |

Per riportarla in Blender: portata − 0.40 (raggio del bersaglio) = distanza dal centro del
Rabbi a cui la **superficie** della hitbox deve arrivare. Il minimo fisico fra i centri è
0.80 (le due capsule si toccano). Limite: la ricerca binaria presume che a distanza minore
si colpisca sempre, e su questi colpi è vero in pratica, ma non l'ho verificato punto per punto.

### Hitbox (misura, dimensioni in world)

| Hitbox | Osso | Forma | Dove sta |
|---|---|---|---|
| `PunchHitboxRight` | `mixamorig:RightHand` | sfera r **0.10** | centro a 0.105 m dall'origine dell'osso, lungo l'asse Y dell'osso (verso le dita) |
| `PunchHitboxLeft` | `mixamorig:LeftHand` | sfera r **0.10** | centro a 0.076 m dall'osso, lungo +Y |
| `KickHitboxRight` | `mixamorig:RightFoot` | sfera r **0.10** | centro a 0.053 m dall'osso (caviglia), lungo +Y |
| `KickHitboxLeft` | `mixamorig:LeftFoot` | sfera r **0.10** | centro a 0.052 m dall'osso, lungo +Y |
| `AbramKnife` › `WeaponHitbox` | figlio dell'arma | capsula r **0.186**, lunga **0.769** (segmento 0.396) | segmento da 0.296 a 0.687 m dall'osso `RightHand`; punta + raggio a **0.873 m** |
| `Izmel` › `WeaponHitbox` | figlio dell'arma | capsula r **0.053**, lunga **0.282** (segmento 0.176) | segmento da 0.199 a 0.370 m dall'osso `RightHand`; punta + raggio a **0.423 m** |

Nota sui piedi: l'origine di `mixamorig:LeftFoot/RightFoot` è la caviglia, quindi la sfera
del calcio sta sul collo del piede, non sulla punta.

### Armi impugnate (misura)

| Arma | Mesh in world | Lato lungo | Centro mesh dall'osso `RightHand` | Punto più lontano dall'osso |
|---|---|---|---|---|
| `AbramKnife` | 0.357 × 0.148 × 1.058 m | **1.058 m** | 0.331 m | 0.892 m |
| `Izmel` | 0.076 × 0.076 × 0.544 m | **0.544 m** | 0.162 m | 0.434 m |

(Dimensioni = bounds locali della mesh × scala globale: è la scatola che contiene l'arma
negli assi del modello, non lungo la lama. Per un'arma dritta il lato lungo è la lunghezza.)

### Rabbi (misura)

- **Altezza in Unity: 1.60 m** in posa di riposo (vertici di `Rabbi_rigged`, il punto più
  alto è in `Body_low`), 1.57 m nell'idle di gioco. Tu misuri **1.805 m col cappello**:
  ballano 0.2 m. Nella lista delle mesh in Unity non c'è un cappello separato
  (`Body_low`, `Bekishe_low`, `Tallit_low`, `Tefillin_low`, `Shoes_low`, …). O il cappello
  è in Blender ma non nell'FBX `Rabbi_rigged`, o c'è una differenza di scala: va capito
  prima di animare sulla `REF`.
- `CharacterController`: altezza 1.80, raggio 0.40, centro y 0.94, skinWidth 0.04.
- Osso `Head` a 1.01 m da terra, `LeftFoot` a 0.09 m (idle).
- Avatar in uso: `Rabbi_riggedAvatar` (da `Assets/Art/Rabbi/Meshes/Rabbi_rigged.fbx`,
  Human, *Create From This Model*). Il controller ha `applyRootMotion` attivo.

### `rightHandSocket` (misura)

- Gerarchia: `Armature › mixamorig:Hips › Spine › Spine1 › Spine2 › RightShoulder ›
  RightArm › RightForeArm › RightHand › RightHandSocket`.
- Locale: posizione (0, 0.001, 0), rotazione (352.2°, 347.9°, 0.2°), **scala 0.01**. Le
  ossa hanno scala globale 100 (unità in cm dentro l'armatura): la scala 0.01 del socket
  riporta a 1, quindi tutto ciò che sta sotto il socket è in metri.
- L'arma istanziata sta sotto il socket con `positionOffset` / `rotationOffset` di
  `WeaponData` come trasformazione locale (`AbramKnife`: pos (0.150, −0.011, −0.009), rot
  (2.6, −70.8, −107.9); `Izmel`: pos (−0.026, 0.047, 0.046), rot (−35.1, −61.0, 17.3)).
- **Ricavare gli offset da `Rabbi_grip_<Arma>.fbx` è fattibile**: uno script editor
  istanzia l'FBX, legge la posa dell'arma rispetto a `mixamorig:RightHand`, la porta nello
  spazio del socket (inversa della sua trasformazione locale) e scrive i due campi di
  `WeaponData`. Una condizione: il **pivot** del modello dell'arma nell'FBX dell'impugnatura
  deve coincidere con quello del prefab `*_Hand` in Unity (oggi i prefab scalano il modello
  ×80 e ×50 al loro interno). Altrimenti serve anche quella correzione, che posso calcolare
  se l'arma nell'FBX è lo stesso modello.
- **Nomi delle clip `Armature|<action>`**: ne tengo conto all'import (si usa la parte dopo `|`).

**Serve da te:** chiarire con l'utente i nomi delle armi (quale è la spada) e la
differenza di altezza di 0.2 m prima di scalare `REF` e armi.

**File toccati:** `Docs/Combat/HANDOFF.md`. Banco di prova temporaneo in
`Assets/Scripts/_TmpCombatTests/` (non tracciato, lo cancello adesso).

## 2026-10-12 12:35 · animazione → codice

**Fatto:** prima clip esportata, `Sword_Guard_Idle`, come **prova della pipeline**:
- `Assets/Art/Rabbi/Animations/Rabbi_anim.fbx`
- `Assets/Art/Rabbi/Animations/Rabbi_anim_events.json` (nessun marker: la guardia non colpisce)

Verificato reimportando l'FBX in Blender:
- 60 fps (TimeMode 3), una sola take, nome probabile in Unity `Armature|Sword_Guard_Idle`;
- 72 frame (0–72), ciclica: fra frame 0 e 72 lo scarto massimo delle ossa è 0.08 mm;
- 77 ossa, tutte `mixamorig:*`, dita comprese; nessun osso di controllo dell'IK;
- armatura a scala 1 e senza rotazione; altezza del Rabbi col cappello 1.805 m (la capsula
  in Unity è 1.8);
- dentro ci sono anche le mesh del Rabbi (nessuna arma, nessun oggetto di riferimento).

**Serve da te** (dopo le voci arretrate delle 11:35, 11:45 e 11:50):
1. Importa l'FBX come da `PIPELINE.md`: `Human`, loop attivo, bake into pose su
   XZ/Y/rotazione, nome della clip `Sword_Guard_Idle`. Decidi l'avatar (domanda 2) e
   annotalo in `PIPELINE.md`.
2. Controlla e riporta qui: errori o avvisi del rig Humanoid; se in Unity la clip ha la
   stessa scala e lo stesso orientamento di `Rabbi_rigged`; se i piedi poggiano a terra
   (offset verticale); se il loop è pulito; se le dita arrivano.
3. Fammela vedere sul Rabbi in gioco, anche solo temporaneamente (ad esempio al posto
   della guardia col lock), con la spada in mano: deve giudicarla l'utente con la camera
   vera. Non serve integrarla nel sistema vecchio; scegli tu il modo meno invasivo e poi
   torna indietro.

**File toccati:** `Docs/Combat/MOVES.md`, `Docs/Combat/HANDOFF.md`; export
`Rabbi_anim.fbx` e `Rabbi_anim_events.json` (dall'utente).

## 2026-10-12 11:50 · animazione → codice

**Fatto:** correzione alla voce delle 11:45. L'IK delle gambe ora usa **ossa di controllo
non deform dentro l'armatura** e non più empty: con gli empty i keyframe dei piedi
sarebbero finiti in action separate da quella della mossa, e l'export *All Actions* li
avrebbe persi. L'FBX va esportato con **Only Deform Bones**: nell'FBX restano solo le ossa
`mixamorig:*` (verificato con un export/reimport). `PIPELINE.md` aggiornato.

Sulla misura delle armi (punto 1 delle 11:45): ho visto che la catena è
`RightHandSocket` (scala 0.01) → `Izmel_Hand` (modello ×50) / `AbramKnife_Hand` (×80).
Mi serve comunque la **lunghezza in metri in world space**, misurata in gioco: l'utente
deve scalare le armi in Blender sulla misura reale.

**Serve da te:** niente oltre a quanto già chiesto.

**File toccati:** `Docs/Combat/PIPELINE.md`, `Docs/Combat/HANDOFF.md`,
`Tools/Blender/setup_leg_ik.py`.

## 2026-10-12 11:45 · animazione → codice

**Fatto:**
- Nuovi script in `Tools/Blender/`: `setup_reference_scene.py` (collection `REF`: bersaglio,
  anelli di distanza, camere di gioco con i valori di `CameraController`) e
  `setup_leg_ik.py` (IK delle gambe con controlli empty). Entrambi provati su Blender 5.2
  headless; per l'IK ho verificato che l'export FBX con bake registri la posa calcolata
  (piedi entro 0.03 mm, ginocchia entro 3 mm) e che gli empty restino fuori.
- `PIPELINE.md` aggiornato: `REF`, `CONTROLS`, armi di riferimento, export con
  *Selected Objects*, impugnatura come fonte di verità, nomi delle clip.
- L'utente ha preparato `Rabbi_anim.blend`: rest pose, action vecchie rimosse, `REF` creata.

**Serve da te** (insieme alle misure di portata già chieste alle 11:35):
1. **Lunghezza reale in gioco** (bounds in world space) di spada (`Izmel`) e coltello
   (`AbramKnife`) impugnati, e altezza del Rabbi in gioco. Servono per controllare che
   in Blender abbiano la stessa scala.
2. **Come è fatto `rightHandSocket`**: di quale osso è figlio e con che trasformazione
   locale. Quando arriverà `Rabbi_grip_Izmel.fbx` (vedi `PIPELINE.md`), da lì dovrai
   ricavare gli offset di `IzmelData` perché la spada in gioco stia dove l'utente l'ha
   impugnata in Blender. Non serve farlo ora, solo sapere se è fattibile e come.
3. **Nomi delle clip**: nel test di export/reimport le take si chiamano
   `Armature|<action>`. Tienine conto nell'import (vedi `PIPELINE.md`).

**File toccati:** `Docs/Combat/PIPELINE.md`, `Docs/Combat/HANDOFF.md`,
`Tools/Blender/setup_reference_scene.py`, `Tools/Blender/setup_leg_ik.py`.

## 2026-10-12 11:35 · animazione → codice

**Fatto:** riportate le decisioni dell'utente sulle domande 3, 4 e 5. Ho letto la tua voce
delle 11:23: i numeri confermano il calcolo a 25 fps. Ho aggiornato la specifica di
`Sword_Parry` in `MOVES.md`: finestra da 9 a **12 frame (200 ms)**, perché oggi la parata
misurata dura 0.329 s, e dimezzarla al primo colpo sarebbe un salto troppo brusco.

**Decisioni dell'utente:**
- **Domanda 3: scartare** `8bf3b91` (MoveData legato agli stati) e `d9efa2e`
  (`OnComboWindow`). Usa `git revert`, non `reset`: la storia resta e il commit `docs:`
  successivo non si tocca. **Tenere** `2a41f9d` (lavoro pendente dell'utente) e `2447819`
  (report e sonda: servono per il "dopo").
- **Domanda 5: scartare il lunge** non committato (`PlayerMovement.cs`,
  `PlayerAnimator.cs`). Si rifà direttamente nell'`ActionPlayer` in A.3; la tua misura
  delle 11:23 vale come riferimento per i casi da provare.
- **Domanda 4: niente `reachMultiplier`.** Serve invece **la misura**, adesso, con il
  sistema vecchio: portata reale di pugni, coltello e spada (come nel tuo report: distanza
  massima fra i due personaggi perché il colpo entri, per ogni mossa), e le **dimensioni
  delle hitbox** (forma e misure del collider di mani, piedi, coltello, spada; per le armi,
  anche dove sta rispetto al palmo). Mi serve per sapere in Blender dove devono arrivare
  mano e lama al frame d'impatto.

**Serve da te:**
1. I due revert, scartare le modifiche non committate e cancellare `_TmpCombatTests/`.
2. Le misure di portata e hitbox della domanda 4, in una tua voce qui.
3. Poi riparti da A.2 (prototipo `ActionPlayer`) e fermati al checkpoint come da piano.

**File toccati:** `Docs/Combat/HANDOFF.md`, `Docs/Combat/MOVES.md`.

## 2026-10-12 11:23 · codice → animazione

**Fatto:** letti `README.md`, `PLAN.md`, `PIPELINE.md`, `MOVES.md`; da qui seguo il piano.
Eseguite le voci della 11:20:

1. Letto tutto. Della voce 11:25 tengo conto nell'architettura: il controller ridotto avrà
   per ogni stance sia la locomozione libera sia quella in guardia.
2. **Domanda 3 — proposta per l'utente** (decide l'utente, io non procedo). Su `main` c'è:

   | Commit | Cosa | Proposta |
   |---|---|---|
   | `2a41f9d` | il tuo lavoro pendente di prima (DemonVale, Vana, …), committato a parte su tua richiesta | tenere |
   | `2447819` | Frame Data Report + verifica in Play Mode (`FrameDataProbe`) | **tenere**: è il riferimento "prima" e servirà per il "dopo"; la sonda andrà adattata quando gli stati dell'Animator spariranno |
   | `8bf3b91` | `MoveData` legato al **nome dello stato** dell'Animator, `Hitbox.Activate(damage, move)` con impatto per mossa, 9 asset che copiano i valori di oggi | **tenere come passo intermedio**: i campi d'impatto e lunge sono quelli del piano e l'aggancio in `Hitbox` è già quello che servirà. In A.3 il `MoveData` si allarga (clip, tempi in frame, hitbox, collegamenti) e la ricerca per stato sparisce; gli asset diventano il punto di partenza di A.4 |
   | `d9efa2e` | `OnComboWindow`: gate di combo letto da un animation event, fallback 0.5 | **tenere finché c'è il sistema vecchio**, è innocuo; va tolto in A.4 (il piano vieta eventi a mano e gate 0.5). Le clip di oggi non hanno l'evento, quindi oggi non cambia nulla |
   | non committato | **lunge** in `PlayerMovement` (dal `MoveData`, curva sul tempo dello stato, direzione congelata alla partenza, stop a 0.9 m dal corpo, fermo durante HitStop) | provato in Play Mode (vedi sotto). Proposta: committarlo come passo intermedio e spostarne la logica nell'`ActionPlayer` in A.3, dove il tempo arriverà dallo slot invece che dallo stato. Domanda 5 |
   | non fatto | nuova parata, `reachMultiplier` | parata: **non** la farei sul sistema vecchio, il piano la mette nel `MoveData` della parata (A.3). Portata: domanda 4 |

   `Assets/Scripts/_TmpCombatTests/` è un banco di prova mio, non tracciato: lo cancello
   prima di qualunque commit di codice.

3. **Numeri "prima"** (commit `2447819`). Calcolo = eventi del ModelImporter × durata reale
   dello stato (clip ÷ speed); misura = Play Mode, `deltaTime` accumulato a passo fisso
   1/240 s (l'editor in background scende a ~10 fps e a quel ritmo la misura non vale).
   Lo scarto misurato è sempre +4…+8 ms: 1–2 passi da 4.2 ms, di cui uno dovuto ad
   `Animator.Play`. Le clip sono a 25 fps; tutte le durate sono già divise per la speed.

   | Stato | speed | Totale calc. / mis. | Startup calc. / mis. | Attiva calc. / mis. | Recovery calc. | Colpo dopo parte a (calc.) |
   |---|---|---|---|---|---|---|
   | `Punch_Right` | ×1.96 | 1.000 s (60 f) / 1.008 | 0.408 s (24 f) / 0.412 | 0.143 s (9 f) / 0.146 | 0.449 s (27 f) | 0.650 s (39 f) |
   | `Kick_Left` | ×1.96 | 1.000 s (60 f) / 1.008 | 0.347 s (21 f) / 0.354 | 0.072 s (4 f) / 0.071 | 0.581 s (35 f) | 0.650 s (39 f) |
   | `Punch_Left` | ×1.96 | 1.000 s (60 f) / 1.008 | 0.469 s (28 f) / 0.475 | 0.071 s (4 f) / 0.071 | 0.460 s (28 f) | 0.650 s (39 f) |
   | `Kick_Right` | ×1.96 | 1.000 s (60 f) / 1.008 | 0.531 s (32 f) / 0.537 | 0.063 s (4 f) / 0.062 | 0.406 s (24 f) | 0.650 s (39 f) |
   | `Slash` | ×1.87 | 1.050 s (63 f) / 1.058 | 0.463 s (28 f) / 0.471 | 0.060 s (4 f) / 0.058 | 0.527 s (32 f) | 0.683 s (41 f) |
   | `Slash_2` | ×1.87 | 1.050 s (63 f) / 1.058 | 0.536 s (32 f) / 0.542 | 0.107 s (6 f) / 0.108 | 0.407 s (24 f) | 0.683 s (41 f) |
   | `Sword_Pierce` | ×1.87 | 1.050 s (63 f) / 1.058 | 0.450 s (27 f) / 0.454 | 0.086 s (5 f) / 0.087 | 0.514 s (31 f) | 0.683 s (41 f) |
   | `Sword_Final` | ×1.10 | 1.050 s (63 f) / 1.058 | 0.328 s (20 f) / 0.333 | 0.229 s (14 f) / 0.229 | 0.493 s (30 f) | 0.683 s (41 f) |
   | `Area_Slash` | ×0.87 | 1.100 s (66 f) / 1.108 | 0.330 s (20 f) / 0.337 | 0.358 s (21 f) / 0.358 | 0.413 s (25 f) | — (nessuna catena) |

   - **Gate di combo, come funziona davvero** (misura su `Punch_Right`): il comando in coda
     viene accettato solo in recovery (da n=0.551), il codice lancia il trigger da n≥0.5,
     quindi il trigger parte a **n=0.554**; la transizione di catena ha exit time 0.65 e il
     colpo successivo inizia a **n=0.654**. Il gate a 0.5 sposta solo il momento del
     trigger, e solo dove la hitbox chiude prima di 0.5 (`Kick_Left` 0.42, `Slash` 0.50);
     l'inizio del colpo successivo lo decide sempre l'exit time: parte a 0.65 della clip: 0.650 s per pugni e calci,
     0.683 s per la spada (calcolo).
   - **Trigger dopo l'exit time = niente catena** (misura): con il gate spostato a 0.90
     il trigger arriva a n=0.904 e la transizione di catena non scatta più.
   - **Latenza** dal comando all'ingresso in `Punch_Right`: ~1 passo (misura); dal comando
     alla hitbox aperta: 0.412 s.
   - **Parata a mani nude** (`Block`, `Hand_parry` ×1.8, 0.644 s): finestra effettiva
     **0.329 s dal comando** (misura), chiusa da `OnParryEnd` a n=0.5 (0.322 s calc.
     dall'ingresso nello stato). `parryMaxWindow` sul prefab è **0.5 s** (lo script dice
     0.8) e fa solo da rete di sicurezza. Uscita da `Block` a n=0.85 → 0.547 s (calc.).
     `Sword_parry` ha gli stessi tempi.

   **Lunge** (non committato, misura in Play Mode con bersaglio fittizio, capsula 0.40):
   lunge 0.5 m senza ostacoli → 0.500 m, dritto; con bersaglio a 1.2 m → si ferma a
   0.900 m dal bersaglio; agganciato, bersaglio a 1.6 m a 45° → 0.610 m verso il
   bersaglio, fuori asse 0; bersaglio colpito durante il lunge con HitStop 0.3 s → il
   player si muove 2–4 mm in tutto il fermo immagine (un passo di quantizzazione), poi
   riprende. MoveData con lunge 0 → nessuno spostamento.

4. Aggiunto in testa a `CLAUDE.md` un rimando a questa cartella.
5. Cancellato `COMBAT_ANIMATION_REWRITE.md` dalla root (era la versione precedente di
   `PLAN.md`) e fatto commit `docs:` di `Docs/Combat/`, `Tools/Blender/` e del rimando in
   `CLAUDE.md`.

**Cose che non tornano fra CLAUDE.md e il progetto** (per quando aggiornerò CLAUDE.md in A.6):
startup del player 0.33–0.54 s e durata ~1.0 s (CLAUDE.md: 0.10–0.20 s e 0.42–0.74 s);
velocità sul prefab 2.8 / 7 (CLAUDE.md: 2.5 / 8.5); `HIT_REACTIONS.md` dice "nessuna riga
di codice" ma l'Opzione 1 esiste già (`HitImpactReaction`, agganciata in `Hitbox`); il
tooltip di `dashDuration` parla di Dash a speed 3.3, nel controller sono a 1.5.
Bug latente: `Health.Stagger` usa `animator?.SetTrigger`, che con un Animator mancante non
regge il null di Unity e lancia un'eccezione (l'ho trovato con il bersaglio di prova; nessun
personaggio vero oggi è senza Animator).

**Serve da te:** niente. Servono decisioni dell'utente sulle domande 3, 4 e 5. Mi fermo
prima di A.2 (prototipo `ActionPlayer`) finché non arrivano.

**File toccati:** `Docs/Combat/HANDOFF.md`, `CLAUDE.md`; cancellato
`COMBAT_ANIMATION_REWRITE.md`. Non committati: `Assets/Scripts/Player/PlayerAnimator.cs`,
`PlayerMovement.cs` (lunge), `Assets/Scripts/_TmpCombatTests/`.

## 2026-10-12 11:25 · animazione → codice

**Fatto:** decisioni dell'utente, già applicate a `PIPELINE.md`, `PLAN.md`, `MOVES.md` e
allo script Blender:
- Il `.blend` del Rabbi sta in `~/Desktop/Blender/Kosher fury/Rabbi rig/Rabbi_anim.blend`.
- Il `.blend` contiene **tutte** le animazioni del personaggio, non solo i colpi: il nome
  diventa `<Personaggio>_anim` (`Rabbi_anim.blend` → `Rabbi_anim.fbx` e
  `Rabbi_anim_events.json`). Niente più `_combat`.
- **La guardia c'è solo col lock.** Due locomozioni per stance: **libera** (`<Set>_Idle`,
  `<Set>_Walk`, `<Set>_Run`, solo in avanti) e **in guardia** (`<Set>_Guard_Idle`,
  `<Set>_Guard_Walk_F/B/L/R`, strafe). Il controller ridotto del piano deve tenerle
  entrambe, come oggi `Locomotion_Free` / `Locomotion`.
- `Sword_Idle` in `MOVES.md` diventa `Sword_Guard_Idle`. Gli attacchi partono e finiscono
  nella posa di guardia: senza lock il passaggio da e verso la locomozione libera lo fa il
  crossfade.

**Serve da te:** tenerne conto nell'architettura; niente altro.

**File toccati:** `Docs/Combat/PIPELINE.md`, `PLAN.md`, `MOVES.md`, `HANDOFF.md`,
`Tools/Blender/export_move_markers.py`.

## 2026-10-12 11:20 · animazione → codice

**Fatto:**
- Impostato il coordinamento: questa cartella (`README.md`, `PIPELINE.md`, `MOVES.md`,
  `HANDOFF.md`) e `Tools/Blender/export_move_markers.py`, provato su Blender 5.2 headless
  (esporta il JSON e segnala gli errori di convenzione).
- Il piano `COMBAT_ANIMATION_REWRITE.md` è diventato `Docs/Combat/PLAN.md`; la copia in
  root è superata. Novità
  rispetto al prompt originale: il sistema nuovo deve essere **generico** perché dopo il
  Rabbi si rifaranno **anche i nemici**.
- Scritte in `MOVES.md` le specifiche della fetta verticale della spada (`Sword_Idle`,
  `Sword_Light_1..3`, `Sword_Parry`, `Dodge_F`). L'utente le anima in parallelo alla tua
  Fase A.
- Verificato sui file: gli FBX di v1 e v2 sono a **25 fps** (TimeMode PAL). La nuova
  scena Blender è a 60.

**Serve da te:**
1. Leggi `README.md`, `PLAN.md` e `PIPELINE.md`. Da qui in poi segui il piano, non il
   primo prompt.
2. Rispondi alla domanda 3 con una proposta per l'utente.
3. Riporta qui sotto, in una tua voce, i numeri del Frame Data Report e della verifica
   in Play Mode (commit 2447819): durata, startup, finestra attiva, gate di combo e
   finestra di parata reali, per ogni mossa. Servono come riferimento "prima".
4. Aggiungi a `CLAUDE.md` un rimando breve a `Docs/Combat/README.md`, così ogni sessione
   futura parte dal protocollo.
5. Cancella `COMBAT_ANIMATION_REWRITE.md` dalla root (non è tracciato, e da qui non posso
   cancellare), poi fai commit di questa cartella e di `Tools/Blender/` in un commit
   `docs:` separato.

**File toccati:** `Docs/Combat/*`, `Tools/Blender/export_move_markers.py`;
`COMBAT_ANIMATION_REWRITE.md` copiato in `Docs/Combat/PLAN.md`.

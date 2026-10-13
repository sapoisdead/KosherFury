# Rifacimento animazioni e combattimento (prima il Rabbi, poi i nemici)

**Stato: piano, nessuna riga di codice scritta.** Scritto il 12 ottobre 2026 dopo aver
letto i file del progetto (FBX, .meta, controller, script). Dove questo documento e
CLAUDE.md non coincidono, i numeri qui vengono dai file; vanno comunque confermati in
Play Mode.

Il lavoro è diviso fra due sessioni (codice e animazione) che si coordinano con i file
di questa cartella: protocollo in `README.md`, contratto in `PIPELINE.md`, mosse in
`MOVES.md`, messaggi in `HANDOFF.md`.

---

## Perché rifare

Problemi verificati sui file:

1. **Le clip sono a 25 fps e accelerate nel controller.** Gli FBX hanno TimeMode PAL
   (25 fps). Gli stati le accelerano: pugni/calci ×1.96, spada ×1.87, Block ×1.8,
   Dash ×1.5, Take_hit ×5. Il timing animato non è quello di gioco.
2. **Colpi lenti.** Calcolo statico (clip ÷ velocità dello stato): i light durano
   ~1.0 s, con startup 0.35–0.53 s e finestra attiva 60–143 ms. CLAUDE.md dice startup
   0.10–0.20 s: dai file non torna.
3. **Tre gate della combo che non si parlano:** buffer d'input 0.25 s,
   `normalizedTime >= 0.5` in `PlayerAnimator`, `exitTime 0.65` sulle transizioni del
   controller.
4. **Fasi e hitbox legate ad animation event normalizzati** nel ModelImporter: si
   spostano se cambia la durata della clip e vanno rifatti a ogni reimport.
5. **Controller rigido:** 23 stati, 187 transizioni, 23 parametri. Ogni mossa nuova
   richiede ~6 transizioni a mano.
6. **Materiale misto:** dash e Sword_Final da v1, il resto da v2; locomozione Mixamo
   non in guardia; una sola hit reaction; il coltello usa le animazioni della spada.
7. **Impatto uguale per tutti i colpi:** le hitbox del player hanno gli stessi valori
   (hit stop 0.07, shake 0.55, knockback 1), e i danni stanno in array paralleli
   (`punchComboMultiplier`, `slashComboMultiplier`).

---

## Contratto da NON rompere

Fuori dal player si usa solo questo (verificato con grep):

| Cosa | Usato da |
|---|---|
| `CombatState`: `Phase`, `IsAttacking`, `IsArmored`, `IsPunishable`, `AttackId` | AttackPlayer, GuardianBT, GuardDuty, LeapAttack, EnemyAnimator, DemonValeAnimator, Health |
| `Health`: tutta l'API pubblica (`IsInvincible`, `IsBlocking`, `Stagger`, `IsVulnerableToExecution`, …) | nemici, Hitbox |
| `Hitbox` | condivisa con i nemici |
| `PlayerMovement.SetInputLocked` / `InputLocked` | PauseController, VanaBoss |

Tutto il resto del player si può riscrivere: `PlayerAnimator`, `Rabbi_AnimatorController`
e la logica d'attacco/parata/schivata dentro `PlayerMovement`. **Durante la Fase A il
codice e i controller dei nemici non si toccano**: verranno migrati nella Fase C.

---

## Architettura nuova

### 0. Un sistema per tutti i personaggi

Probabilmente si rifaranno **anche i nemici e tutte le loro animazioni**. Il sistema nasce
per il Rabbi ma **non deve contenere niente di specifico del player**:

- `ActionPlayer`, `MoveData`, `Moveset`, la macchina a stati delle azioni e l'importer
  dei marker sono **generici** e vanno in una cartella comune (es. `Scripts/Combat/Actions/`).
- La macchina a stati riceve **richieste** (`RequestMove`, `RequestParry`,
  `RequestDodge`…) e non sa chi le manda. Per il player le manda l'input
  (`PlayerInput` + buffer); per i nemici le manderà il behaviour tree al posto dei
  trigger dell'Animator.
- Tutto ciò che è del player (lock-on, buffer d'input, sprint, camera) resta fuori dal
  nucleo.
- Pipeline identica per ogni personaggio: un `.blend` per personaggio a 60 fps, stesso
  script per i marker, stesso importer.

**Ordine: prima il Rabbi, poi i nemici uno alla volta.** Finché un nemico non è migrato
gira sul sistema vecchio, quindi il contratto sotto resta valido per tutta la
transizione. `CombatState`, `Health` e `Hitbox` vanno tenuti come lingua comune:
il player nuovo li alimenta, i nemici vecchi e nuovi continuano a leggerli.

### 1. Mosse come dati

**`MoveData`** (ScriptableObject, una per mossa, in `Assets/ScriptableObjects/Moves/`):

- `clip`, `speed` (default 1: niente più moltiplicatori nascosti)
- tempi **in frame a 60 Hz**: `activeStart`, `activeEnd` (anche più finestre per mossa),
  `comboWindowStart`, `cancelStart` (da qui si può schivare/parare), `end`
- quali hitbox apre (mano dx/sx, piede dx/sx, arma)
- impatto: `damageMultiplier`, `hitStop`, `cameraShake`, `knockback`, `reactionType`
  (Light / Heavy / Launch / Knockdown)
- `lungeDistance` + `lungeCurve`: avanzamento in codice, tarabile senza ri-esportare
- `armorWhileActive` (oggi è sempre vero)
- collegamenti: `nextLight`, `nextHeavy`

**`Moveset`** (ScriptableObject, uno per arma): locomozione/guardia, catena light, heavy,
parata, contrattacco dopo parata, schivate, cambio arma. `WeaponData` punta al proprio
`Moveset`. Spada e coltello hanno moveset distinti.

Fasi e hitbox si aprono **dal tempo dello stato confrontato col MoveData**, non da
animation event. `CombatState` resta identico all'esterno: lo alimenta il nuovo player.

### 2. Riproduzione: Playables

Un `ActionPlayer` costruisce un `PlayableGraph` con:

- un `AnimatorControllerPlayable` con un controller **ridotto alla sola locomozione**,
  in due modi per ogni stance: **libera** (nessun lock: idle, camminata, corsa in avanti)
  e **in guardia** (lock attivo: blend tree 2D di strafe nelle quattro direzioni), come
  oggi `Locomotion_Free` / `Locomotion`;
- un mixer per le azioni con **due slot alternati**, per il crossfade fra un colpo e il
  successivo;
- (dopo) un layer additivo per le reazioni fisiche di `HIT_REACTIONS.md`.

Vantaggi: una mossa nuova è un asset, nessuna transizione; crossfade in secondi decisi
dal codice; hit stop gestibile sullo slot azione; il root motion continua a passare da
`OnAnimatorMove` (serve allo scatto).

Il layer `Weapon_Overlay` (maschera busto) sparisce: ogni stance ha le sue clip intere.

**Prima di migrare tutto serve un prototipo**: una mossa vecchia suonata dall'ActionPlayer,
con hitbox, CombatState e root motion dello scatto funzionanti. Se Playables crea
problemi, il ripiego è un controller con due stati generici `ActionA`/`ActionB` e
`AnimatorOverrideController`. Animancer fa lo stesso lavoro, ma è una dipendenza esterna:
solo se l'utente lo approva.

### 3. Una sola macchina a stati per input e combo

Stati: `Locomotion`, `Attack`, `Parry`, `Dodge`, `HitReaction`, `Dead`.

- Buffer d'input 0.25 s (tenerlo, funziona).
- Il colpo successivo parte **esattamente** a `comboWindowStart` se l'input è già in
  coda, o appena arriva se la finestra è aperta.
- Schivata e parata possono annullare un attacco da `cancelStart`.
- Niente più gate a 0.5 né exitTime.
- La parata separa **finestra di parata** (frame) e **recovery a vuoto** (se non ha parato
  niente), entrambi nel MoveData della parata.

### 4. Pipeline da Blender

Il contratto completo (nomi, marker, export, formato del JSON, import) è in
**`PIPELINE.md`**. In breve: un `.blend` per personaggio a 60 fps, un'action per mossa,
pose marker per le finestre, uno script (`Tools/Blender/export_move_markers.py`) che
scrive i marker in JSON dentro `Assets/`, e un importer Unity che li porta nei
`MoveData`. **I marker sono l'unica fonte dei tempi.**

v1 e v2 restano nel progetto finché ogni mossa non è sostituita.

---

## Moveset di partenza (frame a 60 fps, valori iniziali da tarare in gioco)

### Spada (stabilisce lo standard)

| Mossa | Startup | Attivi | Recovery | Combo da | Note |
|---|---|---|---|---|---|
| `Sword_Light_1` | 10 | 5 | 22 | 18 | diagonale dall'alto a destra |
| `Sword_Light_2` | 10 | 5 | 22 | 18 | ritorno, da sinistra |
| `Sword_Light_3` | 14 | 6 | 30 | — | finisher, knockback |
| `Sword_Heavy` | 22 | 7 | 32 | — | a due mani, hit stop forte |
| `Sword_Parry` | finestra 9 | — | 20 a vuoto | — | ~150 ms |
| `Sword_Riposte` | 8 | 6 | 24 | — | dopo una parata riuscita |

### Pugni e calci

| Mossa | Startup | Attivi | Recovery | Note |
|---|---|---|---|---|
| `Fists_Light_1..4` | 7 | 3 | 16 | jab, cross, gancio, calcio |
| `Fists_Heavy` | 18 | 5 | 28 | |
| `Fists_Parry` / `Fists_Riposte` | come la spada, più rapidi | | | |

### Coltello

| Mossa | Startup | Attivi | Recovery | Note |
|---|---|---|---|---|
| `Knife_Light_1..4` | 6 | 3 | 14 | impugnatura inversa, portata corta |
| `Knife_Heavy` | 14 | 4 | 26 | affondo |
| `Knife_Parry` / `Knife_Riposte` | finestra più stretta, riposta veloce | | | |

### Comuni

- `Dodge_F/B/L/R` (diagonali dopo): ~24 frame, invulnerabilità ~3–14
- `Hit_Light_F`, `Hit_Light_B`, `Hit_Heavy`, `Knockdown`, `GetUp`, `Death`
- `Weapon_Switch` (~8 frame)
- Salto in tre parti: `Jump_Start` (stacco, marker `liftoff` dove il codice dà la spinta),
  `Jump_Air` (in volo, ciclica: vale anche per le cadute), `Jump_Land` (atterraggio)
- Per ogni stance, locomozione libera: `<Set>_Idle`, `<Set>_Walk`, `<Set>_Run`
- Per ogni stance, in guardia (lock): `<Set>_Guard_Idle`, `<Set>_Guard_Walk_F/B/L/R`
  (stessa durata)

---

## Ordine di lavoro

**Fase A, codice (Claude Code), un checkpoint per punto:**

1. Report di frame data dello stato attuale + misura in Play Mode (riferimento prima/dopo).
   _Fatto nel commit 2447819; i numeri vanno riportati in `HANDOFF.md`._
2. Prototipo `ActionPlayer` con una clip vecchia → **stop, conferma dell'utente**.
3. `MoveData`, `Moveset`, macchina a stati, lunge, nuova parata.
4. Migrazione delle mosse **esistenti** sul sistema nuovo con le clip vecchie e i loro
   tempi attuali, così il gioco resta giocabile durante il rifacimento.
5. Importer dei marker da JSON.
6. Aggiornamento di CLAUDE.md.

**Priorità decisa il 13 ottobre:** subito dopo la fetta verticale della spada vengono
**corsa, strafe e salto** (sono le animazioni che si vedono di più), poi il resto.

**Fase B, animazione (Blender), in parallelo alla Fase A.** Le mosse esportate si
integrano quando esistono il nuovo sistema e l'importer (A.3 e A.5). Fetta verticale
della spada
(`Sword_Guard_Idle`, `Sword_Light_1..3`, `Sword_Parry`, `Dodge_F`) → prova in gioco → resto
della spada → hit reaction → pugni → coltello → locomozione.

**Fase C, nemici:** migrazione al sistema comune un nemico alla volta (prima il simp, che
è il più diffuso), con animazioni rifatte allo stesso modo; il behaviour tree passa dai
trigger dell'Animator alle richieste alla macchina a stati. Insieme: parata da parte dei
nemici e ritaratura dell'IA (ritardo di reazione, punizione, schivata reattiva), oggi
tarata sui tempi attuali del player.

---

## Rischi noti

- L'IA dei simp è tarata sui tempi di oggi: cambiando startup e recovery del player
  cambiano punizioni e portate. Rimisurare dopo la Fase A.4 e dopo ogni gruppo di clip.
- Il Humanoid porta in Unity solo rotazioni (più l'hips): niente stretch, l'esagerazione
  va fatta col corpo intero.
- Rig Mixamo: niente *Paste Pose Flipped* senza una mappatura delle ossa.

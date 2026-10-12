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
| 1 | _(risolta: percorso del `.blend` annotato in `PIPELINE.md`)_ | — | — |
| 2 | Avatar delle clip nuove: *Copy From Other Avatar* da `Rabbi_rigged` o *Create From This Model*? Decidere al primo import e annotarlo in `PIPELINE.md`. | codice | animazione |
| 3 | _(risolta 11:35: scartare `8bf3b91` e `d9efa2e`)_ | — | — |
| 4 | _(risolta 11:35: niente `reachMultiplier`, solo misura)_ | — | — |
| 5 | _(risolta 11:35: lunge scartato, si rifà nell'`ActionPlayer`)_ | — | — |

---

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

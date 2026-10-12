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
| 2 | _(risolta 12:41: *Copy From Other Avatar* da `Rabbi_rigged`, annotato in `PIPELINE.md`)_ | — | — |
| 3 | _(risolta 11:35: scartare `8bf3b91` e `d9efa2e`)_ | — | — |
| 4 | _(risolta 11:35: niente `reachMultiplier`, solo misura)_ | — | — |
| 5 | _(risolta 11:35: lunge scartato, si rifà nell'`ActionPlayer`)_ | — | — |
| 6 | _(risolta 15:56: compressione **Off** per tutte le clip, annotato in `PIPELINE.md`)_ | — | — |
| 7 | _(risolta 12:43: la **spada** è `AbramKnife` (1.06 m), il **coltello** è `Izmel` (0.54 m))_ | — | — |
| 8 | _(risolta 12:43: vale l'altezza del modello originale, **1.60 m** come in Unity; Blender si adegua)_ | — | — |

---

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

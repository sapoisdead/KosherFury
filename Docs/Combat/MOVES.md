# Registro delle mosse

Stati e regole in `README.md`. La **Specifica** la scrive la sessione animazione, la
sezione **Integrazione** la sessione codice. Tempi in frame a 60 fps; i valori di
specifica sono **obiettivi** da tarare in gioco.

Legenda delle colonne di frame: startup = frame prima di `hit_start`; attivi =
`hit_end − hit_start`; recovery = da `hit_end` alla fine della clip.

## Riepilogo

| Mossa | Personaggio | Stato | Prossimo passo |
|---|---|---|---|
| `Sword_Guard_Idle` | Rabbi | chiusa | approvata dall'utente in gioco (14 ottobre) |
| `Sword_Light_1` | Rabbi | chiusa | approvata dall'utente in gioco (14 ottobre) |
| `Sword_Light_2` | Rabbi | chiusa | approvata dall'utente in gioco (14 ottobre) |
| `Sword_Light_3` | Rabbi | chiusa | approvata dall'utente in gioco (14 ottobre) |
| `Sword_Parry` | Rabbi | chiusa | approvata dall'utente in gioco (14 ottobre) |
| `Dodge_F` | Rabbi | in prova | utente: forse troppo veloce, da rivedere più avanti |

**Ordine dei lavori** (13 ottobre):
1. Fetta verticale della spada: `Sword_Guard_Idle` ✔, `Sword_Light_1` ✔, `Sword_Light_2` ✔,
   `Sword_Light_3`, `Sword_Parry`, `Dodge_F`, poi prova completa in gioco.
2. **Locomozione e salto** (richiesti dall'utente come minimo indispensabile): strafe in
   guardia `Sword_Guard_Walk_F/B/L/R`; in comune a tutte le armi `Walk`, `Run`,
   `Jump_Start` / `Jump_Air` / `Jump_Land`; per la spada `Sword_Idle`,
   `Sword_Arms_Walk`, `Sword_Arms_Run`.
3. Resto della spada: `Sword_Riposte`, `Sword_Heavy`, `Dodge_B/L/R`.
4. Reazioni: `Hit_Light_F/B`, `Hit_Heavy`, `Knockdown`, `GetUp`, `Death`.
5. Pugni, poi coltello, poi i nemici.

**Velocità di locomozione** (decisioni del 14 ottobre): camminata **1.2 m/s** (anche col
lock), corsa **5.0 m/s**. `Run`: ciclo di **24 frame**, passo 1.0 m, il piede a terra scorre
di 8.3 cm a frame; esportata il 14/10, in prova. `Walk`: **camminata vera**, sempre almeno
un piede a terra, ciclo di **30 frame**, passo 0.30 m (4 passi al secondo), 18 frame a terra
per piede con rullata tallone → punta. Con gambe di 45 cm una camminata più veloce non regge:
la corsetta a 2.4 m/s è stata provata e scartata dall'utente. Se in gioco la camminata è
lenta, si può alzare fino a ~1.4 m/s e la clip accelera in proporzione. Strafe col lock
`Sword_Guard_Walk_F/B/L/R`: **0.8 m/s**, passo accostato (prima il piede dal lato in cui si
va), ciclo di 24 frame, passo 0.32 m per piede, busto e braccia della guardia. Generatori di base:
`Tools/Blender/create_run.py`, `create_walk.py`.

**Nota sui tempi:** dopo la prova di `Sword_Light_1` i tempi di specifica delle altre
mosse (37–50 frame) vanno rivisti con lo stesso criterio prima di animarle.

Fetta verticale della spada: queste sei mosse bastano per provare il sistema nuovo da
capo a fondo (attacco, combo, parata, schivata). Le altre si aggiungono qui quando
tocca a loro (elenco completo in `PLAN.md`).

---

## Sword_Guard_Idle

**Stato:** esportata (12 ottobre, 12:30). Posa chiave approvata dall'utente: più eretta della
specifica, spada verticale vicino al corpo. Da verificare in gioco la leggibilità dalla camera alle spalle.

### Specifica
- Guardia con la spada a nemico agganciato, ciclica. Durata 60–90 frame.
- Non è l'idle libero (`Sword_Idle`, senza lock), che si farà con la locomozione.
- **Fatta:** 72 frame (0–72, 1.2 s), ciclica. Respiro: bacino −1 cm al frame 36, petto,
  testa e spada sfasati di +3/+6/+8 frame.
- Deve leggersi diversa dalla guardia a mani nude anche in silhouette.
- È la posa di partenza e di arrivo di tutte le mosse della spada: va salvata come asset
  `Pose_Guard_Sword` nella Pose Library.

### Integrazione
Primo import, 12 ottobre 12:41 (sessione codice). Non ancora integrata: il sistema nuovo
non esiste, l'anteprima in gioco è un override in memoria valido solo in Play Mode.
- Import: `Human`, avatar copiato da `Rabbi_riggedAvatar`, take `Armature|Sword_Guard_Idle`
  → clip `Sword_Guard_Idle`, frame 0–72, 1.200 s a 60 fps, loop attivo, bake into pose
  su XZ/Y/rotazione. Nessun errore né avviso del rig.
- Misure (campionata su `Rabbi_rigged` in editor):
  - loop: scarto 0.000 fra primo e ultimo frame su tutte le 130 curve;
  - dita: 40 curve, tutte con valori;
  - scala: cima della mesh a 1.54 m (1.60 in posa di riposo), coerente col Rabbi in gioco;
  - respiro: bacino da 0.519 a 0.510 m fra frame 0 e 36 (−0.9 cm, come da specifica);
  - piedi: il vertice più basso sta **1.5 cm sotto** il terreno (l'idle di oggi sta 1.2 cm
    sopra);
  - orientamento: il bacino è ruotato di **30°** rispetto all'avanti del personaggio
    (guardia di tre quarti). Se non è voluto, la mossa va girata in Blender.
- Unity applica la compressione *Keyframe Reduction* di default (curve ridotte al 4.7%).
  Per un idle va bene; per i colpi proporrò di spegnerla (vedi `HANDOFF.md`).
- 12:43: **animazione approvata dall'utente** in gioco (anteprima col lock, spada in
  mano). Bacino a 30° e piedi −1.5 cm restano così.

---

## Sword_Light_1

**Stato:** esportata rifinita (16:53). Carica tenuta e scatto ok. Da valutare in gioco: impatto
poco tenuto (la mano scende ancora a 5 m/s fra 20 e 24) e piede sinistro che arretra fra 21 e 24.

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 18 | 6 | 30 | 28 | 34 | 54 |

Rivista il 12 ottobre dopo la prova del blocking: a 37 frame "si appiattisce tutto"
(utente). Pose: 0 guardia, 10 carica, 14 carica tenuta, 18 ingresso, 20 impatto,
22 impatto tenuto, 24 uscita, 28 fine corsa, 40 recupero, 54 guardia. Lo scatto
fra carica e ingresso resta di 4 frame: il contrasto lento/veloce dà il peso.

- Fendente diagonale dall'alto a destra verso il basso a sinistra.
- Parte da `Pose_Guard_Sword` e finisce in una posa da cui parte `Sword_Light_2`.
- Hitbox: arma (`AbramKnife`, capsula r 0.186 che arriva a 0.873 m dal palmo).
- **Portata obiettivo: 1.40 m fra i centri** (oggi `Slash` entra a 1.43, misura 12:38).
  In Blender: la lama deve attraversare `REF_Target` (a 1.40 m) fra `hit_start` e
  `hit_end`, all'altezza del petto.
- **Sul posto**: il passo avanti c'è (piede sinistro +10–15 cm, bacino +5–8 cm), ma la
  clip è in bake into pose, quindi l'avanzamento vero lo fa il lunge nel codice. Se la
  catena non prosegue, nel recovery i piedi tornano nella posizione della guardia.
- Posa chiave a `combo` (frame 18) = posa di partenza di `Sword_Light_2`: spada bassa a
  sinistra, già caricata per il rovescio.
- Impatto proposto: hit stop 0.06 s, shake 0.25, knockback 1.0, reazione Light.
- Lunge proposto: da decidere dopo la prova della portata al bersaglio di riferimento.

### Integrazione
Blocking importato il 12 ottobre alle 15:56 (sessione codice). Suonato dal prototipo
`ActionPlayer` (Quadrato / P con un'arma in mano), finestra dell'arma 10–15 letta a mano
dal JSON, danno ×2 come la `Slash` di oggi, HitStop attivo, niente lunge. Nessun `MoveData`
ancora (A.3).
- Import: take `Armature|Sword_Light_1` → clip `Sword_Light_1`, 0–37 (0.617 s), niente
  loop, compressione Off, avatar di `Rabbi_rigged`.
- Misure in Play Mode (bersaglio con la capsula del simp, `AbramKnife`, passo 1/240 s):

  | Bersaglio a | Colpito | Frame del contatto | Centro della hitbox da terra |
  |---|---|---|---|
  | 1.00 m | sì | 10.25 | 1.11 m |
  | 1.20 m | sì | 12.25 | 0.80 m |
  | 1.40 m | sì | 12.00 | 0.84 m |

  **Portata: 1.675 m fra i centri** (ricerca binaria, ±6 mm). Obiettivo 1.40 m: supera
  l'obiettivo di 27 cm. La `Slash` di oggi con la stessa arma entra a 1.431 m.
  Il contatto cade sempre dentro la finestra. A 1.20–1.40 m arriva al frame 12 con il
  centro della hitbox a 0.80–0.84 m da terra: è altezza di cintura più che di petto (il
  simp è alto 1.80). La capsula ha raggio 0.186, quindi tocca dai 0.65 ai 1.02 m circa.
- Raccordi (campionati in editor su `Rabbi_rigged`): primo e ultimo frame identici a
  `Sword_Guard_Idle` frame 0, con scarto 0.0° e 0.0 cm su tutte le ossa. Quindi nessuno
  scatto all'ingresso né al ritorno.
- Mano destra al frame 12: 0.53 m avanti e 0.55 m da terra, come misurato in Blender.

**Versione a 54 frame (export delle 16:09–16:10), integrata alle 16:22.** I tempi ora
li scrive l'importer dei marker, non più io a mano: finestra arma 18–24, `combo` 28,
`cancel` 34, fine 54. È il primo `MoveData` vero (`Assets/ScriptableObjects/Moves/
Sword_Light_1.asset`, nel `Moveset_Sword` di `AbramKnife`), suonato dalla macchina a stati
di A.3. Impatto: hit stop 0.06, shake 0.25, knockback 1.0, danno ×2. Niente lunge.

| Bersaglio a | Colpito | Frame del contatto | Centro della hitbox da terra |
|---|---|---|---|
| 1.00 m | sì | 19 | 1.18 m |
| 1.20 m | sì | 20 | 1.08 m |
| 1.40 m | sì | 21 | 0.97 m |

- **Portata: 1.625 m fra i centri** (misura, ±6 mm). Obiettivo 1.40.
- Contatto sempre dentro la finestra 18–24.
- Raccordi: primo e ultimo frame identici a `Sword_Guard_Idle` (0.0°, 0.0 cm).
- Mano destra al frame 21: 0.53 m avanti, 0.59 m da terra.
- Catena (con un `nextLight` di prova): il colpo successivo parte esattamente al frame 28.

**Export rifinito (16:53) e hitbox sottile, misurati il 13 ottobre alle 14:10.** Capsula
della spada a raggio 0.06, dall'elsa alla punta. Portata **1.594 m**; contatto a 1.20–1.40 m
al frame 21, con il centro della hitbox a 0.91 m da terra. All'impatto (frame 20) la punta
sta a 0.89 m dal centro del Rabbi e a 1.36 m d'altezza. Ai frame 22–23 l'accompagnamento la
porta a 1.135–1.235 m, più in basso (1.07–0.82 m), ed è da lì che viene la portata. Dettaglio
frame per frame in `HANDOFF.md` (voce 13 ottobre, 14:10). Avviso d'import: chiave di scala su
`mixamorig:RightFoot`, scartata da Unity.

---

## Sword_Light_2

**Stato:** esportata (13 ottobre, 14:52), senza posa di recupero intermedia (scelta dell'utente). Raccordi a zero.
Da valutare in gioco: fine corsa (20–24) più veloce dello scatto, piedi che tornano in guardia in 4 frame.

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 14 | 6 | 30 | 24 | 30 | 50 |

- **Rovescio**: fendente orizzontale da sinistra verso destra, all'altezza del petto del
  bersaglio.
- **Frame 0 = posa di `Sword_Light_1` al frame 28** (`combo`), copiata identica, controlli
  dell'IK compresi: la catena parte da lì e il raccordo deve essere perfetto.
- Startup più corto del primo colpo: la fine corsa di `Sword_Light_1` è già una carica.
- Pose: 0 partenza, 6 carica (busto ancora più girato a sinistra, lama dietro l'anca
  sinistra), 10 carica tenuta, 14 ingresso, 16 impatto, 18 impatto tenuto, 20 uscita,
  24 fine corsa, 36 recupero, 50 guardia.
- Posa a `combo` (frame 24) = partenza di `Sword_Light_3`: spada alta sopra la spalla
  destra, pronta per il colpo verticale del finisher.
- Hitbox: arma. Impatto proposto come `Sword_Light_1`. Portata: come `Sword_Light_1`
  (un fendente orizzontale va di lato, non in avanti, quindi non dovrebbe allungarla).

### Integrazione
Integrata il 13 ottobre alle 15:10. Nella catena della spada dopo `Sword_Light_1`, prima dei
fendenti vecchi 3 e 4. Tempi dall'importer: finestra 14–20, combo 24, cancel 30, fine 50.
Impatto come `Sword_Light_1`, danno ×2, niente lunge.
- **Portata 1.456 m**; contatto ai frame 14–17.75, centro della hitbox a 0.87–0.88 m da terra.
- Punta nella finestra: arco orizzontale a 1.01–1.04 m dal centro, alta 0.95–0.97 m.
- Passaggio da `Sword_Light_1` in gioco: nessuno scatto (14.7° al cambio, contro 22° dei
  frame prima).
- Da guardare: anulare destro che ruota di 48° in un frame ai frame 2–3.

---

## Sword_Light_3

**Stato:** esportata (13 ottobre, 15:37). Parte dal frame 26 di `Sword_Light_2` (non dal 24): proposto `combo` di `_2` a 26.

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 18 | 6 | 36 | — | 36 | 60 |

- **Finisher**: fendente verticale dall'alto verso il basso, con passo avanti marcato.
  Chiude la catena: niente `combo`.
- **Frame 0 = posa di `Sword_Light_2` al frame 24** (`combo`), copiata identica, controlli
  dell'IK compresi.
- Più pesante dei primi due: carica più alta e tenuta più a lungo, impatto tenuto più a
  lungo, recupero lento.
- Pose: 0 partenza, 8 carica (spada alta sopra la testa, busto inarcato indietro, peso
  dietro), 14 carica tenuta, 18 ingresso (passo avanti del piede sinistro), 20 impatto
  (lama all'altezza della testa/petto del bersaglio), 24 uscita (lama in basso davanti),
  40 recupero, 60 guardia.
- Dopo l'impatto la lama **scende**, non va avanti: la punta non deve superare l'anello
  azzurro (1.00 m), per tenere la portata come gli altri colpi.
- Hitbox: arma. Impatto proposto: hit stop 0.10 s, shake 0.45, knockback 2.0, reazione
  Heavy.

### Integrazione
Integrata il 13 ottobre alle 16:05: terzo colpo della catena della spada, che chiude
(nessun combo). Tempi dall'importer: finestra 18–24, cancel 36, fine 60. Impatto: hit stop
0.10, shake 0.45, knockback 2.0, Heavy, danno ×2.5.
- **Portata 1.463 m**; contatto ai frame 19–21, a 0.73–1.09 m da terra.
- Dopo l'impatto la punta scende avanti fino a 1.15 m dal centro (frame 22).
- Raccordo da `Sword_Light_2` al frame 26 perfetto nei dati, pulito in gioco (4–9°).
- **Torsione dell'avambraccio destro fuori limite** (muscolo fino a 2.61): ribaltamento di
  165° al frame 18. Dettaglio in `HANDOFF.md`, voce 13 ottobre 16:05.

---

## Sword_Parry

**Stato:** esportata (13 ottobre, 16:10). Spada su in 2 frame; durante la finestra la mano scende
sotto la guardia (da 0.92 a 0.66 m); mano sinistra non sulla lama.

### Specifica
| Prima della finestra | Finestra (`parry_start`→`parry_end`) | Dopo | Totale |
|---|---|---|---|
| 2 | 12 (200 ms) | 16 | 30 |

- La guardia scatta subito: il primo frame dopo il comando deve già muoversi.
- Riferimento "prima" (misura): la parata di oggi dura 0.329 s dal comando. Si parte
  da 200 ms e si stringe in gioco se è troppo facile.
- Recovery a vuoto proposto: 20 frame (nel `MoveData`, si applica solo se la parata
  non ha intercettato niente).
- Riposta dopo parata riuscita: `Sword_Riposte`, più avanti.

### Integrazione
Integrata il 13 ottobre alle 16:53 come parata della spada. Tempi dall'importer: finestra
2–14, fine 30. Recupero a vuoto 20 frame, niente cancel; dopo una parata riuscita si attacca
subito. Collaudo col Parry Trainer: 4 parate su 4 contro colpi veri, il simp resta stordito;
recupero a vuoto misurato 19.5 frame.

---

## Dodge_F

**Stato:** esportata (14 ottobre, 09:50). Root motion 2.5 m. Da valutare: rinculo di 10 cm e piedi
che strisciano fra i frame 18 e 24.

### Specifica
| Totale | `iframe_start` | `iframe_end` | `cancel` |
|---|---|---|---|
| 24 | 3 | 14 | 18 |

- Scatto in avanti con root motion (XZ libero all'import).
- Comune a tutte le armi; la posa d'arrivo deve funzionare con qualunque guardia.
- Le altre direzioni (`Dodge_B/L/R`) dopo la prova di questa.

### Integrazione
Integrata il 14 ottobre alle 10:10 come schivata in avanti per tutte le armi, root motion
senza scala. In gioco: 2.518 m, invulnerabile dal frame 3 al 13, rinculo finale 4.6 cm (8.8
cm nella clip, ridotto dal crossfade d'uscita). A ×0.7: stessa distanza in 35 frame.

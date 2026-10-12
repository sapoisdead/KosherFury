# Registro delle mosse

Stati e regole in `README.md`. La **Specifica** la scrive la sessione animazione, la
sezione **Integrazione** la sessione codice. Tempi in frame a 60 fps; i valori di
specifica sono **obiettivi** da tarare in gioco.

Legenda delle colonne di frame: startup = frame prima di `hit_start`; attivi =
`hit_end − hit_start`; recovery = da `hit_end` alla fine della clip.

## Riepilogo

| Mossa | Personaggio | Stato | Prossimo passo |
|---|---|---|---|
| `Sword_Guard_Idle` | Rabbi | esportata | approvata in gioco dall'utente; codice: integrazione col sistema nuovo (A.3/A.4) |
| `Sword_Light_1` | Rabbi | esportata (blocking, 54 f) | codice: prova del nuovo timing in gioco |
| `Sword_Light_2` | Rabbi | spec | animazione |
| `Sword_Light_3` | Rabbi | spec | animazione |
| `Sword_Parry` | Rabbi | spec | animazione |
| `Dodge_F` | Rabbi | spec | animazione |

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

**Stato:** esportata come blocking a 54 frame (16:10). Da fare: pose tenute ai frame 14 e 22, piede sinistro piantato dopo l'impatto, impatto più alto e meno lontano (portata verso 1.40 m).

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

---

## Sword_Light_2

**Stato:** spec

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 10 | 5 | 22 | 18 | 23 | 37 |

- Ritorno: fendente da sinistra verso destra, più orizzontale.
- Parte dalla posa finale di `Sword_Light_1`, finisce in una posa da cui parte
  `Sword_Light_3`.
- Hitbox: arma.
- Impatto proposto: come `Sword_Light_1`.

### Integrazione
_vuota_

---

## Sword_Light_3

**Stato:** spec

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 14 | 6 | 30 | — | 32 | 50 |

- Finisher: colpo verticale o affondo, con passo avanti marcato.
- Finisce in `Pose_Guard_Sword`.
- Hitbox: arma.
- Impatto proposto: hit stop 0.10 s, shake 0.45, knockback 2.0, reazione Heavy.

### Integrazione
_vuota_

---

## Sword_Parry

**Stato:** spec

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
_vuota_

---

## Dodge_F

**Stato:** spec

### Specifica
| Totale | `iframe_start` | `iframe_end` | `cancel` |
|---|---|---|---|
| 24 | 3 | 14 | 18 |

- Scatto in avanti con root motion (XZ libero all'import).
- Comune a tutte le armi; la posa d'arrivo deve funzionare con qualunque guardia.
- Le altre direzioni (`Dodge_B/L/R`) dopo la prova di questa.

### Integrazione
_vuota_

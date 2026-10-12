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
| `Sword_Light_1` | Rabbi | spec | animazione |
| `Sword_Light_2` | Rabbi | spec | animazione |
| `Sword_Light_3` | Rabbi | spec | animazione |
| `Sword_Parry` | Rabbi | spec | animazione |
| `Dodge_F` | Rabbi | spec | animazione |

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

**Stato:** spec

### Specifica
| Startup | Attivi | Recovery | `combo` | `cancel` | Totale |
|---|---|---|---|---|---|
| 10 | 5 | 22 | 18 | 23 | 37 |

- Fendente diagonale dall'alto a destra verso il basso a sinistra.
- Parte da `Pose_Guard_Sword` e finisce in una posa da cui parte `Sword_Light_2`.
- Hitbox: arma.
- Impatto proposto: hit stop 0.06 s, shake 0.25, knockback 1.0, reazione Light.
- Lunge proposto: da decidere dopo la prova della portata al bersaglio di riferimento.

### Integrazione
_vuota_

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

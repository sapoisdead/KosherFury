# Registro delle mosse

Stati e regole in `README.md`. La **Specifica** la scrive la sessione animazione, la
sezione **Integrazione** la sessione codice. Tempi in frame a 60 fps; i valori di
specifica sono **obiettivi** da tarare in gioco.

Legenda delle colonne di frame: startup = frame prima di `hit_start`; attivi =
`hit_end − hit_start`; recovery = da `hit_end` alla fine della clip.

## Riepilogo

| Mossa | Personaggio | Stato | Prossimo passo |
|---|---|---|---|
| `Sword_Guard_Idle` | Rabbi | spec | animazione: guardia della spada (lock) |
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

**Stato:** spec

### Specifica
- Guardia con la spada a nemico agganciato, ciclica. Durata 60–90 frame.
- Non è l'idle libero (`Sword_Idle`, senza lock), che si farà con la locomozione.
- Deve leggersi diversa dalla guardia a mani nude anche in silhouette.
- È la posa di partenza e di arrivo di tutte le mosse della spada: va salvata come asset
  `Pose_Guard_Sword` nella Pose Library.

### Integrazione
_vuota_

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
| 2 | 9 (~150 ms) | 19 | 30 |

- La guardia scatta subito: il primo frame dopo il comando deve già muoversi.
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

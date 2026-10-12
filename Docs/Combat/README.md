# Coordinamento: animazione ↔ codice

Il rifacimento del combattimento è diviso fra due sessioni Claude che **non possono
parlarsi direttamente**. Comunicano solo tramite i file di questa cartella, e l'utente
fa da postino: quando una sessione scrive un handoff, glielo dice all'altra.

| Chi | Dove | Cosa fa |
|---|---|---|
| **Sessione animazione** | claude.ai (legge e scrive questa cartella tramite il computer dell'utente) | guide per animare in Blender, specifiche delle mosse, script Blender, revisione degli export |
| **Sessione codice** | Claude Code nel progetto Unity | sistema di combattimento, import delle clip, `MoveData`, misure in Play Mode, commit |
| **Utente** | Blender e Unity | anima, esporta, prova in gioco, decide; porta gli handoff da una sessione all'altra |

## File

| File | Contenuto | Chi lo scrive |
|---|---|---|
| `README.md` | questo protocollo | modifiche solo con l'ok dell'utente |
| `PLAN.md` | il piano del rifacimento (architettura, fasi, rischi) | codice per l'architettura, animazione per il moveset; il resto con l'ok dell'utente |
| `PIPELINE.md` | contratto Blender → Unity: fps, nomi, marker, export, JSON | modifiche solo con l'ok dell'utente |
| `MOVES.md` | registro delle mosse: stato, specifica, misure | **Specifica**: animazione. **Integrazione**: codice. **Stato**: chi fa avanzare la mossa |
| `HANDOFF.md` | bacheca: messaggi fra le sessioni, domande aperte | entrambe, solo aggiungendo voci |
| `../../Tools/Blender/` | script Blender (export dei marker) | animazione |

## Ciclo di vita di una mossa

```
spec → in lavorazione → esportata → integrata → in prova → chiusa
              ↑                                     │
              └──────────── feedback ───────────────┘
```

| Stato | Chi lo imposta | Quando |
|---|---|---|
| `spec` | animazione | specifica scritta in `MOVES.md` |
| `in lavorazione` | animazione | l'utente sta animando in Blender |
| `esportata` | animazione | FBX e JSON nel progetto, verificati contro la specifica |
| `integrata` | codice | `MoveData` aggiornato, mossa giocabile, misure scritte in `MOVES.md` |
| `in prova` | codice | consegnata all'utente da provare in gioco |
| `chiusa` | utente | va bene così |

Un problema rilevato da una delle due sessioni (la mano non arriva al bersaglio, un
marker in un punto che non funziona in gioco, un timing che non regge) si scrive in
`HANDOFF.md` e la mossa torna `in lavorazione`.

## Regole

1. **All'inizio** di ogni sessione, e quando l'utente dice "c'è un handoff", leggere
   `HANDOFF.md` (dall'alto: le voci nuove sono in cima) e `MOVES.md`.
2. **Prima di chiudere** un blocco di lavoro, scrivere una voce in `HANDOFF.md`, anche
   quando non serve niente all'altra sessione.
3. **Ognuno scrive solo le proprie parti.** La sessione animazione non tocca `Assets/`
   (tranne i file che produce l'export: FBX e JSON dei marker). La sessione codice non
   cambia le specifiche in `MOVES.md` né gli script in `Tools/Blender/`: se servono
   modifiche le chiede in `HANDOFF.md`.
4. **Le decisioni le prende l'utente.** Una sessione che dissente da una specifica o dal
   contratto lo scrive in `HANDOFF.md` come domanda aperta, e non procede da sola.
5. **Commit: solo la sessione codice (o l'utente).** La sessione animazione scrive i
   file nel working tree; la sessione codice li include in commit `docs:` separati dal
   codice.
6. **Numeri e fonti**: ogni numero scritto in questi file dice se è un **obiettivo**,
   un **calcolo** dai file o una **misura** in Play Mode.

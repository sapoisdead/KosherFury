# Pipeline Blender → Unity (contratto)

È l'interfaccia fra la **sessione animazione** (Blender) e la **sessione codice**
(Claude Code in Unity). Vale per tutti i personaggi: prima il Rabbi, poi i nemici.

**Questo file si cambia solo con l'ok dell'utente.** Chi vuole cambiarlo scrive la
proposta in `HANDOFF.md`; dopo l'ok, chi l'ha proposta aggiorna questo file e lo annota
nella voce di handoff.

---

## Blender

- **Un `.blend` per personaggio** con **tutte** le sue animazioni (colpi, locomozione,
  reazioni, morte): `<Personaggio>_anim.blend` (es. `Rabbi_anim.blend`).
  Posizione del Rabbi: `~/Desktop/Blender/Kosher fury/Rabbi rig/Rabbi_anim.blend`.
  Fuori dal repo, quindi non versionato: tenere attivo *Save Versions* nelle preferenze di Blender.
- **Scena a 60 fps** (Output › Format › Frame Rate = 60, Base 1). Unit Scale 1, metrico.
- **Un'action per mossa.** Il nome dell'action è anche il nome della clip in Unity e del
  `MoveData`. Non si rinomina dopo il primo export.
  - Mosse di un'arma: `<Set>_<Mossa>[_<N>]`, con set `Fists`, `Sword` o `Knife`.
    Esempi: `Sword_Light_1`, `Sword_Heavy`, `Fists_Parry`, `Knife_Riposte`.
  - Locomozione **libera** (nessun nemico agganciato, solo in avanti): `<Set>_Idle`,
    `<Set>_Walk`, `<Set>_Run`.
  - Locomozione **in guardia** (nemico agganciato, strafe): `<Set>_Guard_Idle`,
    `<Set>_Guard_Walk_F/B/L/R` (le quattro con la **stessa durata**).
  - Le mosse d'attacco partono e finiscono nella posa di `<Set>_Guard_Idle`.
  - Mosse comuni a tutte le armi: senza prefisso. Esempi: `Dodge_F`, `Hit_Light_F`,
    `Hit_Heavy`, `Knockdown`, `GetUp`, `Death`, `Weapon_Switch`.
  - Niente spazi, niente suffissi `.001`.
- **Ogni action**: Fake User attivo, **Manual Frame Range** impostato (il primo frame del
  range è il tempo 0 della clip in Unity), **Cyclic Animation** solo per i loop.

## Marker

Pose marker dentro l'action (Action Editor › View › Show Pose Markers;
Marker › Add Pose Marker). Una finestra va da `*_start` **incluso** a `*_end` **escluso**.
In secondi: `(frame − frame_start) / fps`.

| Marker | Significato | Serve per |
|---|---|---|
| `hit_start`, `hit_end` | prima finestra attiva: hitbox aperte, fase Active | ogni attacco |
| `hit2_start`, `hit2_end`, `hit3_…` | finestre successive nella stessa mossa | colpi multipli |
| `combo` | da qui parte il colpo successivo, se è già in coda | attacchi in catena |
| `cancel` | da qui schivata e parata annullano la mossa | facoltativo: senza, la mossa non è annullabile |
| `parry_start`, `parry_end` | finestra di parata | parate |
| `iframe_start`, `iframe_end` | invulnerabilità | schivate |

Cosa **non** sta nei marker (sta nel `MoveData` in Unity, e la proposta iniziale sta in
`MOVES.md`): quali hitbox si aprono, danno, impatto, lunge, collegamenti fra mosse.

## Export

1. **Marker** → script `Tools/Blender/export_move_markers.py` (da aprire nel Text Editor
   di Blender e lanciare con Run Script). Scrive
   `Assets/Art/<Personaggio>/Animations/<Personaggio>_anim_events.json` e segnala in
   console gli errori di convenzione.
2. **FBX** → `Assets/Art/<Personaggio>/Animations/<Personaggio>_anim.fbx`. Impostazioni:
   - Bake Animation attivo: **All Actions** sì, **NLA Strips** no (con entrambi le clip
     si duplicano), **Force Start/End Keying** sì, **Simplify 0** (non ammorbidisce gli
     impatti secchi).
   - **Add Leaf Bones** no (rig Mixamo).
   - Il resto come gli FBX attuali: _da annotare al primo export_.
   - Salvato come preset di export: `<Personaggio>_anim`.

## JSON dei marker

```json
{
  "character": "Rabbi",
  "blend": "Rabbi_anim.blend",
  "fps": 60,
  "exported_at": "2026-10-12T11:30:00",
  "actions": {
    "Sword_Light_1": {
      "frame_start": 0,
      "frame_end": 37,
      "cyclic": false,
      "markers": { "hit_start": 10, "hit_end": 15, "combo": 18, "cancel": 23 }
    }
  }
}
```

I frame sono assoluti, come in Blender: il tempo si ricava sottraendo `frame_start`.
Compaiono solo le action con Fake User attivo.

## Unity (lato codice)

- **Import della clip**: `Human`. Avatar: _da decidere al primo import e annotare qui_
  (meglio *Copy From Other Avatar* da `Rabbi_rigged` che un avatar nuovo per ogni FBX).
  Bake into pose su XZ/Y/rotazione per tutte, **tranne** schivate e dash (XZ libero per
  il root motion). Loop solo per le clip `cyclic`.
- **Importer** (menu `KosherFury/Combat/Import Move Markers`): legge il JSON e aggiorna
  **solo i tempi** dei `MoveData` con lo stesso nome, senza toccare gli altri campi.
  Segnala le action senza `MoveData`, i `MoveData` senza action e i marker mancanti
  rispetto al tipo di mossa.
- **Nessun animation event scritto a mano** nel ModelImporter delle clip nuove.

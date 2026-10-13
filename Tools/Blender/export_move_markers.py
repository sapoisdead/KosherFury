# Esporta i pose marker delle action in un JSON per Unity e controlla le convenzioni.
# Contratto: Docs/Combat/PIPELINE.md
#
# Uso: aprire nel Text Editor di Blender (Text > Open), poi Text > Run Script.
# Il risultato e gli errori compaiono nella console di sistema
# (Window > Toggle System Console su Windows; su macOS lanciare Blender da terminale)
# e in un riepilogo nella barra di stato.

import bpy
import json
import os
import re
from datetime import datetime

# Cartella del progetto Unity: cambiarla se il progetto si sposta.
UNITY_PROJECT = os.path.expanduser("~/Desktop/Unity/KosherFury")

EXPECTED_FPS = 60

KNOWN_MARKERS = re.compile(
    r"^(hit\d*_(start|end)|combo|cancel|parry_(start|end)|iframe_(start|end))$"
)
NAME_RULE = re.compile(r"^[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$")


def character_name():
    blend = os.path.basename(bpy.data.filepath)
    if not blend:
        raise RuntimeError("Salva il file .blend prima di esportare.")
    stem = os.path.splitext(blend)[0]
    if not stem.endswith("_anim"):
        raise RuntimeError(f"Il file deve chiamarsi <Personaggio>_anim.blend, non {blend}.")
    return stem[: -len("_anim")], blend


def required_markers(name):
    # Il tipo di mossa si deduce dal nome (vedi PIPELINE.md).
    if "Parry" in name:
        return ["parry_start", "parry_end"]
    if name.startswith("Dodge"):
        return ["iframe_start", "iframe_end"]
    if any(k in name for k in ("_Light", "_Heavy", "_Riposte")):
        return ["hit_start", "hit_end"]
    return []


def check_action(act, problems):
    name = act.name
    if not NAME_RULE.match(name) or "." in name:
        problems.append(f"{name}: nome fuori convenzione (niente spazi, punti o .001)")
    if not act.use_frame_range:
        problems.append(f"{name}: Manual Frame Range non attivo")

    start, end = act.frame_range
    markers = {}
    for m in act.pose_markers:
        if m.name in markers:
            problems.append(f"{name}: marker '{m.name}' ripetuto")
        markers[m.name] = m.frame
        if not KNOWN_MARKERS.match(m.name):
            problems.append(f"{name}: marker sconosciuto '{m.name}'")
        if not (start <= m.frame <= end):
            problems.append(f"{name}: marker '{m.name}' al frame {m.frame}, fuori dal range {start:g}-{end:g}")

    for req in required_markers(name):
        if req not in markers:
            problems.append(f"{name}: manca il marker '{req}'")

    # Ogni *_start deve avere il suo *_end, dopo.
    for key, frame in markers.items():
        if key.endswith("_start"):
            pair = key[: -len("_start")] + "_end"
            if pair not in markers:
                problems.append(f"{name}: '{key}' senza '{pair}'")
            elif markers[pair] <= frame:
                problems.append(f"{name}: '{pair}' ({markers[pair]}) non viene dopo '{key}' ({frame})")

    return {
        "frame_start": int(start),
        "frame_end": int(end),
        "cyclic": bool(act.use_cyclic),
        "markers": dict(sorted(markers.items(), key=lambda kv: kv[1])),
    }


def main():
    scene = bpy.context.scene
    problems = []

    fps = scene.render.fps / scene.render.fps_base
    if abs(fps - EXPECTED_FPS) > 1e-3:
        problems.append(f"Scena a {fps:g} fps: deve essere {EXPECTED_FPS}")

    character, blend = character_name()

    actions = {}
    skipped = []
    for act in sorted(bpy.data.actions, key=lambda a: a.name):
        if not act.use_fake_user:
            skipped.append(act.name)
            continue
        actions[act.name] = check_action(act, problems)

    if not actions:
        # Un JSON vuoto in Unity non porterebbe nessun tempo: meglio non scriverlo.
        detail = ", ".join(skipped) if skipped else "nessuna action nel file"
        msg = f"Nessuna action con Fake User: niente scritto. Action senza Fake User: {detail}"
        print("\n=== export_move_markers ===\n  ERRORE: " + msg)
        if not bpy.app.background:
            bpy.context.window_manager.popup_menu(
                lambda self, ctx: [self.layout.label(text=x) for x in
                                   ["Niente scritto: nessuna action con Fake User (scudo).",
                                    "Senza Fake User: " + detail[:180]]],
                title="Export marker annullato", icon="ERROR")
        return
    for name in skipped:
        problems.append(f"{name}: senza Fake User, non esportata")

    out_dir = os.path.join(UNITY_PROJECT, "Assets", "Art", character, "Animations")
    if not os.path.isdir(out_dir):
        raise RuntimeError(f"Cartella non trovata: {out_dir}")
    out_path = os.path.join(out_dir, f"{character}_anim_events.json")

    data = {
        "character": character,
        "blend": blend,
        "fps": int(round(fps)),
        "exported_at": datetime.now().isoformat(timespec="seconds"),
        "actions": actions,
    }
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=2)
        f.write("\n")

    print("\n=== export_move_markers ===")
    print(f"{len(actions)} action esportate in {out_path}")
    for p in problems:
        print("  ATTENZIONE:", p)
    if not problems:
        print("  Nessun problema.")

    if bpy.app.background:
        return
    summary = f"Marker: {len(actions)} action esportate, {len(problems)} problemi"
    bpy.context.window_manager.popup_menu(
        lambda self, ctx: [self.layout.label(text=p) for p in (problems or ["Nessun problema."])],
        title=summary,
        icon="ERROR" if problems else "CHECKMARK",
    )


main()

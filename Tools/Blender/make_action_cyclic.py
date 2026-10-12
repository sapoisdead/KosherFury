# Chiude il loop dell'action attiva dell'armatura.
# Contratto: Docs/Combat/PIPELINE.md
#
# Per ogni curva: copia la prima chiave un periodo più avanti (periodo = Manual Frame
# Range dell'action, es. 0-72 -> 72 frame) e aggiunge il modificatore Cycles.
# Funziona anche con le chiavi sfasate (overlap): ogni curva si chiude sul proprio
# primo keyframe, quindi lo sfasamento resta.
#
# Uso: Text Editor > Open > Run Script, con l'action del ciclo assegnata all'armatura.
# Si può rilanciare dopo aver modificato le pose: aggiorna le chiavi di chiusura.

import bpy


def find_armature():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"Mi aspetto una sola armatura nella scena, ne trovo {len(arms)}.")
    return arms[0]


def fcurves_of(action, arm):
    # Blender 4.4+: le curve stanno nei channelbag dei layer; prima: action.fcurves.
    if hasattr(action, "layers") and action.layers:
        slot = arm.animation_data.action_slot
        curves = []
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(slot) if slot else None
                if bag:
                    curves.extend(bag.fcurves)
        return curves
    return list(action.fcurves)


def close_curve(fc, period):
    kps = sorted(fc.keyframe_points, key=lambda k: k.co.x)
    if not kps:
        return "vuota"
    first = kps[0]
    end_frame = first.co.x + period

    # Le chiavi oltre la fine del periodo spezzerebbero il ciclo: vanno tolte.
    for k in reversed(list(fc.keyframe_points)):
        if k.co.x > end_frame + 1e-4:
            fc.keyframe_points.remove(k)

    existing = [k for k in fc.keyframe_points if abs(k.co.x - end_frame) < 1e-4]
    if existing:
        closing = existing[0]
        closing.co.y = first.co.y
    else:
        closing = fc.keyframe_points.insert(end_frame, first.co.y, options={"FAST"})
    closing.interpolation = first.interpolation
    closing.handle_left_type = first.handle_left_type
    closing.handle_right_type = first.handle_right_type
    closing.easing = first.easing

    if not any(m.type == "CYCLES" for m in fc.modifiers):
        fc.modifiers.new("CYCLES")
    fc.update()
    return "ok"


def main():
    arm = find_armature()
    if not arm.animation_data or not arm.animation_data.action:
        raise RuntimeError("L'armatura non ha un'action assegnata.")
    action = arm.animation_data.action
    if not action.use_frame_range:
        raise RuntimeError(f"{action.name}: attiva Manual Frame Range (il periodo del ciclo).")

    start, end = action.frame_range
    period = end - start
    curves = fcurves_of(action, arm)
    closed = sum(1 for fc in curves if close_curve(fc, period) == "ok")
    action.use_cyclic = True

    # Riproduzione in loop sul periodo: l'ultimo frame coincide con il primo, quindi
    # la scena si ferma un frame prima per non mostrarlo due volte.
    scene = bpy.context.scene
    scene.frame_start = int(start)
    scene.frame_end = int(end) - 1

    report = [
        f"{action.name}: ciclo di {period:g} frame ({start:g}-{end:g})",
        f"{closed} curve chiuse con chiave finale e modificatore Cycles",
        f"Timeline della scena impostata su {int(start)}-{int(end) - 1}",
    ]
    print("\n=== make_action_cyclic ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title="Loop chiuso", icon="CHECKMARK")


main()

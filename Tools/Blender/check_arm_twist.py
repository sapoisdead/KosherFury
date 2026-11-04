# Trova i frame in cui braccio o avambraccio ruotano troppo sul proprio asse (torsione).
# Contratto: Docs/Combat/PIPELINE.md
#
# Perché: il rig Humanoid di Unity ha limiti per la torsione di braccio e avambraccio
# (circa ±90° rispetto alla posa di riposo). Oltre il limite Unity estrapola e la posa
# diventa instabile: si vedono scatti dell'avambraccio proprio sul colpo.
#
# Uso: Text Editor > Open > Run Script. Controlla tutte le action con Fake User.
# Il riepilogo indica per ogni action l'osso, la torsione massima e i frame da sistemare.

import bpy
import math

LIMIT_DEG = 80.0   # margine sotto i ±90° del rig Humanoid
BONES = ["mixamorig:RightArm", "mixamorig:RightForeArm",
         "mixamorig:LeftArm", "mixamorig:LeftForeArm"]


def twist_deg(q):
    # Torsione attorno all'asse Y dell'osso (l'asse lungo), con il segno.
    w, y = q.w, q.y
    if w < 0:
        w, y = -w, -y
    return math.degrees(2.0 * math.atan2(y, w))


def main():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"Mi aspetto una sola armatura nella scena, ne trovo {len(arms)}.")
    arm = arms[0]
    if arm.animation_data is None:
        arm.animation_data_create()
    original = arm.animation_data.action
    original_frame = bpy.context.scene.frame_current

    report = []
    try:
        for act in sorted(bpy.data.actions, key=lambda a: a.name):
            if not act.use_fake_user:
                continue
            arm.animation_data.action = act
            start, end = (int(round(v)) for v in act.frame_range)
            worst = {}
            for f in range(start, end + 1):
                bpy.context.scene.frame_set(f)
                for name in BONES:
                    pb = arm.pose.bones.get(name)
                    if pb is None:
                        continue
                    t = twist_deg(pb.matrix_basis.to_quaternion())
                    w = worst.setdefault(name, {"max": 0.0, "frames": []})
                    if abs(t) > abs(w["max"]):
                        w["max"] = t
                    if abs(t) > LIMIT_DEG:
                        w["frames"].append(f)
            for name, w in worst.items():
                if w["frames"]:
                    fr = w["frames"]
                    span = f"{fr[0]}-{fr[-1]}" if len(fr) > 1 else f"{fr[0]}"
                    report.append(f"{act.name}: {name.split(':')[1]} fino a {w['max']:.0f} gradi, frame {span}")
    finally:
        arm.animation_data.action = original
        bpy.context.scene.frame_set(original_frame)

    if not report:
        report = [f"Nessuna torsione oltre {LIMIT_DEG:.0f} gradi."]
    print("\n=== check_arm_twist ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report[:20]],
            title="Torsione di braccia e avambracci", icon="INFO")


main()

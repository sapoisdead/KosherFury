# Crea l'action "Run": corsa ciclica di 24 frame, sul posto, a 5.0 m/s.
# Specifica: Docs/Combat/MOVES.md (velocità di locomozione).
#
# Costruisce le pose chiave della tabella data all'utente, frame per frame, con
# spostamenti relativi alla chiave precedente di ogni osso, poi:
#   - chiavi LINEAR sui piedi nel tratto a terra (8.3 cm a frame = 5 m/s a 60 fps),
#   - braccia ritardate di 2 frame,
#   - loop chiuso (chiave finale + modificatore Cycles su ogni curva).
# Le direzioni (avanti, lato, su) sono ricavate dal rig, non dagli assi di Blender.
#
# Uso: Text Editor > Open > Run Script. Se "Run" esiste già viene rifatta da zero.
# Serve l'IK delle gambe (setup_leg_ik.py). Le dita vengono copiate dalla guardia
# (Sword_Guard_Idle, frame 0) se esiste.

import bpy
import math
from mathutils import Matrix, Vector

ACTION = "Run"
PERIOD = 24
GUARD_ACTION = "Sword_Guard_Idle"
P = "mixamorig:"

# Spostamenti (avanti, su) in metri e rotazioni in gradi, relativi alla chiave precedente.
# avanti = direzione in cui guarda il Rabbi. Rotazioni:
#   "yaw":   attorno alla verticale, positivo = il fianco SINISTRO va avanti
#   "swing": avanti/indietro, positivo = l'arto (che pende verso il basso) va AVANTI
#   "lean":  inclinazione del busto, positivo = in avanti
TABLE = {
    # Gambe del Rabbi: 45 cm, tese a riposo. Il bacino resta ~5 cm più basso per tutta la
    # corsa, così il piede può appoggiare fino a ~17 cm davanti e dietro l'anca.
    # Ogni piede: 4 frame a terra (da +16.5 a -16.7 cm, 8.3 cm a frame), 20 in volo.
    0: {
        "IK_Foot.L": [("move", 0.165, 0.00)],
        "IK_Foot.R": [("move", -0.05, 0.25)],
        "Hips": [("move", 0.0, -0.05), ("yaw", 8)],
        "Spine2": [("yaw", -15)],
        "Head": [("yaw", 7)],
        "RightArm": [("swing", 35)], "RightForeArm": [("swing", 10)],
        "LeftArm": [("swing", -25)], "LeftForeArm": [("swing", -15)],
    },
    2: {
        "IK_Foot.L": [("move", -0.165, 0.00)],
        "Hips": [("move", 0.0, -0.02)],
    },
    3: {
        "IK_Foot.R": [("move", 0.15, -0.05)],
    },
    4: {
        "IK_Foot.L": [("move", -0.167, 0.00)],
        "Hips": [("move", 0.0, 0.015)],
    },
    6: {
        "IK_Foot.L": [("move", -0.083, 0.08)],
        "IK_Foot.R": [("move", 0.12, -0.06)],
        "Hips": [("yaw", -8)],
        "Spine2": [("yaw", 15)],
        "Head": [("yaw", -7)],
        "RightArm": [("swing", -35)], "RightForeArm": [("swing", -10)],
        "LeftArm": [("swing", 25)], "LeftForeArm": [("swing", 15)],
    },
    9: {
        "IK_Foot.L": [("move", 0.05, 0.14)],
        "IK_Foot.R": [("move", 0.02, -0.08)],
        "Hips": [("move", 0.0, 0.02)],
    },
    12: {
        "IK_Foot.L": [("move", 0.15, 0.03)],
        "IK_Foot.R": [("move", -0.075, -0.06)],
        "Hips": [("move", 0.0, -0.015), ("yaw", -8)],
        "Spine2": [("yaw", 15)],
        "Head": [("yaw", -7)],
        "RightArm": [("swing", -25)], "RightForeArm": [("swing", -15)],
        "LeftArm": [("swing", 35)], "LeftForeArm": [("swing", 10)],
    },
    14: {
        "IK_Foot.R": [("move", -0.165, 0.00)],
        "Hips": [("move", 0.0, -0.02)],
    },
    15: {
        "IK_Foot.L": [("move", 0.15, -0.05)],
    },
    16: {
        "IK_Foot.R": [("move", -0.167, 0.00)],
        "Hips": [("move", 0.0, 0.015)],
    },
    18: {
        "IK_Foot.L": [("move", 0.12, -0.06)],
        "IK_Foot.R": [("move", -0.083, 0.08)],
        "Hips": [("yaw", 8)],
        "Spine2": [("yaw", -15)],
        "Head": [("yaw", 7)],
        "RightArm": [("swing", 25)], "RightForeArm": [("swing", 15)],
        "LeftArm": [("swing", -35)], "LeftForeArm": [("swing", -10)],
    },
    21: {
        "IK_Foot.L": [("move", 0.02, -0.08)],
        "IK_Foot.R": [("move", 0.05, 0.14)],
        "Hips": [("move", 0.0, 0.02)],
    },
}

LEAN_DEG = 12            # busto in avanti, diviso fra Spine e Spine1
ARM_FROM_VERTICAL = 15   # braccia vicino ai fianchi
ELBOW_DEG = 90           # gomiti piegati
ARM_DELAY = 2            # ritardo delle braccia, in frame
LINEAR_KEYS = {"IK_Foot.L": (0, 2), "IK_Foot.R": (12, 14)}   # tratto a terra
ARM_BONES = ("RightArm", "RightForeArm", "LeftArm", "LeftForeArm")


def full(name):
    return name if name.startswith("IK_") else P + name


def find_armature():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"Mi aspetto una sola armatura nella scena, ne trovo {len(arms)}.")
    return arms[0]


def update():
    bpy.context.view_layer.update()


def world(arm, pb):
    return arm.matrix_world @ pb.matrix


def set_world(arm, pb, m):
    pb.matrix = arm.matrix_world.inverted() @ m
    update()


def rotate_world(arm, pb, axis, deg):
    m = world(arm, pb)
    head = m.translation.copy()
    r = Matrix.Rotation(math.radians(deg), 4, axis)
    set_world(arm, pb, Matrix.Translation(head) @ r @ Matrix.Translation(-head) @ m)


def move_world(arm, pb, delta):
    set_world(arm, pb, Matrix.Translation(delta) @ world(arm, pb))


def bone_dir(arm, pb):
    m = world(arm, pb)
    return (m.to_3x3() @ Vector((0, 1, 0))).normalized()


def axes(arm):
    # Dalla posa di RIPOSO (head_local), non dalla posa attuale: se sul rig è rimasta
    # una posa ruotata (es. la guardia, bacino girato di ~30°) le direzioni non cambiano.
    bones = arm.data.bones
    l = arm.matrix_world @ bones[P + "LeftUpLeg"].head_local
    r = arm.matrix_world @ bones[P + "RightUpLeg"].head_local
    up = Vector((0, 0, 1))
    side_left = Vector((l.x - r.x, l.y - r.y, 0)).normalized()
    raw = side_left.cross(up)
    # Arrotonda all'asse più vicino: a riposo il Rabbi è allineato agli assi del mondo.
    fwd = max((Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))),
              key=lambda a: a.dot(raw))
    side_left = up.cross(fwd).normalized()
    return {
        "fwd": fwd,
        "up": up,
        "yaw": side_left.cross(fwd).normalized(),       # porta il fianco sinistro avanti
        "swing": (-up).cross(fwd).normalized(),         # porta in avanti un arto che pende
        "lean": up.cross(fwd).normalized(),             # porta il busto in avanti
    }


def key_bone(pb, frame, loc=True, rot=True):
    if loc:
        pb.keyframe_insert("location", frame=frame)
    if not rot:
        return
    if pb.rotation_mode == "QUATERNION":
        pb.keyframe_insert("rotation_quaternion", frame=frame)
    elif pb.rotation_mode == "AXIS_ANGLE":
        pb.keyframe_insert("rotation_axis_angle", frame=frame)
    else:
        pb.keyframe_insert("rotation_euler", frame=frame)


def fcurves_of(action, arm):
    if hasattr(action, "layers") and action.layers:
        slot = arm.animation_data.action_slot
        out = []
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(slot) if slot else None
                if bag:
                    out.extend(bag.fcurves)
        return out
    return list(action.fcurves)


def bone_of(fc):
    p = fc.data_path
    return p.split('"')[1] if p.startswith("pose.bones[") else None


def read_fingers(arm):
    # Dita della guardia, se esiste: Run parte con le stesse mani.
    guard = bpy.data.actions.get(GUARD_ACTION)
    if guard is None:
        return {}
    arm.animation_data.action = guard
    bpy.context.scene.frame_set(int(guard.frame_range[0]))
    update()
    return {pb.name: pb.matrix_basis.copy() for pb in arm.pose.bones
            if "Hand" in pb.name and pb.name not in (P + "LeftHand", P + "RightHand")}


def base_pose(arm, ax, fingers):
    pb = arm.pose.bones
    for b in pb:
        b.matrix_basis = Matrix.Identity(4)
    update()
    for name, m in fingers.items():
        if name in pb:
            pb[name].matrix_basis = m
    update()

    # Busto in avanti.
    for name in ("Spine", "Spine1"):
        rotate_world(arm, pb[P + name], ax["lean"], LEAN_DEG / 2)

    down = Vector((0, 0, -1))
    for side in ("Left", "Right"):
        upper = pb[P + side + "Arm"]
        fore = pb[P + side + "ForeArm"]
        # Braccio verso il fianco, a ARM_FROM_VERTICAL gradi dalla verticale.
        d = bone_dir(arm, upper)
        angle = math.degrees(d.angle(down)) - ARM_FROM_VERTICAL
        axis = d.cross(down)
        if axis.length > 1e-6 and angle > 0:
            rotate_world(arm, upper, axis.normalized(), angle)
        # Gomito: avambraccio piegato in avanti di ELBOW_DEG rispetto al braccio.
        u = bone_dir(arm, upper)
        target = (Matrix.Rotation(math.radians(ELBOW_DEG), 3, ax["swing"]) @ u).normalized()
        f = bone_dir(arm, fore)
        axis = f.cross(target)
        if axis.length > 1e-6:
            rotate_world(arm, fore, axis.normalized(), math.degrees(f.angle(target)))


def main():
    arm = find_armature()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for name in ("IK_Foot.L", "IK_Foot.R"):
        if name not in arm.pose.bones:
            raise RuntimeError("Manca l'IK delle gambe: lancia prima setup_leg_ik.py.")
    if arm.animation_data is None:
        arm.animation_data_create()
    previous = arm.animation_data.action

    fingers = read_fingers(arm)

    old = bpy.data.actions.get(ACTION)
    if old:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new(ACTION)
    action.use_fake_user = True
    arm.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_set(0)
    # Via la posa della guardia rimasta sul rig prima di fare qualsiasi calcolo.
    for b in arm.pose.bones:
        b.matrix_basis = Matrix.Identity(4)
    update()

    base_pose(arm, ax := axes(arm), fingers)
    pb = arm.pose.bones

    # La posa resta in memoria da un frame all'altro (il frame della scena non cambia),
    # quindi ogni spostamento è relativo alla chiave precedente dello stesso osso.
    for frame in sorted(TABLE):
        for name, ops in TABLE[frame].items():
            b = pb[full(name)]
            for op in ops:
                if op[0] == "move":
                    move_world(arm, b, ax["fwd"] * op[1] + ax["up"] * op[2])
                else:
                    rotate_world(arm, b, ax[op[0]], op[1])
        if frame == 0:
            # Frame 0: posa completa su tutte le ossa, così niente resta senza chiave.
            for b in pb:
                key_bone(b, 0)
        else:
            # Solo i canali toccati: una chiave di sola posizione non deve bloccare
            # la rotazione (altrimenti il bacino gira a scatti).
            for name, ops in TABLE[frame].items():
                moved = any(op[0] == "move" for op in ops)
                turned = any(op[0] != "move" for op in ops)
                # Le rotazioni sono attorno alla testa dell'osso: la posizione non cambia.
                key_bone(pb[full(name)], frame, loc=moved, rot=turned)

    curves = fcurves_of(action, arm)

    # Piedi: tratto a terra a velocità costante.
    for fc in curves:
        name = bone_of(fc)
        if name in LINEAR_KEYS and fc.data_path.endswith("location"):
            for k in fc.keyframe_points:
                if int(round(k.co.x)) in LINEAR_KEYS[name]:
                    k.interpolation = "LINEAR"

    # Braccia in ritardo.
    for fc in curves:
        if bone_of(fc) in tuple(P + b for b in ARM_BONES):
            for k in fc.keyframe_points:
                k.co.x += ARM_DELAY
                k.handle_left.x += ARM_DELAY
                k.handle_right.x += ARM_DELAY
            fc.update()

    # Loop: chiave di chiusura un periodo dopo la prima chiave, e Cycles.
    for fc in curves:
        kps = sorted(fc.keyframe_points, key=lambda k: k.co.x)
        if not kps:
            continue
        first = kps[0]
        end = first.co.x + PERIOD
        for k in reversed(list(fc.keyframe_points)):
            if k.co.x > end + 1e-4:
                fc.keyframe_points.remove(k)
        closing = fc.keyframe_points.insert(end, first.co.y, options={"FAST"})
        closing.interpolation = first.interpolation
        if not any(m.type == "CYCLES" for m in fc.modifiers):
            fc.modifiers.new("CYCLES")
        fc.update()

    action.use_frame_range = True
    action.frame_start, action.frame_end = 0, PERIOD
    action.use_cyclic = True
    scene.frame_start, scene.frame_end = 0, PERIOD - 1
    scene.frame_set(0)

    report = [
        f"Creata '{ACTION}': {PERIOD} frame, ciclica, Fake User attivo",
        f"Avanti del Rabbi: {tuple(round(c, 2) for c in ax['fwd'])}",
        "Dita copiate da " + (GUARD_ACTION if fingers else "nessuna action (posa di riposo)"),
        "Controlla al frame 0: avanti piede SINISTRO e braccio DESTRO",
    ]
    print("\n=== create_run ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title="Corsa creata", icon="CHECKMARK")


main()

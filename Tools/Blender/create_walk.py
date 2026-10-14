# Crea l'action "Walk": camminata ciclica di 30 frame, sul posto, a 1.2 m/s.
# Specifica: Docs/Combat/MOVES.md (velocità di locomozione).
#
# Camminata vera: sempre almeno un piede a terra, 3 frame di doppio appoggio a ogni passo.
# Il piede rulla: appoggia sul tallone (punta su), sta piatto, si stacca sulla punta
# (tallone su). Con gambe di 45 cm il passo è di 30 cm: 4 passi al secondo, 1.2 m/s.
#
# Valori ASSOLUTI rispetto alla posa di base (riposo + bacino basso + busto e braccia):
# ogni chiave si ricostruisce da zero, quindi non si accumulano errori e rilanciare lo
# script dà sempre lo stesso risultato. Ogni chiave registra solo i canali che cambia.
#
# Uso: Text Editor > Open > Run Script. Se "Walk" esiste già viene rifatta da zero.
# Serve l'IK delle gambe (setup_leg_ik.py). Le dita vengono copiate dalla guardia.

import bpy
import math
from mathutils import Matrix, Vector

VERSION = 3   # v3: perni della rullata presi dalla scarpa. v2: v2: camminata vera (v1 era una corsetta a 2.4 m/s, scartata)
ACTION = "Walk"
GUARD_ACTION = "Sword_Guard_Idle"
P = "mixamorig:"

PERIOD = 30              # frame per ciclo (due passi)
STANCE = 18              # frame a terra per piede (60% del ciclo)
FRONT = 0.16             # caviglia all'appoggio: 16 cm davanti all'anca
BACK = -0.20             # caviglia allo stacco (prima di alzare il tallone): 20 cm dietro
SPEED = (FRONT - BACK) / STANCE * 60     # 1.2 m/s: il piede a terra scorre di 2 cm a frame

HIPS_DOWN = 0.04
LEAN_DEG = 3
ARM_FROM_VERTICAL = 8
ELBOW_DEG = 25
ARM_DELAY = 2

# Rullata del piede durante l'appoggio: frame -> (gradi, perno). Gradi positivi = punta su.
#   "heel": ruota sul tallone (appoggio), "ball": ruota sulla punta della scarpa (stacco).
ROLL = {0: (15, "heel"), 1: (9, "heel"), 2: (4, "heel"), 3: (0, None),
        12: (0, None), 13: (-3, "ball"), 14: (-7, "ball"), 15: (-11, "ball"),
        16: (-15, "ball"), 17: (-19, "ball"), 18: (-22, "ball")}
# Volo del piede: frame -> (avanti, su, gradi) con rotazione sulla caviglia.
SWING = {21: (-0.12, 0.06, -15), 24: (0.02, 0.065, -2), 27: (0.15, 0.035, 12)}

# Bacino per frame del ciclo: (lato verso il piede sinistro, su). Più alto a metà appoggio,
# spostato sopra il piede che regge il peso.
HIPS = {0: (0.0, -0.010), 8: (0.015, 0.008), 15: (0.0, -0.010), 23: (-0.015, 0.008)}
# Rotazioni (gradi) a frame 0 (sinistro davanti) e 15 (destro davanti).
#   yaw: positivo = il fianco SINISTRO va avanti; swing: positivo = l'arto va AVANTI
TWIST = {
    "Hips":         ("yaw",   4, -4),
    "Spine2":       ("yaw",  -7,  7),
    "Head":         ("yaw",   3, -3),
    "RightArm":     ("swing", 18, -12),
    "RightForeArm": ("swing",  6, -6),
    "LeftArm":      ("swing", -12, 18),
    "LeftForeArm":  ("swing", -6,  6),
}
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


def rotate_world(arm, pb, axis, deg, pivot=None):
    m = world(arm, pb)
    c = m.translation.copy() if pivot is None else pivot
    r = Matrix.Rotation(math.radians(deg), 4, axis)
    set_world(arm, pb, Matrix.Translation(c) @ r @ Matrix.Translation(-c) @ m)


def move_world(arm, pb, delta):
    set_world(arm, pb, Matrix.Translation(delta) @ world(arm, pb))


def bone_dir(arm, pb):
    return (world(arm, pb).to_3x3() @ Vector((0, 1, 0))).normalized()


def setup_axes(arm):
    # Dalla posa di riposo, arrotondato all'asse del mondo più vicino.
    bones = arm.data.bones
    mw = arm.matrix_world
    l = mw @ bones[P + "LeftUpLeg"].head_local
    r = mw @ bones[P + "RightUpLeg"].head_local
    up = Vector((0, 0, 1))
    raw = Vector((l.x - r.x, l.y - r.y, 0)).normalized().cross(up)
    fwd = max((Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))),
              key=lambda a: a.dot(raw))
    side_left = up.cross(fwd)
    feet = (mw @ bones[P + "LeftFoot"].head_local + mw @ bones[P + "RightFoot"].head_local) / 2
    ax = {
        "fwd": fwd, "up": up, "side": side_left,
        "yaw": side_left.cross(fwd).normalized(),
        "swing": (-up).cross(fwd).normalized(),     # porta avanti un arto che pende; sul piede alza la punta
        "lean": up.cross(fwd).normalized(),
        # Il passo si centra sotto le anche.
        "foot_center": ((l + r) / 2 - feet).dot(fwd),
        "pivot": {},
    }
    # Perni della rullata, rispetto alla caviglia a riposo (piede piatto), presi dalla
    # scarpa vera: tallone = punto più indietro della suola, punta = punto più avanti.
    # La scarpa del Rabbi è rigida (pesata quasi tutta su ToeBase), quindi allo stacco
    # ruota sulla punta. Se non c'è una mesh, valori di ripiego dalle ossa.
    soles = sole_points(arm)
    for s, side in (("L", "Left"), ("R", "Right")):
        ankle = mw @ bones[P + side + "Foot"].head_local
        toe = bones.get(P + side + "Toe_End")
        ball = (mw @ toe.tail_local) if toe else ankle + fwd * 0.08 - up * 0.055
        pts = [p for p in soles if abs(p.x - ankle.x) < 0.08 and abs(p.y - ankle.y) < 0.20]
        if pts:
            ground = min(p.z for p in pts)
            bottom = [p for p in pts if p.z < ground + 0.015]
            heel = min(bottom, key=lambda p: p.dot(fwd)).copy()
            ball = max(bottom, key=lambda p: p.dot(fwd)).copy()
        else:
            ground = ball.z
            heel = ankle - fwd * 0.02
        heel.z = ground
        ball = ball.copy()
        ball.z = ground
        ax["pivot"][s] = {"ball": ball - ankle, "heel": heel - ankle}
        ax["ground"] = ground
    return ax


def sole_points(arm):
    # Vertici sotto le caviglie delle mesh legate all'armatura, in posa di riposo.
    ankle_z = (arm.matrix_world @ arm.data.bones[P + "LeftFoot"].head_local).z
    pts = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or not any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers):
            continue
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            if w.z < ankle_z:
                pts.append(w)
    return pts


def key_bone(pb, frame, loc=True, rot=True):
    if loc:
        pb.keyframe_insert("location", frame=frame)
    if rot:
        path = {"QUATERNION": "rotation_quaternion",
                "AXIS_ANGLE": "rotation_axis_angle"}.get(pb.rotation_mode, "rotation_euler")
        pb.keyframe_insert(path, frame=frame)


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
    move_world(arm, pb[P + "Hips"], -ax["up"] * HIPS_DOWN)
    for name in ("Spine", "Spine1"):
        rotate_world(arm, pb[P + name], ax["lean"], LEAN_DEG / 2)
    down = Vector((0, 0, -1))
    for side in ("Left", "Right"):
        upper = pb[P + side + "Arm"]
        fore = pb[P + side + "ForeArm"]
        d = bone_dir(arm, upper)
        angle = math.degrees(d.angle(down)) - ARM_FROM_VERTICAL
        axis = d.cross(down)
        if axis.length > 1e-6 and angle > 0:
            rotate_world(arm, upper, axis.normalized(), angle)
        u = bone_dir(arm, upper)
        target = (Matrix.Rotation(math.radians(ELBOW_DEG), 3, ax["swing"]) @ u).normalized()
        f = bone_dir(arm, fore)
        axis = f.cross(target)
        if axis.length > 1e-6:
            rotate_world(arm, fore, axis.normalized(), math.degrees(f.angle(target)))


def foot_keys():
    # Piede sinistro per frame del ciclo: (avanti, su, gradi, perno). Il destro: +15 frame.
    keys = {}
    for f, (deg, pivot) in ROLL.items():
        x = FRONT + (BACK - FRONT) * f / STANCE     # a terra scorre a velocità costante
        keys[f] = (x, 0.0, deg, pivot)
    for f, (x, up, deg) in SWING.items():
        keys[f] = (x, up, deg, None)
    return keys


def build_keys():
    # frame -> {osso: {"foot": (...)} | {"move": (lato, su)} | {"twist": 0 o 1}}
    keys = {}

    def put(frame, bone, kind, value):
        keys.setdefault(frame % PERIOD, {}).setdefault(bone, {})[kind] = value

    for f, val in foot_keys().items():
        put(f, "IK_Foot.L", "foot", val)
        put(f + PERIOD // 2, "IK_Foot.R", "foot", val)
    for f, val in HIPS.items():
        put(f, "Hips", "move", val)
    for bone in TWIST:
        put(0, bone, "twist", 0)
        put(PERIOD // 2, bone, "twist", 1)
    return keys


def pose_for(arm, ax, fingers, entries):
    base_pose(arm, ax, fingers)
    pb = arm.pose.bones
    order = ["Hips", "Spine2", "Head"] + list(ARM_BONES) + ["IK_Foot.L", "IK_Foot.R"]
    for name in order:
        e = entries.get(name)
        if not e:
            continue
        b = pb[full(name)]
        if "move" in e:
            side_m, up_m = e["move"]
            move_world(arm, b, ax["side"] * side_m + ax["up"] * up_m)
        if "twist" in e:
            kind, a, c = TWIST[name]
            rotate_world(arm, b, ax[kind], (a, c)[e["twist"]])
        if "foot" in e:
            x, up_m, deg, pivot = e["foot"]
            move_world(arm, b, ax["fwd"] * (x + ax["foot_center"]) + ax["up"] * up_m)
            if deg:
                p = None
                if pivot:
                    p = world(arm, b).translation + ax["pivot"][name[-1]][pivot]
                rotate_world(arm, b, ax["swing"], deg, p)


def main():
    arm = find_armature()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for name in ("IK_Foot.L", "IK_Foot.R"):
        if name not in arm.pose.bones:
            raise RuntimeError("Manca l'IK delle gambe: lancia prima setup_leg_ik.py.")
    if arm.animation_data is None:
        arm.animation_data_create()

    fingers = read_fingers(arm)
    old = bpy.data.actions.get(ACTION)
    if old:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new(ACTION)
    action.use_fake_user = True
    arm.animation_data.action = action
    scene = bpy.context.scene
    scene.frame_set(0)
    for b in arm.pose.bones:
        b.matrix_basis = Matrix.Identity(4)
    update()
    ax = setup_axes(arm)
    pb = arm.pose.bones

    keys = build_keys()
    for frame in sorted(keys):
        entries = keys[frame]
        pose_for(arm, ax, fingers, entries)
        if frame == 0:
            for b in pb:
                key_bone(b, 0)
        else:
            for name, e in entries.items():
                key_bone(pb[full(name)], frame,
                         loc="move" in e or "foot" in e,
                         rot="twist" in e or "foot" in e)

    curves = fcurves_of(action, arm)

    # Piede a terra: interpolazione lineare fra le chiavi dell'appoggio (velocità costante).
    left = {f % PERIOD for f in range(STANCE)}
    right = {(f + PERIOD // 2) % PERIOD for f in range(STANCE)}
    ground = {"IK_Foot.L": left, "IK_Foot.R": right}
    for fc in curves:
        name = bone_of(fc)
        if name in ground:
            for k in fc.keyframe_points:
                if int(round(k.co.x)) in ground[name]:
                    k.interpolation = "LINEAR"

    for fc in curves:
        if bone_of(fc) in tuple(P + b for b in ARM_BONES):
            for k in fc.keyframe_points:
                k.co.x += ARM_DELAY
                k.handle_left.x += ARM_DELAY
                k.handle_right.x += ARM_DELAY
            fc.update()

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
        f"[v{VERSION}] Creata '{ACTION}': {PERIOD} frame, ciclica, Fake User attivo",
        f"{SPEED:.1f} m/s, passo {(FRONT - BACK) * PERIOD / STANCE / 2:.2f} m, "
        f"{STANCE} frame a terra per piede",
        "Dita copiate da " + (GUARD_ACTION if fingers else "nessuna action (posa di riposo)"),
        "Frame 0: tallone SINISTRO che appoggia, braccio DESTRO avanti",
    ]
    print("\n=== create_walk ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title=f"Camminata creata (script v{VERSION})", icon="CHECKMARK")


main()

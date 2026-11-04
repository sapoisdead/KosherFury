# Crea gli strafe con la guardia: Sword_Guard_Walk_F, _B, _L, _R.
# Specifica: Docs/Combat/MOVES.md (velocità di locomozione). Contratto: PIPELINE.md.
#
# Partono da Sword_Guard_Idle (frame 0): busto, braccia, spada e bacino girato restano
# quelli della guardia. Si muovono le gambe, a PASSO ACCOSTATO: prima il piede dal lato
# in cui si va, poi l'altro, mai incrociati. Ogni piede fa un passo ogni ciclo.
#   - 0.8 m/s, ciclo di 24 frame, un passetto ogni 12 frame (5 al secondo);
#   - sul posto: il piede a terra scorre all'indietro a velocità costante;
#   - bacino più basso della guardia e piedi sotto le anche (fronte-retro), perché
#     dalla guardia da fermo, già quasi a gambe tese, il piede non ha margine per i passi.
#
# Uso: Text Editor > Open > Run Script. Rifà da zero le quattro action se esistono.
# Serve l'IK delle gambe (setup_leg_ik.py) e la guardia Sword_Guard_Idle.

import bpy
import math
from mathutils import Matrix, Vector

VERSION = 1
GUARD_ACTION = "Sword_Guard_Idle"
PREFIX = "Sword_Guard_Walk_"
P = "mixamorig:"

SPEED = 0.8              # m/s
PERIOD = 24              # frame per ciclo
SWING = 7                # frame in aria per passo
TRAIL_DELAY = 12         # il secondo piede parte mezzo ciclo dopo
STRIDE = SPEED * PERIOD / 60                 # 0.32 m: quanto avanza ogni piede a ciclo
REACH = SPEED * (PERIOD - SWING) / 60 / 2    # il piede rispetto all'anca va da +REACH a -REACH
LIFT = 0.045             # altezza del piede in volo
HIPS_DOWN = 0.045        # bacino più basso della guardia da fermo
BOB = 0.012              # il bacino scende quando il piede appoggia
LATERAL_SPREAD = 0.03    # solo negli strafe laterali: piedi un po' più larghi
LEAN_DEG = 3             # il busto si inclina un poco verso dove si va

# direzione, piede che parte per primo
DIRECTIONS = {"F": ("fwd", "L"), "B": ("back", "R"), "L": ("left", "L"), "R": ("right", "R")}


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
    c = m.translation.copy()
    r = Matrix.Rotation(math.radians(deg), 4, axis)
    set_world(arm, pb, Matrix.Translation(c) @ r @ Matrix.Translation(-c) @ m)


def place_world(arm, pb, pos):
    m = world(arm, pb).copy()
    m.translation = pos
    set_world(arm, pb, m)


def move_world(arm, pb, delta):
    set_world(arm, pb, Matrix.Translation(delta) @ world(arm, pb))


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


def rest_axes(arm):
    bones = arm.data.bones
    mw = arm.matrix_world
    l = mw @ bones[P + "LeftUpLeg"].head_local
    r = mw @ bones[P + "RightUpLeg"].head_local
    up = Vector((0, 0, 1))
    raw = Vector((l.x - r.x, l.y - r.y, 0)).normalized().cross(up)
    fwd = max((Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))),
              key=lambda a: a.dot(raw))
    left = up.cross(fwd)
    return {"fwd": fwd, "back": -fwd, "left": left, "right": -left, "up": up}


def foot_offset(frame, start):
    # Posizione del piede lungo la direzione (rispetto al centro) e altezza, al frame dato.
    # Volo da start a start+SWING, poi a terra fino al prossimo start.
    t = (frame - start) % PERIOD
    if t <= SWING:
        k = t / SWING
        s = k * k * (3 - 2 * k)                  # parte e arriva morbido
        return -REACH + 2 * REACH * s, LIFT * math.sin(math.pi * k)
    k = (t - SWING) / (PERIOD - SWING)
    return REACH - 2 * REACH * k, 0.0


def foot_frames(start):
    # Chiavi del piede: inizio volo, due in volo, appoggio, più il frame 0 se è a terra.
    frames = {(start + d) % PERIOD for d in (0, 2, 4, 5, SWING)}
    frames.add(0)
    landing = (start + SWING) % PERIOD
    in_stance = (0 - start) % PERIOD > SWING
    linear = {landing} | ({0} if in_stance else set())
    return sorted(frames), linear


def build(arm, ax, guard_pose, name, direction, lead):
    pb = arm.pose.bones
    d = ax[direction]
    lateral = direction in ("left", "right")
    action = bpy.data.actions.get(name)
    if action:
        bpy.data.actions.remove(action)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data.action = action

    def guard():
        for b in pb:
            b.matrix_basis = guard_pose.get(b.name, Matrix.Identity(4))
        update()

    guard()
    hips_guard = world(arm, pb[P + "Hips"]).translation.copy()
    centers = {}
    for s, side, out in (("L", "Left", "left"), ("R", "Right", "right")):
        foot = world(arm, pb["IK_Foot." + s]).translation.copy()
        upleg = world(arm, pb[P + side + "UpLeg"]).translation
        # Fronte-retro: il piede sotto la sua anca. Di lato: dove sta nella guardia.
        c = foot + ax["fwd"] * (upleg - foot).dot(ax["fwd"])
        if lateral:
            c += ax[out] * LATERAL_SPREAD
        centers[s] = c
    starts = {lead: 0, ("R" if lead == "L" else "L"): TRAIL_DELAY}

    def pose(frame, feet=True):
        guard()
        hips = pb[P + "Hips"]
        land = min((frame - (st + SWING)) % PERIOD for st in starts.values())
        bob = -BOB * max(0.0, 1 - land / 5)       # scende all'appoggio e risale in 5 frame
        place_world(arm, hips, hips_guard - ax["up"] * (HIPS_DOWN - bob))
        rotate_world(arm, pb[P + "Spine"], ax["up"].cross(d), LEAN_DEG)
        if feet:
            for s, st in starts.items():
                along, h = foot_offset(frame, st)
                place_world(arm, pb["IK_Foot." + s], centers[s] + d * along + ax["up"] * h)

    hip_frames = sorted({0} | {(st + SWING + k) % PERIOD for st in starts.values() for k in (0, 5)}
                        | {(st + SWING - 2) % PERIOD for st in starts.values()})
    frames = {f: set() for f in hip_frames}
    linear = {}
    for s, st in starts.items():
        fr, lin = foot_frames(st)
        linear["IK_Foot." + s] = lin
        for f in fr:
            frames.setdefault(f, set()).add("IK_Foot." + s)

    for f in sorted(frames):
        pose(f)
        if f == 0:
            for b in pb:
                key_bone(b, 0)
            continue
        if f in hip_frames:
            key_bone(pb[P + "Hips"], f, rot=False)
        for bone in frames[f]:
            key_bone(pb[bone], f, rot=False)

    curves = fcurves_of(action, arm)
    for fc in curves:
        b = bone_of(fc)
        if b in linear and fc.data_path.endswith("location"):
            for k in fc.keyframe_points:
                if int(round(k.co.x)) in linear[b]:
                    k.interpolation = "LINEAR"
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
    return action


def main():
    arm = find_armature()
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for name in ("IK_Foot.L", "IK_Foot.R"):
        if name not in arm.pose.bones:
            raise RuntimeError("Manca l'IK delle gambe: lancia prima setup_leg_ik.py.")
    guard = bpy.data.actions.get(GUARD_ACTION)
    if guard is None:
        raise RuntimeError(f"Manca {GUARD_ACTION}: gli strafe partono dalla guardia.")
    if arm.animation_data is None:
        arm.animation_data_create()
    scene = bpy.context.scene

    arm.animation_data.action = guard
    scene.frame_set(int(guard.frame_range[0]))
    update()
    guard_pose = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
    ax = rest_axes(arm)

    made = []
    for suffix, (direction, lead) in DIRECTIONS.items():
        scene.frame_set(0)
        made.append(build(arm, ax, guard_pose, PREFIX + suffix, direction, lead).name)

    arm.animation_data.action = bpy.data.actions[PREFIX + "F"]
    scene.frame_start, scene.frame_end = 0, PERIOD - 1
    scene.frame_set(0)

    report = [
        f"[v{VERSION}] Create {', '.join(made)}",
        f"{SPEED} m/s, ciclo di {PERIOD} frame, passo di {STRIDE:.2f} m per piede",
        "Busto e braccia della guardia (frame 0 di " + GUARD_ACTION + ")",
        "Assegnata " + PREFIX + "F: le altre si scelgono nell'Action Editor",
    ]
    print("\n=== create_strafe ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title=f"Strafe creati (script v{VERSION})", icon="CHECKMARK")


main()

# Crea Sword_Arms_Walk e Sword_Arms_Run: le braccia con la spada da sovrapporre a Walk e Run.
# Contratto: Docs/Combat/PIPELINE.md (layer Weapon_Overlay: maschera su busto e braccia).
#
# Ogni clip è a corpo intero e lunga come la sua locomozione (Walk 30, Run 24 frame):
#   - gambe, bacino, busto e testa: identici a Walk / Run (la torsione del busto resta,
#     perché il layer in Unity sovrascrive anche il busto);
#   - braccio sinistro: quello di Walk / Run, libero, che oscilla;
#   - braccio destro e spada: la posa di Sword_Idle (frame 0), orientata sul bacino, con
#     una piccola oscillazione a tempo con i passi;
#   - se la punta della spada scende sotto MIN_TIP (inclinazione del busto in corsa), il
#     polso la alza dello stesso angolo per tutta la clip.
#
# Uso: Text Editor > Open > Run Script. Rifà da zero le due action.
# Rilanciarlo ogni volta che cambiano Walk, Run o la posa di Sword_Idle.

import bpy
import math
from mathutils import Matrix, Vector

VERSION = 1
IDLE = "Sword_Idle"
SWORD = "WPN_Sword"
P = "mixamorig:"
CLIPS = {
    # clip di gambe: (nome nuovo, oscillazione del braccio in gradi, punta minima da terra in m)
    "Walk": ("Sword_Arms_Walk", 6, 0.12),
    "Run": ("Sword_Arms_Run", 10, 0.25),
}
SWING_DELAY = 2   # frame: il braccio destro è avanti al frame 0 + ritardo (come in Walk e Run)
KEY_STEP = 2      # una chiave ogni 2 frame


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


def right_arm_bones(arm):
    names = [P + "RightShoulder", P + "RightArm", P + "RightForeArm", P + "RightHand"]
    names += [b.name for b in arm.pose.bones
              if b.name.startswith(P + "RightHand") and b.name != P + "RightHand"]
    return [n for n in names if n in arm.pose.bones]


def axes(arm):
    bones = arm.data.bones
    mw = arm.matrix_world
    l = mw @ bones[P + "LeftUpLeg"].head_local
    r = mw @ bones[P + "RightUpLeg"].head_local
    up = Vector((0, 0, 1))
    raw = Vector((l.x - r.x, l.y - r.y, 0)).normalized().cross(up)
    fwd = max((Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))),
              key=lambda a: a.dot(raw))
    return {"fwd": fwd, "up": up, "swing": (-up).cross(fwd).normalized()}


def sword_tip_z(sword):
    if sword is None:
        return None
    dg = bpy.context.evaluated_depsgraph_get()
    ev = sword.evaluated_get(dg)
    return min((ev.matrix_world @ Vector(c)).z for c in ev.bound_box)


def read_basis(arm, action, frame):
    # Posa di base delle ossa al frame dato, letta direttamente dalle curve dell'action
    # (senza assegnarla né cambiare frame: così non si mescola con l'action in lavorazione).
    vals = {}
    for fc in fcurves_of(action, arm):
        if fc.data_path.startswith("pose.bones["):
            bone = fc.data_path.split('"')[1]
            prop = fc.data_path.rsplit(".", 1)[1]
            vals.setdefault((bone, prop), {})[fc.array_index] = fc.evaluate(frame)
    out = {}
    for b in arm.pose.bones:
        loc = Vector([vals.get((b.name, "location"), {}).get(i, 0.0) for i in range(3)])
        q = vals.get((b.name, "rotation_quaternion"))
        if q is not None:
            from mathutils import Quaternion
            rot = Quaternion([q.get(i, 1.0 if i == 0 else 0.0) for i in range(4)]).normalized().to_matrix()
        elif (b.name, "rotation_euler") in vals:
            from mathutils import Euler
            e = vals[(b.name, "rotation_euler")]
            rot = Euler([e.get(i, 0.0) for i in range(3)], b.rotation_mode
                        if b.rotation_mode not in ("QUATERNION", "AXIS_ANGLE") else "XYZ").to_matrix()
        else:
            rot = Matrix.Identity(3)
        sc = Vector([vals.get((b.name, "scale"), {}).get(i, 1.0) for i in range(3)])
        out[b.name] = Matrix.LocRotScale(loc, rot, sc)
    return out


def pose_at(arm, ax, loco, frame, period, idle_basis, arm_rel, swing_deg, raise_deg):
    pb = arm.pose.bones
    base = read_basis(arm, loco, frame)
    for b in pb:
        b.matrix_basis = base[b.name]
    update()
    # Braccio destro, avambraccio, mano e dita: dalla posa di Sword_Idle.
    for name in right_arm_bones(arm):
        pb[name].matrix_basis = idle_basis[name]
    update()
    # Il braccio segue il bacino, non l'inclinazione del busto: la spada non va a terra.
    hips = world(arm, pb[P + "Hips"]).to_quaternion()
    upper = pb[P + "RightArm"]
    m = world(arm, upper)
    rot = (hips @ arm_rel).to_matrix().to_4x4()
    rot.translation = m.translation
    set_world(arm, upper, rot)
    phase = 2 * math.pi * (frame - SWING_DELAY) / period
    rotate_world(arm, upper, ax["swing"], swing_deg * math.cos(phase))
    if raise_deg:
        # Alza la punta ruotando il polso (positivo = punta su).
        rotate_world(arm, pb[P + "RightHand"], ax["swing"], raise_deg)


def key_all(arm, frame):
    for b in arm.pose.bones:
        b.keyframe_insert("location", frame=frame)
        path = {"QUATERNION": "rotation_quaternion",
                "AXIS_ANGLE": "rotation_axis_angle"}.get(b.rotation_mode, "rotation_euler")
        b.keyframe_insert(path, frame=frame)


def fcurves_of(action, arm):
    # Tutte le curve dell'action (Blender 4.4+: nei channelbag dei layer).
    if hasattr(action, "layers") and action.layers:
        out = []
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out.extend(bag.fcurves)
        return out
    return list(action.fcurves)


def build(arm, ax, loco, name, swing_deg, min_tip, idle_basis, arm_rel, sword):
    period = int(round(loco.frame_range[1] - loco.frame_range[0]))
    frames = list(range(0, period, KEY_STEP))

    old = bpy.data.actions.get(name)
    if old:
        bpy.data.actions.remove(old)
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    arm.animation_data.action = action

    # Quanto alzare la punta: si prova senza correzione e si misura il frame peggiore.
    raise_deg = 0.0
    if sword is not None:
        for _ in range(4):
            worst = math.inf
            for f in frames:
                pose_at(arm, ax, loco, f, period, idle_basis, arm_rel, swing_deg, raise_deg)
                worst = min(worst, sword_tip_z(sword))
            if worst >= min_tip - 0.005:
                break
            # La lama è lunga ~0.85 m dalla mano: un grado alza la punta di ~1.5 cm.
            raise_deg += math.degrees((min_tip - worst) / 0.85)

    worst = math.inf
    for f in frames:
        pose_at(arm, ax, loco, f, period, idle_basis, arm_rel, swing_deg, raise_deg)
        if sword is not None:
            worst = min(worst, sword_tip_z(sword))
        key_all(arm, f)

    for fc in fcurves_of(action, arm):
        kps = sorted(fc.keyframe_points, key=lambda k: k.co.x)
        if not kps:
            continue
        first = kps[0]
        fc.keyframe_points.insert(first.co.x + period, first.co.y, options={"FAST"})
        if not any(m.type == "CYCLES" for m in fc.modifiers):
            fc.modifiers.new("CYCLES")
        fc.update()
    action.use_frame_range = True
    action.frame_start, action.frame_end = 0, period
    action.use_cyclic = True
    return period, raise_deg, worst


def main():
    arm = find_armature()
    if arm.animation_data is None:
        arm.animation_data_create()
    # Le tracce NLA attive sovrascriverebbero la posa calcolata: spente durante il lavoro.
    nla = arm.animation_data.use_nla
    arm.animation_data.use_nla = False
    try:
        run(arm)
    finally:
        arm.animation_data.use_nla = nla


def run(arm):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    if arm.animation_data is None:
        arm.animation_data_create()
    idle = bpy.data.actions.get(IDLE)
    if idle is None:
        raise RuntimeError(f"Manca {IDLE}: le braccia partono dalla sua posa.")
    sword = bpy.data.objects.get(SWORD)
    scene = bpy.context.scene
    ax = axes(arm)

    idle_basis = read_basis(arm, idle, int(idle.frame_range[0]))
    arm.animation_data.action = None
    pb = arm.pose.bones
    for b in pb:
        b.matrix_basis = idle_basis[b.name]
    update()
    hips_idle = world(arm, pb[P + "Hips"]).to_quaternion()
    arm_rel = hips_idle.inverted() @ world(arm, pb[P + "RightArm"]).to_quaternion()

    report = []
    for loco_name, (name, swing, min_tip) in CLIPS.items():
        loco = bpy.data.actions.get(loco_name)
        if loco is None:
            report.append(f"{loco_name} non trovata: {name} saltata")
            continue
        period, raised, worst = build(arm, ax, loco, name, swing, min_tip, idle_basis, arm_rel, sword)
        line = f"{name}: {period} frame, ciclica"
        if sword is not None:
            line += f", punta della spada almeno a {worst * 100:.0f} cm da terra"
            if raised > 0.05:
                line += f" (polso alzato di {raised:.0f} gradi)"
        report.append(line)

    arm.animation_data.action = bpy.data.actions.get("Sword_Arms_Walk")
    scene.frame_set(0)
    report.insert(0, f"[v{VERSION}] Braccia con la spada da {IDLE}")
    print("\n=== create_sword_arms ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title=f"Braccia create (script v{VERSION})", icon="CHECKMARK")


main()

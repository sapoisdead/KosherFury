# Aggiunge l'IK alle gambe del rig Mixamo, con ossa di controllo dentro l'armatura.
# Contratto: Docs/Combat/PIPELINE.md
#
# Crea, per ogni gamba, due ossa NON deform (nella bone collection "Controls"):
#   IK_Foot.L / IK_Foot.R    -> posizione e rotazione del piede (si animano queste)
#   Pole_Knee.L / Pole_Knee.R -> dove punta il ginocchio
# Stanno nell'armatura, quindi i loro keyframe finiscono nella stessa action della mossa.
# L'export FBX va fatto con "Only Deform Bones": le ossa di controllo restano fuori e
# il bake registra la posa finale con l'IK già calcolato.
#
# Uso: con il personaggio in rest pose, Text Editor > Open > Run Script.
# Si può rilanciare: rimuove e ricrea ossa e vincoli.

import bpy
import math
from mathutils import Vector

PREFIX = "mixamorig:"
POLE_DISTANCE = 0.5          # metri davanti al ginocchio
CONTROL_COLLECTION = "Controls"


def find_armature():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"Mi aspetto una sola armatura nella scena, ne trovo {len(arms)}.")
    return arms[0]


def set_mode(arm, mode):
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.hide_set(False)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode=mode)


def clear_previous(arm):
    for pb in arm.pose.bones:
        for c in list(pb.constraints):
            if c.name.startswith("KF_"):
                pb.constraints.remove(c)
    set_mode(arm, "EDIT")
    eb = arm.data.edit_bones
    for name in ("IK_Foot.L", "IK_Foot.R", "Pole_Knee.L", "Pole_Knee.R"):
        if name in eb:
            eb.remove(eb[name])
    set_mode(arm, "OBJECT")


def forward_vector(arm):
    # Avanti del personaggio, dalle anche (come setup_reference_scene.py), in spazio armatura.
    bones = arm.data.bones
    l = bones[f"{PREFIX}LeftUpLeg"].head_local
    r = bones[f"{PREFIX}RightUpLeg"].head_local
    up_local = (arm.matrix_world.inverted().to_3x3() @ Vector((0, 0, 1))).normalized()
    side = (l - r)
    side -= up_local * side.dot(up_local)
    return side.normalized().cross(up_local), up_local


def create_control_bones(arm):
    fwd, up = forward_vector(arm)
    scale = arm.matrix_world.to_scale().x or 1.0
    pole_dist = POLE_DISTANCE / scale

    set_mode(arm, "EDIT")
    eb = arm.data.edit_bones
    coll = arm.data.collections.get(CONTROL_COLLECTION) or arm.data.collections.new(CONTROL_COLLECTION)
    for side, s in (("Left", "L"), ("Right", "R")):
        foot = eb[f"{PREFIX}{side}Foot"]
        knee = eb[f"{PREFIX}{side}Leg"]

        ik = eb.new(f"IK_Foot.{s}")
        ik.head, ik.tail, ik.roll = foot.head.copy(), foot.tail.copy(), foot.roll
        ik.parent = None
        ik.use_deform = False
        coll.assign(ik)

        pole = eb.new(f"Pole_Knee.{s}")
        pole.head = knee.head + fwd * pole_dist
        pole.tail = pole.head + up * (pole_dist * 0.2)
        pole.parent = None
        pole.use_deform = False
        coll.assign(pole)

    # Tutte le ossa Mixamo devono restare deform, o "Only Deform Bones" le scarterebbe.
    fixed = 0
    for b in eb:
        if b.name.startswith(PREFIX) and not b.use_deform:
            b.use_deform = True
            fixed += 1
    set_mode(arm, "OBJECT")
    return fixed


def add_constraints(arm, side, s):
    leg = arm.pose.bones[f"{PREFIX}{side}Leg"]
    foot = arm.pose.bones[f"{PREFIX}{side}Foot"]
    rest_knee = arm.matrix_world @ leg.head

    ik = leg.constraints.new("IK")
    ik.name = "KF_IK"
    ik.target, ik.subtarget = arm, f"IK_Foot.{s}"
    ik.pole_target, ik.pole_subtarget = arm, f"Pole_Knee.{s}"
    ik.chain_count = 2

    rot = foot.constraints.new("COPY_ROTATION")
    rot.name = "KF_FootRotation"
    rot.target, rot.subtarget = arm, f"IK_Foot.{s}"

    # Il pole angle dipende dal roll delle ossa: si sceglie quello che lascia il
    # ginocchio dov'era in rest pose.
    def error(angle_deg):
        ik.pole_angle = math.radians(angle_deg)
        bpy.context.view_layer.update()
        return ((arm.matrix_world @ leg.head) - rest_knee).length

    best = min(range(-180, 180, 5), key=error)
    best = min((best + d * 0.5 for d in range(-10, 11)), key=error)
    return best, error(best)


def main():
    arm = find_armature()
    clear_previous(arm)
    fixed = create_control_bones(arm)

    report = []
    for side, s in (("Left", "L"), ("Right", "R")):
        angle, residual = add_constraints(arm, side, s)
        report.append(f"{side}: pole angle {angle:.1f} gradi, scarto del ginocchio {residual * 1000:.1f} mm")
    if fixed:
        report.append(f"{fixed} ossa Mixamo non erano deform: corrette")
    report.append("Ossa di controllo (collection Controls): IK_Foot.L/R, Pole_Knee.L/R")
    report.append("Export FBX: attivare Only Deform Bones")

    print("\n=== setup_leg_ik ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=x) for x in report],
            title="IK delle gambe pronto", icon="CHECKMARK")


main()

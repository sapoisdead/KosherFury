# Crea la collection REF: bersaglio, anelli di distanza e camera di gioco.
# Contratto: Docs/Combat/PIPELINE.md
#
# Uso: Text Editor di Blender > Open > Run Script, con il file del personaggio aperto.
# Si può rilanciare quando cambiano le misure: ricrea REF da zero.
# Gli oggetti di REF non sono selezionabili né renderizzabili, così non finiscono
# nell'export FBX (che si fa con "Selected Objects").

import bpy
import math
from mathutils import Vector

# --- Misure dal progetto Unity (verificate o misurate, vedi Docs/Combat/HANDOFF.md) ---
TARGET_DISTANCE = 1.05     # distanza fra i centri a cui i colpi di oggi entrano (da rimisurare)
CAPSULE_RADIUS = 0.40      # raggio del CharacterController / capsula del nemico
CAPSULE_HEIGHT = 1.80      # altezza del CharacterController del Rabbi
MIN_DISTANCE = 0.80        # distanza minima fra due corpi (0.40 + 0.40)

CAM_DISTANCE = 4.0         # CameraController.distance
CAM_PITCH_DEG = 28.0       # CameraController.defaultPitch
CAM_HEIGHT = 1.6           # CameraController.heightOffset: punto guardato sopra l'origine
CAM_FOV_DEG = 60.0         # FOV verticale della Main Camera in Unity
CAM_SHOULDER = 0.75        # CameraController.lockShoulderOffset (camera col lock)

REF_NAME = "REF"


def find_armature():
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise RuntimeError(f"Mi aspetto una sola armatura nella scena, ne trovo {len(arms)}.")
    return arms[0]


def facing_direction(arm):
    # Il davanti del personaggio, dalle anche: avanti = sinistra x alto.
    bones = arm.pose.bones
    left = bones.get("mixamorig:LeftUpLeg")
    right = bones.get("mixamorig:RightUpLeg")
    if not (left and right):
        print("  Ossa delle anche non trovate: assumo che il personaggio guardi verso -Y.")
        return Vector((0, -1, 0))
    l = arm.matrix_world @ left.head
    r = arm.matrix_world @ right.head
    side = Vector((l.x - r.x, l.y - r.y, 0)).normalized()
    fwd = side.cross(Vector((0, 0, 1)))
    # Arrotonda all'asse più vicino: in rest pose il personaggio è allineato agli assi.
    axis = max((Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))),
               key=lambda a: a.dot(fwd))
    return axis


def character_height(arm):
    meshes = [o for o in bpy.context.scene.objects
              if o.type == "MESH" and any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)]
    if not meshes:
        return None
    deps = bpy.context.evaluated_depsgraph_get()
    top = -math.inf
    bottom = math.inf
    for o in meshes:
        ev = o.evaluated_get(deps)
        for c in ev.bound_box:
            z = (ev.matrix_world @ Vector(c)).z
            top, bottom = max(top, z), min(bottom, z)
    return top - bottom


def reset_ref_collection():
    old = bpy.data.collections.get(REF_NAME)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(REF_NAME)
    bpy.context.scene.collection.children.link(col)
    return col


def material(name, rgba):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = rgba
    return mat


def lock(obj, col):
    col.objects.link(obj)
    obj.hide_select = True
    obj.hide_render = True
    return obj


def ring(name, radius, col, rgba):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    spline = curve.splines.new("POLY")
    n = 64
    spline.points.add(n)
    for i in range(n + 1):
        a = 2 * math.pi * i / n
        spline.points[i].co = (radius * math.cos(a), radius * math.sin(a), 0.002, 1)
    curve.bevel_depth = 0.006
    obj = bpy.data.objects.new(name, curve)
    obj.data.materials.append(material("REF_" + name, rgba))
    obj.color = rgba
    return lock(obj, col)


def target(col, fwd):
    mesh = bpy.data.meshes.new("REF_Target")
    import bmesh
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=32,
                          radius1=CAPSULE_RADIUS, radius2=CAPSULE_RADIUS, depth=CAPSULE_HEIGHT)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("REF_Target", mesh)
    obj.location = fwd * TARGET_DISTANCE + Vector((0, 0, CAPSULE_HEIGHT / 2))
    obj.display_type = "WIRE"
    obj.show_in_front = True
    obj.color = (1.0, 0.25, 0.2, 1)
    return lock(obj, col)


def game_camera(col, fwd, name, shoulder):
    cam_data = bpy.data.cameras.new(name)
    cam_data.sensor_fit = "VERTICAL"     # Unity misura il FOV in verticale
    cam_data.angle_y = math.radians(CAM_FOV_DEG)
    cam_data.clip_start = 0.05
    cam = bpy.data.objects.new(name, cam_data)

    pitch = math.radians(CAM_PITCH_DEG)
    look = Vector((0, 0, CAM_HEIGHT))
    side = fwd.cross(Vector((0, 0, 1)))  # destra del personaggio
    pos = look - fwd * CAM_DISTANCE * math.cos(pitch) + Vector((0, 0, CAM_DISTANCE * math.sin(pitch)))
    pos += side * shoulder
    look += side * shoulder
    cam.location = pos
    cam.rotation_euler = (look - pos).to_track_quat("-Z", "Y").to_euler()
    return lock(cam, col)


def main():
    arm = find_armature()
    fwd = facing_direction(arm)
    col = reset_ref_collection()

    target(col, fwd)
    ring("Ring_MinDistance_0.80", MIN_DISTANCE, col, (1.0, 0.25, 0.2, 1))
    ring("Ring_Reach_1.05", TARGET_DISTANCE, col, (1.0, 0.8, 0.1, 1))
    ring("Ring_TargetFace_0.65", TARGET_DISTANCE - CAPSULE_RADIUS, col, (0.3, 0.8, 1.0, 1))
    free_cam = game_camera(col, fwd, "CAM_Game", 0.0)
    game_camera(col, fwd, "CAM_Game_Lock", CAM_SHOULDER)
    bpy.context.scene.camera = free_cam

    height = character_height(arm)
    report = [
        f"Il personaggio guarda verso {tuple(round(c) for c in fwd)}",
        f"Bersaglio a {TARGET_DISTANCE} m, faccia anteriore a {TARGET_DISTANCE - CAPSULE_RADIUS:.2f} m",
        f"Altezza del personaggio nella posa attuale: {height:.3f} m" if height
        else "Altezza non misurata: nessuna mesh legata all'armatura",
        f"Action nel file: {len(bpy.data.actions)}",
        "Camera della scena: CAM_Game (CAM_Game_Lock = vista col lock)",
    ]
    print("\n=== setup_reference_scene ===")
    for line in report:
        print("  " + line)
    if not bpy.app.background:
        bpy.context.window_manager.popup_menu(
            lambda self, ctx: [self.layout.label(text=l) for l in report],
            title="REF creata", icon="CHECKMARK")

main()

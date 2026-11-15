"""Export Far Signal Earth part sources to Unity-ready FBX assets.

Run with Blender 4.5+:
blender --background --python Tools/Blender/export_earth_parts.py -- <source folder> <target folder>
"""

import bpy
import bmesh
import csv
import pathlib
import sys
from mathutils import Matrix, Vector


MATERIAL_PALETTE = {
    "M_carbon": ((0.02, 0.035, 0.055, 1.0), 0.15, 0.28),
    "M_copper": ((0.62, 0.22, 0.055, 1.0), 0.85, 0.30),
    "M_foil_gold": ((0.88, 0.45, 0.045, 1.0), 0.90, 0.24),
    "M_hazard": ((0.95, 0.28, 0.015, 1.0), 0.0, 0.38),
    "M_metal_dark": ((0.075, 0.105, 0.15, 1.0), 0.78, 0.32),
    "M_metal_worn": ((0.28, 0.34, 0.40, 1.0), 0.62, 0.52),
    "M_mount_A_cyan": ((0.0, 0.78, 0.86, 1.0), 0.15, 0.30),
    "M_mount_B_orange": ((1.0, 0.26, 0.02, 1.0), 0.10, 0.34),
    "M_paint_black": ((0.012, 0.022, 0.038, 1.0), 0.05, 0.42),
    "M_paint_grey": ((0.18, 0.25, 0.34, 1.0), 0.05, 0.40),
    "M_paint_white": ((0.72, 0.80, 0.86, 1.0), 0.02, 0.34),
    "M_paint_yellow": ((0.98, 0.52, 0.02, 1.0), 0.02, 0.34),
    "M_quilt": ((0.62, 0.68, 0.74, 1.0), 0.02, 0.82),
    "M_solar": ((0.012, 0.07, 0.22, 1.0), 0.28, 0.24),
}


def apply_material_palette():
    for material in bpy.data.materials:
        values = MATERIAL_PALETTE.get(material.name)
        if values is None:
            continue
        color, metallic, roughness = values
        material.diffuse_color = color
        if not material.use_nodes:
            continue
        principled = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
        if principled is None:
            continue
        principled.inputs["Base Color"].default_value = color
        principled.inputs["Metallic"].default_value = metallic
        principled.inputs["Roughness"].default_value = roughness


def mesh_bounds(objects):
    corners = []
    for obj in objects:
        corners.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    minimum = Vector((min(v.x for v in corners), min(v.y for v in corners), min(v.z for v in corners)))
    maximum = Vector((max(v.x for v in corners), max(v.y for v in corners), max(v.z for v in corners)))
    return minimum, maximum


def flatten_evaluated_meshes(source_objects, center, scale):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    exported = []
    for source in source_objects:
        evaluated = source.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
        mesh.transform(Matrix.Scale(scale, 4) @ Matrix.Translation(-center) @ source.matrix_world)
        triangles = bmesh.new()
        triangles.from_mesh(mesh)
        bmesh.ops.triangulate(triangles, faces=list(triangles.faces))
        triangles.to_mesh(mesh)
        triangles.free()
        mesh.update()
        obj = bpy.data.objects.new(source.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        exported.append(obj)

    for obj in list(bpy.context.scene.objects):
        if obj not in exported:
            bpy.data.objects.remove(obj, do_unlink=True)
    return exported


def export_part(source_path, target_path, category):
    bpy.ops.wm.open_mainfile(filepath=str(source_path), load_ui=False)
    apply_material_palette()
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if not meshes:
        raise RuntimeError(f"No mesh objects found in {source_path.name}")

    minimum, maximum = mesh_bounds(meshes)
    center = (minimum + maximum) * 0.5
    # Ship components share one physical scale. Cargo remains large enough to read in the exploded preview.
    scale = 0.8 if category == "cargo" else 0.1
    exported = flatten_evaluated_meshes(meshes, center, scale)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in exported:
        obj.select_set(True)

    target_path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=str(target_path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        bake_space_transform=False,
        path_mode="AUTO",
    )
    print(f"FARSIGNAL_EXPORTED|{source_path.name}|{target_path.name}|meshes={len(exported)}|scale={scale}")


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    if len(args) != 2:
        raise SystemExit("Expected source and target folders after --")
    source_folder = pathlib.Path(args[0]).resolve()
    target_folder = pathlib.Path(args[1]).resolve()
    with (source_folder / "Parts-Index.csv").open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if len(rows) != 30:
        raise RuntimeError(f"Expected 30 mapped parts, found {len(rows)}")
    for row in rows:
        export_part(source_folder / row["File"], target_folder / f"{row['ID']}.fbx", row["Category"])


if __name__ == "__main__":
    main()

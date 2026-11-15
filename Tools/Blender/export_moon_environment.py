"""Export the Moon scene's evaluated mesh instances without modifying its source.

Blender 4.5 or newer: blender -b FarSignal-Moon-70.blend --python
Tools/Blender/export_moon_environment.py -- <output.fbx>

The supplied file reports a newer Blender version. Inspect every exported asset in
Unity and re-export with its authoring Blender version before approving final art.
"""
import bpy
import pathlib
import sys
from mathutils import Vector


def relocate_cart_mark(pad, original_cart, terrain):
    """Move just the small yellow cart rectangle, keeping the landing ring."""
    mesh = pad.data
    inverse = pad.matrix_world.inverted()
    terrain_inverse = terrain.matrix_world.inverted()
    selected = set()
    for face in mesh.polygons:
        material = mesh.materials[face.material_index] if face.material_index < len(mesh.materials) else None
        if material is None or 'Paint_Edge' not in material.name:
            continue
        points = [pad.matrix_world @ mesh.vertices[i].co for i in face.vertices]
        if all(abs(p.x - original_cart.x) < 1.45 and abs(p.y - original_cart.y) < 1.8 and
               p.z < original_cart.z + .15 for p in points):
            selected.update(face.vertices)
    if not selected:
        raise RuntimeError('Cart parking rectangle not found in ' + pad.name)
    bottom = min((pad.matrix_world @ mesh.vertices[i].co).z for i in selected)
    for i in selected:
        point = pad.matrix_world @ mesh.vertices[i].co
        thickness = point.z - bottom
        # Blender +X becomes Unity -X: the same offset used for the cart.
        point.x += 5.5
        origin = terrain_inverse @ Vector((point.x, point.y, point.z + 30))
        direction = (terrain_inverse.to_3x3() @ Vector((0, 0, -1))).normalized()
        hit, location, _, _ = terrain.ray_cast(origin, direction, distance=1000)
        if not hit:
            raise RuntimeError('No terrain below cart mark in ' + pad.name)
        point.z = (terrain.matrix_world @ location).z + .02 + thickness
        mesh.vertices[i].co = inverse @ point
    mesh.update()
    print('FARSIGNAL_MOVED_CART_MARK', pad.name, 'vertices=', len(selected))


def main():
    args = sys.argv[sys.argv.index('--') + 1:]
    if len(args) != 1:
        raise SystemExit('Expected one output path after --')
    target = pathlib.Path(args[0]).resolve()
    if target.exists():
        raise SystemExit('Refusing to overwrite an existing export: ' + str(target))
    depsgraph = bpy.context.evaluated_depsgraph_get()
    exported = []
    # Copy evaluated meshes and instance world transforms into an isolated collection.
    # The source .blend is never saved.
    instances = [(item.object.original, item.matrix_world.copy())
                 for item in depsgraph.object_instances if item.object.type == 'MESH']
    collection = bpy.data.collections.new('FarSignal Unity Export')
    bpy.context.scene.collection.children.link(collection)
    for index, (original, matrix) in enumerate(instances):
        source = original.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(source, depsgraph=depsgraph)
        if not mesh or not mesh.vertices:
            continue
        obj = bpy.data.objects.new('Moon_%03d_%s' % (index, original.name), mesh)
        collection.objects.link(obj)
        obj.matrix_world = matrix
        exported.append(obj)
    bpy.context.view_layer.update()
    def named(suffix):
        return next((obj for obj in exported if obj.name.endswith('_' + suffix)), None)
    cart, beacon, ridge, terrain = (named(name) for name in
        ('Cart_Chassis', 'LandingPad_Beacon', 'LandingPad_Ridge', 'Moon_Terrain'))
    if not all((cart, beacon, ridge, terrain)):
        raise RuntimeError('Moon export is missing a pad, cart, or terrain mesh')
    relocate_cart_mark(beacon, cart.matrix_world.translation, terrain)
    ridge_cart = cart.matrix_world.translation + ridge.matrix_world.translation - beacon.matrix_world.translation
    relocate_cart_mark(ridge, ridge_cart, terrain)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in exported:
        obj.select_set(True)
    target.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=True, object_types={'MESH'},
        use_mesh_modifiers=False, add_leaf_bones=False, axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, path_mode='AUTO')
    print('FARSIGNAL_MOON_EXPORT meshes=%d path=%s' % (len(exported), target))


if __name__ == '__main__':
    main()

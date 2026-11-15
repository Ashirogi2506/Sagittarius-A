"""Export the Moon scene's evaluated mesh instances without modifying its source.

Blender 4.5 or newer: blender -b FarSignal-Moon-70.blend --python
Tools/Blender/export_moon_environment.py -- <output.fbx>

The supplied file reports a newer Blender version. Inspect every exported asset in
Unity and re-export with its authoring Blender version before approving final art.
"""
import bpy
import pathlib
import sys


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

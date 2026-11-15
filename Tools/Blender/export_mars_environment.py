"""Read-only Mars export: evaluated meshes, independent parts and baked terrain.
blender -b --factory-startup --disable-autoexec --python this.py -- source.blend output preview.png
"""
import bpy, sys, pathlib, json
from mathutils import Vector

def bake_terrain(obj, path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UnityTerrainUV')
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    for loop in obj.data.loops:
        p=points[loop.vertex_index]; uv.data[loop.index].uv=((p.x+200)/400,(p.y+200)/400)
    image=bpy.data.images.new('MarsTerrain',width=2048,height=2048,alpha=False)
    for index, source in enumerate(list(obj.data.materials)):
        mat=source.copy(); obj.data.materials[index]=mat
        nodes=mat.node_tree.nodes; links=mat.node_tree.links
        bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
        emission=nodes.new('ShaderNodeEmission'); base=bsdf.inputs['Base Color']
        if base.is_linked: links.new(base.links[0].from_socket,emission.inputs['Color'])
        else: emission.inputs['Color'].default_value=base.default_value
        links.new(emission.outputs[0],output.inputs['Surface'])
        texture=nodes.new('ShaderNodeTexImage'); texture.image=image; nodes.active=texture
    scene=bpy.context.scene; scene.render.engine='CYCLES'; scene.cycles.device='CPU'; scene.cycles.samples=1
    scene.render.bake.margin=4; scene.render.bake.use_clear=True
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw=str(path); image.file_format='PNG'; image.save()
    mat=bpy.data.materials.new('MarsTerrain'); mat.use_nodes=True
    nodes=mat.node_tree.nodes; texture=nodes.new('ShaderNodeTexImage'); texture.image=image
    mat.node_tree.links.new(texture.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
    mat.diffuse_color=(.34,.14,.07,1); obj.data.materials.clear(); obj.data.materials.append(mat)
    for polygon in obj.data.polygons: polygon.material_index=0

def main():
    source, output, preview=sys.argv[sys.argv.index('--')+1:]
    out=pathlib.Path(output).resolve(); out.mkdir(parents=True,exist_ok=True); preview=str(pathlib.Path(preview).resolve()); source=str(pathlib.Path(source).resolve())
    bpy.ops.wm.open_mainfile(filepath=source)
    deps=bpy.context.evaluated_depsgraph_get()
    snapshots=[]
    for instance in deps.object_instances:
        o=instance.object
        if o.type!='MESH' or o.original.hide_render or o.original.name.startswith('Atmosphere |'): continue
        mesh=bpy.data.meshes.new_from_object(o,depsgraph=deps)
        snapshots.append((o.original.name,mesh,instance.matrix_world.copy()))
    anchors={o.name:list(o.matrix_world.translation) for o in bpy.context.scene.objects if o.type=='EMPTY' and (o.name in ['MarsSpawn','AssemblyBench','LoadingLane','DustDiary'] or o.name.startswith('Pickup_'))}
    anchors.update({'Origin':[0,0,0],'AxisX':[1,0,0],'AxisY':[0,1,0],'AxisZ':[0,0,1]})
    for o in list(bpy.context.scene.objects):
        if o.type in {'MESH','FONT'}:
            o.hide_render=True
            if o.name in bpy.context.view_layer.objects: o.hide_set(True)
        o.name='SOURCE_'+o.name
    coll=bpy.data.collections.new('Unity Mars Export'); bpy.context.scene.collection.children.link(coll)
    exported=[]
    for name,mesh,matrix in snapshots:
        obj=bpy.data.objects.new(name,mesh);coll.objects.link(obj);obj.matrix_world=matrix;exported.append(obj)
    empties=[]
    for name,position in anchors.items():
        obj=bpy.data.objects.new('Anchor_'+name,None);coll.objects.link(obj);obj.location=position;empties.append(obj)
    terrain=next(o for o in exported if o.name=='Mars_Terrain')
    bake_terrain(terrain,out/'MarsTerrain.png')
    specs=[]
    for material in {m for o in exported for m in o.data.materials if m}:
        node=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if material.use_nodes else None
        color=list(node.inputs['Base Color'].default_value) if node and not node.inputs['Base Color'].is_linked else list(material.diffuse_color)
        specs.append({'name':material.name,'color':color,'metallic':node.inputs['Metallic'].default_value if node else 0,'roughness':node.inputs['Roughness'].default_value if node else .75})
    bpy.ops.object.select_all(action='DESELECT')
    for obj in exported+empties:obj.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(out/'MarsEnvironment.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    (out/'MarsMaterials.json').write_text(json.dumps({'materials':specs},indent=2),encoding='utf-8')
    (out/'MarsExport.json').write_text(json.dumps({'meshCount':len(exported),'anchors':anchors,'terrainWidth':400,'source':pathlib.Path(source).name},indent=2),encoding='utf-8')
    scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
    camera=bpy.data.objects.new('Export overview',bpy.data.cameras.new('Export overview'));scene.collection.objects.link(camera)
    camera.location=(-27,-33,23);camera.rotation_euler=(Vector((2,1,1.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=42
    scene.camera=camera;scene.render.filepath=preview;bpy.ops.render.render(write_still=True)
    print('MARS_EXPORT_OK',len(exported),'meshes',len(anchors),'anchors',flush=True)

if __name__=='__main__':main()

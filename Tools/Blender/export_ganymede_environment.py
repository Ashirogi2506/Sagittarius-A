"""Export one shared environment and independent construction-state objects.

blender -b --factory-startup --disable-autoexec --python this.py -- start.blend completed.blend output_directory preview.png
Source files are read only. Blender procedural base colors are baked for Unity.
"""
import bpy, sys, pathlib, json, hashlib, array
from mathutils import Vector

def fingerprint(obj, matrix):
    vertices=array.array('f',[0.0])*(len(obj.data.vertices)*3);obj.data.vertices.foreach_get('co',vertices)
    return hashlib.sha256(vertices.tobytes()+str(tuple(v for row in matrix for v in row)).encode()).hexdigest()

def bake_color(obj,path,resolution,planar=False):
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if planar:
        uv=obj.data.uv_layers.new(name='UnityBakedUV') if not obj.data.uv_layers else obj.data.uv_layers.active
        xy=[(obj.matrix_world@v.co) for v in obj.data.vertices];minx=min(v.x for v in xy);maxx=max(v.x for v in xy);miny=min(v.y for v in xy);maxy=max(v.y for v in xy)
        for loop in obj.data.loops:
            v=xy[loop.vertex_index];uv.data[loop.index].uv=((v.x-minx)/(maxx-minx),(v.y-miny)/(maxy-miny))
    image=bpy.data.images.new(path.stem,width=resolution,height=resolution,alpha=False)
    material=obj.data.materials[0].copy();obj.data.materials[0]=material
    nodes=material.node_tree.nodes;links=material.node_tree.links;principled=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    base=principled.inputs['Base Color'];emission=nodes.new('ShaderNodeEmission')
    if base.is_linked:links.new(base.links[0].from_socket,emission.inputs['Color'])
    else:emission.inputs['Color'].default_value=base.default_value
    links.new(emission.outputs[0],output.inputs['Surface'])
    texture=nodes.new('ShaderNodeTexImage');texture.image=image;nodes.active=texture
    bpy.context.scene.render.engine='CYCLES';bpy.context.scene.cycles.device='CPU';bpy.context.scene.cycles.samples=1
    bpy.context.scene.render.bake.margin=4;bpy.context.scene.render.bake.use_clear=True
    bpy.ops.object.bake(type='EMIT')
    image.filepath_raw=str(path);image.file_format='PNG';image.save()
    links.new(principled.outputs[0],output.inputs['Surface']);links.new(texture.outputs['Color'],base);nodes.remove(emission)
    # Exported material references the baked texture rather than unsupported Blender nodes.
    material.name=path.stem
    print('GANYMEDE_BAKED',path.name,flush=True)
    return material

def main():
    args=sys.argv[sys.argv.index('--')+1:];start,complete,output,preview=args;out=pathlib.Path(output);out.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=start);deps=bpy.context.evaluated_depsgraph_get()
    original={i.object.original.name:fingerprint(i.object,i.matrix_world) for i in deps.object_instances if i.object.type=='MESH'}
    bpy.ops.wm.open_mainfile(filepath=complete);deps=bpy.context.evaluated_depsgraph_get();instances=[(i.object.original,i.object,i.matrix_world.copy()) for i in deps.object_instances if i.object.type=='MESH']
    anchors={o.name:list(o.matrix_world.translation) for o in bpy.data.objects if o.type=='EMPTY' and o.name in ['crater-plain','grooved-rise','photo-overlook','PlayerSpawn','Lander_Return']}
    collection=bpy.data.collections.new('Unity export');bpy.context.scene.collection.children.link(collection)
    groups={}
    for name in ['SharedEnvironment','ArrivalEquipment','Built_Radiation','Built_IceSurvey','Built_Uplink','Anchors']:
        group=bpy.data.objects.new(name,None);collection.objects.link(group);groups[name]=group
    exported=[];changed=[];shared=0
    for original_obj,evaluated,matrix in instances:
        name=original_obj.name
        if name.startswith('Radiation station |'):group='Built_Radiation'
        elif name.startswith('Ice survey station |'):group='Built_IceSurvey'
        elif name.startswith('Uplink relay |'):group='Built_Uplink'
        elif name in original:
            group='SharedEnvironment';shared+=1
            if original[name]!=fingerprint(evaluated,matrix):changed.append(name)
        else:group='ArrivalEquipment'
        mesh=bpy.data.meshes.new_from_object(evaluated,depsgraph=deps)
        obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);obj.parent=groups[group];obj.matrix_world=matrix;exported.append(obj)
    # Original names get .001 suffixes in the working file, so strip the generated suffix at export after originals are unlinked.
    originals=[o for o in bpy.context.scene.objects if o.name not in {o.name for o in exported} and o not in groups.values()]
    for obj in originals:
        if obj.type in {'MESH','FONT'}:obj.hide_render=True;obj.hide_set(True)
    for name,position in anchors.items():
        anchor=bpy.data.objects.new('Anchor_'+name,None);collection.objects.link(anchor);anchor.parent=groups['Anchors'];anchor.location=position
    find=lambda stem:next(o for o in exported if o.name.startswith(stem))
    terrain=find('Ganymede | continuous terrain');horizon=find('Ganymede | 360 degree');jupiter=find('Jupiter | procedural')
    bake_color(terrain,out/'GanymedeTerrain.png',2048,True)
    bake_color(horizon,out/'GanymedeHorizon.png',1024,True)
    bake_color(jupiter,out/'GanymedeJupiter.png',1024,False)
    materials=[]
    for material in {m for o in exported for m in o.data.materials if m}:
        principled=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if material.use_nodes else None
        materials.append({'name':material.name,'color':list(material.diffuse_color),'metallic':principled.inputs['Metallic'].default_value if principled else 0,'roughness':principled.inputs['Roughness'].default_value if principled else .7})
    bpy.ops.object.select_all(action='DESELECT')
    for o in list(exported)+list(groups.values())+[o for o in collection.objects if o.name.startswith('Anchor_')]:o.hide_set(False);o.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(out/'GanymedeEnvironment.fbx'),use_selection=True,object_types={'MESH','EMPTY'},use_mesh_modifiers=False,add_leaf_bones=False,axis_forward='-Z',axis_up='Y',path_mode='AUTO')
    (out/'GanymedeMaterials.json').write_text(json.dumps({'materials':materials},indent=2),encoding='utf-8')
    (out/'GanymedeExport.json').write_text(json.dumps({'sharedMeshes':shared,'changedSharedMeshes':changed,'meshCount':len(exported),'anchors':anchors,'groups':{g:sum(o.parent==p for o in exported) for g,p in groups.items()}},indent=2),encoding='utf-8')
    scene=bpy.context.scene;scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.display.shading.light='STUDIO';scene.display.shading.color_type='MATERIAL';scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True;scene.display.shading.background_type='WORLD';scene.world.color=(.015,.018,.025)
    scene.camera=bpy.data.objects.get('Camera | Overview');scene.render.filepath=preview;bpy.ops.render.render(write_still=True)
    print('GANYMEDE_EXPORT',len(exported),'shared',shared,'changed',changed,flush=True)

if __name__=='__main__':main()

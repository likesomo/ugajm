"""MCP proof render from the actual baked atlas and retained XYZ normals."""
import bpy,math
from mathutils import Vector
from pathlib import Path

board_scene=bpy.data.scenes['BoardShapeA'];material=next(o for o in board_scene.objects if o.type=='MESH').data.materials[0]
if not any(n.type=='NEW_GEOMETRY' for n in material.node_tree.nodes):
    # Apply the same top/side roughness change to an already-running bake scene.
    nodes=material.node_tree.nodes;links=material.node_tree.links;bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    source=bs.inputs['Roughness'].links[0].from_socket
    geo=nodes.new('ShaderNodeNewGeometry');xyz=nodes.new('ShaderNodeSeparateXYZ');links.new(geo.outputs['Normal'],xyz.inputs[0])
    slope=nodes.new('ShaderNodeMath');slope.operation='MULTIPLY_ADD';slope.inputs[1].default_value=-.32;slope.inputs[2].default_value=.20;links.new(xyz.outputs['Z'],slope.inputs[0])
    add=nodes.new('ShaderNodeMath');add.operation='ADD';add.use_clamp=True;links.new(source,add.inputs[0]);links.new(slope.outputs[0],add.inputs[1]);links.new(add.outputs[0],bs.inputs['Roughness'])
old=bpy.data.scenes.get('BSA_StoneLibrary')
if old:
    for ob in list(old.objects):bpy.data.objects.remove(ob,do_unlink=True)
    bpy.data.scenes.remove(old)
gallery=bpy.data.scenes.new('BSA_StoneLibrary');bpy.context.window.scene=gallery
names=['MOSS / GRANITE','HOLLOW / BASALT','CARVED / SANDSTONE','FROST / MARBLE','JOINERY / SLATE','CRUMBLE / LIMESTONE','ALTAR / SARSEN']
slots=[0,8,9,10,11,12,13]
ink=bpy.data.materials.new('BSA_StoneLibrary_Ink');ink.diffuse_color=(.85,.88,.82,1)
for i,(name,slot) in enumerate(zip(names,slots)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=((i-3)*2.15,0,.35));ob=bpy.context.object;ob.name='Proof_'+name;ob.dimensions=(1.85,1.65,.70)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel=ob.modifiers.new('Worn stone bevel','BEVEL');bevel.width=.07;bevel.segments=1;bpy.ops.object.modifier_apply(modifier=bevel.name)
    mesh=ob.data;uv=mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap');mesh.uv_layers.active=uv;mesh.materials.append(material)
    attr=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for c in attr.data:c.color=(1,1,1,1)
    for p in mesh.polygons:
        axis=max(range(3),key=lambda a:abs(p.normal[a]));a,b=[(1,2),(0,2),(0,1)][axis]
        lo=[min(v.co[j] for v in mesh.vertices) for j in range(3)];hi=[max(v.co[j] for v in mesh.vertices) for j in range(3)]
        for li in p.loop_indices:
            v=mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv=((slot%4+.04+.92*(v[a]-lo[a])/(hi[a]-lo[a]))/4,(slot//4+.04+.92*(v[b]-lo[b])/(hi[b]-lo[b]))/4)
    text=bpy.data.curves.new('Proof_label','FONT');text.body=name;text.align_x='CENTER';text.size=.15;text.materials.append(ink)
    label=bpy.data.objects.new(name,text);gallery.collection.objects.link(label);label.location=((i-3)*2.15,-1.15,.0);label.rotation_euler.x=math.pi/2
bpy.ops.object.camera_add(location=(0,-12,13));cam=bpy.context.object;cam.rotation_euler=(Vector((0,0,.05))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=15.7;gallery.camera=cam
bpy.ops.object.light_add(type='SUN',location=(-5,-5,8));sun=bpy.context.object;sun.data.energy=3.0;sun.data.angle=.06;sun.rotation_euler=Vector((.627,.439,-.643)).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('StoneLibrary_World');gallery.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.03,.04,.065,1);world.node_tree.nodes['Background'].inputs[1].default_value=.35
gallery.render.engine='CYCLES';gallery.cycles.samples=32;gallery.render.resolution_x=2560;gallery.render.resolution_y=900;gallery.render.resolution_percentage=100;gallery.view_settings.view_transform='AgX'
output=Path('C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/stone-pass/after')
gallery.render.filepath=str(output/'blender-stone-library.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=board_scene;bpy.ops.wm.save_as_mainfile(filepath='C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_BoardShapeA.blend')
result={'library':str(output/'blender-stone-library.png'),'stone_slots':slots}

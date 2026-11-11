"""Render a procedural prop/state contact sheet from the saved Blender source."""
import bpy, math, os
from mathutils import Vector
ROOT='C:/unityjam/shared-assets/seven-steps'
bpy.ops.wm.open_mainfile(filepath=ROOT+'/Source/Blender/SS_RuinsHenge_StageProps.blend')
scene=bpy.context.scene
stone=bpy.data.materials['RH_Stone'];stone.use_nodes=True
bs=stone.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(.22,.28,.23,1);bs.inputs['Roughness'].default_value=.85
noise=stone.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=25
bump=stone.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.2;bump.inputs['Distance'].default_value=.035
stone.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);stone.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
def mat(name,color,emission=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*color,1);b.inputs['Roughness'].default_value=.5;b.inputs['Emission Color'].default_value=(*color,1);b.inputs['Emission Strength'].default_value=emission;return m
colors=[(.93,.66,.22),(.31,.75,.91),(.77,.48,.89),(.27,.78,.56)]
accents=[mat('Inlay_'+str(i),c,.45) for i,c in enumerate(colors)]
ink=mat('Ink',(.025,.045,.035));paper=mat('Warm paper',(.70,.69,.63))
def hide_tree(o,hidden):
 o.hide_render=hidden
 for child in o.children:hide_tree(child,hidden)
def text(s,x,y,size=.11):
 c=bpy.data.curves.new('Caption','FONT');c.body=s;c.align_x='CENTER';c.size=size;c.extrude=0
 o=bpy.data.objects.new('Caption',c);scene.collection.objects.link(o);o.location=(x,y,.03);o.rotation_euler.x=math.radians(40);c.materials.append(ink)
def copy(kind,x,y,variant=0,open=False,number=3,filled=0):
 src=bpy.data.collections['Prop_'+kind];dst=bpy.data.collections.new('Sheet_'+kind);scene.collection.children.link(dst);mapping={}
 for original in src.objects:
  o=original.copy();dst.objects.link(o);mapping[original]=o;o.hide_render=False
 for original,o in mapping.items():o.parent=mapping.get(original.parent)
 root=next(o for o in mapping.values() if o.parent is None);root.location=(x,y,0)
 for o in mapping.values():
  if o.name.split('.')[0].startswith('Glyph_'):hide_tree(o,int(o.name.split('_')[1].split('.')[0])!=variant)
  if o.name.split('.')[0].startswith('Lamp_'):hide_tree(o,int(o.name.split('_')[1].split('.')[0])>=number)
  if o.name.split('.')[0].startswith('Socket_'):hide_tree(o,int(o.name.split('_')[1].split('.')[0])>=number)
  if o.name.split('.')[0].startswith('Token_'):hide_tree(o,int(o.name.split('_')[1].split('.')[0])>=filled)
  if o.name.split('.')[0]=='Slab' and open:o.location.z-=.61
  if o.type=='MESH' and o.name.startswith('inlay_'):
   for slot in o.material_slots:slot.link='OBJECT';slot.material=accents[variant]
 return root
shots=[('Exit','Star exit',0,False,0,0),('Plate','Sun plate',0,False,0,0),('Door','Sun / closed',0,False,0,0),('Door','Sun / lowered',0,True,0,0),('Gate','3 lamps',0,False,3,0),('Gate','7 lamps',0,False,7,0),('Plate','Moon plate',1,False,0,0),('Door','Moon door',1,False,0,0),('Plate','Star plate',2,False,0,0),('Door','Star door',2,False,0,0),('Plate','Knot plate',3,False,0,0),('Door','Knot door',3,False,0,0),('CountDoor','2 sockets / empty',0,False,2,0),('CountDoor','1 of 2 statues',0,False,2,1),('CountDoor','2 of 2 / lowered',0,True,2,2),('CountDoor','7 sockets / filled',0,True,7,7),('FlipUp','Raised barrier',0,False,0,0),('FlipDown','Lowered barrier',0,False,0,0),('Bridge','Stone causeway',0,False,0,0),('Cracked','Fractured slabs',0,False,0,0),('Ice','Ice + crystal',1,False,0,0),('Lever','Lever',0,False,0,0),('Footprint','Footprints',1,False,0,0),('Mud','Mud ripples',0,False,0,0),('Start','Single start ring',1,False,0,0)]
for index,(kind,label,variant,open,number,filled) in enumerate(shots):
 x=(index%6-2.5)*1.6;y=(2-index//6)*2.1
 copy(kind,x,y,variant,open,number,filled);text(label,x,y-.84,.115)
text('RUINSHENGE  /  STAGE PROPS',0,6.0,.27)
text('Blender mesh geometry   -   matching glyph + accent   -   counted lamps and statue sockets',0,5.53,.12)
text('All instances below 1,400 triangles, including stored variants   /   camera: orthographic, pitch 50 degrees',0,-5.5,.12)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.20));bpy.context.object.data.materials.append(paper)
world=bpy.data.worlds.new('Paper studio');scene.world=world;world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.65,.69,.72,1);world.node_tree.nodes['Background'].inputs[1].default_value=.3
for location,power,size in [((-4,-5,10),850,7),((6,4,7),600,6)]:
 bpy.ops.object.light_add(type='AREA',location=location);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(Vector((0,0,0))-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(0,-14,16.68));cam=bpy.context.object;cam.rotation_euler=(Vector((0,.45,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=12.0;scene.camera=cam
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=2400;scene.render.resolution_y=2000;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=ROOT+'/concepts/ui/props-2026-11-12/props-sheet.png';bpy.ops.render.render(write_still=True)

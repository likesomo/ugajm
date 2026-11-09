import bpy, math, random, shutil
from pathlib import Path
from mathutils import Vector
root=Path('C:/unityjam/shared-assets/seven-steps')
out=root/'concepts/themes/ruins-henge-planets/seven-2026-11-10';out.mkdir(parents=True,exist_ok=True)
ui=root/'concepts/ui';backup=ui/'before-2026-11-10';backup.mkdir(exist_ok=True)
for lang in ['en','ko']:
 if not (backup/lang).exists():shutil.copytree(ui/lang,backup/lang)
bg=root/'Assets/UI/RuinsHenge/Handwritten_Background.png'
if bg.exists() and not (backup/'Handwritten_Background.png').exists():shutil.copy2(bg,backup/'Handwritten_Background.png')
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('DeepSpace');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.012,.025,.06,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45

def mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1);m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.86;return m
stone=mat('Granite',(.31,.40,.43));wood=mat('Weathered timber',(.26,.21,.14));moss=mat('Moss',(.21,.32,.15));brass=mat('Wedge end',(.48,.37,.17));ground=mat('Planet surface',(.21,.29,.23))
def cube(name,p,s,m):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.dimensions=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m);b=o.modifiers.new('Worn edges','BEVEL');b.width=.07;b.segments=1;return o

def rock(name,p,s,m):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=p);o=bpy.context.object;o.name=name;o.scale=s;o.data.materials.append(m);return o
rock('Board supporting planet',(0,0,-2),(4.7,4.1,2.1),ground)
for x in range(-2,3):
 for y in range(-2,3):cube('Summit tile',(x,y,.03),(.96,.96,.18),stone)
# An asymmetric braced timber repair attached to three separate standing stones.
# One straight diagonal beam, exposed tenon ends and transverse wedges; no paired lintels.
for x,y,h in [(-2.7,1.7,2.1),(.2,2.6,1.4),(2.4,1.6,2.7)]:
 rock('Independent menhir',(x,y,h*.45),(.40,.35,h*.6),stone)
beam=cube('Single straight mortise beam',(.5,1.7,1.72),(5.1,.30,.32),wood);beam.rotation_euler.y=.14
brace=cube('Diagonal timber brace',(1.55,1.7,1.0),(.23,.26,1.8),wood);brace.rotation_euler.y=-.7
for x in [-1.7,1.3,2.6]:
 cube('Exposed tenon',(x,1.7,1.72),(.22,.65,.20),wood)
 wedge=cube('Visible cross wedge',(x,1.33,1.79),(.36,.10,.25),brass);wedge.rotation_euler.y=.22
for i in range(24):
 a=i*math.tau/24;rock('Moss edge',(3.3*math.cos(a),3.1*math.sin(a),-.06),(.34,.27,.15),moss)
moons=[rock('Distant planet',(-5,4,2),(.7,.7,.7),ground),rock('Distant satellite',(5,5,3),(.43,.43,.43),stone)]
random.seed(74)
star=mat('Starlight',(.7,.8,1));bs=star.node_tree.nodes['Principled BSDF'];bs.inputs['Emission Color'].default_value=(.4,.6,1,1);bs.inputs['Emission Strength'].default_value=2
for i in range(75):rock('Star',(random.uniform(-15,15),8,random.uniform(-2,12)),(.018,.018,.018),star)
for name,p,power,color in [('Key',(-4,-5,9),1600,(1,.85,.64)),('Fill',(5,0,6),1100,(.55,.72,1))]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=7;d.color=color;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=p;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('Camera');camera=bpy.data.objects.new('Camera',d);scene.collection.objects.link(camera);scene.camera=camera;camera.location=(8,-13,10);camera.rotation_euler=(Vector((0,0,.2))-camera.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=16
bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source/Blender/SS_RuinsHenge_UI_MenhirJoinery.blend'))
scene.render.filepath=str(bg);bpy.ops.render.render(write_still=True)
shutil.copy2(bg,ui/'menhir-joinery-background.png')
styles=[('moss',(.21,.29,.23)),('hollow',(.17,.24,.29)),('carved',(.38,.32,.23)),('frost',(.48,.64,.69)),('joinery',(.32,.24,.16)),('crumble',(.34,.28,.26)),('altar',(.22,.31,.30))]
for idx,(name,color) in enumerate(styles):
 ground.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*color,1)
 added=[]
 if name=='carved':
  for x in [-2,-1,0,1,2]:
   added.append(cube('Counting pillar',(x,2.7,.65),(.4,.4,1.3),stone))
   for z in [.3,.6,.9]:added.append(cube('Carved tally',(x,2.48,z),(.23,.04,.05),wood))
 if name=='hollow':
  for x in [-1.5,1.5]:added.append(rock('Hollow rim',(x,-1,-.2),(.6,.6,.18),wood))
 if name=='crumble':
  for x in [-2,0,2]:added.append(cube('Detached fallen fragment',(x,-3,-.7),(.8,.6,.4),stone))
 if name=='altar':
  for j in range(7):
   a=j*math.tau/7;added.append(rock('Stone circle',(3*math.cos(a),3*math.sin(a),.6),(.25,.25,.75),stone))
 if name=='frost':
  for x in [-2,2]:added.append(cube('Frost rail',(x,0,.3),(.14,4,.2),wood))
 scene.render.filepath=str(out/(name+'.png'));bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source/Blender'/('SS_RuinsHenge_'+name+'.blend')));bpy.ops.render.render(write_still=True)
 for o in added:bpy.data.objects.remove(o,do_unlink=True)

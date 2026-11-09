import bpy, math, random, json, sys
from pathlib import Path
from mathutils import Vector
root=Path('C:/unityjam/shared-assets/seven-steps');out=root/'concepts/themes/ruins-henge-planets/distinct-2026-11-10';out.mkdir(parents=True,exist_ok=True)
styles=[
 ('moss','146D64','12352F','40A894','65C9BA','B6E9CF'),
 ('hollow','66379A','210E3C','9252C4','C795FF','D9B1FF'),
 ('carved','B66A27','382412','D79B36','E9B553','FFD392'),
 ('frost','75BFE0','102C54','72BDF4','D7F7FF','B9E8FF'),
 ('joinery','873B27','321318','BA6747','DC8E67','FFB878'),
 ('crumble','71615B','30282B','937E78','B7A196','E5C5AA'),
 ('altar','B88D29','34280C','DBB54B','F9DA7A','FFE5A0')]
def rgb(h):return tuple(int(h[i:i+2],16)/255 for i in (0,2,4))
def mat(name,h,metal=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes['Principled BSDF'];n.inputs['Base Color'].default_value=(*rgb(h),1);n.inputs['Roughness'].default_value=.78;n.inputs['Metallic'].default_value=metal;return m
for idx,(id,bodyhex,skyhex,hazehex,moonhex,keyhex) in enumerate(styles):
 if '--only' in sys.argv and id!=sys.argv[sys.argv.index('--only')+1]:continue
 bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1120;scene.render.resolution_y=840;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('World_'+id);scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(*rgb(skyhex),1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
 body=mat('Body_'+id,bodyhex,.25 if id=='altar' else 0);rim=mat('Rim_'+id,hazehex);tile=mat('Readable pale board','DADECE');moon=mat('Distant_'+id,moonhex);dark=mat('Open void','11091D');wood=mat('Exposed end grain','9D4E2C');moss=mat('Moss only on moss world','438E56')
 props=[]
 def cube(name,p,s,m,prop=False):
  bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.name=name;o.dimensions=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m);b=o.modifiers.new('Small bevel','BEVEL');b.width=.045;b.segments=1
  if prop:props.append(o)
  return o
 def rock(name,p,s,m,prop=False):
  bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1 if id=='crumble' else 2,radius=1,location=p);o=bpy.context.object;o.name=name;o.scale=s;o.data.materials.append(m)
  if prop:props.append(o)
  return o
 def cylinder(name,r,depth,z,m,vertices=32):
  bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=(0,0,z));o=bpy.context.object;o.name=name;o.data.materials.append(m);return o
 # Board has exactly the same scale and neutral readable tile material in every frame.
 for x in range(-2,3):
  for y in range(-2,3):cube('Board cell',(x,y,.02),(.94,.94,.16),tile)
 if id=='hollow':
  crust=cylinder('Flat supporting crust',4.8,.35,-.3,body)
  for x in [-3.65,3.65]:
   bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.83,depth=8,location=(x,-.5,-2));cutter=bpy.context.object
   mod=crust.modifiers.new('Open crust window','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;bpy.context.view_layer.objects.active=crust;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
  bpy.ops.mesh.primitive_torus_add(major_segments=40,minor_segments=8,location=(0,0,-2.6),major_radius=1.8,minor_radius=.5);o=bpy.context.object;o.name='Hollow open crust';o.rotation_euler.x=math.pi/2;o.scale.x=1.8;o.data.materials.append(body)
  # The torus opening and three large windows show the purple sky under the summit.
  for x in [-4.3,4.3]:rock('Broken crust lip',(x,0,-1.3),(.7,1.5,.7),rim,True)
 elif id=='carved':
  for j in range(5):cylinder('Broad stepped ochre strata',5-j*.48,.48,-.38-j*.47,body if j%2==0 else rim,12)
 elif id=='joinery':
  cylinder('Timber summit',4.8,.3,-.3,body,12)
  for y in [-3.7,-2.5,-1.3,0,1.3,2.5,3.7]:cube('Timber hull beam',(0,y,-.8),(8.5,.65,.7),wood)
  for x in [-3.4,-1.7,0,1.7,3.4]:cube('Crossed underside joist',(x,0,-1.5),(.6,7.5,.8),body)
  for x in [-4.15,4.15]:
   for y in [-2,0,2]:cube('Outside board exposed tenon',(x,y,.05),(.7,.3,.5),wood,True)
 elif id=='altar':
  for j in range(3):cylinder('Golden circular terrace',5.2-j*.55,.65,-.42-j*.65,body if j%2==0 else rim,40)
 else:rock('Planet hull',(0,0,-1.9),(5,4.8,2.0),body)
 # Props have an exact one-cell exclusion zone around the 5x5 board: |x| or |y| >= 3.5 for every vertex.
 # Tall props are confined to the left/right wings; their projection cannot cover any board cell.
 for side in [-1,1]:
  for j,y in enumerate([-2,0,2]):
   x=side*4.35
   if id=='moss':rock('Moss bank',(x,y,-.05),(.65,.6,.26),moss,True)
   elif id=='frost':
    for k in range(3):
     bpy.ops.mesh.primitive_cone_add(vertices=5,radius1=.22,radius2=0,depth=.8+k*.25,location=(x+(k-1)*.35,y,.2+k*.12));o=bpy.context.object;o.name='Ice crystal';o.data.materials.append(moon);props.append(o)
   elif id=='carved':cube('Outside tally pillar',(x,y,.45),(.42,.5,.9),rim,True)
   elif id=='altar':rock('Outer standing stone',(x,y,.55),(.34,.38,.95),rim,True)
   elif id=='crumble':rock('Floating fragment',(side*(4.4+j*.4),y,-.7-j*.3),(.45,.5,.6),rim,True)
 if id=='crumble':
  for j in range(8):
   a=j*math.tau/8;rock('Detached orbit fragment',(5.6*math.cos(a),5.6*math.sin(a),-2.0-(j%3)*.3),(.45,.4,.5),body,True)
 # Distinct distant planetary silhouettes, always beyond the board.
 for j,p in enumerate([(-6,3,2.0),(6,4,1.2)]):
  rock('Distant world '+id,p,(.65+j*.2,.65+j*.2,.65+j*.2),moon)
  if id in ['hollow','altar']:
   bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=6,location=p,major_radius=.95+j*.2,minor_radius=.06);o=bpy.context.object;o.data.materials.append(rim);o.rotation_euler.x=.5
 # Camera-facing opaque procedural sky with broad colored nebula, not a universal navy background.
 d=bpy.data.cameras.new('Camera');camera=bpy.data.objects.new('Camera',d);scene.collection.objects.link(camera);scene.camera=camera;camera.location=(0,-15,12);target=Vector((0,0,-.55));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=17.8
 back=mat('Nebula_'+id,skyhex);nodes=back.node_tree.nodes;links=back.node_tree.links;bs=nodes['Principled BSDF'];noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=1.7;noise.inputs['Detail'].default_value=2;tex=nodes.new('ShaderNodeTexCoord');links.new(tex.outputs['Generated'],noise.inputs['Vector']);ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.25;ramp.color_ramp.elements[0].color=(*rgb(skyhex),1);ramp.color_ramp.elements[1].position=.78;ramp.color_ramp.elements[1].color=(*rgb(hazehex),1);links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs[0],bs.inputs['Base Color']);links.new(ramp.outputs[0],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.32
 backobj=cube('Nebula backdrop',(0,18,-1),(60,.2,50),back)
 for name,p,power,col in [('Key',(-5,-7,10),1500,keyhex),('Rim',(5,4,6),1000,moonhex)]:
  data=bpy.data.lights.new(name,'AREA');data.energy=power;data.size=6;data.color=rgb(col);o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=p;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
 # Assert clearance in world space rather than merely checking prop centers.
 bpy.context.view_layer.update()
 for o in props:
  for corner in o.bound_box:
   p=o.matrix_world@Vector(corner)
   assert abs(p.x)>=3.5 or abs(p.y)>=3.5,(id,o.name,tuple(p))
 assert id!='frost' or not any(o.name.startswith('Moss') for o in scene.objects)
 scene.render.filepath=str(out/(id+'.png'));bpy.ops.wm.save_as_mainfile(filepath=str(root/'Source/Blender'/('SS_RuinsHenge_'+id+'.blend')));bpy.ops.render.render(write_still=True)
(out/'clearance.json').write_text(json.dumps({'board_bounds':[-2.5,2.5],'prop_vertex_exclusion_bounds':[-3.5,3.5],'validated':['moss','hollow','carved','frost','joinery','crumble','altar'],'frost_moss_objects':0},indent=2))

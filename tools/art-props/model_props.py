"""Blender 5.2 headless source for readable RuinsHenge stage props. No external assets."""
import bpy, math, os, json, random
from mathutils import Vector
random.seed(712)
ROOT='C:/unityjam/shared-assets/seven-steps'
DEST=ROOT+'/Assets/Art/RuinsHenge/Props'
OUT=ROOT+'/concepts/ui/props-2026-11-12'
os.makedirs(DEST,exist_ok=True);os.makedirs(OUT,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
stone=bpy.data.materials.new('RH_Stone');stone.diffuse_color=(.42,.49,.44,1)
accent=bpy.data.materials.new('RH_Accent');accent.diffuse_color=(.12,.72,.65,1)
dark=bpy.data.materials.new('RH_Carving');dark.diffuse_color=(.06,.09,.10,1)
objects=[]
def empty(name,parent=None):
 o=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(o);o.parent=parent;objects.append(o);return o
def finish(o,name,mat,parent,bevel=0):
 o.name=name;o.data.materials.append(mat);o.parent=parent
 if bevel:
  m=o.modifiers.new('Hand-chipped arris','BEVEL');m.width=bevel;m.segments=1
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=m.name)
 # Real UVs into the common stone quadrant; UV2 opts in to the existing per-planet stone recipe.
 if not o.data.uv_layers:o.data.uv_layers.new(name='Atlas')
 for l in o.data.uv_layers[0].data:l.uv=(.005+l.uv.x*.24,.005+l.uv.y*.24)
 u=o.data.uv_layers.new(name='StoneRecipe')
 seed=random.random()
 for l in u.data:l.uv=(4.0 if mat==stone else 0.0,seed)
 c=o.data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
 for v in c.data:v.color=(.06,.09,.10,1) if mat==dark else (1,1,1,1)
 o.select_set(False);objects.append(o);return o
def box(name,loc,size,parent,mat=stone,bevel=.025):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);return finish(o,name,mat,parent,bevel)
def cyl(name,loc,r,depth,parent,mat=stone,n=12,rotation=None):
 bpy.ops.mesh.primitive_cylinder_add(vertices=n,radius=r,depth=depth,location=loc,rotation=rotation or (0,0,0));return finish(bpy.context.object,name,mat,parent,0)
def line(name,pts,width,parent,mat=accent):
 # Bevelled polyline: actual extruded mesh, never a decal or text.
 curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.resolution_u=1;curve.bevel_depth=width;curve.bevel_resolution=0;curve.resolution_u=1
 s=curve.splines.new('POLY');s.points.add(len(pts)-1)
 for p,co in zip(s.points,pts):p.co=(*co,1)
 o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');return finish(o,name,mat,parent)
def glyph(k,parent,center,vertical=False,scale=1):
 x,y,z=center
 def p(u,v):return (x+u*scale,y if vertical else y+v*scale,z+v*scale if vertical else z)
 if k==0:
  pts=[p(math.cos(a)*.11,math.sin(a)*.11) for a in [i*math.tau/16 for i in range(17)]];line('inlay_sun_ring',pts,.018,parent)
  for j in range(8):
   a=j*math.tau/8;line('inlay_sun_ray',[p(math.cos(a)*.16,math.sin(a)*.16),p(math.cos(a)*.21,math.sin(a)*.21)],.017,parent)
 elif k==1:
  pts=[p(.16*math.cos(a),.19*math.sin(a)) for a in [math.pi*.3+i*math.pi*1.4/14 for i in range(15)]]+ [p(.08*math.cos(a)-.02,.13*math.sin(a)) for a in [math.pi*1.7-i*math.pi*1.4/12 for i in range(13)]];line('inlay_moon',pts+[pts[0]],.027,parent)
 elif k==2:
  pts=[p(math.sin(i*math.pi/5)*(.21 if i%2==0 else .085),math.cos(i*math.pi/5)*(.21 if i%2==0 else .085)) for i in range(10)];line('inlay_star',pts+[pts[0]],.022,parent)
 else:
  line('inlay_knot',[p(-.16,0),p(0,.18),p(.16,0),p(0,-.18),p(-.16,0),p(.16,0)],.026,parent);line('inlay_knot_cross',[p(0,-.18),p(0,.18)],.022,parent)
def floor(parent):box('stone_foundation',(0,0,-.09),(.94,.94,.18),parent,bevel=.045)
def frame(parent,height=.72):
 for x in [-.35,.35]:box('stone_jamb',(x,.12,height/2),(.17,.27,height),parent,bevel=.035)
 box('stone_lintel',(0,.12,height+.04),(.88,.32,.18),parent,bevel=.04)
def slab(parent):
 s=empty('Slab',parent);box('stone_sliding_slab',(0,.12,.31),(.52,.19,.62),s,bevel=.035)
 for x in [-.25,.25]:box('stone_guide',(x,-.015,.33),(.035,.035,.58),parent,bevel=.008)
 return s
models={}
for kind in ['Exit','Plate','Door','Gate','CountDoor','Bridge','Cracked','FlipUp','FlipDown','Ice','Lever','Footprint','Mud','Start']:
 objects=[];root=empty(kind);floor(root) if kind not in ['Footprint','Start'] else None
 if kind=='Exit':
  frame(root,.68);glyph(2,root,(0,-.065,.79),True,.70)
  for i in range(3):box('inlay_way_out',(0,-.34+i*.14,.025),(.18+i*.07,.055,.018),root,accent,.009)
  box('stone_threshold',(0,.12,.025),(.53,.31,.06),root)
 elif kind=='Plate':
  s=empty('Slab',root)
  cyl('stone_pressure_disk',(0,0,.045),.37,.09,s,n=16)
  for k in range(4):g=empty('Glyph_'+str(k),s);glyph(k,g,(0,0,.102));g.hide_render=k!=0
  for x,y in [(-.37,-.37),(.37,-.37),(-.37,.37),(.37,.37)]:cyl('stone_pin',(x,y,.015),.045,.04,root,n=8)
 elif kind in ['Door','Gate','CountDoor']:
  frame(root);s=slab(root)
  if kind=='Door':
   for k in range(4):g=empty('Glyph_'+str(k),s);glyph(k,g,(0,.011,.34),True,.9);g.hide_render=k!=0
   for x in [-.35,.35]:box('inlay_pair_band',(x,-.024,.58),(.18,.02,.06),root,accent,.008)
  if kind=='Gate':
   for k in range(7):
    g=empty('Lamp_'+str(k),root);x=(k-3)*.112;cyl('stone_lamp_cup',(x,-.07,.79),.047,.04,g,n=8);cyl('inlay_lamp',(x,-.07,.82),.029,.028,g,accent,n=8)
   # Side-carved rung marks make the counted shape survive the overhead camera.
  if kind=='CountDoor':
   for k in range(7):
    g=empty('Socket_'+str(k),root);x=(k-3)*.11;cyl('stone_socket_rim',(x,-.12,.83),.05,.045,g,n=10);cyl('carving_socket_hollow',(x,-.12,.856),.036,.006,g,dark,n=10)
    t=empty('Token_'+str(k),g);box('inlay_token_body',(x,-.12,.89),(.045,.04,.065),t,accent,.01);cyl('inlay_token_head',(x,-.12,.938),.027,.028,t,accent,n=8)
 elif kind=='Bridge':
  # Raised dry-laid causeway, distinct from a smooth floor, with actual gaps and pins.
  for i in range(5):box('stone_bridge_plank',(0,(i-2)*.177,.02),(.86,.153,.12),root,bevel=.022)
  for x in [-.35,.35]:box('stone_bridge_sleeper',(x,0,-.025),(.09,.9,.09),root)
 elif kind=='Cracked':
  # Separate wedge fragments leave deep, readable fissures.
  for k in range(5):
   a=k*math.tau/5;b=(k+1)*math.tau/5
   verts=[(.04*math.cos((a+b)/2),.04*math.sin((a+b)/2),.025),(.42*math.cos(a+.05),.42*math.sin(a+.05),.025),(.42*math.cos(b-.05),.42*math.sin(b-.05),.025)]
   verts+= [(x,y,-.035) for x,y,z in verts];me=bpy.data.meshes.new('split');me.from_pydata(verts,[],[(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)]);o=bpy.data.objects.new('stone_fractured',me);bpy.context.collection.objects.link(o);finish(o,'stone_fractured',stone,root)
 elif kind in ['FlipUp','FlipDown']:
  z=.32 if kind=='FlipUp' else .04
  for x in [-.30,0,.30]:box('stone_counterweight',(x,.12,z),(.24,.30,z*2+.08),root)
  cyl('inlay_pivot',(0,-.065,.10),.045,.84,root,accent,n=10,rotation=(0,math.pi/2,0))
 elif kind=='Ice':
  cyl('stone_ice_sheet',(0,0,.015),.46,.09,root,n=8)
  for j in range(3):
   a=j*math.tau/3;line('inlay_frost_fracture',[(0,0,.07),(.32*math.cos(a),.32*math.sin(a),.07)],.018,root)
  for x,y in [(-.3,.3),(.31,.2)]:
   bpy.ops.mesh.primitive_cone_add(vertices=5,radius1=.065,radius2=.012,depth=.20,location=(x,y,.14));finish(bpy.context.object,'stone_ice_crystal',stone,root)
 elif kind=='Lever':
  cyl('stone_lever_socket',(0,0,.08),.24,.16,root,n=12);s=empty('Handle',root)
  bar=box('stone_lever_stem',(0,0,.32),(.075,.075,.47),s,bevel=.015);bar.rotation_euler.x=-.35
  box('inlay_lever_grip',(0,.075,.54),(.32,.13,.09),s,accent)
  for x in [-.28,.28]:box('inlay_lever_detent',(x,0,.03),(.08,.22,.04),root,accent,.01)
 elif kind=='Footprint':
  for x,y in [(-.10,-.07),(.10,.10)]:
   o=cyl('inlay_footstep',(x,y,.018),.065,.025,root,accent,n=10);o.scale.y=1.65
 elif kind=='Start':
  line('inlay_start_ring',[(math.cos(i*math.tau/48)*.32,math.sin(i*math.tau/48)*.32,.018) for i in range(49)],.012,root)
 elif kind=='Mud':
  for i in range(3):cyl('stone_mud_ripple',(0,0,.015+i*.008),.38-i*.10,.022,root,n=12)
  for x,y in [(-.3,-.22),(.31,.20)]:box('stone_sunken_pebble',(x,y,.018),(.11,.08,.06),root)
 # Export source hierarchies, not generated Unity primitive stand-ins.
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=DEST+'/RH_Prop_'+kind+'.fbx',use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False)
 tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects if o.type=='MESH');models[kind]={'tris_all_variants':tris,'objects':len(objects)}
 if kind!='Footprint':assert tris<2000,(kind,tris)
 # Archive the source collection for a later Blender sheet render.
 coll=bpy.data.collections.new('Prop_'+kind);bpy.context.scene.collection.children.link(coll)
 for o in objects:
  for c in list(o.users_collection):c.objects.unlink(o)
  coll.objects.link(o)
 coll.hide_render=True
with open(OUT+'/blender-mesh-audit.json','w') as f:json.dump(models,f,indent=2)
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/Source/Blender/SS_RuinsHenge_StageProps.blend')
print('PROPS_DONE',json.dumps(models))

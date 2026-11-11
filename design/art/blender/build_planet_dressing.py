"""Free procedural seven-planet library. Run Blender -b --python this_file.
All binary outputs go to shared storage; deterministic seed, no downloaded assets.
"""
import bpy, math, random, json
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/unityjam/shared-assets/seven-steps')
OUT=ROOT/'concepts/themes/planet-dressing-2026-11-12'
ASSET=ROOT/'Assets/Art/RuinsHenge/PlanetDressing'
SOURCE=ROOT/'Source/Blender/PlanetDressing'
for p in (OUT,ASSET,SOURCE): p.mkdir(parents=True,exist_ok=True)
IDS=['moss','hollow','carved','frost','joinery','crumble','altar']
PALETTES=[(.58,.61,.55),(.57,.40,.27),(.79,.37,.22),(.64,.85,.96),(.25,.30,.34),(.86,.76,.58),(.68,.63,.43)]
random.seed(12074)

def image_file(name,pixels):
 im=bpy.data.images.new(name,width=pixels.shape[1],height=pixels.shape[0],alpha=True)
 if 'Detail' in name:im.colorspace_settings.name='Non-Color'
 im.pixels.foreach_set(pixels.astype(np.float32).ravel());im.filepath_raw=str(ASSET/(name+'.png'));im.file_format='PNG';im.save();return im

def bake(identity,base):
 # Periodic procedural strata, pits, marble veins and frost. RG normal / B roughness / A AO.
 n=512;y,x=np.mgrid[0:n,0:n]/n; rng=np.random.default_rng(700+IDS.index(identity))
 grain=rng.random((n,n)); waves=np.sin(x*math.tau*7+np.sin(y*math.tau*4))*np.cos(y*math.tau*9)
 height=.5+.10*waves+.055*(grain-.5);col=np.ones((n,n,4));col[:,:,:3]=base
 def smooth_noise(scale):
  grid=rng.random((scale,scale));fx=x*scale;fy=y*scale;ix=fx.astype(int)%scale;iy=fy.astype(int)%scale;tx=fx%1;ty=fy%1;tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty)
  return (grid[iy,ix]*(1-tx)+grid[iy,(ix+1)%scale]*tx)*(1-ty)+(grid[(iy+1)%scale,ix]*(1-tx)+grid[(iy+1)%scale,(ix+1)%scale]*tx)*ty
 fractal=smooth_noise(3)*.45+smooth_noise(7)*.27+smooth_noise(17)*.18+smooth_noise(43)*.10
 if identity in ('hollow','crumble'):
  pits=np.ones((n,n))*5
  for a,b in rng.random((110 if identity=='hollow' else 160,2)):
   dx=np.minimum(abs(x-a),1-abs(x-a));dy=np.minimum(abs(y-b),1-abs(y-b));r=rng.uniform(.008,.044 if identity=='hollow' else .023)
   pits=np.minimum(pits,np.sqrt(dx*dx+dy*dy)/r)
  cavity=np.clip(1-pits,0,1);height-=cavity*.6;col[:,:,:3]*=(1-cavity[:,:,None]*.78)
  if identity=='crumble':
   drip=np.clip(np.sin(x*math.tau*23+.3*np.sin(y*math.tau))* .7-.35,0,1)*(.5+.5*np.sin(y*math.tau))
   col[:,:,:3]*=1-drip[:,:,None]*.27;height+=.03*np.sin(y*math.tau*26)
 if identity=='carved':
  strata=np.sin(y*math.tau*9+np.sin(x*math.tau*2)*.55+fractal*1.6)
  height+=.075*strata;col[:,:,:3]*=(.87+.085*strata[:,:,None])*(.90+fractal[:,:,None]*.20);col[:,:,0]+=.025
 if identity in ('moss','altar'):
  lichen=np.clip((fractal-.49)*5+grain*.12,0,1)
  moss=np.array((.24,.39,.10) if identity=='moss' else (.68,.64,.24))
  mineral=np.clip((grain-.82)*2,0,.30)-np.clip((.15-grain)*2,0,.25)
  col[:,:,:3]*=.88+fractal[:,:,None]*.24+mineral[:,:,None]
  col[:,:,:3]=col[:,:,:3]*(1-lichen[:,:,None]*.74)+moss*lichen[:,:,None]*.74;height+=lichen*.13
 if identity=='joinery':
  slate=np.sin(y*math.tau*17+np.sin(x*math.tau*3))
  height+=.08*slate;col[:,:,:3]*=.83+.20*slate[:,:,None]
 if identity=='frost':
  distance=np.full((n,n),20.);second=np.full((n,n),20.)
  sites=rng.random((28,2))
  for a,b in sites:
   dx=np.minimum(abs(x-a),1-abs(x-a));dy=np.minimum(abs(y-b),1-abs(y-b));d=dx*dx+dy*dy
   swap=d<distance;second=np.where(swap,distance,np.minimum(second,d));distance=np.minimum(distance,d)
  edge=np.sqrt(second)-np.sqrt(distance);cracks=np.exp(-edge*430)
  marble=.5+.5*np.sin((x+y)*math.tau*5+np.sin(y*math.tau*3)*2)
  cloud=.5+.5*np.sin(x*math.tau*2)*np.sin(y*math.tau*3)
  col[:,:,:3]=np.array((.35,.66,.87))+(cloud*.25+marble*.09)[:,:,None]
  col[:,:,:3]=col[:,:,:3]*(1-cracks[:,:,None]*.73)+np.array((.91,.98,1))*cracks[:,:,None]*.73
  height=.5+.018*waves-.09*cracks+.008*marble
 col[:,:,:3]*=(.94+.12*grain[:,:,None]);col=np.clip(col,0,1)
 dx=(np.roll(height,-1,1)-np.roll(height,1,1))*3;dy=(np.roll(height,-1,0)-np.roll(height,1,0))*3
 normal=np.ones_like(col);normal[:,:,0]=.5-dx;normal[:,:,1]=.5-dy;normal[:,:,2]=.14 if identity=='frost' else .78;normal[:,:,3]=np.clip(height+.5,.50,1)
 color=image_file('PD_'+identity+'_Color',col);detail=image_file('PD_'+identity+'_Detail',np.clip(normal,0,1))
 return color,detail

bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.render.resolution_x=1500;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Space studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.021,.035,.065,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.55
scene.view_settings.view_transform='AgX'
def material(name,c,rough=.8,metal=0):
 m=bpy.data.materials.get(name) or bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=(*c,1)
 b=m.node_tree.nodes.get('Principled BSDF');b.inputs['Base Color'].default_value=(*c,1);b.inputs['Roughness'].default_value=rough;b.inputs['Metallic'].default_value=metal
 return m
green=material('Fern green',(.23,.39,.10));flower=material('Ivory petals',(.96,.87,.64));gold=material('Warm brass',(.63,.37,.10),.28,.72)
wood=material('Weathered oak',(.30,.15,.065));dark=material('Engraving shadow',(.075,.052,.033));rope=material('Hemp rope',(.58,.43,.23));water=material('Mineral water',(.08,.57,.62),.12,.15)
snow=material('Rime white',(.83,.95,1),.46);ice=material('Blue ice',(.26,.65,.87),.12,.08)
ice.node_tree.nodes['Principled BSDF'].inputs['Transmission Weight'].default_value=.32
lamp=material('Lamp glow',(1,.54,.12),.3);lamp.node_tree.nodes['Principled BSDF'].inputs['Emission Color'].default_value=(1,.33,.03,1);lamp.node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value=2
objects=[]
def finish(o,m):
 o.data.materials.append(m);objects.append(o);return o
def cube(p,s,m,bevel=.035):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 if bevel:
  mod=o.modifiers.new('Worn chamfers','BEVEL');mod.width=bevel;mod.segments=1;bpy.ops.object.modifier_apply(modifier=mod.name)
 return finish(o,m)
def rock(p,s,m,sub=1):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub,radius=1,location=p);o=bpy.context.object;o.scale=s
 for v in o.data.vertices:v.co*=random.uniform(.88,1.12)
 return finish(o,m)
def cylinder(p,r,d,m,verts=12):
 bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=d,location=p);return finish(bpy.context.object,m)
def cone(p,r,d,m):
 bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=r,radius2=0,depth=d,location=p);return finish(bpy.context.object,m)
def ring(p,r,t,m,vertical=False,arc=math.tau,segments=24):
 verts=[];faces=[]
 for i in range(segments+1):
  a=i*arc/segments
  for j in range(6):
   b=j*math.tau/6;v=Vector(((r+t*math.cos(b))*math.cos(a),(r+t*math.cos(b))*math.sin(a),t*math.sin(b)))
   if vertical:v=Vector((v.x,v.z,v.y))
   verts.append(v+Vector(p))
 for i in range(segments):
  for j in range(6):a=i*6+j;b=i*6+(j+1)%6;faces.append((a,b,b+6,a+6))
 mesh=bpy.data.meshes.new('Ring');mesh.from_pydata(verts,[],faces);o=bpy.data.objects.new('Ring',mesh);scene.collection.objects.link(o);return finish(o,m)
def line(points,width,m):
 curve=bpy.data.curves.new('Inlaid stroke','CURVE');curve.dimensions='3D';curve.bevel_depth=width;curve.bevel_resolution=0;curve.resolution_u=1
 sp=curve.splines.new('POLY');sp.points.add(len(points)-1)
 for v,p in zip(sp.points,points):v.co=(*p,1)
 o=bpy.data.objects.new('Stroke',curve);scene.collection.objects.link(o);o.data.materials.append(m);objects.append(o);return o
def menhir(m,height=1.2):
 rock((0,0,height*.46),(.29,.22,height*.54),m,2)
def crystal(p,h,m=ice):
 cylinder((p[0],p[1],p[2]+h*.38),h*.14,h*.76,m,6);cone((p[0],p[1],p[2]+h*.88),h*.14,h*.24,m)
def fern(p=(0,0,0),size=1):
 for j in range(7):
  a=j*math.tau/7;end=Vector((math.cos(a)*.48,math.sin(a)*.48,.18))*size;start=Vector(p)
  line([start,start+end*.5+Vector((0,0,.12*size)),start+end],.012*size,green)
  for k in range(1,5):
   q=start+end*k/5+Vector((0,0,.10*size));side=Vector((-math.sin(a),math.cos(a),0))*.10*size*(1-k/6)
   for sign in (-1,1):line([q,q+side*sign+end*.12],.024*size,green)
def rune_slab(m,height=1.15):
 cube((0,0,height*.5),(.58,.20,height),m,.07)
 for z in [.28,.55,.82]:
  line([(-.14,-.108,z*height),(.13,-.108,(z+.09)*height),(-.08,-.108,(z+.17)*height)],.013,gold)
def export_object(name):
 global objects
 for o in objects:
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  if o.type!='MESH':bpy.ops.object.convert(target='MESH')
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  # Per-corner color keeps vegetation/brass/ice in one Unity draw/material.
  attr=o.data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
  for poly in o.data.polygons:
   c=o.data.materials[poly.material_index].diffuse_color
   for loop in poly.loop_indices:attr.data[loop].color=c
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
 # UV2.x tag 9 is used by the common Unity shader for literal prop colors.
 if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
 style=o.data.uv_layers.new(name='SurfaceStyle')
 for d in style.data:d.uv=(9,0)
 mod=o.modifiers.new('Triangulate export','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
 count=len(o.data.polygons)
 bpy.ops.export_scene.fbx(filepath=str(ASSET/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,colors_type='LINEAR')
 objects=[];return o,count

stats={}
for identity,base in zip(IDS,PALETTES):
 color,detail=bake(identity,base);stone=material(identity+' stone',base,.16 if identity=='frost' else .85)
 nodes=stone.node_tree.nodes;links=stone.node_tree.links;tex=nodes.new('ShaderNodeTexImage');tex.image=color;coord=nodes.new('ShaderNodeTexCoord');links.new(coord.outputs['Generated'],tex.inputs['Vector']);links.new(tex.outputs['Color'],nodes.get('Principled BSDF').inputs['Base Color'])
 if identity=='frost':
  tex=ice.node_tree.nodes.new('ShaderNodeTexImage');tex.image=color;coord=ice.node_tree.nodes.new('ShaderNodeTexCoord');ice.node_tree.links.new(coord.outputs['Generated'],tex.inputs['Vector']);ice.node_tree.links.new(tex.outputs['Color'],ice.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
 current=[];counts={}
 def save(label):
  o,tris=export_object('PD_'+identity+'_'+label);current.append(o);counts[label]=tris
 if identity=='moss':
  rock((0,0,.28),(.59,.43,.39),stone,2)
  for p in [(-.18,.05,.53),(.18,.15,.52),(.32,-.1,.40)]:rock(p,(.23,.19,.10),green,1)
  save('MossBoulder');fern(size=1.2);save('Fern')
  for i in range(7):
   a=i*2.4;p=(math.cos(a)*.30,math.sin(a)*.23,.24+random.random()*.15);line([(p[0],p[1],0),p],.012,green)
   for j in range(5):rock((p[0]+math.cos(j*math.tau/5)*.055,p[1]+math.sin(j*math.tau/5)*.055,p[2]),(.055,.04,.025),flower)
   rock(p,(.025,.025,.035),gold)
  save('Flowers');rock((0,0,.10),(.50,.38,.16),stone,2);rock((0,-.02,.255),(.13,.22,.018),dark,2)
  for j in range(4):rock((-.13+j*.075,.21,.235),(.042,.05,.02),dark)
  save('FootprintStone');rock((0,0,.12),(.39,.25,.19),stone,2);fern((.12,0,.17),.7);save('FernRock')
 elif identity=='hollow':
  ring((0,0,.63),.49,.18,stone,True);save('PumiceRing');ring((0,0,.13),.65,.18,stone,True,math.pi,20);save('PumiceArch')
  for j in range(3):rock((j*.22-.22,0,.35+j*.10),(.20,.24,.55+j*.1),stone,2)
  save('PumiceSpires');ring((0,0,.24),.34,.23,stone);ring((.28,.15,.39),.21,.10,stone,True);save('StarWindow')
  ring((0,0,.39),.26,.12,stone,True);rock((-.23,0,.16),(.26,.22,.2),stone,1);save('PerforatedBoulder')
 elif identity=='carved':
  rune_slab(stone);save('RuneSlab')
  for i in range(5):cube((0,0,.13+i*.22),(.49-i*.03,.44-i*.025,.22),stone,.06)
  save('StrataPillar');rune_slab(stone,.62);rock((.34,.1,.08),(.25,.2,.12),stone);save('BrokenStela')
  cube((0,0,.09),(.86,.68,.17),stone,.06)
  for j in range(3):line([(-.25,-.18+j*.16,.18),(.2,-.08+j*.16,.18),(.1,-.22+j*.16,.18)],.014,gold)
  save('RuneTablet');cube((0,0,.48),(.74,.23,.96),stone,.06)
  pts=[(math.cos(i*.25)*(.02+i*.006),-.125,.51+math.sin(i*.25)*(.02+i*.006)) for i in range(60)];line(pts,.014,gold);save('SpiralStele')
 elif identity=='frost':
  for p,h in [((0,0,0),1.25),((-.26,.10,0),.78),((.27,.05,0),.92),((.05,-.20,0),.52)]:crystal(p,h)
  rock((0,0,.05),(.47,.35,.08),snow);save('IceCluster')
  for i in range(7):
   c=cone((-.55+i*.18,0,-.20-random.random()*.18),.085,.5+random.random()*.25,ice);c.rotation_euler.x=math.pi
  cube((0,0,.03),(1.26,.22,.11),snow,.025);save('IcicleFringe')
  pts=[(math.sin(i*.30)*.13,-.72+i*.072,.055+.008*math.sin(i*.53)) for i in range(21)]
  verts=[];faces=[]
  for p in pts:
   for z in (p[2]-.065,p[2]):
    for side in (-1,1):verts.append((p[0]+side*.24,p[1],z))
  for i in range(20):
   a=i*4;b=a+4;faces.extend([(a+2,a+3,b+3,b+2),(a,b,b+1,a+1),(a,a+2,b+2,b),(a+1,b+1,b+3,a+3)])
  faces.extend([(0,1,3,2),(80,82,83,81)])
  mesh=bpy.data.meshes.new('Continuous frozen stream');mesh.from_pydata(verts,[],faces);ob=bpy.data.objects.new('Frozen water ribbon',mesh);scene.collection.objects.link(ob);finish(ob,ice)
  for i,p in enumerate(pts):
   if i%3==0:
    for side in (-1,1):rock((p[0]+side*.27,p[1],.04),(.08,.13,.065),snow)
  for i in [3,9,15]:line([(pts[i][0]-.17,pts[i][1]-.04,pts[i][2]+.003),(pts[i][0],pts[i][1]+.02,pts[i][2]+.003),(pts[i][0]+.17,pts[i][1]-.02,pts[i][2]+.003)],.006,snow)
  save('FrozenStream');menhir(stone);rock((0,-.14,.65),(.27,.11,.56),ice,2);rock((0,0,1.18),(.30,.24,.07),snow);save('IceMenhir')
  for j in range(5):crystal((math.cos(j*1.4)*.20,math.sin(j*1.4)*.16,0),.4+j*.12,snow if j%2 else ice)
  save('QuartzFan')
 elif identity=='joinery':
  for x in (-.45,.45):cube((x,0,.58),(.14,.18,1.16),wood)
  cube((0,0,1.08),(1.1,.2,.17),wood);brace=cube((0,0,.55),(.12,.14,1.08),wood);brace.rotation_euler.y=-.65
  for x in (-.45,.45):cylinder((x,0,1.18),.06,.05,gold,8)
  save('TimberFrame');cylinder((0,0,.09),.43,.16,stone);ring((0,0,.38),.24,.065,gold,True,segments=20)
  for j in range(12):
   a=j*math.tau/12;o=cube((math.cos(a)*.31,0,.38+math.sin(a)*.31),(.11,.14,.10),gold,.01);o.rotation_euler.y=-a
  line([(0,0,.38),(.46,0,.85)],.045,gold);rock((.46,0,.85),(.095,.095,.095),gold,2);save('GearLever')
  for i in range(4):ring((0,0,.035+i*.045),.25+i*.015,.026,rope,segments=28)
  line([(.26,0,.06),(.48,.12,.04),(.48,.3,.03)],.025,rope);save('RopeCoil')
  for x in (-.4,.4):
   for y in (-.24,.24):cube((x,y,.44),(.1,.1,.88),wood)
  for x in [-.3,0,.3]:cube((x,0,.77),(.27,.76,.09),wood)
  for z in [.2,.4,.6]:cube((-.46,0,z),(.07,.57,.06),wood)
  save('Scaffold')
  for i in range(5):cube((0,(i%2)*.15-.08,.04+i*.09),(.92,.23,.085),wood)
  save('PlankStack')
 elif identity=='crumble':
  ring((0,0,.11),.42,.12,stone,segments=28);cylinder((0,0,.13),.38,.05,water,28);ring((0,0,.15),.25,.006,flower,segments=28);save('MineralPool')
  for p,s in [((-.18,0,.07),(.48,.52,.13)),((.22,.15,.10),(.34,.38,.20)),((.28,-.25,.05),(.26,.22,.10))]:cube(p,s,stone,.04)
  save('BrokenTiles');menhir(stone,.95)
  for j in range(5):
   c=cone((-.23+j*.11,-.13,.39+random.random()*.15),.05,.48,stone);c.rotation_euler.x=math.pi
  save('DrippingStone');cylinder((0,0,.11),.24,.22,stone);cylinder((0,0,.37),.13,.33,gold)
  for x in (-.12,.12):cube((x,0,.55),(.025,.18,.3),gold,.008)
  rock((0,0,.55),(.075,.075,.13),lamp,2);cube((0,0,.72),(.35,.29,.07),gold);save('WarmLamp')
  ring((0,0,.31),.3,.09,stone);cylinder((0,0,.31),.26,.035,water,20);ring((.15,-.25,.1),.3,.1,stone);cylinder((.15,-.25,.10),.25,.025,water,20);save('CascadePools')
 elif identity=='altar':
  for x in (-.43,.43):rock((x,0,.55),(.21,.23,.61),stone,2)
  save('RingSegment')
  for x in (-.42,.42):rock((x,0,.41),(.19,.20,.47),stone,2)
  cube((0,0,.9),(1.22,.40,.22),stone,.06);save('Lintel')
  cylinder((0,0,.10),.57,.20,stone,32);ring((0,0,.214),.44,.018,gold,segments=32)
  for j in range(7):
   a=j*math.tau/7;b=(j+3)*math.tau/7;line([(math.cos(a)*.35,math.sin(a)*.35,.22),(math.cos(b)*.35,math.sin(b)*.35,.22)],.009,gold)
   rock((math.cos(a)*.44,math.sin(a)*.44,.23),(.035,.035,.025),gold)
  save('StarAltarDisc');rock((0,0,.22),(.46,.35,.30),stone,2)
  for i in range(6):rock((random.uniform(-.3,.3),random.uniform(-.2,.2),.4),(.12,.10,.025),green)
  save('LichenStone')
  for j in range(14):
   a=j*2.4;line([(math.cos(a)*.13,math.sin(a)*.13,0),(math.cos(a)*.32,math.sin(a)*.32,.24+random.random()*.20)],.015,green)
  save('GrassTuft')
 stats[identity]=counts
 # Five source modules on a studio sheet, separated for silhouette review.
 for i,o in enumerate(current):o.location=((i%3-1)*2.1,(i//3)*2.3,0)
 d=bpy.data.cameras.new('SheetCamera');cam=bpy.data.objects.new('SheetCamera',d);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(4,-8,7);cam.rotation_euler=(Vector((0,1,.35))-cam.location).to_track_quat('-Z','Y').to_euler();d.type='ORTHO';d.ortho_scale=8
 lights=[]
 for pos,power,c in [((-3,-4,8),1200,(1,.88,.70)),((4,2,6),1000,(.58,.80,1))]:
  data=bpy.data.lights.new('Studio','AREA');data.energy=power;data.size=5;data.color=c;o=bpy.data.objects.new('Studio',data);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler();lights.append(o)
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/('PD_'+identity+'.blend')));scene.render.filepath=str(OUT/('objects-'+identity+'.png'));bpy.ops.render.render(write_still=True)
 for o in current+lights+[cam]:bpy.data.objects.remove(o,do_unlink=True)
(OUT/'blender-triangles.json').write_text(json.dumps(stats,indent=2));print('PLANET_DRESSING_COMPLETE '+json.dumps(stats))

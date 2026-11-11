import bpy, math, random, zlib
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/unityjam/shared-assets/seven-steps')
OUTDIR=ROOT/'Assets/Art/RuinsHenge/BoardShapeA'
SOURCE=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'))
HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'))
mat=bpy.data.materials.get('SS_M_BoardShapeA')

def remove(name):
 o=bpy.data.objects.get(name)
 if o:bpy.data.objects.remove(o,do_unlink=True)
def setup_uv(obj, swatch=8):
 m=obj.data
 if not m.uv_layers.get('UVMap'):uv=m.uv_layers.new(name='UVMap')
 else:uv=m.uv_layers['UVMap']
 if not m.uv_layers.get('Style'):style=m.uv_layers.new(name='Style')
 else:style=m.uv_layers['Style']
 if not m.color_attributes.get('Color'):colattr=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
 else:colattr=m.color_attributes['Color']
 mins=[min(v.co[i] for v in m.vertices) for i in range(3)];maxs=[max(v.co[i] for v in m.vertices) for i in range(3)]
 for poly in m.polygons:
  axis=max(range(3),key=lambda i:abs(poly.normal[i]));axes=[(1,2),(0,2),(0,1)][axis]
  fs=3 if poly.material_index==1 else swatch
  for li in poly.loop_indices:
   co=m.vertices[m.loops[li].vertex_index].co
   a=.035+.90*(co[axes[0]]-mins[axes[0]])/max(.001,maxs[axes[0]]-mins[axes[0]])
   b=.035+.90*(co[axes[1]]-mins[axes[1]])/max(.001,maxs[axes[1]]-mins[axes[1]])
   uv.data[li].uv=((fs%4+a)/4,(fs//4+b)/4)
   style.data[li].uv=(4.0,.618)
   colattr.data[li].color=(1,1,1,1)
 m.materials.clear();m.materials.append(mat);m.materials.append(mat)
 m.update()

def make_crust():
 name='SS_BSA_HollowCrust';remove(name);remove('HP_HollowCrust_Pores')
 # A clipped, irregular ellipsoid sits directly beneath the playable slab. The
 # flat cap ends below tile undersides; the rest keeps a rounded asteroid outline.
 N=128; R=52; zcap=.76; theta0=math.acos(zcap); verts=[(0,0,zcap)];loops=[];cups=[]
 for k in range(30):
  row,col=divmod(k,5);zz=-.85+row*.25+((k*7)%5)*.012
  phi=math.pi+.22+col*.58+(row%2)*.14;rr=math.sqrt(1-zz*zz)
  cups.append((rr*math.cos(phi),rr*math.sin(phi),zz,.075+(k%4)*.012,.18+(k%3)*.04))
 cups.extend(((-.50,-.757,.42,.17,.22),(0,-.908,.42,.20,.26),(.50,-.757,.42,.17,.21)))
 for r in range(R):
  theta=theta0+(math.pi-theta0)*(r/(R-1));loop=[]
  for i in range(N):
   phi=2*math.pi*i/N
   x=math.sin(theta)*math.cos(phi);y=math.sin(theta)*math.sin(phi);z=math.cos(theta)
   # Geological bulges are broad and asymmetric; low amplitude keeps the body rounded.
   wav=1+.018*math.sin(5*phi+1.1)*math.sin(theta*4)+.012*math.sin(11*phi-1.4)*math.sin(theta*7)+.008*math.sin(x*19+y*13+z*7)
   # Thirty camera-facing impact bowls, deliberately staggered across the exposed
   # lower hemisphere so the silhouette reads as porous basalt at game distance.
   for cx,cy,cz,crater_r,depth in cups:
    d=math.acos(max(-1,min(1,x*cx+y*cy+z*cz)))
    wav-=depth*math.exp(-(d/crater_r)**2)
    wav+=depth*.28*math.exp(-((d-crater_r*1.24)/(crater_r*.22))**2)
   loop.append(len(verts));verts.append((x*wav,y*wav,z*wav))
  loops.append(loop)
 faces=[]
 for i in range(N):faces.append((0,loops[0][i],loops[0][(i+1)%N]))
 for r in range(R-1):
  for i in range(N):faces.append((loops[r][i],loops[r+1][i],loops[r+1][(i+1)%N],loops[r][(i+1)%N]))
 bottom=len(verts);verts.append((0,0,-1))
 for i in range(N):faces.append((loops[-1][i],bottom,loops[-1][(i+1)%N]))
 me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(verts,[],faces);me.update();ob=bpy.data.objects.new(name,me);COL.objects.link(ob)
 # Boolean impact vents open into the hollow core; two are visible from the player camera.
 for i,(x,z,rad) in enumerate(((-.50,.42,.145),(0,.42,.16),(.50,.42,.145))):
  bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=rad,depth=2.45,location=(x,0,z),rotation=(math.pi/2,0,0))
  cutter=bpy.context.object;mod=ob.modifiers.new('Crater vent %02d'%i,'BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
  bpy.context.view_layer.objects.active=ob;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cutter,do_unlink=True)
 me=ob.data;me.update();setup_uv(ob)
 # Vertex tint bakes narrow AO into each bowl; the low-poly mesh still carries
 # its actual depression and chipped lip.
 colors=me.color_attributes['Color']
 for poly in me.polygons:
  for li in poly.loop_indices:
   co=me.vertices[me.loops[li].vertex_index].co.normalized();shade=0.0;rim=0.0
   for cx,cy,cz,crater_r,depth in cups:
    d=math.acos(max(-1,min(1,co.x*cx+co.y*cy+co.z*cz)))
    shade=max(shade,.36*math.exp(-(d/(crater_r*.72))**2))
    rim=max(rim,.075*math.exp(-((d-crater_r*1.22)/(crater_r*.24))**2))
   value=max(.52,1-shade+rim);colors.data[li].color=(value,value,value,1)
 for p in me.polygons:p.use_smooth=True
 high=bpy.data.objects.new('HP_HollowCrust_Pores',me.copy());HP.objects.link(high)
 sub=high.modifiers.new('Subdivided lava crust','SUBSURF');sub.levels=2;sub.render_levels=2
 tex=bpy.data.textures.new('HollowCrust_Porosity',type='CLOUDS');tex.noise_scale=.065;tex.noise_depth=2
 dis=high.modifiers.new('Basalt vesicles and impact spall','DISPLACE');dis.texture=tex;dis.strength=.042;dis.mid_level=.53;dis.texture_coords='GLOBAL'
 high.hide_render=True;high.hide_viewport=True
 return ob

def make_kerb():
 name='SS_BSA_HollowKerb';remove(name);remove('HP_HollowKerb_Pitting')
 base=bpy.data.objects['SS_BSA_Kerb']; src=base.data;verts=[v.co.copy() for v in src.vertices];faces=[tuple(p.vertices) for p in src.polygons]
 rng=random.Random(8201)
 for i,v in enumerate(verts):
  # Break selected corners asymmetrically; preserve the flat bearing face.
  if rng.random()<.25 and (abs(v.x)>.42 or abs(v.y)>.42):v+=Vector((rng.uniform(-.035,.035),rng.uniform(-.035,.035),rng.uniform(-.018,.012)))
 me=bpy.data.meshes.new(name+'_Mesh');me.from_pydata(verts,[],faces);me.update()
 ob=bpy.data.objects.new(name,me);COL.objects.link(ob)
 # Two rivet bosses in the front and rear faces; bronze material comes from swatch 3.
 for yy in (-.18,.18):
  for xx in (-.36,.36):
   center=Vector((xx,yy,.22));u=8;rad=.09;sign=1 if yy>0 else -1
   baseidx=len(me.vertices);newverts=[tuple(center+Vector((rad*math.cos(2*math.pi*i/u),0,rad*math.sin(2*math.pi*i/u)))) for i in range(u)]+[tuple(center+Vector((0,sign*.035,0)))]
   me2verts=[tuple(x.co) for x in me.vertices]+newverts
   newfaces=[tuple(baseidx+i for i in range(u))+(baseidx+u,)];faces2=[tuple(p.vertices) for p in me.polygons]+newfaces
   old=me;me=bpy.data.meshes.new(name+'_Rivets');me.from_pydata(me2verts,[],faces2);me.update();bpy.data.objects.remove(ob,do_unlink=True);ob=bpy.data.objects.new(name,me);COL.objects.link(ob)
 # Add one 70mm bronze strap at each block joint; it wraps the full kerb cross-section.
 section=[(-.205,-.17),(.205,-.17),(.245,-.11),(.245,.32),(.205,.40),(-.205,.40),(-.245,.32),(-.245,-.11)]
 baseidx=len(me.vertices);bandverts=[]
 for x in (-.035,.035):
  for y,z in section:bandverts.append((x,y,z))
 bandfaces=[]
 for i in range(len(section)):
  j=(i+1)%len(section);bandfaces.append((baseidx+i,baseidx+j,baseidx+len(section)+j,baseidx+len(section)+i))
 allverts=[tuple(v.co) for v in me.vertices]+bandverts
 allfaces=[tuple(p.vertices) for p in me.polygons]+bandfaces
 bpy.data.objects.remove(ob,do_unlink=True);me=bpy.data.meshes.new(name+'_Strap');me.from_pydata(allverts,[],allfaces);me.update();ob=bpy.data.objects.new(name,me);COL.objects.link(ob)
 # Bronze rivets and strap share a single atlas material draw.
 setup_uv(ob)
 uv=me.uv_layers['UVMap']
 for p in me.polygons:
  if p.index>=len(src.polygons):
   p.material_index=1
   for li in p.loop_indices:uv.data[li].uv=(.80,.16) # bronze atlas swatch 3
 high=bpy.data.objects.new('HP_HollowKerb_Pitting',me.copy());HP.objects.link(high)
 sub=high.modifiers.new('Eroded basalt edge','SUBSURF');sub.levels=2;sub.render_levels=2
 tex=bpy.data.textures.new('HollowKerbPits',type='CLOUDS');tex.noise_scale=.035;tex.noise_depth=2
 dis=high.modifiers.new('Vesicles','DISPLACE');dis.texture=tex;dis.strength=.018;dis.mid_level=.54;dis.texture_coords='GLOBAL'
 high.hide_render=True;high.hide_viewport=True
 return ob

def export(ob,filename):
 bpy.ops.object.select_all(action='DESELECT');ob.hide_set(False);ob.hide_render=False;ob.select_set(True);bpy.context.view_layer.objects.active=ob
 path=OUTDIR/filename
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
 ob.hide_set(True);ob.hide_render=True
 return str(path)
crust=make_crust();kerb=make_kerb()
paths=[export(crust,'SS_BSA_HollowCrust.fbx'),export(kerb,'SS_BSA_HollowKerb.fbx')]
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
result={'paths':paths,'crust_tris':len(crust.data.polygons)*2,'kerb_tris':sum(len(p.vertices)-2 for p in kerb.data.polygons),'pores':'high-poly subdivision and clouds displace retained','crust_openings':3}

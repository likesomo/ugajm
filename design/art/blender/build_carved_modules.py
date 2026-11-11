import bpy, math, random
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend';COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
def remove(n):
 o=bpy.data.objects.get(n)
 if o:bpy.data.objects.remove(o,do_unlink=True)
def uv(o,slot):
 m=o.data
 U=m.uv_layers.new(name='UVMap');S=m.uv_layers.new(name='Style');C=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
 mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
 for p in m.polygons:
  ax=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][ax]
  for li in p.loop_indices:
   co=m.vertices[m.loops[li].vertex_index].co
   q=[.035+.93*(co[a]-mn[a])/max(.001,mx[a]-mn[a]) for a in axes]
   U.data[li].uv=((slot%4+q[0])/4,(slot//4+q[1])/4);S.data[li].uv=(4,.713);C.data[li].color=(1,1,1,1)
 m.materials.clear();m.materials.append(MAT);m.update()
def mesh(name,V,F):
 m=bpy.data.meshes.new(name+'_Mesh');m.from_pydata(V,[],F);m.update();o=bpy.data.objects.new(name,m);COL.objects.link(o);uv(o,9);return o
# Ten discontinuous sandstone beds; asymmetrical ledges and pinch-outs are real silhouette.
name='SS_BSA_Terrace';remove(name);remove('HP_CarvedTerrace_Layers')
N=32
# Uneven bed thickness, discontinuous ledges and cross-bed dip break the bark-like
# parallel stripe silhouette. The pale cut faces are exposed rather than painted on.
profiles=[(.80,0),(.96,-.10),(.86,-.23),(.99,-.39),(.78,-.47),(.94,-.61),(.84,-.70),(.99,-.85),(.88,-.96)]
ring=[];V=[];F=[];rng=random.Random(912)
for r,(s,z) in enumerate(profiles):
 loop=[]
 for i in range(N):
  a=2*math.pi*i/N+math.pi/8
  ir=s*(1+rng.uniform(-.11,.11))
  xx=math.copysign(abs(math.cos(a))**.72,math.cos(a));yy=math.copysign(abs(math.sin(a))**.72,math.sin(a))
  crossbed=(.055*math.sin(a*2.4+r*.83)+.028*math.sin(a*5.1-r*.37)) if r in (2,4,7) else 0
  pinch=.09*math.sin(a*1.7+r*1.3) if r in (1,5,8) else 0
  loop.append(len(V));V.append((xx*ir,yy*ir,z+rng.uniform(-.052,.052)+crossbed+pinch))
 ring.append(loop)
for j in range(len(ring)-1):
 for i in range(N):F.append((ring[j][i],ring[j][(i+1)%N],ring[j+1][(i+1)%N],ring[j+1][i]))
F.append(tuple(reversed(ring[0])));F.append(tuple(ring[-1]))
terrace=mesh(name,V,F)
high=bpy.data.objects.new('HP_CarvedTerrace_Layers',terrace.data.copy());HP.objects.link(high);sm=high.modifiers.new('Sandstone layer subdivision','SUBSURF');sm.levels=2;sm.render_levels=2;tx=bpy.data.textures.new('CarvedBeddingWeathering',type='CLOUDS');tx.noise_scale=.085;tx.noise_depth=2;dm=high.modifiers.new('Bedding spall relief','DISPLACE');dm.texture=tx;dm.strength=.018;dm.mid_level=.52;dm.texture_coords='GLOBAL';high.hide_render=True;high.hide_viewport=True
# Clean red-sandstone kerb with chipped corners, bedding seams and tool marks.
name='SS_BSA_CarvedKerb';remove(name);remove('HP_CarvedKerb_ToolMarks')
V=[(-.48,-.22,-.16),(.48,-.22,-.16),(.48,.22,-.16),(-.48,.22,-.16),(-.48,-.22,.37),(.48,-.22,.37),(.48,.22,.37),(-.48,.22,.37)]
F=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
# Inset broken ledges protrude just beyond the block; no repeating chevrons or moss stripe.
for z,w in ((.235,.014),(.045,.011),(-.105,.012)):
 base=len(V);V.extend([(-.49,-.226,z-w/2),(.49,-.226,z-w/2),(.49,-.226,z+w/2),(-.49,-.226,z+w/2)])
 F.append((base,base+1,base+2,base+3))
 base=len(V);V.extend([(-.49,.226,z-w/2),(.49,.226,z-w/2),(.49,.226,z+w/2),(-.49,.226,z+w/2)])
 F.append((base+3,base+2,base+1,base))
kerb=mesh(name,V,F)
# Deepen three chisel grooves using per-loop tint while retaining one atlas material.
attr=kerb.data.color_attributes['Color']
for p in kerb.data.polygons:
 if p.index>=6:
  for li in p.loop_indices:attr.data[li].color=(.42,.28,.22,1)
# One special front module carries the three engraved strokes for Roman III.
name='SS_BSA_CarvedKerbMarker';remove(name)
MV=[tuple(v.co) for v in kerb.data.vertices];MF=[tuple(p.vertices) for p in kerb.data.polygons]
for x in (-.12,0,.12):
 b=len(MV);MV.extend([(x-.022,-.235,.055),(x+.022,-.235,.055),(x+.022,-.235,.345),(x-.022,-.235,.345)]);MF.append((b,b+1,b+2,b+3))
marker=mesh(name,MV,MF);mc=marker.data.color_attributes['Color']
for p in marker.data.polygons[-3:]:
 for li in p.loop_indices:
  mc.data[li].color=(1,1,1,1)
  marker.data.uv_layers['UVMap'].data[li].uv=(.875,.375) # pale quartz swatch: legible III from the game camera
high=bpy.data.objects.new('HP_CarvedKerbMarker_Engraving',marker.data.copy());HP.objects.link(high);sm=high.modifiers.new('Chisel cut strokes','SUBSURF');sm.levels=2;sm.render_levels=2;high.hide_render=True;high.hide_viewport=True
high=bpy.data.objects.new('HP_CarvedKerb_ToolMarks',kerb.data.copy());HP.objects.link(high);sm=high.modifiers.new('Sandstone chisel detail','SUBSURF');sm.levels=2;sm.render_levels=2;tx=bpy.data.textures.new('CarvedKerbGrain',type='CLOUDS');tx.noise_scale=.045;tx.noise_depth=2;dm=high.modifiers.new('Spalled bedding edges','DISPLACE');dm.texture=tx;dm.strength=.018;dm.mid_level=.52;dm.texture_coords='GLOBAL';high.hide_render=True;high.hide_viewport=True
def export(o,file):
 bpy.ops.object.select_all(action='DESELECT');o.hide_set(False);o.hide_render=False;o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.export_scene.fbx(filepath=str(OUT/file),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
 o.hide_set(True);o.hide_render=True
 return sum(len(p.vertices)-2 for p in o.data.polygons)
tris={'terrace':export(terrace,'SS_BSA_Terrace.fbx'),'carvedKerb':export(kerb,'SS_BSA_CarvedKerb.fbx'),'carvedKerbMarker':export(marker,'SS_BSA_CarvedKerbMarker.fbx')};bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'exports':tris,'bed_count':len(profiles),'source':str(SRC)}

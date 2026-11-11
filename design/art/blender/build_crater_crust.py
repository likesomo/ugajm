"""Deterministic, original crater relief; headless Blender 5.2. FBX binaries live in Shared only.
Reference board is 5x5. Runtime stretches only the hidden central support to fit board bounds.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge/CraterCrust')
OUT.mkdir(parents=True,exist_ok=True)
IDS=['moss','hollow','carved','frost','joinery','crumble','altar']
N=256;R=40
report={}
for pi,planet in enumerate(IDS):
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 rng=random.Random(911+pi*337);craters=[]
 # Front and lateral impacts remain legible at the orthographic 50 degree game camera.
 for x,z,r in [(-2.7,-3.83,.73),(-.65,-3.86,.82),(1.7,-3.88,.67),(3.65,-2.1,.68),(-3.72,-.7,.76),(3.84,.65,.64),(-3.65,2.35,.62),(.8,3.85,.72),(-1.65,3.8,.56)]:
  craters.append((x+rng.uniform(-.12,.12),z+rng.uniform(-.10,.10),r,rng.uniform(.38,.62)*(1.65 if planet=='hollow' else 1)))
 for k in range(38):
  a=rng.random()*math.tau;c=math.cos(a);s=math.sin(a);rad=rng.uniform(3.25,4.65)/max(abs(c),abs(s));x=c*rad;z=s*rad
  if (x/5)**4+(z/5)**4<.97:craters.append((x,z,rng.uniform(.10,.26),rng.uniform(.08,.20)))
 def field(x,z):
  edge=max(abs(x),abs(z))-2.96;fade=min(1,max(0,edge/.30));rho=((x/5)**4+(z/5)**4)**.25
  rough=.035*math.sin(x*17+math.sin(z*9))+.024*math.sin(z*27+x*13)+.030*math.sin(x*6-z*7)
  y=-.17+fade*(rough-.10*max(0,rho-.8)/.2);ao=1;bow=0
  for cx,cz,r,d in craters:
   q=math.hypot(x-cx,z-cz)/r
   if q>1.65:continue
   bowl=math.exp(-(q/.70)**4);rim=math.exp(-((q-1.0)/.17)**2)
   y+=fade*(-d*bowl+d*.40*rim*(1+.20*math.sin(math.atan2(z-cz,x-cx)*7)))
   ao=min(ao,1-.37*bowl);bow=max(bow,bowl)
  if planet=='carved':y+=fade*.045*math.sin(y*44+x*.8)
  if planet=='crumble':y+=fade*.055*math.sin(x*23+z*4)*max(0,rho-.7)
  return y,ao,bow
 verts=[];colors=[];faces=[]
 def vertex(x,y,z,ao=1,bowl=0):
  tint=[ao]*3
  if planet=='moss':tint=[ao*(1-.32*bowl),ao*(1-.10*bowl),ao*(1-.40*bowl)]
  if planet=='frost':tint=[ao*(1-.12*bowl),ao*(1+.11*bowl),ao*(1+.24*bowl)]
  if planet=='altar':tint=[ao*(1-.09*bowl),ao,ao*(1-.18*bowl)]
  # Blender Z up; FBX axis conversion yields Unity Y up.
  verts.append((x,-z,y));colors.append((*tint,1));return len(verts)-1
 loops=[]
 for j in range(R+1):
  t=j/R;loop=[]
  for i in range(N):
   a=i*math.tau/N;c=math.cos(a);s=math.sin(a)
   inner=2.96/max(abs(c),abs(s));outer=5/(c**4+s**4)**.25
   outer*=1+.012*math.sin(a*19+pi)+.009*math.sin(a*37-1)
   rad=inner+(outer-inner)*t;x=c*rad;z=s*rad;y,ao,bowl=field(x,z)
   loop.append(vertex(x,y,z,ao,bowl))
  loops.append(loop)
 center=vertex(0,-.17,0)
 for i in range(N):faces.append((center,loops[0][i],loops[0][(i+1)%N]))
 for j in range(R):
  for i in range(N):
   a=loops[j][i];b=loops[j][(i+1)%N];c=loops[j+1][i];d=loops[j+1][(i+1)%N]
   faces.extend([(a,c,b),(b,c,d)])
 last=loops[-1]
 for j in range(1,7):
  t=j/6;loop=[]
  for i in range(N):
   vx,vz,vy=verts[loops[-1][i]];x=vx*(1-.45*t*t);z=-vz*(1-.45*t*t)
   chips=.10*math.sin(i*1.71+pi)+.065*math.sin(i*.73)
   y=vy*(1-t)-2.6*t+chips*math.sin(t*math.pi)
   # Side scallops and fluted erosion expose the depth of the impact crust.
   a=i*math.tau/N;scar=.16*math.sin(a*17+t*3)**6*math.sin(t*math.pi)
   if planet=='crumble':scar*=2.4
   loop.append(vertex(x*(1-scar*.13),y,z*(1-scar*.13),.76+.12*(1-t)))
  for i in range(N):a=last[i];b=last[(i+1)%N];c=loop[i];d=loop[(i+1)%N];faces.extend([(a,c,b),(b,c,d)])
  last=loop
 bottom=vertex(0,-2.9,0,.65)
 for i in range(N):faces.append((last[i],bottom,last[(i+1)%N]))
 me=bpy.data.meshes.new('CraterCrust_'+planet);me.from_pydata(verts,[],faces);me.update()
 ob=bpy.data.objects.new('CraterCrust_'+planet,me);bpy.context.collection.objects.link(ob);bpy.context.view_layer.objects.active=ob;ob.select_set(True)
 # Recalculate outward normals after explicit triangulation.
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
 uv=me.uv_layers.new(name='UVMap');style=me.uv_layers.new(name='Style');col=me.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
 for p in me.polygons:
  p.use_smooth=True
  for li in p.loop_indices:
   vi=me.loops[li].vertex_index;x,z,y=verts[vi]
   uv.data[li].uv=((.05+.90*(x/10.6+.5))/4,(.05+.90*(-z/10.6+.5))/4)
   style.data[li].uv=(4,.618);col.data[li].color=colors[vi]
 bpy.ops.export_scene.fbx(filepath=str(OUT/(ob.name+'.fbx')),use_selection=True,axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,add_leaf_bones=False,bake_anim=False,use_mesh_modifiers=True,mesh_smooth_type='OFF',colors_type='LINEAR')
 report[planet]={'triangles':len(faces),'vertices':len(verts),'impacts':len(craters),'materials':1}
 source=Path('C:/unityjam/shared-assets/seven-steps/Source/Blender/CraterCrust');source.mkdir(parents=True,exist_ok=True)
 bpy.ops.wm.save_as_mainfile(filepath=str(source/(ob.name+'.blend')))
(OUT/'mesh-report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))

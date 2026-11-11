"""Timber mortise-tenon frame and laminated slate cliffs for the R2 board kit."""
import bpy, math, random
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
for name in ('SS_BSA_TimberFrame','HP_JoineryTimber_MortiseTenon','SS_BSA_JoineryCrust','HP_JoinerySlate_Bedding'):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
V=[];F=[];SW=[]
def box(center,size,swatch,bevel=.025):
    cx,cy,cz=center;sx,sy,sz=[v*.5 for v in size];base=len(V)
    V.extend([(cx-sx,cy-sy,cz-sz),(cx+sx,cy-sy,cz-sz),(cx+sx,cy+sy,cz-sz),(cx-sx,cy+sy,cz-sz),(cx-sx,cy-sy,cz+sz),(cx+sx,cy-sy,cz+sz),(cx+sx,cy+sy,cz+sz),(cx-sx,cy+sy,cz+sz)])
    for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
        F.append(tuple(base+i for i in face));SW.append(swatch)
# Sawn slate-capped frame: posts, double tie rails, large diagonal knee braces.
box((-.64,0,-.34),(.28,.33,1.08),2,.045);box((.64,0,-.34),(.28,.33,1.08),2,.045)
box((0,0,.16),(1.50,.34,.25),2,.035);box((0,0,-.53),(1.36,.31,.19),2,.035)
box((-.39,0,-.18),(.20,.28,.58),2,.025);box((.39,0,-.18),(.20,.28,.58),2,.025)
# Exposed cross-braces make the joint read as structural, not decorative trim.
# Add diagonal bars as explicitly rotated box vertices.
def diag(center,size,angle):
    cx,cy,cz=center;sx,sy,sz=[v*.5 for v in size];base=len(V);co=[]
    for x,y,z in ((-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),(-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)):
        a=math.radians(angle);co.append((cx+x*math.cos(a)+z*math.sin(a),cy+y,cz-x*math.sin(a)+z*math.cos(a)))
    V.extend(co)
    for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):F.append(tuple(base+i for i in face));SW.append(2)
diag((-.43,-.01,-.27),(.17,.28,.69),-39);diag((.43,-.01,-.27),(.17,.28,.69),39)
# Forged bronze wedges, cotter straps and visible wooden pegs at the joints.
for x in (-.64,.64):
    box((x,-.19,-.08),(.34,.045,.13),3,.01);box((x,-.205,-.08),(.18,.032,.055),3,.01)
    for z in (-.42,.02):
        box((x,-.20,z),(.105,.065,.105),3,.018)
        # Small tenon shoulders frame the bright wedge plates.
        box((x*.72,-.19,z),(.12,.045,.12),2,.012)
m=bpy.data.meshes.new('Joinery_MortiseTenonTimber_Mesh');m.from_pydata(V,[],F);m.update();ob=bpy.data.objects.new('SS_BSA_TimberFrame',m);COL.objects.link(ob);m.materials.append(MAT)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');color=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
    sw=SW[p.index];n=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][n]
    for li in p.loop_indices:
        q=m.vertices[m.loops[li].vertex_index].co;u=.045+.91*(q[axes[0]]-mn[axes[0]])/max(.001,mx[axes[0]]-mn[axes[0]]);v=.045+.91*(q[axes[1]]-mn[axes[1]])/max(.001,mx[axes[1]]-mn[axes[1]])
        uv.data[li].uv=((sw%4+u)/4,(sw//4+v)/4);style.data[li].uv=(4,.713);color.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
be=ob.modifiers.new('Worn timber edges and join seams','BEVEL');be.width=.018;be.segments=1;be.limit_method='ANGLE'
hp=bpy.data.objects.new('HP_JoineryTimber_MortiseTenon',m.copy());HP.objects.link(hp);sub=hp.modifiers.new('Wood grain relief density','SUBSURF');sub.levels=2;sub.render_levels=2;tex=bpy.data.textures.new('JoineryOakGrain','WOOD');tex.noise_scale=.10;dis=hp.modifiers.new('Long grain and worn endgrain','DISPLACE');dis.texture=tex;dis.strength=.008;dis.mid_level=.52;hp.hide_render=True;hp.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
bpy.ops.export_scene.fbx(filepath=str(OUT/'SS_BSA_TimberFrame.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
# Thin, chipped slate laminae replace bulky generic crust on the joinery world edge.
V2=[];F2=[];rings=[];N=8;rng2=random.Random(8851)
for ri,(radius,z) in enumerate(((.80,-.50),(.92,-.24),(.74,.12),(.87,.50))):
    row=[]
    for i in range(N):
        a=2*math.pi*i/N+math.pi/8;rr=radius*rng2.uniform(.88,1.12)
        xx=math.copysign(abs(math.cos(a))**.72,math.cos(a));yy=math.copysign(abs(math.sin(a))**.72,math.sin(a))
        row.append(len(V2));V2.append((xx*rr,yy*rr,z+rng2.uniform(-.025,.025)))
    rings.append(row)
for k in range(len(rings)-1):
    for i in range(N):F2.append((rings[k][i],rings[k][(i+1)%N],rings[k+1][(i+1)%N],rings[k+1][i]))
F2.extend((tuple(reversed(rings[0])),tuple(rings[-1])))
m2=bpy.data.meshes.new('Joinery_LaminatedSlate_Mesh');m2.from_pydata(V2,[],F2);m2.update();slate=bpy.data.objects.new('SS_BSA_JoineryCrust',m2);COL.objects.link(slate);m2.materials.append(MAT)
uv2=m2.uv_layers.new(name='UVMap');st2=m2.uv_layers.new(name='Style');co2=m2.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mn2=[min(v.co[i] for v in m2.vertices) for i in range(3)];mx2=[max(v.co[i] for v in m2.vertices) for i in range(3)]
for p in m2.polygons:
    axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][axis]
    for li in p.loop_indices:
        q=m2.vertices[m2.loops[li].vertex_index].co;u=.035+.93*(q[axes[0]]-mn2[axes[0]])/max(.001,mx2[axes[0]]-mn2[axes[0]]);v=.035+.93*(q[axes[1]]-mn2[axes[1]])/max(.001,mx2[axes[1]]-mn2[axes[1]])
        uv2.data[li].uv=((11%4+u)/4,(11//4+v)/4);st2.data[li].uv=(4,.618);co2.data[li].color=(1,1,1,1)
uv2.active=True
hp2=bpy.data.objects.new('HP_JoinerySlate_Bedding',m2.copy());HP.objects.link(hp2);sub=hp2.modifiers.new('Slate lamination density','SUBSURF');sub.levels=2;sub.render_levels=2;tex2=bpy.data.textures.new('JoinerySlateBeddingSpall',type='CLOUDS');tex2.noise_scale=.055;dis=hp2.modifiers.new('Slate chipped foliation','DISPLACE');dis.texture=tex2;dis.strength=.025;dis.mid_level=.52;hp2.hide_render=True;hp2.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');slate.select_set(True);bpy.context.view_layer.objects.active=slate
bpy.ops.export_scene.fbx(filepath=str(OUT/'SS_BSA_JoineryCrust.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
tris=sum(len(p.vertices)-2 for p in m.polygons);bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'timber_frame_source_tris':tris,'laminated_slate_tris':sum(len(p.vertices)-2 for p in m2.polygons),'members':'posts, rails, diagonals, tenon shoulders, bronze wedges, pegs','highpoly':[hp.name,hp2.name],'source':str(SRC)}

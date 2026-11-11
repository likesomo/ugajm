"""R2 A production kit. Execute via Blender MCP: exec(compile(open(__file__).read(), __file__, 'exec')).

Only the owned BoardShapeA scene is replaced. Original startup scene is preserved.
Set BSA_BAKE=False in MCP globals for a quick blockout; default final build bakes
real high resolution relief to tangent normals and AO, exports FBX and saves .blend.
No downloaded assets. All lengths are metres; Z=0 is the playable surface.
"""
import bpy, math, random, json, zlib, sys, importlib
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/unityjam/shared-assets/seven-steps')
OUT = Path(globals().get('BSA_OUT', ROOT / 'Assets/Art/RuinsHenge/BoardShapeA'))
SOURCE = Path(globals().get('BSA_SOURCE', ROOT / 'Source/Blender/SS_BoardShapeA.blend'))
REVIEW = Path(globals().get('BSA_REVIEW', ROOT / 'concepts/themes/board-shape-2026-11-10-r2/unity'))
REVIEW_TARGET=REVIEW/'stone-pass/after'
REVIEW_TARGET.mkdir(parents=True,exist_ok=True)
sys.dont_write_bytecode=True
sys.path.insert(0,'C:/Users/soso1/orca/workspaces/ugajm/art/design/art/blender')
import stone_recipes
importlib.reload(stone_recipes)
from stone_recipes import recipe, STONE_SLOTS
for p in (OUT, SOURCE.parent, REVIEW): p.mkdir(parents=True, exist_ok=True)
BAKE = globals().get('BSA_BAKE', True)
PREVIEW_SIZE = globals().get('BSA_SIZE',(8,6))
old = bpy.data.scenes.get('BoardShapeA')
if old:
    for o in list(old.objects): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new('BoardShapeA')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
library = bpy.data.collections.new('01_EXPORT_LIBRARY_BSA'); scene.collection.children.link(library)
preview = bpy.data.collections.new('02_BOARD_INSTANCES_BSA'); scene.collection.children.link(preview)
studio = bpy.data.collections.new('99_STUDIO_NO_EXPORT_BSA'); scene.collection.children.link(studio)
highpoly = bpy.data.collections.new('03_HIGH_POLY_SOURCES'); scene.collection.children.link(highpoly)

# Sixteen 256px swatches in TWO 1024-square sheets. The extra rows contain
# actual geological recipes, not chapter tints of the same grey crack pattern.
COLORS = [(.43,.45,.44),(.37,.39,.38),(.69,.52,.31),(.60,.38,.15),(.19,.44,.42),(.65,.30,.17),(.262,.323,.060),(.66,.81,.88)]+[(.5,.5,.5)]*8
def mat():
    m = bpy.data.materials.get('SS_M_BoardShapeA') or bpy.data.materials.new('SS_M_BoardShapeA')
    m.use_nodes = True
    return m
material = mat()
def uv_map(obj, swatch):
    mesh = obj.data
    uv = mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    mins=[min(v.co[i] for v in mesh.vertices) for i in range(3)]
    maxs=[max(v.co[i] for v in mesh.vertices) for i in range(3)]
    for poly in mesh.polygons:
        n = poly.normal; axis = max(range(3), key=lambda i: abs(n[i]))
        axes = [(1,2),(0,2),(0,1)][axis]
        for li in poly.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co
            a,b = v[axes[0]],v[axes[1]]
            # Whole repeatable swatch with a 6px gutter; no palette bleed.
            amin=mins[axes[0]];amax=maxs[axes[0]]
            bmin=mins[axes[1]];bmax=maxs[axes[1]]
            seed=zlib.crc32((obj.name+':'+str(poly.index)).encode())
            u = .035 + .77 * ((a-amin)/max(.001,amax-amin))+(seed%97)/97*.16
            w = .035 + .77 * ((b-bmin)/max(.001,bmax-bmin))+(seed%71)/71*.16
            # Granite hairlines live only on top variants. Rough sides get no
            # copied top-face crack, so a block cannot become a web of fissures.
            face_swatch=1 if swatch in (0,14,15) and axis!=2 else swatch
            uv.data[li].uv=((face_swatch%4+u)/4, (face_swatch//4+w)/4)
    mesh.materials.append(material)
    attr = mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for c in attr.data: c.color=(1,1,1,1)
    style=mesh.uv_layers.new(name='Style')
    for item in style.data:item.uv=(4,(zlib.crc32(obj.name.encode())%997)/997)
    mesh.uv_layers.active=uv
    return obj
def mesh_obj(name, verts, faces, swatch):
    me=bpy.data.meshes.new(name); me.from_pydata(verts,[],faces); me.update()
    ob=bpy.data.objects.new(name,me); library.objects.link(ob)
    uv_map(ob,swatch); return ob
def box(name, size, pos=(0,0,0), swatch=0, bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    ob=bpy.context.object; ob.name=name; ob.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    for c in list(ob.users_collection): c.objects.unlink(ob)
    library.objects.link(ob)
    if bevel:
        mod=ob.modifiers.new('One broad chamfer','BEVEL');mod.width=bevel;mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
        if swatch==0:
            rng=random.Random(zlib.crc32(name.encode()))
            # Asymmetric small spalls in the broad bevel, never the clean centre.
            for vertex in ob.data.vertices:
                if rng.random()<.22:
                    inward=Vector((-vertex.co.x,-vertex.co.y,-vertex.co.z)).normalized()
                    vertex.co+=inward*rng.uniform(.004,.017)
            ob.data.update()
    uv_map(ob,swatch); return ob
def join(name, parts):
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join(); ob=bpy.context.object;ob.name=name
    # Store exported pivots at the specified module origin, never bounds center.
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return ob
def tile(k):
    rng=random.Random(109+k)
    ring=[(-.44,-.49),(.43,-.49),(.49,-.43),(.49,.43),(.43,.49),(-.43,.49),(-.49,.43),(-.49,-.43)]
    notch=-.17+k*.15
    ring[1:1]=[(notch-.05,-.49),(notch-.025,-.465),(notch+.025,-.47),(notch+.05,-.49)]
    ring=[(x+rng.uniform(-.003,.003),y+rng.uniform(-.003,.003)) for x,y in ring]
    n=len(ring)
    verts=[]
    for j,(scale,z) in enumerate([(1,-.16),(1,-.07),(.86,0)]):
        verts.extend([(x*scale,y*scale,z+(rng.uniform(-.016,.011) if j==1 else 0)) for x,y in ring])
    faces=[tuple(reversed(range(n))),tuple(range(n*2,n*3))]
    for j in range(2):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    ob=mesh_obj('SS_BSA_Floor_'+str(k),verts,faces,[0,14,15][k])
    # Explicit bevel/edge color is a curvature mask without an extra material or sample.
    colors=ob.data.color_attributes['Color']
    for face in ob.data.polygons:
        c=(.98,.98,.98,1) if face.index==1 or face.index>=n+2 else (.64,.65,.63,1)
        for li in face.loop_indices:colors.data[li].color=c
    # Moss occupies the recessed outer seam only, never the central label area.
    parts=[ob]
    for i in range(2):
        x=(-1 if i==0 else 1)*.493;y=.21-k*.17
        parts.append(mesh_obj('SeamMoss',[(x-.006,y-.13,-.012),(x+.006,y-.13,-.012),(x+.006,y+.13,-.012),(x-.006,y+.13,-.012)],[(0,1,2,3)],6))
    return join('SS_BSA_Floor_'+str(k),parts)
def kerb():
    # Each 1m module leaves a 32mm joint. Broad bevels make a real V groove.
    parts=[box('KerbStone',(.968,.44,.52),(0,0,.11),0,.07)]
    parts.append(mesh_obj('InlaidTeal',[(-.47,-.226,-.01),(.47,-.226,-.01),(.47,-.226,.24),(-.47,-.226,.24)],[(0,1,2,3)],4))
    parts.append(mesh_obj('TopTeal',[(-.47,-.14,.373),(.47,-.14,.373),(.47,.14,.373),(-.47,.14,.373)],[(0,1,2,3)],4))
    # Broad alternating wedges on the front face, actual geometry at low cost.
    for x in [-.32,.20]:
        v=[(x-.17,-.231,-.005),(x,-.231,.235),(x+.17,-.231,-.005),(x+.09,-.231,-.005),(x,-.231,.145),(x-.09,-.231,-.005)]
        parts.append(mesh_obj('TerracottaChevron',v,[(5,4,1,0),(4,3,2,1)],5))
        v=[(x-.17,-.13,.381),(x,.13,.381),(x+.17,-.13,.381),(x+.09,-.13,.381),(x,.035,.381),(x-.09,-.13,.381)]
        parts.append(mesh_obj('TopChevron',v,[(5,4,1,0),(4,3,2,1)],5))
    # FBX handedness must not hide the exterior strip: both physical faces are authored.
    import bmesh
    for src in list(parts):
        if src.name.startswith(('InlaidTeal','TerracottaChevron')):
            ob=src.copy();ob.data=src.data.copy();library.objects.link(ob)
            for vert in ob.data.vertices:vert.co.y=-vert.co.y
            bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();parts.append(ob)
    return join('SS_BSA_Kerb',parts)
def rock(k):
    rng=random.Random(701+k);n=12;v=[]
    # Convex top ledge, broken strata and taper. Long axis is local X.
    perimeter=[(-.52,-.5),(-.15,-.57),(.18,-.53),(.49,-.46),(.56,-.12),(.52,.20),(.42,.52),(.12,.56),(-.16,.51),(-.49,.40),(-.58,.10),(-.53,-.19)]
    for r,(s,z) in enumerate([(1,-.16),(1.06,-.44),(.98,-1.02),(.94,-1.89),(.90,-2.65)]):
        for i,(x,y) in enumerate(perimeter):
            zz=z+rng.uniform(-.20 if r==4 else -.13,.22 if r==4 else .05)
            # Bottom remains broad along the module seam, but retreats inward in depth.
            # This joins into a fractured cliff instead of a row of hanging round teeth.
            sx=1.03 if r==4 else s;sy=.40 if r==4 else s
            v.append((x*sx+rng.uniform(-.095,.095),y*sy+rng.uniform(-.09,.09),zz))
    v.append((.04,-.03,-.035))
    f=[(60,i,(i+1)%n) for i in range(n)]+[tuple(reversed(range(48,60)))]
    for r in range(4):
        for i in range(n):
            a=r*n+i;b=r*n+(i+1)%n;c=b+n;d=a+n
            f.extend([(a,d,b),(b,d,c)])
    ob=mesh_obj('SS_BSA_Crust_'+str(k),v,f,1)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    mod=ob.modifiers.new('Chipped broad edges','BEVEL');mod.width=.065;mod.segments=1;mod.limit_method='ANGLE';mod.angle_limit=.80
    bpy.ops.object.modifier_apply(modifier=mod.name)
    # Vertex color contains a baked vertical AO/strata gradient, independent of lighting.
    colors=ob.data.color_attributes['Color']
    for loop in ob.data.loops:
        z=ob.data.vertices[loop.vertex_index].co.z;t=max(0,min(1,(z+3)/2.85));shade=.42+.58*t
        colors.data[loop.index].color=(shade,shade,shade,1)
    if k%2:
        for uv in ob.data.uv_layers[0].data:uv.uv.x=.75-uv.uv.x
    return ob
def menhir(k):
    rng=random.Random(k+18);v=[]
    for z,s in [(0,1),(.55,.94),(1.25,.67),(1.70,.38)]:
        for i in range(5):
            a=2*math.pi*i/5;v.append((math.cos(a)*.30*s,math.sin(a)*.26*s,z+rng.uniform(0,.1)))
    f=[tuple(reversed(range(5))),tuple(range(15,20))]
    for j in range(3):
        for i in range(5):f.append((j*5+i,j*5+(i+1)%5,(j+1)*5+(i+1)%5,(j+1)*5+i))
    return mesh_obj('SS_BSA_Menhir_'+str(k),v,f,0)
modules={}
for k in range(3):
    for o in [tile(k),rock(k)]:modules[o.name]=o
for k in range(2):
    o=menhir(k);modules[o.name]=o
o=kerb();modules[o.name]=o
cap=mesh_obj('PyramidCap',[(-.27,-.27,.43),(.27,-.27,.43),(.27,.27,.43),(-.27,.27,.43),(0,0,.69)],[(3,2,1,0),(0,1,4),(1,2,4),(2,3,4),(3,0,4)],0)
o=join('SS_BSA_Corner',[box('CornerBase',(.45,.45,.59),(0,0,.145),0,.065),cap,box('Inlay',(.39,.018,.10),(0,-.235,.15),4,.005)]);modules[o.name]=o
o=join('SS_BSA_Medallion',[box('Diamond',(.29,.045,.29),(0,0,0),5,.015),box('Inner',(.17,.055,.17),(0,-.006,0),4,.01)]);o.rotation_euler.y=math.pi/4;bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=False,rotation=True,scale=False);modules[o.name]=o
parts=[box('WoodenChest',(.64,.40,.42),(0,0,.21),2,.018)]
for x in [-.24,.24]:
    parts.append(box('BronzeBinding',(.06,.43,.45),(x,0,.225),3,.008))
    for y in [-.15,.15]:parts.append(box('BronzeRivet',(.045,.045,.025),(x,y,.46),3,.01))
o=join('SS_BSA_Chest',parts);modules[o.name]=o
parts=[]
for x in [-.78,.78]:
    parts.extend([box('StonePier',(.30,.32,.65),(x,0,.325),0),box('PierCap',(.37,.39,.1),(x,0,.71),0)])
for z in [.16,.33,.50]:parts.append(box('TimberPlank',(1.25,.14,.15),(0,0,z),2,.01))
for x in [-.51,.51]:
    parts.append(box('Strap',(.085,.18,.59),(x,0,.32),3,.008))
    for z in [.10,.33,.55]:parts.append(box('Rivet',(.06,.04,.06),(x,-.105,z),3,.018))
o=join('SS_BSA_BackGate',parts);modules[o.name]=o
o=join('SS_BSA_Wall',[box('StoneWall',(.96,.35,.29),(0,0,.145),0,.025),box('BrokenCourseA',(.43,.36,.24),(-.23,0,.40),0,.035),box('BrokenCourseB',(.43,.35,.19),(.25,0,.375),0,.03)]);modules[o.name]=o
o=join('SS_BSA_Joist',[box('Joist',(1,.25,.28),(0,0,0),2,.02),box('BronzeBrace',(.14,.28,.31),(.32,0,0),3,.01)]);modules[o.name]=o
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1)
o=bpy.context.object;o.name='SS_BSA_Rubble';o.scale=(.25,.19,.13)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
for c in list(o.users_collection):c.objects.unlink(o)
library.objects.link(o);uv_map(o,1);modules[o.name]=o
ring=[(-.36,-.22),(.26,-.25),(.36,-.12),(.29,.21),(-.25,.24),(-.38,.09)]
v=[(x,y,z) for z in [-.07,.03] for x,y in ring]
f=[tuple(reversed(range(6))),tuple(range(6,12))]+[(i,(i+1)%6,(i+1)%6+6,i+6) for i in range(6)]
o=mesh_obj('SS_BSA_Fragment',v,f,0);modules[o.name]=o
v=[];f=[]
for k in range(3):
    a=k*math.pi/3;x=math.cos(a)*.06;y=math.sin(a)*.06;s=len(v)
    v.extend([(x-.05,y,0),(x+.05,y,0),(x+.018,y,.17),(x,y,.11)])
    f.extend([(s,s+1,s+2),(s,s+2,s+3)])
o=mesh_obj('SS_BSA_Grass',v,f,6);modules[o.name]=o
# Explicit landmark modules make the seven silhouettes readable at thumbnail scale.
parts=[];n=12
for i in range(n):
    a=i*2*math.pi/n;b=(i+1)*2*math.pi/n
    section=[(1.02*math.cos(a),-1.42+1.22*math.sin(a)),(1.02*math.cos(b),-1.42+1.22*math.sin(b)),(.67*math.cos(b),-1.42+.79*math.sin(b)),(.67*math.cos(a),-1.42+.79*math.sin(a))]
    vv=[(x,y,z) for y in [-.24,.24] for x,z in section]
    ff=[(0,1,2,3),(7,6,5,4),(4,5,1,0),(5,6,2,1),(6,7,3,2),(7,4,0,3)]
    parts.append(mesh_obj('OpenCaveVoussoir',vv,ff,1))
o=join('SS_BSA_HollowArch',parts);modules[o.name]=o
o=join('SS_BSA_Terrace',[box('CutStratum',(1,.72,.21),(0,0,-.105),1,.025),box('OchreStratum',(1.01,.735,.035),(0,0,-.165),3,.008)]);modules[o.name]=o
v=[(math.cos(i*math.pi/3)*.22,math.sin(i*math.pi/3)*.18,0) for i in range(6)]+[(.05,-.03,1.3)]
f=[tuple(reversed(range(6)))]+[(i,(i+1)%6,6) for i in range(6)]
o=mesh_obj('SS_BSA_FrostSpire',v,f,7);modules[o.name]=o
parts=[box('ExposedJoist',(1.65,.34,.24),(0,0,-.1),2,.02)]
for x in [-.63,.63]:
    parts.append(box('TenonLeg',(.22,.35,.96),(x,0,-.42),2,.02))
    parts.append(box('BronzePeg',(.27,.40,.08),(x,0,-.19),3,.015))
o=join('SS_BSA_TimberFrame',parts);modules[o.name]=o
ring=[(-.37,-.21),(-.16,-.32),(.19,-.28),(.42,-.07),(.26,.23),(-.11,.29),(-.40,.08)]
v=[(0,0,.085)]+[(x*.63,y*.63,.065+random.Random(i+4).random()*.025) for i,(x,y) in enumerate(ring)]+[(x,y,.005) for x,y in ring]
f=[(0,i+1,(i+1)%7+1) for i in range(7)]+[(i+1,i+8,(i+1)%7+8,(i+1)%7+1) for i in range(7)]
o=mesh_obj('SS_BSA_MossPatch',v,f,6);modules[o.name]=o
for loop in o.data.loops:
    height=o.data.vertices[loop.vertex_index].co.z
    # Exact olive palette, dark roots and yellow-green exposed tips.
    t=min(1,height/.08);dark=(.076/.262,.105/.323,.027/.060)
    o.data.color_attributes['Color'].data[loop.index].color=tuple(a+(1-a)*t for a in dark)+(1,)
o=join('SS_BSA_Block',[box('GraniteBlock',(.90,.90,.34),(0,0,.17),0,.07),box('Cap',(.94,.94,.085),(0,0,.3825),0,.04)]);modules[o.name]=o
parts=[]
for x in [-.425,.425]:parts.append(box('BronzePitRim',(.11,.96,.12),(x,0,-.06),3,.018))
for y in [-.425,.425]:parts.append(box('BronzePitRim',(.74,.11,.12),(0,y,-.06),3,.018))
for x in [-.42,.42]:
    for y in [-.42,.42]:parts.append(box('Rivet',(.075,.075,.022),(x,y,.005),3,.014))
o=join('SS_BSA_Pit',parts);modules[o.name]=o
o=box('SS_BSA_Cavity',(.74,.74,.015),(0,0,-.25),1,0)
for c in o.data.color_attributes['Color'].data:c.color=(.035,.035,.035,1)
o=join('SS_BSA_Cavity',[o]);modules[o.name]=o
parts=[box('IceSlab',(.96,.96,.12),(0,0,-.06),7,.04)]
for y in [-.20,0,.20]:parts.append(box('ParallelGlideGroove',(.58,.045,.006),(0,y,.003),4,.012))
o=join('SS_BSA_Ice',parts);modules[o.name]=o
o=box('MudEarth',(.96,.96,.12),(0,0,-.06),1,.015)
for c in o.data.color_attributes['Color'].data:c.color=(.78,.52,.28,1)
parts=[o]
for k in range(7):
    rng=random.Random(k+41);parts.append(box('MudPebble',(.06,.07,.014),(rng.uniform(-.38,.38),rng.uniform(-.38,.38),.004),1,0))
for x in [-.455,.455]:parts.append(box('LowEarthRim',(.035,.94,.028),(x,0,.006),2,0))
for y in [-.455,.455]:parts.append(box('LowEarthRim',(.87,.035,.028),(0,y,.006),2,0))
o=join('SS_BSA_Mud',parts);modules[o.name]=o
parts=[box('Pier',(.18,.34,.38),(-.36,0,.19),0,.022),box('Pier',(.18,.34,.38),(.36,0,.19),0,.022)]
for k in range(5):
    a=k*math.pi/5+.025;b=(k+1)*math.pi/5-.025
    section=[(.46*math.cos(a),.30+.52*math.sin(a)),(.46*math.cos(b),.30+.52*math.sin(b)),(.285*math.cos(b),.30+.35*math.sin(b)),(.285*math.cos(a),.30+.35*math.sin(a))]
    verts=[(x,y,z) for y in [-.17,.17] for x,z in section]
    faces=[(0,1,2,3),(7,6,5,4),(4,5,1,0),(5,6,2,1),(6,7,3,2),(7,4,0,3)]
    stone=mesh_obj('ArchVoussoir',verts,faces,0);bpy.context.view_layer.objects.active=stone
    mod=stone.modifiers.new('Arch edge wear','BEVEL');mod.width=.012;mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name);parts.append(stone)
    for sign in [-1,1]:
        front=[(.465*math.cos(a),sign*.184,.30+.525*math.sin(a)),(.465*math.cos(b),sign*.184,.30+.525*math.sin(b)),(.412*math.cos(b),sign*.184,.30+.465*math.sin(b)),(.412*math.cos(a),sign*.184,.30+.465*math.sin(a))]
        parts.append(mesh_obj('TerracottaArchBorder',front,[(0,1,2,3) if sign<0 else (3,2,1,0)],5))
o=join('SS_BSA_DoorFrame',parts);modules[o.name]=o
profile=[(-.255,0),(.255,0),(.255,.22),(.235,.31),(.18,.39),(.10,.445),(0,.46),(-.10,.445),(-.18,.39),(-.235,.31),(-.255,.22)]
n=len(profile);v=[(x*1.08,y,z*1.43) for y in [-.10,.10] for x,z in profile]
f=[tuple(range(n)),tuple(reversed(range(n,n*2)))]+[(i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n)]
panel=mesh_obj('ArchedTealPanel',v,f,4)
o=join('SS_BSA_DoorPanel',[panel,box('BronzeSill',(.55,.15,.035),(0,-.04,.0175),3,.008)]);modules[o.name]=o

# Isolate the library in another view layer for the preview; meshes remain exportable.
def instance(name,pos=(0,0,0),scale=(1,1,1),yaw=0):
    ob=modules[name].copy();ob.data=modules[name].data;preview.objects.link(ob)
    ob.location=pos;ob.scale=scale;ob.rotation_euler.z=yaw;return ob
def board(w=8,h=6):
    sy=h//2;mudx=min(6,w-2);special_z=(h-1)/2-sy;gate_z=(h-1)/2-max(0,sy-1)
    for y in range(h):
        for x in range(w):
            if (x,y) not in [(2,h-1-sy),(3,h-1-sy),(mudx,h-2-sy)]:instance('SS_BSA_Floor_'+str((x+y*7)%3),(x-(w-1)/2,y-(h-1)/2,0),yaw=((x+y)%4)*math.pi/2)
    for sign in [-1,1]:
        for x in range(w):instance('SS_BSA_Kerb',(x-(w-1)/2,sign*(h/2+.22),0),yaw=0 if sign<0 else math.pi)
        for y in range(h):instance('SS_BSA_Kerb',(sign*(w/2+.22),y-(h-1)/2,0),yaw=sign*math.pi/2)
        for x in [-1,1]:instance('SS_BSA_Corner',(x*(w/2+.22),sign*(h/2+.22),0))
    instance('SS_BSA_Medallion',(0,-h/2-.46,.12),scale=(1.6,1,1.6))
    for side in range(4):
        length=w+2.2 if side<2 else h+2.2
        count=math.ceil(length/1.65)
        for i in range(count):
            a=(i-(count-1)/2)*length/count
            p=(a,(-1 if side==0 else 1)*(h/2+.72),0) if side<2 else ((-1 if side==2 else 1)*(w/2+.72),a,0)
            instance('SS_BSA_Crust_'+str((i+side)%3),p,scale=(length/count*1.20,1.4+(i%3)*.10,.83+(i%3)*.12),yaw=(i%3-1)*.20+(math.pi/2 if side>1 else 0))
    for sign in [-1,1]:
        instance('SS_BSA_Menhir_'+str(int(sign==1)),(sign*(w/2+1.4),1.2,-.16))
        instance('SS_BSA_Chest',(sign*(w/2+1.4),-1.2,-.16),yaw=sign*.16)
    instance('SS_BSA_BackGate',(0,h/2+1.4,-.16))
    for x in [-2,-1,1,2]:instance('SS_BSA_Wall',(x*1.05,h/2+1.4,-.16))
    for sign in [-1,1]:
        for k in [-1,0,1]:
            instance('SS_BSA_Rubble',(sign*(w/2+1.6),k*h*.36,-.02),(2,1.7,2.4),yaw=k*sign*.51)
            instance('SS_BSA_Fragment',(sign*(w/2+1.5),k*h*.36+.25,.10),(1.15,1,1),yaw=k*.7)
    for i in range(5):
        x=(i-2.1)*w/5
        instance('SS_BSA_Fragment',(x,-h/2-1.5,-.03),(1,1,1),yaw=i*.71)
        instance('SS_BSA_Rubble',(x+.15,-h/2-1.52,.06),(1.5,1.1,1.8),yaw=i*.9)
        if globals().get('BSA_STAGE','')!='forms':instance('SS_BSA_MossPatch',(x-.16,-h/2-1.43,.06),(.65,.65,1),yaw=i*.5)
    if globals().get('BSA_STAGE','')!='forms':
        for x in range(w-1):
            for y in range(h-1):
                if random.Random(173+x*17+y*53).random()<.42:
                    instance('SS_BSA_MossPatch',(x-(w-2)/2,y-(h-2)/2,-.018),(.26,.26,.55),yaw=x+y)
        for side in [-1,1]:
            for i in range(w):
                instance('SS_BSA_MossPatch',(i-(w-1)/2,side*(h/2+.48),-.105),(.9,.40,.65),yaw=i*.5)
    # Same gimmick examples used by the Unity review player.
    instance('SS_BSA_Block',(-(w-1)/2,(h-1)/2,0))
    gatex=2-(w-1)/2
    instance('SS_BSA_DoorFrame',(gatex,gate_z,0));instance('SS_BSA_DoorPanel',(gatex,gate_z,0))
    instance('SS_BSA_Pit',(gatex,special_z,0));instance('SS_BSA_Cavity',(gatex,special_z,0))
    instance('SS_BSA_Ice',(3-(w-1)/2,special_z,0));instance('SS_BSA_Mud',(mudx-(w-1)/2,special_z-1,0))
    text=bpy.data.curves.new('BSA_Number','FONT');text.body='3';text.align_x='CENTER';text.align_y='CENTER';text.size=.55;text.extrude=.002
    ob=bpy.data.objects.new('Preview_Number_3',text);preview.objects.link(ob);ob.location=(gatex,gate_z-.19,.43);ob.rotation_euler.x=math.pi/2
    number=bpy.data.materials.get('BSA_Number_Ink') or bpy.data.materials.new('BSA_Number_Ink');number.diffuse_color=(1,.88,.65,1);text.materials.append(number)
board(*PREVIEW_SIZE)
for o in library.objects:o.hide_render=True;o.hide_set(True)

def setup_preview():
    world=bpy.data.worlds.new('BSA_Space');scene.world=world;world.color=(.012,.018,.035)
    distance=30.0
    bpy.ops.object.camera_add(location=(0,-distance*math.cos(math.radians(50)),distance*math.sin(math.radians(50))-.75));cam=bpy.context.object;cam.name='BSA_Camera_Pitch50'
    for c in list(cam.users_collection):c.objects.unlink(cam)
    studio.objects.link(cam);cam.rotation_euler=(math.radians(40),0,0);cam.data.type='ORTHO'
    w,h=PREVIEW_SIZE;cam.data.ortho_scale=max(4.65,(w+3.86)/(2*(1280/720)*.75),h*.383+2.18)*2*1280/720;scene.camera=cam
    for name,pos,power,size in [('Fill',(5,2,7),220,8)]:
        bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name='BSA_'+name;o.data.energy=power;o.data.shape='DISK';o.data.size=size
        for c in list(o.users_collection):c.objects.unlink(o)
        studio.objects.link(o)
        o.rotation_euler=(Vector((0,0,-.4))-o.location).to_track_quat('-Z','Y').to_euler()
    light=bpy.data.lights.new('BSA_Directional','SUN');light.energy=3.2;light.color=(1,.91,.80);light.use_shadow=True;light.angle=math.radians(3)
    ob=bpy.data.objects.new('BSA_Directional',light);studio.objects.link(ob);ob.rotation_euler=Vector((.627,.439,-.643)).to_track_quat('-Z','Y').to_euler()
    world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.015,.025,.06,1);world.node_tree.nodes['Background'].inputs[1].default_value=.3
    scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
setup_preview()

def bake_mesh_ao_and_shadow_profiles():
    """Blender BVH ray bake. Self AO -> vertex colour; casts+contact -> atlas alpha.

    The bake uses the exported Unity axes (x,z,-y), 40 degree upper-left sun,
    four penumbra rays and eight short contact rays. RGB still contains relief AO.
    No runtime ray tracing, shadow map, third sheet or extra material is needed.
    """
    import numpy as np
    from mathutils.bvhtree import BVHTree
    incoming=Vector((-.627,.643,.439)).normalized()
    profiles=['Kerb','Menhir_0','Chest','Wall','BackGate','Rubble','Kerb','DoorFrame']
    result=np.zeros((512,1024),dtype=np.float32)
    def bvh(ob,unity=False,rotate=False):
        vv=[Vector((v.co.x,v.co.z,-v.co.y)) if unity else v.co.copy() for v in ob.data.vertices]
        if rotate:vv=[Vector((v.z,v.y,-v.x)) for v in vv]
        return BVHTree.FromPolygons(vv,[list(p.vertices) for p in ob.data.polygons],all_triangles=False)
    for ob in modules.values():
        tree=bvh(ob);attr=ob.data.color_attributes['Color']
        for poly in ob.data.polygons:
            n=poly.normal;u=n.cross(Vector((.123,.697,.327))).normalized();v=n.cross(u)
            for li in poly.loop_indices:
                co=ob.data.vertices[ob.data.loops[li].vertex_index].co+n*.006
                blocked=0
                for k in range(12):
                    phi=k*2.399963;z=(k+.5)/12;r=math.sqrt(1-z*z)
                    ray=u*(r*math.cos(phi))+v*(r*math.sin(phi))+n*z
                    if tree.ray_cast(co,ray,.32)[0] is not None:blocked+=1
                c=attr.data[li].color;factor=1-.48*blocked/12
                attr.data[li].color=(c[0]*factor,c[1]*factor,c[2]*factor,c[3])
    for slot,name in enumerate(profiles):
        tree=bvh(modules['SS_BSA_'+name],True,slot==6)
        for iy in range(256):
            for ix in range(256):
                p=Vector(((ix+.5)/256*4-2,.003,(iy+.5)/256*4-2));cast=0;contact=0
                for k in range(4):
                    delta=Vector(((k%2-.5)*.025,0,(k//2-.5)*.025))
                    if tree.ray_cast(p,(incoming+delta).normalized(),6)[0] is not None:cast+=1
                for k in range(8):
                    phi=k*2.399963;z=(k+.5)/8;r=math.sqrt(1-z*z)
                    if tree.ray_cast(p,Vector((r*math.cos(phi),z,r*math.sin(phi))),.30)[0] is not None:contact+=1
                result[slot//4*256+iy,slot%4*256+ix]=min(1,cast/4*.82+contact/8*.42)
    (REVIEW/'stone-pass/after').mkdir(parents=True,exist_ok=True)
    image=bpy.data.images.new('BSA_Shadow_Bake_Proof',1024,512,alpha=True)
    proof=np.ones((512,1024,4),dtype=np.float32);proof[:,:,:3]=1-result[:,:,None];image.pixels.foreach_set(proof.ravel())
    image.filepath_raw=str(REVIEW/'stone-pass/after/blender-shadow-bake-proof.png');image.file_format='PNG';image.save()
    return result

# The geology bake shares this owned scene/library namespace.
exec(compile(open('C:/Users/soso1/orca/workspaces/ugajm/art/design/art/blender/stone_atlas_bake.py',encoding='utf-8').read(),'stone_atlas_bake.py','exec'))

if BAKE:bake_atlas()
else:
    # Temporary one-sheet swatches allow a clear material blockout before final bake.
    import numpy as np
    pix=np.ones((1024,1024,4),dtype=np.float32)
    for i,c in enumerate(COLORS):pix[i//4*256:(i//4+1)*256,i%4*256:(i%4+1)*256,:3]=c
    im=bpy.data.images.new('BSA_BlockoutColors',1024,1024);im.pixels.foreach_set(pix.ravel())
    nodes=material.node_tree.nodes;bs=nodes.get('Principled BSDF');tex=nodes.new('ShaderNodeTexImage');tex.image=im;material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.76

report={}
for name,ob in modules.items():
    ob.hide_set(False);ob.hide_render=False
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    ob.data.calc_loop_triangles();report[name]={'tris':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices)}
    if BAKE:
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
    ob.hide_set(True);ob.hide_render=True
preview_tris=sum(len(o.data.loop_triangles) for o in preview.objects if o.type=='MESH')
report['preview_8x6_tris']=preview_tris;report['bake']=BAKE;report['atlas_count']=2 if BAKE else 0
(REVIEW_TARGET/'blender-kit-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
scene.render.filepath=str(REVIEW_TARGET/('blender-final.png' if BAKE else 'blender-blockout.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.render.render(write_still=True)
result={'source':str(SOURCE),'render':scene.render.filepath,'report':report}

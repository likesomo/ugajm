"""R2 A production kit. Execute via Blender MCP: exec(compile(open(__file__).read(), __file__, 'exec')).

Only the owned BoardShapeA scene is replaced. Original startup scene is preserved.
Set BSA_BAKE=False in MCP globals for a quick blockout; default final build bakes
real high resolution relief to tangent normals and AO, exports FBX and saves .blend.
No downloaded assets. All lengths are metres; Z=0 is the playable surface.
"""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector

ROOT = Path('C:/unityjam/shared-assets/seven-steps')
OUT = ROOT / 'Assets/Art/RuinsHenge/BoardShapeA'
SOURCE = ROOT / 'Source/Blender/SS_BoardShapeA.blend'
REVIEW = ROOT / 'concepts/themes/board-shape-2026-11-10-r2/unity'
for p in (OUT, SOURCE.parent, REVIEW): p.mkdir(parents=True, exist_ok=True)
BAKE = globals().get('BSA_BAKE', True)
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

# Eight 256px shared swatches: granite, rock, timber, bronze, teal,
# terracotta, moss and frost. Only TWO delivered texture sheets.
COLORS = [(0.70,.68,.65),(.58,.59,.60),(.34,.20,.105),(.60,.38,.15),(.19,.44,.42),(.65,.30,.17),(.22,.30,.12),(.66,.81,.88)]
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
            u = .025 + .95 * ((a-amin)/max(.001,amax-amin))
            w = .025 + .95 * ((b-bmin)/max(.001,bmax-bmin))
            uv.data[li].uv=((swatch%4+u)/4, (swatch//4+w)/2)
    mesh.materials.append(material)
    attr = mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    for c in attr.data: c.color=(1,1,1,1)
    style=mesh.uv_layers.new(name='Style')
    for item in style.data:item.uv=(4,0)
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
    for scale,z in [(1,-.14),(1,-.038),(.91,0)]:verts.extend([(x*scale,y*scale,z) for x,y in ring])
    faces=[tuple(reversed(range(n))),tuple(range(n*2,n*3))]
    for j in range(2):
        for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
    ob=mesh_obj('SS_BSA_Floor_'+str(k),verts,faces,0)
    # Explicit bevel/edge color is a curvature mask without an extra material or sample.
    colors=ob.data.color_attributes['Color']
    for face in ob.data.polygons:
        c=(.98,.98,.98,1) if face.index==1 or face.index>=n+2 else (.48,.40,.27,1)
        for li in face.loop_indices:colors.data[li].color=c
    # Moss occupies the recessed outer seam only, never the central label area.
    parts=[ob]
    for i in range(2):
        x=(-1 if i==0 else 1)*.493;y=.21-k*.17
        parts.append(mesh_obj('SeamMoss',[(x-.006,y-.13,-.012),(x+.006,y-.13,-.012),(x+.006,y+.13,-.012),(x-.006,y+.13,-.012)],[(0,1,2,3)],6))
    return join('SS_BSA_Floor_'+str(k),parts)
def kerb():
    parts=[box('KerbStone',(1,.44,.52),(0,0,.11),0,.045)]
    parts.append(mesh_obj('InlaidTeal',[(-.47,-.226,-.01),(.47,-.226,-.01),(.47,-.226,.24),(-.47,-.226,.24)],[(0,1,2,3)],4))
    parts.append(mesh_obj('TopTeal',[(-.47,-.14,.373),(.47,-.14,.373),(.47,.14,.373),(-.47,.14,.373)],[(0,1,2,3)],4))
    # Broad alternating wedges on the front face, actual geometry at low cost.
    for x in [-.32,.20]:
        v=[(x-.17,-.231,-.005),(x,-.231,.235),(x+.17,-.231,-.005),(x+.09,-.231,-.005),(x,-.231,.145),(x-.09,-.231,-.005)]
        parts.append(mesh_obj('TerracottaChevron',v,[(5,4,1,0),(4,3,2,1)],5))
        v=[(x-.17,-.13,.381),(x,.13,.381),(x+.17,-.13,.381),(x+.09,-.13,.381),(x,.035,.381),(x-.09,-.13,.381)]
        parts.append(mesh_obj('TopChevron',v,[(5,4,1,0),(4,3,2,1)],5))
    return join('SS_BSA_Kerb',parts)
def rock(k):
    rng=random.Random(701+k);n=12;v=[]
    # Convex top ledge, broken strata and taper. Long axis is local X.
    perimeter=[(-.52,-.5),(-.15,-.57),(.18,-.53),(.49,-.46),(.56,-.12),(.52,.20),(.42,.52),(.12,.56),(-.16,.51),(-.49,.40),(-.58,.10),(-.53,-.19)]
    for r,(s,z) in enumerate([(1,-.16),(1.06,-.44),(.95,-1.02),(.78,-1.89),(.44,-3.0)]):
        for i,(x,y) in enumerate(perimeter):v.append((x*s+rng.uniform(-.095,.095),y*s+rng.uniform(-.09,.09),z+rng.uniform(-.13,.05)))
    v.append((.04,-.03,-.035))
    f=[(60,i,(i+1)%n) for i in range(n)]+[tuple(reversed(range(48,60)))]
    for r in range(4):
        for i in range(n):
            a=r*n+i;b=r*n+(i+1)%n;c=b+n;d=a+n
            f.extend([(a,d,b),(b,d,c)])
    ob=mesh_obj('SS_BSA_Crust_'+str(k),v,f,1)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    mod=ob.modifiers.new('Chipped broad edges','BEVEL');mod.width=.035;mod.segments=1;mod.limit_method='ANGLE';mod.angle_limit=.80
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
    for z,s in [(0,1),(.80,.84),(1.65,.45)]:
        for i in range(5):
            a=2*math.pi*i/5;v.append((math.cos(a)*.30*s,math.sin(a)*.26*s,z+rng.uniform(0,.1)))
    f=[tuple(reversed(range(5))),tuple(range(10,15))]
    for j in range(2):
        for i in range(5):f.append((j*5+i,j*5+(i+1)%5,(j+1)*5+(i+1)%5,(j+1)*5+i))
    return mesh_obj('SS_BSA_Menhir_'+str(k),v,f,0)
modules={}
for k in range(3):
    for o in [tile(k),rock(k)]:modules[o.name]=o
for k in range(2):
    o=menhir(k);modules[o.name]=o
o=kerb();modules[o.name]=o
o=join('SS_BSA_Corner',[box('CornerBase',(.45,.45,.59),(0,0,.145),0,.035),box('CornerCap',(.51,.51,.12),(0,0,.49),0,.035),box('Inlay',(.39,.018,.10),(0,-.235,.15),4,.005)]);modules[o.name]=o
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
o=join('SS_BSA_Block',[box('GraniteBlock',(.90,.90,.34),(0,0,.17),0,.035),box('Cap',(.94,.94,.085),(0,0,.3825),0,.025)]);modules[o.name]=o
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
parts=[box('Pier',(.18,.26,.32),(-.36,0,.16),0,.022),box('Pier',(.18,.26,.32),(.36,0,.16),0,.022)]
for k in range(5):
    a=k*math.pi/5+.025;b=(k+1)*math.pi/5-.025
    section=[(.46*math.cos(a),.22+.36*math.sin(a)),(.46*math.cos(b),.22+.36*math.sin(b)),(.27*math.cos(b),.22+.24*math.sin(b)),(.27*math.cos(a),.22+.24*math.sin(a))]
    verts=[(x,y,z) for y in [-.13,.13] for x,z in section]
    faces=[(0,1,2,3),(7,6,5,4),(4,5,1,0),(5,6,2,1),(6,7,3,2),(7,4,0,3)]
    stone=mesh_obj('ArchVoussoir',verts,faces,0);bpy.context.view_layer.objects.active=stone
    mod=stone.modifiers.new('Arch edge wear','BEVEL');mod.width=.012;mod.segments=1
    bpy.ops.object.modifier_apply(modifier=mod.name);parts.append(stone)
    front=[(.465*math.cos(a),-.144,.22+.365*math.sin(a)),(.465*math.cos(b),-.144,.22+.365*math.sin(b)),(.412*math.cos(b),-.144,.22+.312*math.sin(b)),(.412*math.cos(a),-.144,.22+.312*math.sin(a))]
    parts.append(mesh_obj('TerracottaArchBorder',front,[(0,1,2,3)],5))
o=join('SS_BSA_DoorFrame',parts);modules[o.name]=o
profile=[(-.255,0),(.255,0),(.255,.22),(.235,.31),(.18,.39),(.10,.445),(0,.46),(-.10,.445),(-.18,.39),(-.235,.31),(-.255,.22)]
n=len(profile);v=[(x,y,z) for y in [-.07,.045] for x,z in profile]
f=[tuple(range(n)),tuple(reversed(range(n,n*2)))]+[(i,i+n,(i+1)%n+n,(i+1)%n) for i in range(n)]
panel=mesh_obj('ArchedTealPanel',v,f,4)
o=join('SS_BSA_DoorPanel',[panel,box('BronzeSill',(.55,.15,.035),(0,-.04,.0175),3,.008)]);modules[o.name]=o

# Isolate the library in another view layer for the preview; meshes remain exportable.
def instance(name,pos=(0,0,0),scale=(1,1,1),yaw=0):
    ob=modules[name].copy();ob.data=modules[name].data;preview.objects.link(ob)
    ob.location=pos;ob.scale=scale;ob.rotation_euler.z=yaw;return ob
def board(w=8,h=6):
    for y in range(h):
        for x in range(w):instance('SS_BSA_Floor_'+str((x+y*7)%3),(x-(w-1)/2,y-(h-1)/2,0),yaw=((x+y)%4)*math.pi/2)
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
board()
for o in library.objects:o.hide_render=True;o.hide_set(True)

def setup_preview():
    world=bpy.data.worlds.new('BSA_Space');scene.world=world;world.color=(.012,.018,.035)
    distance=30.0
    bpy.ops.object.camera_add(location=(0,-distance*math.cos(math.radians(50)),distance*math.sin(math.radians(50))-.6));cam=bpy.context.object;cam.name='BSA_Camera_Pitch50'
    for c in list(cam.users_collection):c.objects.unlink(cam)
    studio.objects.link(cam);cam.rotation_euler=(math.radians(40),0,0);cam.data.type='ORTHO';cam.data.ortho_scale=15.3;scene.camera=cam
    for name,pos,power,size in [('Key',(-5,-6,10),1700,7),('Fill',(5,2,7),900,8)]:
        bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.name='BSA_'+name;o.data.energy=power;o.data.shape='DISK';o.data.size=size
        for c in list(o.users_collection):c.objects.unlink(o)
        studio.objects.link(o)
        o.rotation_euler=(Vector((0,0,-.4))-o.location).to_track_quat('-Z','Y').to_euler()
    light=bpy.data.lights.new('BSA_Directional','SUN');light.energy=2;light.use_shadow=False
    ob=bpy.data.objects.new('BSA_Directional',light);studio.objects.link(ob);ob.rotation_euler=(math.radians(55),math.radians(-25),0)
    world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.015,.025,.06,1);world.node_tree.nodes['Background'].inputs[1].default_value=.3
    scene.render.engine='CYCLES';scene.cycles.samples=16
    scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
setup_preview()

def bake_atlas():
    import numpy as np
    n=128;W,H=1024,512
    for o in preview.objects:o.hide_render=True
    normal=bpy.data.images.new('BSA_Normal_Roughness',width=W,height=H,alpha=True);normal.colorspace_settings.name='Non-Color'
    ao=bpy.data.images.new('BSA_BakedAO_Temporary',width=W,height=H,alpha=True);ao.colorspace_settings.name='Non-Color'
    material.node_tree.nodes.clear()
    bs=material.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=material.node_tree.nodes.new('ShaderNodeOutputMaterial');material.node_tree.links.new(bs.outputs['BSDF'],out.inputs[0])
    target=material.node_tree.nodes.new('ShaderNodeTexImage');target.image=normal;material.node_tree.nodes.active=target
    # Relief is actual high-poly geometry: grain, shallow chipped border, recessed trim.
    lows=[];highs=[]
    for swatch in range(8):
        xoff=(swatch%4)*1.2;yoff=(swatch//4)*1.2
        low=mesh_obj('BAKE_Low_'+str(swatch),[(xoff,yoff,0),(xoff+1,yoff,0),(xoff+1,yoff+1,0),(xoff,yoff+1,0)],[(0,1,2,3)],swatch)
        uv=low.data.uv_layers.active
        for li,co in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[li].uv=((swatch%4+co[0])/4,(swatch//4+co[1])/2)
        verts=[];faces=[];rng=random.Random(912+swatch)
        from mathutils import noise
        for iy in range(n+1):
            for ix in range(n+1):
                u=ix/n;v=iy/n
                grain=rng.uniform(-1,1)
                z=.0011*grain+.0010*noise.noise(Vector((u*17,v*17,swatch*3)))
                if swatch==1:
                    # Long vertical fissures and stone strata, not repeated diagonal hatching.
                    crack=min(abs(u-(.24+.035*math.sin(v*11))),abs(u-(.73+.045*math.sin(v*8+2))))
                    z-=max(0,.013-crack)*.9
                    z+=.002*math.sin(v*35+math.sin(u*18))
                if swatch==2:z=.004*math.sin(v*85+math.sin(u*22)*1.4)
                if swatch in (4,5):
                    # Recessed V chevrons share one strip; lower trim belongs at tile edges only.
                    line=abs((u*3)%1-.5)*1.5
                    z=-.016 if abs(v-line-.12)<.025 else z*.4
                if swatch in (0,1):
                    border=min(u,v,1-u,1-v)
                    z-=max(0,.045-border)*.12*(.5+.5*math.sin(u*93+v*117)**2)
                verts.append((xoff+u,yoff+v,z))
        for iy in range(n):
            for ix in range(n):
                a=iy*(n+1)+ix;faces.append((a,a+1,a+n+2,a+n+1))
        high=mesh_obj('BAKE_HighRelief_'+str(swatch),verts,faces,swatch)
        lows.append(low);highs.append(high)
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.05;scene.render.bake.max_ray_distance=.12;scene.render.bake.margin=4;scene.render.bake.use_clear=False
    for image,kind in [(normal,'NORMAL'),(ao,'AO')]:
        target.image=image
        for low,high in zip(lows,highs):
            bpy.ops.object.select_all(action='DESELECT');low.hide_set(False);high.hide_set(False);low.hide_render=False;high.hide_render=False
            low.select_set(True);high.select_set(True);bpy.context.view_layer.objects.active=low
            bpy.ops.object.bake(type=kind)
    a=np.array(ao.pixels[:],dtype=np.float32).reshape(H,W,4)[:,:,0]
    pixels=np.ones((H,W,4),dtype=np.float32)
    for swatch,c in enumerate(COLORS):
        yy,xx=np.mgrid[0:256,0:256];rng=np.random.default_rng(410+swatch)
        noise=rng.random((256,256));grain=(noise-.5)*.055
        def cloudy(res):
            coarse=rng.random((res+1,res+1));axis=np.linspace(0,res-.0001,256);cell=axis.astype(int);t=axis-cell;t=t*t*(3-2*t)
            a=coarse[cell[:,None],cell[None,:]];b=coarse[cell[:,None],cell[None,:]+1]
            c0=coarse[cell[:,None]+1,cell[None,:]];d=coarse[cell[:,None]+1,cell[None,:]+1]
            top=a*(1-t[None,:])+b*t[None,:];bottom=c0*(1-t[None,:])+d*t[None,:]
            return top*(1-t[:,None])+bottom*t[:,None]
        cloud=(cloudy(6)-.5)*.15+(cloudy(27)-.5)*.07
        shade=.96+grain+cloud
        speck=(noise>.975)*.12-(noise<.025)*.14
        if swatch in (0,1):
            edge=np.minimum.reduce([xx,yy,255-xx,255-yy]);shade+=speck
            shade+=np.clip((11-edge)/11,0,1)*.14
            if swatch==1:
                u=xx/255;v=yy/255;crack=np.minimum(np.abs(u-(.24+.035*np.sin(v*11))),np.abs(u-(.73+.045*np.sin(v*8+2))))
                shade-=np.clip((.005-crack)/.005,0,1)*.13
        if swatch==2:shade=.86+.18*np.sin(yy*.23+np.sin(xx*.025)*2)**2+grain
        for channel in range(3):pixels[swatch//4*256:(swatch//4+1)*256,swatch%4*256:(swatch%4+1)*256,channel]=np.clip(c[channel]*shade,0,1)
    pixels[:,:,:3]*=(.62+.38*a[:,:,None]);pixels[:,:,3]=a
    color=bpy.data.images.new('BSA_Color_AO',width=W,height=H,alpha=True);color.colorspace_settings.name='sRGB';color.pixels.foreach_set(pixels.ravel())
    color.filepath_raw=str(OUT/'BSA_ColorAO.png');color.file_format='PNG';color.save()
    normals=np.array(normal.pixels[:],dtype=np.float32).reshape(H,W,4);normals[:,:,3]=.76
    normal.pixels.foreach_set(normals.ravel());normal.filepath_raw=str(OUT/'BSA_NormalRough.png');normal.file_format='PNG';normal.save()
    for low,high in zip(lows,highs):bpy.data.objects.remove(low,do_unlink=True);bpy.data.objects.remove(high,do_unlink=True)
    for o in preview.objects:o.hide_render=False
    # Keep the high-poly relief source reproducible in this script; bake intermediates are not exports.
    nodes=material.node_tree.nodes;links=material.node_tree.links
    tex=nodes.new('ShaderNodeTexImage');tex.image=color;links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.76
    target.image=normal;nm=nodes.new('ShaderNodeNormalMap');links.new(target.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
    bpy.data.images.remove(ao)

if BAKE:bake_atlas()
else:
    # Temporary one-sheet swatches allow a clear material blockout before final bake.
    import numpy as np
    pix=np.ones((512,1024,4),dtype=np.float32)
    for i,c in enumerate(COLORS):pix[i//4*256:(i//4+1)*256,i%4*256:(i%4+1)*256,:3]=c
    im=bpy.data.images.new('BSA_BlockoutColors',1024,512);im.pixels.foreach_set(pix.ravel())
    nodes=material.node_tree.nodes;bs=nodes.get('Principled BSDF');tex=nodes.new('ShaderNodeTexImage');tex.image=im;material.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.76

report={}
for name,ob in modules.items():
    ob.hide_set(False);ob.hide_render=False
    bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
    ob.data.calc_loop_triangles();report[name]={'tris':len(ob.data.loop_triangles),'vertices':len(ob.data.vertices)}
    if BAKE:
        bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
    ob.hide_set(True);ob.hide_render=True
preview_tris=sum(len(o.data.loop_triangles) for o in preview.objects)
report['preview_8x6_tris']=preview_tris;report['bake']=BAKE;report['atlas_count']=2 if BAKE else 0
(REVIEW/'blender-kit-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
scene.render.filepath=str(REVIEW/('blender-final.png' if BAKE else 'blender-blockout.png'))
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
bpy.ops.render.render(write_still=True)
result={'source':str(SOURCE),'render':scene.render.filepath,'report':report}

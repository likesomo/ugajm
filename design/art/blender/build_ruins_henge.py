"""Chapter 01 production source: 23 reusable modules, atlas, vertex moss, FBX.

Blender 5.2.3: -b --python-exit-code 1 -P build_ruins_henge.py -- --out <shared Assets/Art/RuinsHenge> --renders <shared concepts/themes/ruins-henge/blender>
No third-party downloads. No Unity project writes. All labels are preview-only.
"""
import argparse
import json
import math
import random
import sys
from pathlib import Path

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--out', required=True)
parser.add_argument('--renders', required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
out = Path(args.out).resolve()
renders = Path(args.renders).resolve()
asset_root = Path('C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge').resolve()
concept_root = Path('C:/unityjam/shared-assets/seven-steps/concepts/themes/ruins-henge').resolve()
source_root = Path('C:/unityjam/shared-assets/seven-steps/Source/Blender').resolve()
source_root.mkdir(parents=True, exist_ok=True)
report_root = source_root/'RuinsHengeReports'
report_root.mkdir(parents=True, exist_ok=True)
assert out == asset_root or asset_root in out.parents
assert renders == concept_root or concept_root in renders.parents
for p in (out / 'Models', out / 'Textures', renders):
    p.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
try:
    scene.render.engine = 'BLENDER_EEVEE'
except TypeError:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.render.resolution_x = 1600
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast'
world = bpy.data.worlds.new('RH_World')
scene.world = world
world.use_nodes = True
world.node_tree.nodes['Background'].inputs[0].default_value = (.40, .63, .80, 1)
world.node_tree.nodes['Background'].inputs[1].default_value = .35


def coll(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c


library = coll('01_EXPORT_LIBRARY')
kit = coll('02_KIT_PREVIEW')
board = coll('03_BOARD_PREVIEW')
kit_labels = coll('90_KIT_LABELS_NO_EXPORT')
board_labels = coll('91_BOARD_LABELS_NO_EXPORT')
studio = coll('99_STUDIO_NO_EXPORT')
library.hide_render = True
board.hide_render = True
board_labels.hide_render = True
current = library


def lin(v):
    return v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4


def color(h):
    return tuple(lin(int(h[i:i+2], 16) / 255) for i in (0, 2, 4))


palette = ['DEDAC8', 'A7AD98', '6F8B4E', '846147', '9C7560', 'A4BDC1', '344C4B', 'B29768']
atlas = bpy.data.images.new('RH_Atlas512', width=512, height=512, alpha=False)
pixels = []
for y in range(512):
    for x in range(512):
        c = color(palette[x // 64])
        band = x // 64
        grain = math.sin(x*12.9898+y*78.233)*43758.5453 % 1
        shade = .91 + .07 * y / 511 + .055*(grain-.5)
        if band == 3:
            shade += .08*math.sin(y*.23+math.sin(x*.18)*2)+.025*math.sin(y*1.9+x*.09)
        if y < 64:
            # Desaturated geometric painted timber trim, never writing/sacred marks.
            q = x % 64
            edge = y in range(6, 11) or y in range(52, 57)
            key = (q in range(8, 13) and y in range(14, 43)) or (q in range(8, 43) and y in range(14, 19))
            c = color('9C7560' if key else 'B29768' if edge else '344C4B')
        pixels.extend([*(v * shade for v in c), 1])
atlas.pixels = pixels
atlas.filepath_raw = str(out / 'Textures/RH_Atlas512.png')
atlas.file_format = 'PNG'
atlas.save()


def mat(name, rough=.8, metallic=0, tint=None, moss=False, unlit=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nodes, links = m.node_tree.nodes, m.node_tree.links
    bs = nodes['Principled BSDF']
    bs.inputs['Roughness'].default_value = rough
    bs.inputs['Metallic'].default_value = metallic
    image = nodes.new('ShaderNodeTexImage')
    image.image = atlas
    image.interpolation = 'Linear'
    source = image.outputs['Color']
    if tint:
        mul = nodes.new('ShaderNodeMixRGB')
        mul.blend_type = 'MULTIPLY'
        mul.inputs[0].default_value = 1
        mul.inputs[2].default_value = (*color(tint), 1)
        links.new(source, mul.inputs[1])
        source = mul.outputs[0]
    if moss:
        attribute = nodes.new('ShaderNodeVertexColor')
        attribute.layer_name = 'MossMask'
        separate = nodes.new('ShaderNodeSeparateColor')
        links.new(attribute.outputs['Color'], separate.inputs[0])
        noise = nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 9
        noise.inputs['Detail'].default_value = 1
        remap = nodes.new('ShaderNodeMapRange')
        remap.inputs['To Min'].default_value = .7
        remap.inputs['To Max'].default_value = 1
        links.new(noise.outputs['Fac'], remap.inputs['Value'])
        multiply = nodes.new('ShaderNodeMath')
        multiply.operation = 'MULTIPLY'
        links.new(separate.outputs['Red'], multiply.inputs[0])
        links.new(remap.outputs['Result'], multiply.inputs[1])
        mix = nodes.new('ShaderNodeMixRGB')
        links.new(multiply.outputs[0], mix.inputs[0])
        links.new(source, mix.inputs[1])
        mix.inputs[2].default_value = (*color('6F8B4E'), 1)
        source = mix.outputs[0]
    links.new(source, bs.inputs['Base Color'])
    if unlit:
        bs.inputs['Emission Color'].default_value = (*color('D5D9C8'), 1)
        bs.inputs['Emission Strength'].default_value = 1
    m.diffuse_color = (*color(tint or 'D5D9C8'), 1)
    return m


base = mat('SS_RH_M_BaseAtlas', moss=True)
player_mat = mat('SS_RH_M_PlayerAtlas', tint='C1A179')
menhir_mat = mat('SS_RH_M_MenhirAtlas', tint='BFC3AA')
metal = mat('SS_RH_M_MetalAtlas', .4, .45)
frost = mat('SS_RH_M_FrostOpaque', .4)
mark = mat('SS_RH_M_MarkUnlit', unlit=True)
assert len(bpy.data.materials) == 6


def link_to(obj):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    current.objects.link(obj)


def finish(obj, name, swatch=0, material=base, bevel=.018, moss_strength=0):
    obj.name = name
    link_to(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new('Bevel_1_segment', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mesh = obj.data
    mesh.materials.clear()
    mesh.materials.append(material)
    uv = mesh.uv_layers.active or mesh.uv_layers.new(name='UV0_Atlas')
    attribute = mesh.color_attributes.new(name='MossMask', type='FLOAT_COLOR', domain='CORNER')
    mesh.color_attributes.active_color_index = 0
    for loop in mesh.loops:
        p = mesh.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((swatch + .5 + max(-.43,min(.43,.85*p.x))) / 8, min(.94, max(.18, .50 + .40*p.y + .22*p.z)))
        distance = min(abs(.48-abs(p.x)),abs(.48-abs(p.y)))
        irregular = .065 + .025*math.sin(p.x*24+p.y*17)
        mask = max(0,min(1,(irregular-distance)/.035)) * moss_strength if p.z >= -.025 else 0
        attribute.data[loop.index].color = (mask, 0, 0, 1)
    obj.select_set(False)
    return obj


def box(name, size, center, swatch=0, material=base, bevel=.018, moss_strength=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o = bpy.context.object
    o.dimensions = size
    return finish(o, name, swatch, material, bevel, moss_strength)


def cone(name, r1, r2, depth, center, swatch=0, material=base):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=r1, radius2=r2, depth=depth, location=center)
    return finish(bpy.context.object, name, swatch, material, 0)


def mesh_obj(name, verts, faces, swatch=0, moss_strength=0):
    data = bpy.data.meshes.new(name + '_Mesh')
    data.from_pydata(verts, [], faces)
    data.update()
    o = bpy.data.objects.new(name, data)
    current.objects.link(o)
    return finish(o, name, swatch, base, .012, moss_strength)


def moss_floor():
    # A top grid supports a true vertex-color corner mask, not green floating blobs.
    n = 12
    verts = [(x / n * .96 - .48, y / n * .96 - .48, 0) for y in range(n+1) for x in range(n+1)]
    for i,(x,y,z) in enumerate(verts):
        if abs(x)>.479: x-=math.copysign(.006+.012*(.5+.5*math.sin(i*4)),x)
        if abs(y)>.479: y-=math.copysign(.006+.012*(.5+.5*math.cos(i*3)),y)
        verts[i]=(x,y,z)
    faces = []
    for y in range(n):
        for x in range(n):
            a = y * (n+1) + x
            faces.append((a, a+1, a+n+2, a+n+1))
    rim = list(range(n+1)) + [y*(n+1)+n for y in range(1,n+1)] + [n*(n+1)+x for x in range(n-1,-1,-1)] + [y*(n+1) for y in range(n-1,0,-1)]
    bottom = []
    for i in rim:
        bottom.append(len(verts))
        verts.append((verts[i][0], verts[i][1], -.12))
    for j in range(len(rim)):
        k = (j+1) % len(rim)
        faces.append((rim[j], bottom[j], bottom[k], rim[k]))
    faces.append(tuple(reversed(bottom)))
    return mesh_obj('MossFloorMesh', verts, faces, moss_strength=.95)


def root(name, parts):
    r = bpy.data.objects.new(name, None)
    library.objects.link(r)
    for o in parts:
        o.parent = r
    return r


modules = {}
def register(key, name, parts):
    modules[key] = root(name, parts)
    return modules[key]


register('Floor_Clean', 'SS_Tile_Floor_Clean', [box('CleanFloorMesh', (.96,.96,.12), (0,0,-.06))])
mf = moss_floor()
register('Floor_Moss', 'SS_Tile_Floor_Moss', [mf])
grass_base = mf.copy()
grass_base.data = mf.data
library.objects.link(grass_base)
grass_base.name = 'GrassEdgeFloorMesh'
blades_v, blades_f = [], []
for j, x in enumerate((-.36,-.22,-.08,.08,.22,.36)):
    for k in range(3):
        xx = x + (k-1)*.025
        yy = -.46 + (k%2)*.025
        h = .18 + .025*((j+k)%3)
        i = len(blades_v)
        blades_v += [(xx-.026,yy,0),(xx+.026,yy,0),(xx+.045*(k-1),yy+.025,h)]
        blades_f.append((i,i+1,i+2))
blades = mesh_obj('ShortGrassEdges', blades_v, blades_f, 2)
register('Floor_GrassEdge', 'SS_Tile_Floor_GrassEdge', [grass_base, blades])
for variant in range(3):
    pieces=[]
    for row in range(2):
        columns=3 if variant and row==variant-1 else 2
        width=.88/columns
        for column in range(columns):
            x=(column-(columns-1)*.5)*width
            pieces.append(box('StoneCourse',(width-.012,.44,.185),(x,0,.10+row*.195),0 if (row+column+variant)%3 else 1,bevel=.028))
    pieces.append(box('ChamferedCap',(.94,.49,.08),(0,0,.435),0,bevel=.032))
    key='StoneBlock' if variant==0 else 'StoneBlock_V'+str(variant+1)
    register(key,'SS_Block_Stone'+('' if variant==0 else '_V'+str(variant+1)),pieces)
wood_parts=[box('TimberPlank',(.88,.124,.34),(0,y,.17),3,bevel=.016) for y in (-.136,0,.136)]
wood_parts += [box('IronBand',(.07,.423,.35),(x,0,.175),7,metal,.008) for x in (-.30,.30)]
wood=wood_parts[0]
trim = box('PaintedEndTrim', (.035,.41,.10), (.448,0,.24), 6)
for loop in trim.data.loops:
    p = trim.data.vertices[loop.vertex_index].co
    trim.data.uv_layers.active.data[loop.index].uv = (.02+(p.y+.205)/.41*.96, .005+(p.z-.19)/.10*.115)
register('WoodBlock', 'SS_Block_Wood', wood_parts+[trim])
register('Wall', 'SS_Tile_Wall', [box('WallCourse',(.44,.88,.225),(x,0,z),0 if x<0 else 1,bevel=.025) for z in (.1125,.3525) for x in (-.23,.23)]+[box('WallCap',(.96,.94,.06),(0,0,.515),0,bevel=.028)])
# The same faceted, asymmetrically cut silhouette reads as cloak or standing stone.
verts=[]
for ring,(z,rx,ry,dx) in enumerate([(0,.22,.19,0),(.12,.24,.18,-.015),(.51,.17,.135,.01),(.68,.13,.11,-.025),(.84,.09,.075,-.045),(.89,.025,.035,-.025)]):
    for i in range(8):
        a=2*math.pi*i/8
        verts.append((math.cos(a)*rx+dx,math.sin(a)*ry,z))
faces=[tuple(reversed(range(8)))]
for ring in range(5):
    for i in range(8): faces.append((ring*8+i,ring*8+(i+1)%8,(ring+1)*8+(i+1)%8,(ring+1)*8+i))
faces.append(tuple(range(40,48)))
body_parts = [mesh_obj('CarvedCloakMenhir',verts,faces)]
bpy.ops.object.select_all(action='DESELECT')
for p in body_parts:
    p.select_set(True)
bpy.context.view_layer.objects.active = body_parts[0]
if len(body_parts)>1:
    bpy.ops.object.join()
body = bpy.context.object
body.name = 'RH_CloakSharedMesh'
body.select_set(False)
body.material_slots[0].link = 'OBJECT'
body.material_slots[0].material = player_mat
register('Player','SS_Actor_Player',[body])
menhir = body.copy()
menhir.data = body.data
library.objects.link(menhir)
menhir.name = 'RH_MenhirSharedMesh'
menhir.material_slots[0].link = 'OBJECT'
menhir.material_slots[0].material = menhir_mat
register('Menhir','SS_Actor_Menhir',[menhir])
register('Exit','SS_Tile_Exit',[box('ExitStone',(.76,.76,.045),(0,0,.0225),0),box('ExitInset',(.54,.54,.008),(0,0,.049),7,metal,.005)])
register('Border_Straight','SS_Border_Straight',[box('RimStraight',(1,.16,.18),(0,0,.03),1)]+[box('RimCap',(.48,.19,.065),(x,0,.135),0,bevel=.02) for x in (-.25,.25)])
register('Border_Corner','SS_Border_Corner',[box('RimCornerX',(.30,.16,.18),(.07,0,.03),1),box('RimCornerY',(.16,.30,.18),(0,.07,.03),1)])
for key in ('NumberDoor','CountDoor'):
    name = 'SS_Tile_'+key
    frame = [box(name+'__Post_L',(.17,.26,.50),(-.32,0,.25),1),box(name+'__Post_R',(.17,.26,.50),(.32,0,.25),1)]
    panel = box(name+'__GatePanel',(.52,.12,.28),(0,-.04,.28),3 if key=='NumberDoor' else 0)
    register(key,name,frame+[panel])
lever_base = box('SS_Tile_Lever__Base',(.50,.36,.08),(0,0,.04),1)
arm = box('SS_Tile_Lever__HandleMesh',(.28,.07,.07),(.12,0,.24),7,metal,.01)
grip = cone('SS_Tile_Lever__Grip',.055,.055,.08,(.24,0,.28),7,metal)
lever_root = register('Lever','SS_Tile_Lever',[lever_base])
pivot = bpy.data.objects.new('SS_Tile_Lever__HandlePivot',None)
library.objects.link(pivot)
pivot.parent = lever_root
pivot.location = (0,0,.09)
for o in (arm,grip):
    for v in o.data.vertices:
        v.co.z -= .09
    o.parent = pivot
register('FlipWall_Up','SS_Tile_FlipWall_Up',[box('FlipUpPanel',(.88,.24,.55),(0,0,.275),1)])
register('FlipWall_Down','SS_Tile_FlipWall_Down',[box('FlipDownPanel',(.88,.24,.05),(0,0,.025),1)])
register('Plate','SS_Tile_Plate',[box('PressureStone',(.70,.70,.04),(0,0,.02),7,metal)])
ice = [box('IceBase',(.96,.96,.12),(0,0,-.06),5,frost)]
for x in (-.25,0,.25):
    ice.append(box('FrostGroove',(.025,.68,.006),(x,0,-.003),0,frost,.002))
register('Ice','SS_Tile_Ice',ice)
pit = [box('PitSide',(.10,.96,.12),(x,0,-.06),1) for x in (-.43,.43)]
pit += [box('PitEnd',(.76,.10,.12),(0,y,-.06),1) for y in (-.43,.43)]
register('Pit','SS_Tile_PitRim',pit)
register('Bridge','SS_Tile_Bridge',[box('BridgeTimber',(.27,.96,.10),(x,0,-.05),3) for x in (-.30,0,.30)])
register('Cracked','SS_Tile_Cracked',[box('CrackedHalf',(.455,.96,.12),(x,0,-.06),0) for x in (-.2525,.2525)])
assert len(modules)==23
assert modules['Player'].children[0].data == modules['Menhir'].children[0].data


def instance(key,x,y,z=0,target=kit):
    source=modules[key]
    r=source.copy()
    target.objects.link(r)
    r.location=(x,y,z)
    copies={source:r}
    for original in source.children_recursive:
        o=original.copy()
        if original.data:
            o.data=original.data
        target.objects.link(o)
        o.parent=copies[original.parent]
        copies[original]=o
    return r


for key,x,y in [('Floor_Clean',-2,1),('Floor_Moss',0,1),('Floor_GrassEdge',2,1),('StoneBlock',-2,-1),('WoodBlock',0,-1),('Menhir',2,-1)]:
    instance(key,x,y)
current=studio
box('StudioGround',(200,200,.1),(0,0,-.37),2,base,0)
current=board
box('RuinsFoundation',(6.4,6.4,.18),(0,0,-.22),1)
for y in range(6):
    for x in range(6):
        xx,yy=x-2.5,y-2.5
        key='Pit' if (x,y)==(1,3) else 'Bridge' if (x,y)==(2,3) else 'Ice' if x==3 and y in (1,2,3) else 'Cracked' if (x,y)==(0,4) else 'Floor_GrassEdge' if y==0 else 'Floor_Moss' if (x+y)%3==0 else 'Floor_Clean'
        instance(key,xx,yy,target=board)
for key,x,y,z in [('StoneBlock',-2.5,2.5,0),('WoodBlock',-2.5,-1.5,0),('NumberDoor',1.5,1.5,0),('CountDoor',-1.5,1.5,0),('Lever',1.5,-1.5,0),('FlipWall_Up',2.5,-.5,0),('Plate',-.5,-.5,0),('Menhir',-.5,-.5,.04),('Player',1.5,-1.5,.08),('Exit',2.5,2.5,0),('Wall',2.5,1.5,0)]:
    instance(key,x,y,z,board)
for side in (-1,1):
    for j in range(6):
        instance('Border_Straight',j-2.5,side*3.08,target=board)
        r=instance('Border_Straight',side*3.08,j-2.5,target=board)
        r.rotation_euler.z=math.pi/2
for x in (-3.08,3.08):
    for y in (-3.08,3.08):
        instance('Border_Corner',x,y,target=board)

# Background geometry is outside the board and never included in module FBX.
random.seed(71)
for j in range(13):
    x=-8+j*1.3
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1,location=(x,7+random.random()*2,-.30))
    hill=bpy.context.object
    hill.scale=(2.8,2.5,.65+random.random()*.5)
    finish(hill,'BackgroundHill',5 if j%3==0 else 2,base,0)
for x,y in [(-4.5,4.4),(4.9,5.3),(-6,6.0)]:
    r=instance('Menhir',x,y,0,board)
    r.scale=(1.6,1.5,2.5)
for j in range(7):
    instance('StoneBlock'+('' if j%3==0 else '_V'+str(j%3+1)),j-3,3.5,target=board)
grass_vertices,grass_faces=[],[]
for j in range(200):
    side=-1 if j%2 else 1
    x=side*(3.28+random.random()*.65)
    y=-3.3+random.random()*7.4
    h=random.uniform(.12,.30)
    i=len(grass_vertices)
    grass_vertices.extend([(x-.035,y,-.23),(x+.035,y,-.23),(x+random.uniform(-.09,.09),y+.03,h-.23)])
    grass_faces.append((i,i+1,i+2))
mesh_obj('BackgroundGrassCards_NO_EXPORT',grass_vertices,grass_faces,2)


def light(name,position,power,size,tint):
    d=bpy.data.lights.new(name,'AREA')
    d.energy=power
    d.shape='DISK'
    d.size=size
    d.color=tint
    o=bpy.data.objects.new(name,d)
    studio.objects.link(o)
    o.location=position
    o.rotation_euler=(Vector((0,0,0))-o.location).to_track_quat('-Z','Y').to_euler()


light('MorningKey',(-5,-4,6),500,2.5,(1,.83,.58))
light('SkyFill',(4,3,6),700,7,(.67,.83,1))
sun_data=bpy.data.lights.new('MorningSun','SUN')
sun_data.energy=1.6
sun_data.angle=math.radians(8)
sun_data.color=(1,.87,.68)
sun=bpy.data.objects.new('MorningSun',sun_data)
studio.objects.link(sun)
sun.rotation_euler=(math.radians(28),math.radians(-30),math.radians(-38))
cam_data=bpy.data.cameras.new('RH_Camera')
cam=bpy.data.objects.new('RH_Camera',cam_data)
studio.objects.link(cam)
angle=math.radians(50)
target=Vector((0,0,.1))
cam.location=target+Vector((0,-12*math.cos(angle),12*math.sin(angle)))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam_data.type='ORTHO'
cam_data.ortho_scale=8.6
scene.camera=cam


def text(body,position,collection=kit_labels,size=.20):
    d=bpy.data.curves.new('PREVIEW_Text','FONT')
    d.body=body
    d.size=size
    d.align_x='CENTER'
    o=bpy.data.objects.new('PREVIEW_'+body,d)
    collection.objects.link(o)
    o.location=position
    o.rotation_euler=cam.rotation_euler
    d.materials.append(mark)


for label,x,y in [('CLEAN',-2,1),('MOSS VERTEX MASK',0,1),('GRASS EDGE',2,1),('STONE',-2,-1),('TIMBER',0,-1),('MENHIR',2,-1)]:
    text(label,(x,y-.75,.08),size=.16)
text('BLENDER 5.2.3 | 1 m MODULES | SHARED ATLAS',(0,2.2,.35),size=.19)
text('3',(1.5,1.32,.60),board_labels,.2)
text('STONE = 1',(-1.5,1.32,.60),board_labels,.15)
text('CHAPTER 01 PRODUCTION KIT | GRID PREVIEW',(0,-3.7,.05),board_labels,.18)

# Production checks and measured module manifest.
bpy.context.view_layer.update()
records={}
for key,r in modules.items():
    triangles=0
    vertices=[]
    materials=set()
    for o in r.children_recursive:
        if o.type!='MESH':
            continue
        o.data.calc_loop_triangles()
        triangles+=len(o.data.loop_triangles)
        vertices.extend(r.matrix_world.inverted() @ o.matrix_world @ v.co for v in o.data.vertices)
        materials.update(s.material.name for s in o.material_slots if s.material)
        assert o.data.uv_layers.active is not None
        assert o.data.color_attributes.get('MossMask') is not None
    lo=[min(v[i] for v in vertices) for i in range(3)]
    hi=[max(v[i] for v in vertices) for i in range(3)]
    assert len(materials)<=2,(key,materials)
    assert hi[0]-lo[0]<=1.0001 and hi[1]-lo[1]<=1.0001,(key,lo,hi)
    if key in ('Wall','NumberDoor','CountDoor','FlipWall_Up'):
        assert hi[2]<=.6001
    if key in ('Player','Menhir'):
        assert hi[2]<=1.0001 and lo[2]>=-.0001
    if key in ('Floor_Clean','Floor_Moss','Ice','Bridge','Cracked'):
        assert abs(hi[2])<.0001
    records[r.name]={'key':key,'triangles':triangles,'material_slots':len(materials),'materials':sorted(materials),'bounds_min':lo,'bounds_max':hi,'fbx':'Models/'+r.name+'.fbx'}
for r in modules.values():
    bpy.ops.object.select_all(action='DESELECT')
    r.select_set(True)
    for o in r.children_recursive:
        o.select_set(True)
    bpy.context.view_layer.objects.active=r
    bpy.ops.export_scene.fbx(filepath=str(out/'Models'/(r.name+'.fbx')),use_selection=True,
        object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,
        bake_anim=False,path_mode='RELATIVE',embed_textures=False,use_custom_props=True)
bpy.ops.object.select_all(action='DESELECT')
board_triangles=0
for o in board.objects:
    if o.type=='MESH':
        o.data.calc_loop_triangles()
        board_triangles+=len(o.data.loop_triangles)
assert board_triangles<50000
report={'blender_version':bpy.app.version_string,'engine':scene.render.engine,'module_count':len(records),'material_count':len(bpy.data.materials),'shared_atlas':'Textures/RH_Atlas512.png','moss_attribute':'MossMask.R','player_menhir_shared_mesh':body.data==menhir.data,'visible_board_triangles':board_triangles,'camera_pitch':50,'camera_yaw':0,'preview_labels_exported':False,'unity_import_tested':False,'modules':records,'hooks':{'NumberDoor':'SS_Tile_NumberDoor__GatePanel','CountDoor':'SS_Tile_CountDoor__GatePanel','Lever':'SS_Tile_Lever__HandlePivot'}}
(report_root/'production-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(renders/'production-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(source_root/'SS_RuinsHenge_Chapter01.blend'))
scene.render.filepath=str(renders/'modules-eevee.png')
bpy.ops.render.render(write_still=True)
kit.hide_render=True
kit_labels.hide_render=True
board.hide_render=False
board_labels.hide_render=False
cam_data.ortho_scale=9.6
scene.render.filepath=str(renders/'chapter01-kit-board-eevee.png')
bpy.ops.render.render(write_still=True)
cam_data.ortho_scale=12
target=Vector((0,1,.15))
cam.location=target+Vector((0,-12*math.cos(angle),12*math.sin(angle)))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(renders/'chapter01-context-eevee.png')
bpy.ops.render.render(write_still=True)
print('RUINS_HENGE_READY '+str(out))

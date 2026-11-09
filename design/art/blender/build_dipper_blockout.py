"""Blender background blockout, never writes into Unity or the repository.

blender -b -P build_dipper_blockout.py -- --out C:/unityjam/shared-assets/seven-steps/concepts/themes/dipper-3d/blender
Optional: --export-fbx. All numbers/stars/footprints belong to PREVIEW, not exports.
"""
import argparse
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def arguments():
    tail = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument('--out', required=True)
    p.add_argument('--export-fbx', action='store_true')
    return p.parse_args(tail)


ARGS = arguments()
OUT = Path(ARGS.out).resolve()
SAFE_ROOTS = [Path('C:/unityjam/shared-assets/seven-steps/concepts/themes/' + name).resolve()
              for name in ('dipper-3d', 'dipper-3d-variants')]
if not any(OUT == r or r in OUT.parents for r in SAFE_ROOTS):
    raise ValueError('Output must stay inside the named shared dipper-3d folder')
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
try:
    scene.render.engine = 'BLENDER_EEVEE_NEXT'  # Blender 4.x
except TypeError:
    scene.render.engine = 'BLENDER_EEVEE'       # Blender 5.2+
scene.render.resolution_x = 1600
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new('SS_World')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (0.025, 0.045, 0.08, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = 0.4


def collection(name):
    c = bpy.data.collections.new(name)
    scene.collection.children.link(c)
    return c


LIB = collection('01_EXPORT_LIBRARY')
BOARD = collection('02_BOARD_INSTANCES')
PREVIEW = collection('90_PREVIEW_LABELS_NO_EXPORT')
STUDIO = collection('99_STUDIO_NO_EXPORT')
LIB.hide_render = True
COL = LIB
PALETTE = ['152239', 'DDE5DF', '607A88', '77A294', 'BBA171', 'B95B46', 'A9CFDF', '323A46']


def linear_channel(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def rgb(h):
    return tuple(int(h[i:i+2], 16) / 255.0 for i in (0, 2, 4))


# One generated palette lookup image, no imported textures or hidden packages.
atlas = bpy.data.images.new('SS_Palette_256', width=256, height=256, alpha=False)
pixels = []
for y in range(256):
    shade = 0.90 + 0.10 * y / 255.0
    for x in range(256):
        color = rgb(PALETTE[x // 32])
        if y < 32:  # Reserved trim strip; palette UV lookup starts above 0.15.
            u = x % 64
            outer = ((u - 32) / 22) ** 2 + ((y - 15) / 10) ** 2
            inner = ((u - 26) / 12) ** 2 + ((y - 15) / 6) ** 2
            ink = abs(outer - 1) < .20 or abs(inner - 1) < .25
            color = rgb('263B41' if ink else 'BBA171')
        pixels.extend([*(v * shade for v in color), 1.0])
atlas.pixels = pixels
atlas.filepath_raw = str(OUT / 'SS_Palette_256.png')
atlas.file_format = 'PNG'
atlas.save()


def material(name, roughness, metallic=0.0, color=None, emission=False):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    n = m.node_tree.nodes.get('Principled BSDF')
    n.inputs['Roughness'].default_value = roughness
    n.inputs['Metallic'].default_value = metallic
    if color is None:
        tex = m.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = atlas
        tex.interpolation = 'Linear'
        m.node_tree.links.new(tex.outputs['Color'], n.inputs['Base Color'])
    else:
        n.inputs['Base Color'].default_value = (*[linear_channel(v) for v in rgb(color)], 1)
        if emission:
            n.inputs['Emission Color'].default_value = n.inputs['Base Color'].default_value
            n.inputs['Emission Strength'].default_value = 0.4
    return m


MATTE = material('SS_M_OpaqueAtlas', 0.8)
METAL = material('SS_M_MetalAtlas', 0.4, 0.55)
GLAZE = material('SS_M_GlazeAtlas', 0.28)
FROST = material('SS_M_FrostOpaqueAtlas', 0.45)
LABEL = material('SS_M_PreviewLabel_NO_EXPORT', 1, color='EAE8D8', emission=True)
label_bsdf = LABEL.node_tree.nodes['Principled BSDF']
label_info = LABEL.node_tree.nodes.new('ShaderNodeObjectInfo')
label_tint = LABEL.node_tree.nodes.new('ShaderNodeMixRGB')
label_tint.blend_type = 'MULTIPLY'
label_tint.inputs[0].default_value = 1
label_tint.inputs[1].default_value = label_bsdf.inputs['Base Color'].default_value
LABEL.node_tree.links.new(label_info.outputs['Color'], label_tint.inputs[2])
LABEL.node_tree.links.new(label_tint.outputs[0], label_bsdf.inputs['Base Color'])
LABEL.node_tree.links.new(label_tint.outputs[0], label_bsdf.inputs['Emission Color'])
STAGE = material('SS_M_Studio_NO_EXPORT', 0.9, color='152239')
assert len(bpy.data.materials) == 6


def move_collection(obj):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    COL.objects.link(obj)


def finish(obj, name, swatch, mat=MATTE, bevel=0.02):
    obj.name = name
    move_collection(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    # Bake geometry relative to a common cell-center/top-plane origin.
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel:
        mod = obj.modifiers.new('Bevel_1_segment', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name='UV0_Palette')
    for poly in obj.data.polygons:
        for li in poly.loop_indices:
            z = obj.data.vertices[obj.data.loops[li].vertex_index].co.z
            uv.data[li].uv = ((swatch + 0.5) / 8, min(0.85, max(0.15, 0.3 + z * 0.5)))
    obj.select_set(False)
    return obj


def box(name, size, center, swatch=0, mat=MATTE, bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, name, swatch, mat, bevel)


def cone(name, r1, r2, depth, center, swatch=2):
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=r1, radius2=r2, depth=depth, location=center)
    return finish(bpy.context.object, name, swatch, bevel=0)


def root(name, parts):
    obj = bpy.data.objects.new(name, None)
    LIB.objects.link(obj)
    obj.empty_display_size = 0.15
    for p in parts:
        p.parent = obj
    return obj


modules = {}
modules['Floor'] = root('SS_Tile_Floor', [box('FloorMesh', (.96, .96, .12), (0, 0, -.06), 1)])
ice = [box('IceMesh', (.96, .96, .12), (0, 0, -.06), 6, FROST)]
for x in (-.25, 0, .25):
    ice.append(box('SlideGroove', (.025, .68, .006), (x, 0, -.003), 1, FROST, .002))
modules['Ice'] = root('SS_Tile_Ice', ice)
modules['Wall'] = root('SS_Tile_Wall', [box('WallMesh', (.90, .90, .55), (0, 0, .275), 0)])
for kind, sw, mat in [('NumberDoor', 0, MATTE), ('CountDoor', 3, GLAZE)]:
    parts = [box('Post_L', (.17, .25, .50), (-.32, 0, .25), sw, mat),
             box('Post_R', (.17, .25, .50), (.32, 0, .25), sw, mat),
             box('GatePanel', (.52, .13, .30), (0, -.03, .28), 4, METAL)]
    modules[kind] = root('SS_Tile_' + kind, parts)
lever = [box('LeverBase', (.50, .36, .08), (0, 0, .04), 0)]
bpy.ops.mesh.primitive_cube_add(size=1, location=(.08, 0, .22), rotation=(0, math.radians(40), 0))
bpy.context.object.dimensions = (.065, .08, .33)
lever.append(finish(bpy.context.object, 'Handle', 4, METAL, .01))
lever.append(cone('Grip', .055, .055, .085, (.18, 0, .34), 4))
modules['Lever'] = root('SS_Tile_Lever', lever)
modules['FlipWall_Up'] = root('SS_Tile_FlipWall_Up', [box('Panel', (.88, .24, .55), (0, 0, .275), 0)])
modules['FlipWall_Down'] = root('SS_Tile_FlipWall_Down', [box('Panel', (.88, .24, .05), (0, 0, .025), 0)])
modules['Plate'] = root('SS_Tile_Plate', [box('PlateMesh', (.70, .70, .04), (0, 0, .02), 3, GLAZE)])
rim = [box('RimSide', (.10, .96, .12), (x, 0, -.06), 0) for x in (-.43, .43)]
rim += [box('RimEnd', (.76, .10, .12), (0, y, -.06), 0) for y in (-.43, .43)]
modules['PitRim'] = root('SS_Tile_PitRim', rim)
modules['Bridge'] = root('SS_Tile_Bridge', [box('BridgeSlab', (.27, .96, .10), (x, 0, -.05), 7) for x in (-.30, 0, .30)])
modules['Cracked'] = root('SS_Tile_Cracked', [box('CrackedHalf', (.455, .96, .12), (x, 0, -.06), 1) for x in (-.2525, .2525)])
modules['Exit'] = root('SS_Tile_Exit', [box('ExitPad', (.74, .74, .045), (0, 0, .0225), 3, GLAZE)])
modules['Border_Straight'] = root('SS_Border_Straight', [box('Straight', (1, .15, .18), (0, 0, .03), 0)])
modules['Border_Corner'] = root('SS_Border_Corner', [box('CornerX', (.30, .15, .18), (.075, 0, .03), 3, GLAZE), box('CornerY', (.15, .30, .18), (0, .075, .03), 3, GLAZE)])
body = [cone('Cloak', .22, .12, .56, (0, 0, .40), 2),
        cone('Head', .105, .105, .18, (0, 0, .78), 2),
        box('Feet', (.25, .22, .10), (0, -.02, .05), 2, bevel=.01)]
# One actual mesh shared by Player and Statue; only UV swatch differs per object material.
bpy.ops.object.select_all(action='DESELECT')
for p in body:
    p.select_set(True)
bpy.context.view_layer.objects.active = body[0]
bpy.ops.object.join()
body_obj = bpy.context.object
body_obj.name = 'CloakSharedMesh'
body_obj.select_set(False)
modules['Player'] = root('SS_Actor_Player', [body_obj])
stone = body_obj.copy()
stone.data = body_obj.data  # Identical geometry, not a second sculpt.
LIB.objects.link(stone)
stone.name = 'StatueSharedMesh'
# Dark stone material uses the same atlas with an offset UV vector in nodes.
# Avoid an extra material: render instances use object color through the shared opaque shader.
attr = MATTE.node_tree.nodes.new('ShaderNodeObjectInfo')
mix = MATTE.node_tree.nodes.new('ShaderNodeMixRGB')
mix.blend_type = 'MULTIPLY'
mix.inputs[0].default_value = 1.0
tex_node = next(n for n in MATTE.node_tree.nodes if n.type == 'TEX_IMAGE')
MATTE.node_tree.links.new(tex_node.outputs['Color'], mix.inputs[1])
MATTE.node_tree.links.new(attr.outputs['Color'], mix.inputs[2])
MATTE.node_tree.links.new(mix.outputs[0], MATTE.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
for o in LIB.objects:
    o.color = (1, 1, 1, 1)
stone.color = (.30, .33, .36, 1)
modules['Statue'] = root('SS_Actor_Statue', [stone])


def instance(kind, x, y, z=0):
    original = modules[kind]
    parent = original.copy()
    BOARD.objects.link(parent)
    parent.location = (x, y, z)
    for child in original.children:
        cp = child.copy()
        cp.data = child.data
        BOARD.objects.link(cp)
        cp.parent = parent
    return parent


COL = BOARD
box('BoardPlinth', (6.45, 6.45, .25), (0, 0, -.245), 0, bevel=.06)
for x in (-2.15, 2.15):
    box('TransverseStoneFoot', (.65, 5.8, .36), (x, 0, -.55), 0, bevel=.035)
band = box('FrontBronzeBand', (6.20, .035, .09), (0, -3.25, -.18), 4, METAL, .008)
for loop in band.data.loops:
    vertex = band.data.vertices[loop.vertex_index].co
    band.data.uv_layers.active.data[loop.index].uv = (
        .02 + (vertex.x + 3.1) / 6.2 * .96,
        .005 + (vertex.z + .225) / .09 * .115)
pit_cells = {(1, 3), (2, 3)}
for row in range(6):
    for col in range(6):
        x, y = col - 2.5, row - 2.5
        if (col, row) in pit_cells:
            instance('PitRim' if col == 1 else 'Bridge', x, y)
        else:
            instance('Ice' if col == 3 and row in (1, 2, 3) else 'Cracked' if (col, row) == (0, 4) else 'Floor', x, y)
instance('Player', 1.5, -1.5, .08)
instance('Statue', -.5, .5, .04)
instance('NumberDoor', 1.5, 1.5)
instance('CountDoor', -1.5, 1.5)
instance('Lever', 1.5, -1.5)
instance('FlipWall_Up', 2.5, -.5)
instance('Plate', -.5, .5)
instance('Wall', 2.5, 1.5)
instance('Exit', 2.5, 2.5)
for side in (-1, 1):
    for i in range(6):
        instance('Border_Straight', i - 2.5, side * 3.08)
        p = instance('Border_Straight', side * 3.08, i - 2.5)
        p.rotation_euler.z = math.pi / 2
for x in (-3.08, 3.08):
    for y in (-3.08, 3.08):
        instance('Border_Corner', x, y)

COL = STUDIO
box('StudioGround', (200, 200, .1), (0, 0, -.83), 0, STAGE, 0)


def area(name, location, energy, size, color):
    light = bpy.data.lights.new(name, 'AREA')
    light.energy = energy
    light.shape = 'DISK'
    light.size = size
    light.color = color
    o = bpy.data.objects.new(name, light)
    STUDIO.objects.link(o)
    o.location = location
    o.rotation_euler = (Vector((0, 0, 0)) - o.location).to_track_quat('-Z', 'Y').to_euler()


area('Key', (4, -1, 8), 1200, 7, (.78, .88, 1))
area('Fill', (-4, -5, 4), 700, 6, (1, .82, .60))
cam_data = bpy.data.cameras.new('SS_Camera')
cam = bpy.data.objects.new('SS_Camera', cam_data)
STUDIO.objects.link(cam)
angle = math.radians(50)
cam.location = (0, -12 * math.cos(angle), 12 * math.sin(angle))
target = Vector((0, .2, .1)) + Vector((0, math.sin(angle), math.cos(angle))) * .5
# Aim is exactly 50 degrees down, yaw zero; target accounts only for framing.
cam.location = target + Vector((0, -12 * math.cos(angle), 12 * math.sin(angle)))
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 11.5
scene.camera = cam


def preview_text(body, location, size=.22):
    curve = bpy.data.curves.new('PreviewText', 'FONT')
    curve.body = body
    curve.size = size
    curve.align_x = 'CENTER'
    obj = bpy.data.objects.new('PREVIEW_' + body, curve)
    PREVIEW.objects.link(obj)
    obj.location = location
    obj.rotation_euler = cam.rotation_euler
    curve.materials.append(LABEL)
    return obj


preview_text('3', (1.5, 1.35, .65))
preview_text('STONE = 1', (-1.5, 1.35, .65), .16)
preview_text('BLENDER EEVEE BLOCKOUT | 1 CELL = 1 m', (0, -3.65, .05), .18)
# Fixed seven-node diagram in camera space. PREVIEW only, never in FBX meshes.
frame_center = target
right = cam.rotation_euler.to_quaternion() @ Vector((1, 0, 0))
up = cam.rotation_euler.to_quaternion() @ Vector((0, 1, 0))
points = [(100, 60), (90, 220), (400, 270), (500, 150), (720, 180), (910, 220), (1130, 340)]
hud = [frame_center + right * (-3.5 + x * 7 / 1280) + up * (4.0 - y * 7 / 1280)
       for x, y in points]
for a, b in [(1, 2), (2, 3), (3, 4), (4, 1), (4, 5), (5, 6), (6, 7)]:
    curve = bpy.data.curves.new('PREVIEW_DipperEdge', 'CURVE')
    curve.dimensions = '3D'
    curve.bevel_depth = .008
    curve.bevel_resolution = 0
    spline = curve.splines.new('POLY')
    spline.points.add(1)
    for p, xyz in zip(spline.points, (hud[a-1], hud[b-1])):
        p.co = (*xyz, 1)
    obj = bpy.data.objects.new('PREVIEW_Edge_%d_%d' % (a, b), curve)
    PREVIEW.objects.link(obj)
    curve.materials.append(LABEL)
    if (a, b) == (6, 7):
        obj.color = (1, .24, .10, 1)
for i, point in enumerate(hud, 1):
    star = preview_text('+', point - up * .045, .16)
    number = preview_text(str(i), point + right * .12 + up * .11, .17)
    if i == 7:
        star.color = number.color = (1, .24, .10, 1)
preview_text('7', (1.5, -1.85, .1), .20).color = (1, .24, .10, 1)
stats = {}
for key, r in modules.items():
    triangles = 0
    vertices = []
    slots = set()
    for o in r.children:
        o.data.calc_loop_triangles()
        triangles += len(o.data.loop_triangles)
        vertices.extend(v.co.copy() for v in o.data.vertices)
        slots.update(m.name for m in o.data.materials)
    lo = [min(v[i] for v in vertices) for i in range(3)]
    hi = [max(v[i] for v in vertices) for i in range(3)]
    stats[r.name] = {'triangles': triangles, 'material_slots': len(slots), 'bounds_min': lo, 'bounds_max': hi}
    if key in ('Wall', 'NumberDoor', 'CountDoor', 'FlipWall_Up'):
        assert hi[2] <= .6001, (key, hi)
    if key in ('Player', 'Statue'):
        assert hi[2] <= 1.0001 and lo[2] >= -.0001
assert modules['Player'].children[0].data == modules['Statue'].children[0].data
report = {'blender_version': bpy.app.version_string, 'engine': scene.render.engine, 'units': '1 Blender unit = 1 meter',
          'camera_pitch': 50, 'camera_yaw': 0, 'shared_material_count': len(bpy.data.materials),
          'module_count': len(modules), 'modules': stats, 'preview_labels_exported': False,
          'statue_shared_mesh': True, 'unity_import_tested': False}
report['hud_nodes'] = points
report['hud_edges'] = [[1, 2], [2, 3], [3, 4], [4, 1], [4, 5], [5, 6], [6, 7]]
board_triangles = 0
for obj in BOARD.objects:
    if obj.type == 'MESH':
        obj.data.calc_loop_triangles()
        board_triangles += len(obj.data.loop_triangles)
report['visible_board_triangles'] = board_triangles
assert board_triangles < 50000
(OUT / 'blockout-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
if ARGS.export_fbx:
    exports = OUT / 'fbx'
    exports.mkdir(exist_ok=True)
    for r in modules.values():
        bpy.ops.object.select_all(action='DESELECT')
        r.select_set(True)
        for child in r.children:
            child.select_set(True)
        bpy.context.view_layer.objects.active = r
        bpy.ops.export_scene.fbx(filepath=str(exports / (r.name + '.fbx')), use_selection=True,
            object_types={'EMPTY', 'MESH'}, global_scale=1, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, use_mesh_modifiers=True, add_leaf_bones=False,
            bake_anim=False, path_mode='AUTO', embed_textures=False)
    bpy.ops.object.select_all(action='DESELECT')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'SS_Dipper_Blockout.blend'))
scene.render.filepath = str(OUT / 'blockout-eevee.png')
bpy.ops.render.render(write_still=True)
print('SS_BLOCKOUT_DONE ' + str(OUT))

"""Triplet/planet blockout, Blender5.2.3. Source outside Assets, no Unity writes.
Run: blender -b --python-exit-code 1 -P design/art/blender/build_ruins_planets.py
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector

shared=Path('C:/unityjam/shared-assets/seven-steps')
source=shared/'Source/Blender'
assets=shared/'Assets/Art/RuinsHenge'
output=shared/'concepts/themes/ruins-henge-planets/blender'
output.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source/'SS_RuinsHenge_Chapter01.blend'))
scene=bpy.context.scene
library=bpy.data.collections['01_EXPORT_LIBRARY']
body_mesh=bpy.data.objects['RH_CloakSharedMesh'].data.copy()
height=max(v.co.z for v in body_mesh.vertices)
for v in body_mesh.vertices: v.co.z*=.88/height
for col in list(bpy.data.collections):
    if col!=library:
        for obj in list(col.objects): bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.collections.remove(col)
library.hide_render=True
collection=bpy.data.collections.new('PLANET_PREVIEW_NO_EXPORT')
scene.collection.children.link(collection)
actors=bpy.data.collections.new('TRIPLETS_EXPORT')
scene.collection.children.link(actors)
base=bpy.data.materials['SS_RH_M_BaseAtlas']
cloth=bpy.data.materials['SS_RH_M_PlayerAtlas']
stone=bpy.data.materials['SS_RH_M_MenhirAtlas']
mark=bpy.data.materials['SS_RH_M_MarkUnlit']

def finish(obj,name,swatch=0,material=base):
    obj.name=name
    for col in list(obj.users_collection): col.objects.unlink(obj)
    collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    obj.data.materials.clear()
    obj.data.materials.append(material)
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UV0_Atlas')
    mask=obj.data.color_attributes.get('MossMask') or obj.data.color_attributes.new(name='MossMask',type='FLOAT_COLOR',domain='CORNER')
    for loop in obj.data.loops:
        uv.data[loop.index].uv=((swatch+.5)/8,.5)
        mask.data[loop.index].color=(0,0,0,1)
    obj.select_set(False)
    return obj

def box(name,size,position,swatch=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=position)
    o=bpy.context.object
    o.dimensions=size
    return finish(o,name,swatch)

def sphere(name,radius,position,swatch=2,subdivisions=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions,radius=radius,location=position)
    return finish(bpy.context.object,name,swatch)

# A real hood opening, short cloak and feet, rather than recoloring a plain rock.
pieces=[]
bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.23,radius2=.12,depth=.46,location=(0,0,.33))
pieces.append(finish(bpy.context.object,'TripletCloak',0,cloth))
outline=[(-.14,.72),(-.09,.82),(0,.88),(.09,.82),(.14,.72),(.11,.63),(0,.60),(-.11,.63)]
verts=[(x,-.12,z) for x,z in outline]+[(x*.82,.10,.74+(z-.74)*.88) for x,z in outline]+[(x*.65,-.14,.74+(z-.74)*.65) for x,z in outline]+[(x*.56,-.06,.74+(z-.74)*.56) for x,z in outline]
faces=[]
for i in range(8):
    j=(i+1)%8
    faces.extend([(i,j,8+j,8+i),(i,16+i,16+j,j),(16+i,24+i,24+j,16+j)])
faces.extend([tuple(range(8,16)),tuple(reversed(range(24,32)))])
data=bpy.data.meshes.new('TripletHoodMesh');data.from_pydata(verts,[],faces);data.update()
hood=bpy.data.objects.new('TripletHood',data);collection.objects.link(hood)
finish(hood,'TripletHood',0,cloth)
for polygon in hood.data.polygons:
    if polygon.index==len(hood.data.polygons)-1:
        for loop in polygon.loop_indices:hood.data.uv_layers.active.data[loop].uv=(6.5/8,.5)
pieces.append(hood)
pieces.extend(box('TripletBoot',(.13,.19,.10),(x,-.015,.05),3) for x in (-.085,.085))
bpy.ops.object.select_all(action='DESELECT')
for obj in pieces:obj.select_set(True)
bpy.context.view_layer.objects.active=pieces[0]
bpy.ops.object.join()
combined=bpy.context.object
# One shared body material; palette UV gives the recessed face/boots contrast.
for polygon in combined.data.polygons:polygon.material_index=0
combined.data.materials.clear();combined.data.materials.append(cloth)
body_mesh=combined.data.copy()
bpy.data.objects.remove(combined,do_unlink=True)
for node in cloth.node_tree.nodes:
    if node.type=='MIX_RGB':node.inputs[2].default_value=(.80,.78,.70,1)

def accent_mesh(swatch):
    # Flat scarf + short fitted tail; contour stays within .48m footprint.
    parts=[box('ScarfWrap',(.25,.16,.055),(0,-.005,.64),swatch),
           box('ScarfTail',(.055,.025,.18),(-.07,-.14,.55),swatch),
           box('HemTrim',(.30,.02,.028),(0,-.15,.13),swatch),
           box('SmallTravelClasp',(.035,.025,.035),(.05,-.10,.65),7)]
    bpy.ops.object.select_all(action='DESELECT')
    for p in parts:p.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.select_set(False)
    return obj

records={}
roots={}
for name,swatch,is_stone in [('A',6,False),('B',7,False),('C',4,False),('Identical',7,False),('Menhir',0,True)]:
    root=bpy.data.objects.new('SS_Actor_Triplet_'+name,None)
    actors.objects.link(root)
    body=bpy.data.objects.new(root.name+'__Body',body_mesh)
    actors.objects.link(body)
    body.parent=root
    body.material_slots[0].link='OBJECT'
    body.material_slots[0].material=stone if is_stone else cloth
    accent=accent_mesh(swatch)
    collection.objects.unlink(accent)
    actors.objects.link(accent)
    accent.parent=root
    if is_stone:
        accent.material_slots[0].link='OBJECT'
        accent.material_slots[0].material=stone
    tris=0
    for o in root.children:
        o.data.calc_loop_triangles()
        tris+=len(o.data.loop_triangles)
        assert o.data.uv_layers.active and o.data.color_attributes.get('MossMask')
    assert abs(max(v.co.z for o in root.children for v in o.data.vertices)-.88)<.0001
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for obj in root.children:obj.select_set(True)
    bpy.context.view_layer.objects.active=root
    path=assets/'Models'/(root.name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},
        global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE',embed_textures=False)
    records[root.name]={'triangles':tris,'height':.88,'fbx':str(path),'accent_swatch':swatch,'rig':'none'}
    roots[name]=root
actors.hide_render=True

def instance(root,x,y,z=0):
    r=root.copy()
    collection.objects.link(r)
    r.location=(x,y,z)
    for child in root.children_recursive:
        o=child.copy()
        collection.objects.link(o)
        o.parent=r
    return r

# Exactly three siblings on board: one active, one waiting, one already KEEP stone.
for y in range(5):
    for x in range(5):
        instance(bpy.data.objects['SS_Tile_Floor_Moss' if (x+y)%2 else 'SS_Tile_Floor_Clean'],x-2,y-2)
for name,x,y in [('A',-1.4,-1.3),('B',0,-1.3),('Menhir',1.4,-1.3)]: instance(roots[name],x,y)
for name,x,y in [('SS_Tile_NumberDoor',1,1),('SS_Block_Wood',-2,1),('SS_Tile_Lever',0,1),('SS_Block_Stone',2,2)]:
    instance(bpy.data.objects[name],x,y)
sphere('HomePlanet_CurvedHorizon',3.65,(0,0,-3.90),2,3)
sphere('DistantPlanet_Celadon',.72,(-4.2,2.5,.8),5,2)
sphere('DistantPlanet_Ochre',.48,(4.1,2.5,1.2),7,2)
for x,y in [(-2.8,2.2),(2.7,2.1),(-2.4,2.7)]:
    r=instance(bpy.data.objects['SS_Actor_Menhir'],x,y,-.2)
    r.scale=(1.3,1.3,1.6)

scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.007,.014,.04,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
for name,position,energy,size,tint in [('StarlightKey',(-4,-4,7),1100,4,(1,.85,.64)),('BlueSpaceFill',(4,4,6),850,6,(.53,.72,1))]:
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.size=size;data.color=tint
    obj=bpy.data.objects.new(name,data);collection.objects.link(obj);obj.location=position
    obj.rotation_euler=(-obj.location).to_track_quat('-Z','Y').to_euler()
cam_data=bpy.data.cameras.new('PlanetCamera');cam=bpy.data.objects.new('PlanetCamera',cam_data)
collection.objects.link(cam)
target=Vector((0,0,-1.2));angle=math.radians(50)
cam.location=target+Vector((0,-14*math.cos(angle),14*math.sin(angle)))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
cam_data.type='ORTHO';cam_data.ortho_scale=14
scene.camera=cam
# Seven unnumbered preview stars: accurate bowl1-2-3-4-1 and handle4-5-6-7.
points=[(-4,0),(-4,-.7),(-2.7,-.9),(-2.2,-.35),(-1.3,-.48),(-.5,-.65),(.4,-1.1)]
for i,(x,z) in enumerate(points):
    star=sphere('PreviewDipperStar_'+str(i+1),.035,(x,2.7,1.8+z),0)
    star.data.materials[0]=mark
for a,b in [(0,1),(1,2),(2,3),(3,0),(3,4),(4,5),(5,6)]:
    curve=bpy.data.curves.new('DipperLine','CURVE');curve.dimensions='3D';curve.bevel_depth=.008;curve.bevel_resolution=0
    poly=curve.splines.new('POLY');poly.points.add(1)
    for p,index in zip(poly.points,(a,b)):p.co=(*Vector((points[index][0],2.7,1.8+points[index][1])),1)
    obj=bpy.data.objects.new('PreviewDipperLine',curve);collection.objects.link(obj);curve.materials.append(mark)
scene.render.resolution_x=1600;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
assert len(bpy.data.materials)==6
assert all(next(o for o in roots[name].children if o.name.endswith('__Body')).data==body_mesh for name in roots)
preview_triangles=0
for obj in collection.objects:
    if obj.type=='MESH':
        obj.data.calc_loop_triangles()
        preview_triangles+=len(obj.data.loop_triangles)
assert preview_triangles<50000
scene.render.filepath=str(output/'triplets-planet-eevee.png')
bpy.ops.wm.save_as_mainfile(filepath=str(source/'SS_RuinsHenge_Planets.blend'))
bpy.ops.render.render(write_still=True)
report={'blender_version':bpy.app.version_string,'material_count':6,'height':.88,'body_mesh_shared':True,
        'preview_triangles':preview_triangles,'planet_count':3,'camera_pitch':50,'camera_yaw':0,
        'source':str(source/'SS_RuinsHenge_Planets.blend'),'models':records,'unity_import_tested':False,
        'preview':'three siblings; two cloth and one KEEP stone; background non-exported'}
(source/'RuinsHengeReports/planets-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(output/'planets-report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('PLANETS_READY',len(records))

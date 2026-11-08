"""Blender FBX round trip for approved triplets and KEEP candidates; no Unity writes."""
import bpy
import hashlib
import json
from pathlib import Path

root=Path('C:/unityjam/shared-assets/seven-steps')
reports=root/'Source/Blender/RuinsHengeReports'
data=json.loads((reports/'planets-report.json').read_text())
assert data['approved_palette']=={'A':'568C88','B':'B99A57','C':'B96C59'}
assert data['summit_z']==data['board_underside_z']==-.12
assert data['summit_radius']>2.48*2**.5
assert data['material_count']<=6
assert 'SS_Actor_Triplet_Identical' not in data['models']
image=bpy.data.images.load(data['atlas'],check_existing=False)
def lin(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
atlas_samples={}
for name,band in [('A',6),('B',7),('C',4)]:
    offset=(500*512+band*64+32)*4
    actual=image.pixels[offset:offset+3]
    hexcolor=data['approved_palette'][name]
    expected=[lin(int(hexcolor[i:i+2],16)/255) for i in (0,2,4)]
    assert max(abs(a-b) for a,b in zip(actual,expected))<.004,(name,actual,expected)
    atlas_samples[name]=hexcolor
assert not list((root/'Assets/Art/RuinsHenge').rglob('*.blend*'))
body_hashes=[]
results={}
for name,expected in data['models'].items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=expected['fbx'])
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    tris=0
    for obj in meshes:
        assert obj.data.uv_layers.active and obj.data.color_attributes.get('MossMask')
        obj.data.calc_loop_triangles()
        tris+=len(obj.data.loop_triangles)
    assert tris==expected['triangles'],name
    assert len({slot.material.name for obj in meshes for slot in obj.material_slots if slot.material})<=2
    vertices=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
    zmin=min(v.z for v in vertices)
    zmax=max(v.z for v in vertices)
    assert abs(zmin)<.0001 and abs(zmax-.88)<.0001,(name,zmin,zmax)
    body=next(o for o in meshes if o.name.endswith('__Body'))
    body_hashes.append(hashlib.sha256(str([tuple(round(v,5) for v in point.co) for point in body.data.vertices]).encode()).hexdigest())
    assert bpy.data.objects[name].location.length<.0001
    results[name]={'triangles':tris,'uv_vertex_color_origin':True,'height':zmax-zmin}
assert len(set(body_hashes))==1,'Sibling and stone body geometry must match'
(reports/'planets-roundtrip.json').write_text(json.dumps({'verified':len(results),'same_body_geometry':True,'atlas_hex_verified':atlas_samples,'summit_contact_verified':True,'models':results},indent=2))
print('PLANETS_FBX_OK',len(results))

"""Blender FBX round trip for five triplet variants; does not touch Unity."""
import bpy
import hashlib
import json
from pathlib import Path

root=Path('C:/unityjam/shared-assets/seven-steps')
reports=root/'Source/Blender/RuinsHengeReports'
data=json.loads((reports/'planets-report.json').read_text())
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
    vertices=[obj.matrix_world@v.co for obj in meshes for v in obj.data.vertices]
    zmin=min(v.z for v in vertices)
    zmax=max(v.z for v in vertices)
    assert abs(zmin)<.0001 and abs(zmax-.88)<.0001,(name,zmin,zmax)
    body=next(o for o in meshes if o.name.endswith('__Body'))
    body_hashes.append(hashlib.sha256(str([tuple(round(v,5) for v in point.co) for point in body.data.vertices]).encode()).hexdigest())
    assert bpy.data.objects[name].location.length<.0001
    results[name]={'triangles':tris,'uv_vertex_color_origin':True,'height':zmax-zmin}
assert len(set(body_hashes))==1,'Sibling and stone body geometry must match'
(reports/'planets-roundtrip.json').write_text(json.dumps({'verified':len(results),'same_body_geometry':True,'models':results},indent=2))
print('PLANETS_FBX_OK',len(results))

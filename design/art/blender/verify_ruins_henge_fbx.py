"""Blender 5.2 FBX round trip; run after build_ruins_henge.py, never touches Unity."""
import bpy
import json
from pathlib import Path

root = Path('C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge')
assert not list(root.rglob('*.blend*')), 'Blender source/backup/meta must stay outside Assets'
assert all(p.suffix.lower() in ('.fbx','.png','.meta') for p in root.rglob('*') if p.is_file()), 'Only runtime asset files belong in Assets'
reports = Path('C:/unityjam/shared-assets/seven-steps/Source/Blender/RuinsHengeReports')
report = json.loads((reports/'production-report.json').read_text(encoding='utf-8'))
results = {}
for name, expected in report['modules'].items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(root/expected['fbx']))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert meshes, name
    triangles = 0
    for obj in meshes:
        assert obj.data.uv_layers.active, name
        assert obj.data.color_attributes, name
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    assert triangles == expected['triangles'], (name, triangles)
    assert name in bpy.data.objects, name
    assert bpy.data.objects[name].location.length < .0001, name
    results[name] = {'triangles': triangles, 'uv_and_vertex_color': True, 'origin': [0,0,0]}
output = {'blender_version': bpy.app.version_string, 'files_verified': len(results),
          'unity_import_tested': False, 'modules': results}
(reports/'fbx-roundtrip.json').write_text(json.dumps(output, indent=2), encoding='utf-8')
print('FBX_ROUNDTRIP_OK', len(results))

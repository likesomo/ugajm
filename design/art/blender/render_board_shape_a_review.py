"""MCP render-only pass after build_board_shape_a.py; never re-exports the kit."""
import bpy, ast, math, random
from pathlib import Path
from mathutils import Vector

scene=bpy.data.scenes['BoardShapeA'];bpy.context.window.scene=scene
library=next(c for c in scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'))
preview=next(c for c in scene.collection.children if c.name.startswith('02_BOARD_INSTANCES_BSA'))
studio=next(c for c in scene.collection.children if c.name.startswith('99_STUDIO_NO_EXPORT_BSA'))
modules={o.name:o for o in library.objects}
source=Path('C:/Users/soso1/orca/workspaces/ugajm/art/design/art/blender/build_board_shape_a.py')
tree=ast.parse(source.read_text(encoding='utf-8'))
definitions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ('instance','board','setup_preview')]
exec(compile(ast.Module(body=definitions,type_ignores=[]),str(source),'exec'))
output=Path('C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/distinct-pass/after')
BSA_STAGE='baked'
for w,h in [(12,5),(10,3),(8,6)]:
    for ob in list(preview.objects)+list(studio.objects):bpy.data.objects.remove(ob,do_unlink=True)
    for ob in library.objects:ob.hide_render=False
    PREVIEW_SIZE=(w,h);board(w,h);setup_preview()
    for ob in library.objects:ob.hide_render=True;ob.hide_set(True)
    scene.render.filepath=str(output/('blender-03-baked.png' if (w,h)==(8,6) else f'blender-{w}x{h}.png'))
    bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath='C:/unityjam/shared-assets/seven-steps/Source/Blender/SS_BoardShapeA.blend')
result={'renders':str(output),'sizes':['12x5','10x3','8x6'],'pitch':50}

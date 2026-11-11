"""Porous, spalled limestone fragments with an editable high-poly source."""
import bpy, math, random
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
for name in ('SS_BSA_CrumbleShard','HP_CrumbleShard_TravertineSpall'):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
rng=random.Random(38042);ring=[];V=[];F=[];n=9
per=[(-.55,-.25),(-.32,-.47),(.08,-.49),(.49,-.29),(.57,.06),(.34,.36),(-.09,.49),(-.48,.28),(-.62,-.02)]
for zf,s in ((-.25,1),(-.06,.90),(.12,.72),(.22,.42)):
    row=[]
    for x,y in per:
        row.append(len(V));V.append((x*s+rng.uniform(-.06,.06),y*s+rng.uniform(-.06,.06),zf+rng.uniform(-.045,.045)))
    ring.append(row)
F.append(tuple(reversed(ring[0])));F.append(tuple(ring[-1]))
for r in range(len(ring)-1):
    for i in range(n):
        j=(i+1)%n
        if rng.random()<.35:F.extend([(ring[r][i],ring[r][j],ring[r+1][j]),(ring[r][i],ring[r+1][j],ring[r+1][i])])
        else:F.append((ring[r][i],ring[r][j],ring[r+1][j],ring[r+1][i]))
m=bpy.data.meshes.new('CrumbleTravertine_SpalledShard_Mesh');m.from_pydata(V,[],F);m.update();o=bpy.data.objects.new('SS_BSA_CrumbleShard',m);COL.objects.link(o);m.materials.append(MAT)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');col=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
    for li in p.loop_indices:
        q=m.vertices[m.loops[li].vertex_index].co;u=.035+.93*(q.x-mn[0])/max(.001,mx[0]-mn[0]);v=.035+.93*(q.z-mn[2])/max(.001,mx[2]-mn[2])
        uv.data[li].uv=((12%4+u)/4,(12//4+v)/4);style.data[li].uv=(4,.713);col.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
hp=bpy.data.objects.new('HP_CrumbleShard_TravertineSpall',m.copy());HP.objects.link(hp);sub=hp.modifiers.new('Pore sculpt subdivision','SUBSURF');sub.levels=2;sub.render_levels=2;tex=bpy.data.textures.new('CrumbleLimestonePores',type='CLOUDS');tex.noise_scale=.055;dis=hp.modifiers.new('Dissolution pits and spall relief','DISPLACE');dis.texture=tex;dis.strength=.035;dis.mid_level=.53;hp.hide_render=True;hp.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
bpy.ops.export_scene.fbx(filepath=str(OUT/'SS_BSA_CrumbleShard.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
tris=sum(len(p.vertices)-2 for p in m.polygons);bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'shard_tris':tris,'highpoly_source':hp.name,'holes':'irregular travertine weathering in slot 12','source':str(SRC)}

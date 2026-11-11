"""Sarsen-like capstone-free altar upright, low-poly export and high-poly erosion source."""
import bpy, math, random
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
for name in ('SS_BSA_AltarMenhir','HP_AltarMenhir_SarsenWeathering'):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
rng=random.Random(74381);n=9;profiles=((.48,0),(.52,.16),(.43,.42),(.47,.75),(.35,1.04),(.32,1.42),(.20,1.70),(.16,1.82));V=[];F=[];rings=[]
for level,(radius,z) in enumerate(profiles):
    row=[];offset=rng.random()*.14
    for i in range(n):
        a=2*math.pi*i/n+offset;rr=radius*rng.uniform(.86,1.12);row.append(len(V));V.append((math.cos(a)*rr+rng.uniform(-.04,.04),math.sin(a)*rr*rng.uniform(.82,1.08),z+rng.uniform(-.04,.04)))
    rings.append(row)
F.append(tuple(reversed(rings[0])));F.append(tuple(rings[-1]))
for k in range(len(rings)-1):
    for i in range(n):
        j=(i+1)%n
        if rng.random()<.22:F.extend([(rings[k][i],rings[k][j],rings[k+1][j]),(rings[k][i],rings[k+1][j],rings[k+1][i])])
        else:F.append((rings[k][i],rings[k][j],rings[k+1][j],rings[k+1][i]))
m=bpy.data.meshes.new('AltarSarsen_Upright_Mesh');m.from_pydata(V,[],F);m.update();o=bpy.data.objects.new('SS_BSA_AltarMenhir',m);COL.objects.link(o);m.materials.append(MAT)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');col=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
    ax=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][ax]
    for li in p.loop_indices:
        q=m.vertices[m.loops[li].vertex_index].co;u=.04+.92*(q[axes[0]]-mn[axes[0]])/max(.001,mx[axes[0]]-mn[axes[0]]);v=.04+.92*(q[axes[1]]-mn[axes[1]])/max(.001,mx[axes[1]]-mn[axes[1]])
        uv.data[li].uv=((13%4+u)/4,(13//4+v)/4);style.data[li].uv=(4,.713);col.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
hp=bpy.data.objects.new('HP_AltarMenhir_SarsenWeathering',m.copy());HP.objects.link(hp);sub=hp.modifiers.new('Sarsen hand-hewn surface','SUBSURF');sub.levels=2;sub.render_levels=2;tex=bpy.data.textures.new('AltarSarsenSaltWeathering',type='CLOUDS');tex.noise_scale=.11;dis=hp.modifiers.new('Long bedding and weathered pits','DISPLACE');dis.texture=tex;dis.strength=.018;dis.mid_level=.52;hp.hide_render=True;hp.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
bpy.ops.export_scene.fbx(filepath=str(OUT/'SS_BSA_AltarMenhir.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
tris=sum(len(p.vertices)-2 for p in m.polygons);bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'altar_menhir_tris':tris,'ring_profiles':len(profiles),'highpoly_source':hp.name,'source':str(SRC)}

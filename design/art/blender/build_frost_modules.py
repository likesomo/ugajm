"""Low-poly frost crystal clusters plus retained high-poly source for the R2 board kit."""
import bpy, math, random
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
for name in ('SS_BSA_FrostSpire','HP_FrostQuartz_Rime','SS_BSA_PlainKerb','HP_PlainKerb'):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
V=[];F=[];rng=random.Random(23041)
def shard(cx,cy,r,h,rot):
    sides=5; rings=[];leanx=rng.uniform(-.16,.16);leany=rng.uniform(-.14,.14)
    for zf,rf,shift in ((0,1,0),(.12,.95,.08),(.68,.53,.48),(1,.035,1)):
        row=[];ox=leanx*shift;oy=leany*shift
        for i in range(sides):
            a=rot+2*math.pi*i/sides
            jitter=rng.uniform(.84,1.13)
            row.append(len(V));V.append((cx+ox+math.cos(a)*r*rf*jitter,cy+oy+math.sin(a)*r*rf*jitter,h*zf))
        rings.append(row)
    F.append(tuple(reversed(rings[0])))
    for k in range(len(rings)-1):
        for i in range(sides):F.append((rings[k][i],rings[k][(i+1)%sides],rings[k+1][(i+1)%sides],rings[k+1][i]))
    F.append(tuple(rings[-1]))
for params in ((0,0,.38,1.72,0),(.34,.03,.25,1.32,.4),(-.31,.02,.25,1.18,1.1)):
    shard(*params)
m=bpy.data.meshes.new('FrostQuartz_RimeLowPoly_Mesh');m.from_pydata(V,[],F);m.update();ob=bpy.data.objects.new('SS_BSA_FrostSpire',m);COL.objects.link(ob);m.materials.append(MAT)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');col=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
    for li in p.loop_indices:
        q=m.vertices[m.loops[li].vertex_index].co;u=.035+.93*(q.x-mn[0])/max(.001,mx[0]-mn[0]);v=.035+.93*(q.z-mn[2])/max(.001,mx[2]-mn[2])
        # Slot 7 is un-overridden crystalline ice, while stone chunks use chapter slot 10.
        uv.data[li].uv=((7%4+u)/4,(7//4+v)/4);style.data[li].uv=(4,.713);col.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
hp=bpy.data.objects.new('HP_FrostQuartz_Rime',m.copy());HP.objects.link(hp);bev=hp.modifiers.new('Crystal chipped facets','BEVEL');bev.width=.025;bev.segments=2;sub=hp.modifiers.new('Rime crystal polish','SUBSURF');sub.levels=1;sub.render_levels=1;hp.hide_render=True;hp.hide_viewport=True
def export(o,path):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
    o.select_set(False);return sum(len(p.vertices)-2 for p in o.data.polygons)
# Plain beveled stone replaces the old teal/terracotta chevron strip on non-moss planets.
kv=[(-.484,-.22,-.15),(.484,-.22,-.15),(.484,.22,-.15),(-.484,.22,-.15),(-.484,-.22,.37),(.484,-.22,.37),(.484,.22,.37),(-.484,.22,.37)]
kf=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
km=bpy.data.meshes.new('PlainKerb_Mesh');km.from_pydata(kv,[],kf);km.update();ko=bpy.data.objects.new('SS_BSA_PlainKerb',km);COL.objects.link(ko);km.materials.append(MAT)
ku=km.uv_layers.new(name='UVMap');ks=km.uv_layers.new(name='Style');kc=km.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
for p in km.polygons:
    for li in p.loop_indices:
        q=km.vertices[km.loops[li].vertex_index].co;u=.035+.93*(q.x+.484)/.968;v=.035+.93*(q.z+.15)/.52
        # Stable source slot 0 lets the chapter override resolve to its own stone,
        # preventing old teal/terracotta palette bands from sampling through.
        ku.data[li].uv=(u/4,v/4);ks.data[li].uv=(4,.713);kc.data[li].color=(1,1,1,1)
km.uv_layers.active=ku
be=ko.modifiers.new('Worn block arris','BEVEL');be.width=.045;be.segments=1;be.limit_method='ANGLE'
kh=bpy.data.objects.new('HP_PlainKerb',km.copy());HP.objects.link(kh);be=kh.modifiers.new('Rounded worn edges','BEVEL');be.width=.065;be.segments=3;kh.hide_render=True;kh.hide_viewport=True
tris=export(ob,OUT/'SS_BSA_FrostSpire.fbx');kerbtris=export(ko,OUT/'SS_BSA_PlainKerb.fbx');bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'frost_crystal_cluster_tris':tris,'crystal_shards_per_cluster':3,'frost_plain_kerb_tris':kerbtris,'highpoly_sources':[hp.name,kh.name],'source':str(SRC)}

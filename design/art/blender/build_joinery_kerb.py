"""Warm oak mortise-tenon curb modules with forged bronze joint wedges."""
import bpy
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps');OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA';SRC=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
COL=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'));HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'));MAT=bpy.data.materials['SS_M_BoardShapeA']
for name in ('SS_BSA_JoineryKerb','HP_JoineryKerb_TenonOak'):
    ob=bpy.data.objects.get(name)
    if ob:bpy.data.objects.remove(ob,do_unlink=True)
V=[];F=[];SW=[]
def box(center,size,sw):
    cx,cy,cz=center;sx,sy,sz=[v*.5 for v in size];base=len(V)
    V.extend([(cx-sx,cy-sy,cz-sz),(cx+sx,cy-sy,cz-sz),(cx+sx,cy+sy,cz-sz),(cx-sx,cy+sy,cz-sz),(cx-sx,cy-sy,cz+sz),(cx+sx,cy-sy,cz+sz),(cx+sx,cy+sy,cz+sz),(cx-sx,cy+sy,cz+sz)])
    for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):F.append(tuple(base+i for i in face));SW.append(sw)
# Broad oak beam; tenon shoulders on each end and a bronze wedge/peg at every joint.
box((0,0,.10),(.968,.44,.52),2)
for x in (-.425,.425):
    box((x,0,.10),(.12,.48,.34),2)
    box((x,-.231,.10),(.20,.035,.39),3)
    for z in (-.03,.23):box((x,-.254,z),(.075,.055,.075),3)
    box((x,.231,.10),(.14,.035,.30),3)
m=bpy.data.meshes.new('Joinery_OakTenonKerb_Mesh');m.from_pydata(V,[],F);m.update();o=bpy.data.objects.new('SS_BSA_JoineryKerb',m);COL.objects.link(o);m.materials.append(MAT)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');color=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
    sw=SW[p.index];axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][axis]
    for li in p.loop_indices:
        q=m.vertices[m.loops[li].vertex_index].co;u=.045+.91*(q[axes[0]]-mn[axes[0]])/max(.001,mx[axes[0]]-mn[axes[0]]);v=.045+.91*(q[axes[1]]-mn[axes[1]])/max(.001,mx[axes[1]]-mn[axes[1]])
        uv.data[li].uv=((sw%4+u)/4,(sw//4+v)/4);style.data[li].uv=(4,.713);color.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
be=o.modifiers.new('Rounded oak arris','BEVEL');be.width=.018;be.segments=1;be.limit_method='ANGLE'
hp=bpy.data.objects.new('HP_JoineryKerb_TenonOak',m.copy());HP.objects.link(hp);sub=hp.modifiers.new('Oak endgrain and tool marks','SUBSURF');sub.levels=2;sub.render_levels=2;tex=bpy.data.textures.new('JoineryKerbOakGrain',type='WOOD');tex.noise_scale=.075;dis=hp.modifiers.new('Long grain relief','DISPLACE');dis.texture=tex;dis.strength=.010;dis.mid_level=.52;hp.hide_render=True;hp.hide_viewport=True
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
bpy.ops.export_scene.fbx(filepath=str(OUT/'SS_BSA_JoineryKerb.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC));result={'wood_beam_faces':len(m.polygons),'wood_swatch':2,'bronze_wedge_swatch':3,'highpoly':hp.name,'source':str(SRC)}

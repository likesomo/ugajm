"""Joinery kerb LOD: oak arrises remain bevelled; eight tiny brass fittings use crisp edges.
Same silhouette, atlas slots, dimensions and joints as SS_BSA_JoineryKerb; no crust change.
"""
import bpy
from pathlib import Path
ROOT=Path('C:/unityjam/shared-assets/seven-steps')
OUT=ROOT/'Assets/Art/RuinsHenge/PlanetDressing'
bpy.ops.wm.read_factory_settings(use_empty=True)
V=[];F=[];SW=[];WOOD=[]
def box(center,size,sw):
 cx,cy,cz=center;sx,sy,sz=[v*.5 for v in size];base=len(V)
 V.extend([(cx-sx,cy-sy,cz-sz),(cx+sx,cy-sy,cz-sz),(cx+sx,cy+sy,cz-sz),(cx-sx,cy+sy,cz-sz),(cx-sx,cy-sy,cz+sz),(cx+sx,cy-sy,cz+sz),(cx+sx,cy+sy,cz+sz),(cx-sx,cy+sy,cz+sz)])
 for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):F.append(tuple(base+i for i in face));SW.append(sw)
 if sw==2:WOOD.extend(range(base,base+8))
box((0,0,.10),(.968,.44,.52),2)
for x in (-.425,.425):
 box((x,0,.10),(.12,.48,.34),2);box((x,-.231,.10),(.20,.035,.39),3)
 for z in (-.03,.23):box((x,-.254,z),(.075,.055,.075),3)
 box((x,.231,.10),(.14,.035,.30),3)
m=bpy.data.meshes.new('PD_JoineryKerbLite');m.from_pydata(V,[],F);m.update();o=bpy.data.objects.new('PD_JoineryKerbLite',m);bpy.context.scene.collection.objects.link(o)
uv=m.uv_layers.new(name='UVMap');style=m.uv_layers.new(name='Style');color=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mn=[min(v.co[i] for v in m.vertices) for i in range(3)];mx=[max(v.co[i] for v in m.vertices) for i in range(3)]
for p in m.polygons:
 sw=SW[p.index];axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=[(1,2),(0,2),(0,1)][axis]
 for li in p.loop_indices:
  q=m.vertices[m.loops[li].vertex_index].co;u=.045+.91*(q[axes[0]]-mn[axes[0]])/max(.001,mx[axes[0]]-mn[axes[0]]);v=.045+.91*(q[axes[1]]-mn[axes[1]])/max(.001,mx[axes[1]]-mn[axes[1]])
  uv.data[li].uv=((sw%4+u)/4,(sw//4+v)/4);style.data[li].uv=(4,.713);color.data[li].color=(1,1,1,1)
m.uv_layers.active=uv
vg=o.vertex_groups.new(name='Oak arrises');vg.add(WOOD,1,'REPLACE')
be=o.modifiers.new('Oak bevels only','BEVEL');be.width=.018;be.segments=1;be.limit_method='VGROUP';be.vertex_group=vg.name
o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=be.name)
tri=o.modifiers.new('Export triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
bpy.ops.export_scene.fbx(filepath=str(OUT/'PD_JoineryKerbLite.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Source/Blender/PlanetDressing/PD_JoineryKerbLite.blend'))
print('KERB_LOD_TRIS',len(o.data.polygons))

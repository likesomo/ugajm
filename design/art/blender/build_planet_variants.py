"""Build low-poly hollow-star opening and retained high-poly crater source via Blender MCP."""
import bpy, math, random
from pathlib import Path
from mathutils import Vector
import bmesh

ROOT=Path('C:/unityjam/shared-assets/seven-steps')
OUT=ROOT/'Assets/Art/RuinsHenge/BoardShapeA/SS_BSA_HollowStar.fbx'
SOURCE=ROOT/'Source/Blender/SS_BoardShapeA_planetsR2.blend'
LIB=next(c for c in bpy.context.scene.collection.children if c.name.startswith('01_EXPORT_LIBRARY_BSA'))
HP=next(c for c in bpy.context.scene.collection.children if c.name.startswith('03_HIGH_POLY_SOURCES'))
if HP is None:
    HP=bpy.data.collections.new('03_HIGH_POLY_SOURCES');bpy.context.scene.collection.children.link(HP)
    bpy.context.scene.collection.children.link(HP)

base=bpy.data.objects['SS_BSA_HollowArch']
material=base.data.materials[0]
old=bpy.data.objects.get('SS_BSA_HollowStar')
if old:bpy.data.objects.remove(old,do_unlink=True)
old=bpy.data.objects.get('HP_HollowStar_Craters')
if old:bpy.data.objects.remove(old,do_unlink=True)

N=40; RINGS=7; front=[]; back=[]
def radial(t,theta,side):
    # Five-point aperture, inset into a chipped basalt rim.
    points=1.0+.34*math.cos(5*theta+.24)+.055*math.sin(9*theta-.6)
    innerx=.43*points; innerz=.54*points
    outer_noise=1+.035*math.sin(7*theta+.4)+.022*math.sin(13*theta-1.2)+.012*math.sin(19*theta+.7)
    outerx=.98*outer_noise; outerz=1.16*outer_noise
    cx=math.cos(theta); sz=math.sin(theta)
    ix=cx*innerx; iz=-1.42+sz*innerz
    ox=cx*outerx; oz=-1.42+sz*outerz
    x=ix*(1-t)+ox*t; z=iz*(1-t)+oz*t
    # Irregular pumice vesicles: individual shallow depressions and broken lips.
    y=side*.24
    if side<0:
        for k,(angle,t0,depth,radius) in enumerate(((.25,.56,.047,.075),(1.10,.68,.038,.062),(2.05,.43,.052,.082),(3.26,.60,.034,.060),(4.42,.50,.050,.078),(5.34,.74,.036,.056))):
            da=(theta-angle+math.pi)%(2*math.pi)-math.pi
            d=math.sqrt((da*.66)**2+((t-t0)*.78)**2)
            y+=depth*math.exp(-(d/radius)**2)-.014*math.exp(-((d-radius*1.55)/(radius*.32))**2)
    return (x,y,z)

verts=[];faces=[];front_ids=[];back_ids=[]
for side,target in ((-1,front),(1,back)):
    for r in range(RINGS+1):
        loop=[]
        t=r/RINGS
        for i in range(N):
            loop.append(len(verts));verts.append(radial(t,i*2*math.pi/N,side))
        target.append(loop)
    for r in range(RINGS):
        for i in range(N):
            j=(i+1)%N
            faces.append((target[r][i],target[r][j],target[r+1][j],target[r+1][i]))
# Close the chipped outer and five-point star inner walls through the stone slab.
for edge in (0,RINGS):
    for i in range(N):
        j=(i+1)%N
        faces.append((front[edge][i],back[edge][i],back[edge][j],front[edge][j]))

mesh=bpy.data.meshes.new('HollowStar_PumiceLowPoly_Mesh')
mesh.from_pydata(verts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(mesh);bm.free();mesh.update()
obj=bpy.data.objects.new('SS_BSA_HollowStar',mesh);LIB.objects.link(obj)
mesh.materials.append(material)
uv=mesh.uv_layers.new(name='UVMap');style=mesh.uv_layers.new(name='Style')
color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
mins=[min(v.co[i] for v in mesh.vertices) for i in range(3)];maxs=[max(v.co[i] for v in mesh.vertices) for i in range(3)]
for poly in mesh.polygons:
    for li in poly.loop_indices:
        v=mesh.vertices[mesh.loops[li].vertex_index].co
        u=.035+.93*(v.x-mins[0])/max(.001,maxs[0]-mins[0])
        w=.035+.93*(v.z-mins[2])/max(.001,maxs[2]-mins[2])
        uv.data[li].uv=((8%4+u)/4,(8//4+w)/4)
        style.data[li].uv=(4.0,.418)
        color.data[li].color=(1,1,1,1)
mesh.uv_layers.active=uv

# Preserve an editable, subdivided crater-relief sculpt source. The slot-8
# high-poly relief from stone_recipes.py bakes the shared pumice normal/AO map.
high=bpy.data.objects.new('HP_HollowStar_Craters',mesh.copy());HP.objects.link(high)
sub=high.modifiers.new('Basalt erosion subdivision','SUBSURF');sub.levels=2;sub.render_levels=2
tex=bpy.data.textures.new('HollowStar_BasaltClouds',type='CLOUDS');tex.noise_scale=.095;tex.noise_depth=2
dis=high.modifiers.new('Vesicle and spall relief','DISPLACE');dis.texture=tex;dis.strength=.028;dis.mid_level=.52;dis.texture_coords='GLOBAL'
high.hide_render=True;high.hide_viewport=True

obj.data.calc_loop_triangles();triangles=len(obj.data.loop_triangles)
bpy.ops.object.select_all(action='DESELECT');obj.hide_set(False);obj.hide_render=False;obj.select_set(True);bpy.context.view_layer.objects.active=obj
OUT.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(OUT),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO',use_custom_props=False)
obj.hide_set(True);obj.hide_render=True
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
result={'asset':str(OUT),'source':str(SOURCE),'lowpoly_tris':triangles,'highpoly_subdivision':2,'opening':'five-point star','baked_surface_slot':8}

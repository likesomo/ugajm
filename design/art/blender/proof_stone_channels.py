"""Export readable bake channels outside Assets; no extra runtime textures."""
import bpy,json,numpy as np
from pathlib import Path
scene=bpy.data.scenes['BoardShapeA']
material=next(o for o in scene.objects if o.type=='MESH').data.materials[0]
nr=next(n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.filepath.endswith('BSA_NormalRough.png'))
color=next(n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.filepath.endswith('BSA_ColorAO.png'))
data=np.array(nr.pixels[:],dtype=np.float32).reshape(1024,1024,4)
rgb=np.array(color.pixels[:],dtype=np.float32).reshape(1024,1024,4)
normal=data.copy();xy=data[:,:,:2]*2-1;normal[:,:,2]=np.sqrt(np.clip(1-(xy*xy).sum(axis=2),0,1))*.5+.5
rough=np.repeat(data[:,:,2:3],4,axis=2);ao=np.repeat(data[:,:,3:4],4,axis=2)
output=Path('C:/unityjam/shared-assets/seven-steps/concepts/themes/board-shape-2026-11-10-r2/unity/stone-pass/after')
for name,pixels in [('color',rgb),('normal',normal),('roughness',rough),('ao',ao)]:
    pixels[:,:,3]=1;im=bpy.data.images.new('BSA_Proof_'+name,1024,1024,alpha=True)
    im.colorspace_settings.name='sRGB' if name=='color' else 'Non-Color';im.pixels.foreach_set(pixels.ravel())
    im.filepath_raw=str(output/('blender-bake-'+name+'.png'));im.file_format='PNG';im.save()
rows=[]
for slot in (0,1,8,9,10,11,12,13,14,15):
    patch=data[slot//4*256:(slot//4+1)*256,slot%4*256:(slot%4+1)*256]
    rows.append({'slot':slot,'roughnessMin':float(patch[:,:,2].min()),'roughnessMax':float(patch[:,:,2].max()),'aoMin':float(patch[:,:,3].min())})
(output/'packed-channel-proof.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
result={'proofs':str(output),'runtime_atlas_count':2,'channels':rows}

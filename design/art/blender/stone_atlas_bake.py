"""Executed in the owned BoardShapeA build namespace, through Blender MCP."""
def bake_atlas():
    import numpy as np
    n=128;W=H=1024
    for o in preview.objects:o.hide_render=True
    normal=bpy.data.images.new('BSA_Preview_TangentNormal',width=W,height=H,alpha=True);normal.colorspace_settings.name='Non-Color'
    ao=bpy.data.images.new('BSA_BakedAO_Temporary',width=W,height=H,alpha=True);ao.colorspace_settings.name='Non-Color'
    material.node_tree.nodes.clear()
    bs=material.node_tree.nodes.new('ShaderNodeBsdfPrincipled');out=material.node_tree.nodes.new('ShaderNodeOutputMaterial');material.node_tree.links.new(bs.outputs['BSDF'],out.inputs[0])
    target=material.node_tree.nodes.new('ShaderNodeTexImage');target.image=normal;material.node_tree.nodes.active=target
    lows=[];highs=[];pixels=np.ones((H,W,4),dtype=np.float32);rough=np.full((H,W),.72,dtype=np.float32);source_ao=np.ones((H,W),dtype=np.float32)
    # Each recipe becomes real displaced high-poly relief, then Cycles bakes it
    # selected-to-active onto a low-poly receiver. One swatch represents ~1m.
    for swatch in range(16):
        rgb,height,r,a=recipe(swatch)
        if swatch not in STONE_SLOTS:
            yy,xx=np.mgrid[0:256,0:256];rng=np.random.default_rng(900+swatch)
            rgb=np.ones((256,256,3),dtype=np.float32)*COLORS[swatch]
            grain=(rng.random((256,256))-.5)*.04;rgb*=1+grain[:,:,None]
            if swatch==2:
                line=np.sin(yy*.24+np.sin(xx*.035)*1.7)
                rgb*=.86+.18*line[:,:,None]**2;height=line*.0035
            if swatch in (4,5):
                line=np.abs((xx/255*3)%1-.5)*1.5
                cut=np.abs(yy/255-line-.12)<.025;height=np.where(cut,-.012,height*.3)
                rgb*=np.where(cut,.77,1)[:,:,None]
            r[:]=.56 if swatch in (3,4,7) else .79
        ys=slice(swatch//4*256,(swatch//4+1)*256);xs=slice(swatch%4*256,(swatch%4+1)*256)
        pixels[ys,xs,:3]=rgb;rough[ys,xs]=r;source_ao[ys,xs]=a
        xoff=(swatch%4)*1.2;yoff=(swatch//4)*1.2
        low=mesh_obj('BAKE_Low_'+str(swatch),[(xoff,yoff,0),(xoff+1,yoff,0),(xoff+1,yoff+1,0),(xoff,yoff+1,0)],[(0,1,2,3)],swatch)
        for li,co in enumerate([(0,0),(1,0),(1,1),(0,1)]):low.data.uv_layers.active.data[li].uv=((swatch%4+co[0])/4,(swatch//4+co[1])/4)
        indices=np.rint(np.linspace(0,255,n+1)).astype(int);verts=[];faces=[]
        for iy in range(n+1):
            for ix in range(n+1):verts.append((xoff+ix/n,yoff+iy/n,float(height[indices[iy],indices[ix]])))
        for iy in range(n):
            for ix in range(n):
                k=iy*(n+1)+ix;faces.append((k,k+1,k+n+2,k+n+1))
        high=mesh_obj('BAKE_HighRelief_'+str(swatch),verts,faces,swatch);lows.append(low);highs.append(high)
    scene.render.bake.use_selected_to_active=True;scene.render.bake.cage_extrusion=.05;scene.render.bake.max_ray_distance=.12;scene.render.bake.margin=4;scene.render.bake.use_clear=False
    for image,kind in [(normal,'NORMAL'),(ao,'AO')]:
        target.image=image
        for low,high in zip(lows,highs):
            bpy.ops.object.select_all(action='DESELECT');low.hide_set(False);high.hide_set(False);low.hide_render=False;high.hide_render=False
            low.select_set(True);high.select_set(True);bpy.context.view_layer.objects.active=low;bpy.ops.object.bake(type=kind)
    a=np.array(ao.pixels[:],dtype=np.float32).reshape(H,W,4)[:,:,0]*source_ao
    pixels[:,:,:3]*=(.78+.22*a[:,:,None]);pixels[:,:,3]=0
    pixels[:512,:,3]=bake_mesh_ao_and_shadow_profiles()
    color=bpy.data.images.new('BSA_Color_AO',width=W,height=H,alpha=True);color.colorspace_settings.name='sRGB';color.alpha_mode='CHANNEL_PACKED';color.pixels.foreach_set(pixels.ravel())
    color.filepath_raw=str(OUT/'BSA_ColorAO.png');color.file_format='PNG';color.save()
    normals=np.array(normal.pixels[:],dtype=np.float32).reshape(H,W,4)
    packed=normals.copy();packed[:,:,2]=rough;packed[:,:,3]=a
    nr=bpy.data.images.new('BSA_NormalRG_Roughness_AO',width=W,height=H,alpha=True);nr.colorspace_settings.name='Non-Color';nr.alpha_mode='CHANNEL_PACKED';nr.pixels.foreach_set(packed.ravel())
    nr.filepath_raw=str(OUT/'BSA_NormalRough.png');nr.file_format='PNG';nr.save()
    # Outside Assets, .blend retains true XYZ normals for the Cycles preview.
    # Runtime uses RG normals / B roughness / A AO as linear data, never DXT5nm.
    normal.pack()
    for low,high in zip(lows,highs):bpy.data.objects.remove(low,do_unlink=True);bpy.data.objects.remove(high,do_unlink=True)
    for o in preview.objects:o.hide_render=False
    nodes=material.node_tree.nodes;links=material.node_tree.links
    tex=nodes.new('ShaderNodeTexImage');tex.image=color
    vc=nodes.new('ShaderNodeVertexColor');vc.layer_name='Color'
    mult=nodes.new('ShaderNodeMixRGB');mult.blend_type='MULTIPLY';mult.inputs[0].default_value=1
    links.new(tex.outputs['Color'],mult.inputs[1]);links.new(vc.outputs['Color'],mult.inputs[2]);links.new(mult.outputs[0],bs.inputs['Base Color'])
    target.image=normal;nm=nodes.new('ShaderNodeNormalMap');links.new(target.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
    rt=nodes.new('ShaderNodeTexImage');rt.image=nr;sep=nodes.new('ShaderNodeSeparateColor');links.new(rt.outputs['Color'],sep.inputs[0])
    geo=nodes.new('ShaderNodeNewGeometry');xyz=nodes.new('ShaderNodeSeparateXYZ');links.new(geo.outputs['Normal'],xyz.inputs[0])
    slope=nodes.new('ShaderNodeMath');slope.operation='MULTIPLY_ADD';slope.inputs[1].default_value=-.32;slope.inputs[2].default_value=.20;links.new(xyz.outputs['Z'],slope.inputs[0])
    add=nodes.new('ShaderNodeMath');add.operation='ADD';add.use_clamp=True;links.new(sep.outputs['Blue'],add.inputs[0]);links.new(slope.outputs[0],add.inputs[1]);links.new(add.outputs[0],bs.inputs['Roughness'])
    bpy.data.images.remove(ao)

"""Deterministic stone bake sources, in linear colour and metres (one swatch ~= 1m).

No polygon crack network. Only granite variants 14/15 have one/two sparse cracks;
other dark lines are mineral veins, bedding or tool marks, not fracture outlines.
Run by build_board_shape_a.py inside Blender MCP, not a runtime texture generator.
"""
import numpy as np

STONE_SLOTS={0:'granite',1:'granite_side',8:'basalt_pumice',9:'sandstone',10:'marble_quartz',11:'slate',12:'limestone_travertine',13:'sarsen',14:'granite',15:'granite'}

def recipe(slot, size=256):
    rng=np.random.default_rng(410+slot);v,u=np.mgrid[0:size,0:size]/(size-1)
    def cloud(cells):
        coarse=rng.random((cells+1,cells+1));axis=np.linspace(0,cells-.0001,size);i=axis.astype(int);t=axis-i;t=t*t*(3-2*t)
        a=coarse[i[:,None],i[None,:]];b=coarse[i[:,None],i[None,:]+1]
        c=coarse[i[:,None]+1,i[None,:]];d=coarse[i[:,None]+1,i[None,:]+1]
        return (a*(1-t[None,:])+b*t[None,:])*(1-t[:,None])+ (c*(1-t[None,:])+d*t[None,:])*t[:,None]
    micro=rng.random((size,size));large=cloud(5);fine=cloud(49)
    h=(fine-.5)*.0015;rough=np.full((size,size),.61);ao=np.ones((size,size))
    base={0:(.43,.45,.44),1:(.37,.39,.38),8:(.12,.14,.15),9:(.48,.34,.19),10:(.64,.69,.70),11:(.16,.21,.23),12:(.55,.51,.40),13:(.38,.34,.26),14:(.43,.45,.44),15:(.43,.45,.44)}.get(slot,(.5,.5,.5))
    rgb=np.ones((size,size,3))*base;rgb*=1+(large[:,:,None]-.5)*.10+(fine[:,:,None]-.5)*.08
    if slot in (0,1,14,15):
        # Irregular interlocking crystal domains: feldspar, quartz, sparse dark mica.
        # 6–8mm grains at one metre; larger patches read as camouflage/concrete.
        cells=140;px=u*cells;py=v*cells;cx=np.floor(px).astype(int);cy=np.floor(py).astype(int)
        sites=rng.random((cells+3,cells+3,2));phases=rng.random((cells+3,cells+3));dist=np.full((size,size),99.);phase=np.zeros((size,size))
        for oy in (-1,0,1):
            for ox in (-1,0,1):
                ix=cx+ox+1;iy=cy+oy+1;s=sites[iy,ix];d=(cx+ox+s[:,:,0]-px)**2+(cy+oy+s[:,:,1]-py)**2
                pick=d<dist;phase=np.where(pick,phases[iy,ix],phase);dist=np.minimum(dist,d)
        minerals=np.where((phase<.07)[:,:,None],(.045,.052,.055),np.where((phase<.40)[:,:,None],(.65,.67,.66),(.57,.57,.55)))
        rgb=rgb*.75+minerals*.25;rough=np.where(phase<.07,.30,np.where(phase<.40,.43,.61))
        h+=(phase-.5)*.0011;h-=np.clip((dist-.33)*.005,0,.0010)
        # One or two isolated, interrupted hairline fractures on the TOP variants only.
        for k in range(0 if slot not in (14,15) else slot-13):
            path=.23+k*.39+.035*np.sin(v*9+k*3);crack=np.exp(-((u-path)/.0035)**2)*np.clip((v-.12)*5,0,1)*np.clip((.88-v)*6,0,1)
            h-=crack*.006;rgb*=1-crack[:,:,None]*.34;ao*=1-crack*.38
    elif slot==8:
        # Dense basalt with local porous pumice patches, not purple granite.
        pumice=np.clip((large-.52)*4,0,.55);rgb+=pumice[:,:,None]*np.array((.22,.22,.20));rough[:]=.84
        for k in range(63):
            x,y=rng.random(2);rx=rng.uniform(.005,.022);ry=rx*rng.uniform(.6,1.7)
            pit=np.exp(-(((u-x)/rx)**2+((v-y)/ry)**2)*2)
            h-=pit*.009;rgb*=1-pit[:,:,None]*.52;ao*=1-pit*.43
    elif slot in (9,13):
        # Bedding is directional and low contrast; individual sand grains remain visible.
        bedding=np.sin(v*(38 if slot==9 else 110)+np.sin(u*7)*.7+large*2.7)
        h+=bedding*(.0012 if slot==9 else .0007);rgb*=1+bedding[:,:,None]*(.035 if slot==9 else .035)
        rgb+=(micro[:,:,None]-.5)*(.065 if slot==9 else .040);rough[:]=.78 if slot==9 else .72
        if slot==13:rgb*=1-np.clip((cloud(11)-.58)*1.2,0,.24)[:,:,None]
    elif slot==10:
        # Two mineral veins in a fine calcite body; bright angular quartz inclusions.
        for k in range(2):
            path=.18+k*.52+.12*np.sin(v*3.8+k*2)+.045*np.sin(v*11+k*3)+.02*np.sin(v*43)
            vein=np.exp(-((u-path)/(.009+large*.009))**2)
            rgb=rgb*(1-vein[:,:,None]*.14);h-=vein*.0007
            branch=np.exp(-((u-path-.11*np.maximum(v-.4,0))/.003)**2)*np.clip((v-.4)*3,0,1)
            rgb*=1-branch[:,:,None]*.05
        quartz=np.clip((fine-.72)*3,0,.32);rgb+=quartz[:,:,None]*.22;rough[:]=.34
    elif slot==11:
        folia=np.sin(v*112+np.sin(u*6)*.7+large*.7)
        rgb*=1+folia[:,:,None]*.045;h+=np.maximum(0,folia)*.0014;rough[:]=.77
        # Sparse split flakes, all aligned with the foliation plane.
        for k in range(12):
            x,y=rng.random(2);flake=np.clip(1-np.abs(u-x)/.12,0,1)*np.exp(-((v-y)/.005)**2)
            h-=flake*.004;rgb*=1-flake[:,:,None]*.18
    elif slot==12:
        layers=np.sin(v*50+np.sin(u*8)*.5);rgb*=1+layers[:,:,None]*.055;h+=layers*.001
        for k in range(38):
            x,y=rng.random(2);pore=np.exp(-(((u-x)/rng.uniform(.004,.016))**2+((v-y)/rng.uniform(.008,.035))**2)*2)
            h-=pore*.006;rgb*=1-pore[:,:,None]*.25;ao*=1-pore*.36
        rough[:]=.79
    if slot in STONE_SLOTS:
        # Short, irregular chisel strikes, separate from geological bedding.
        for k in range(14 if slot in (1,9,11,13) else 6):
            x,y=rng.random(2);mark=np.clip(1-np.abs(u-x)/rng.uniform(.025,.055),0,1)*np.exp(-((v-y)/.0025)**2)
            h-=mark*.0025;rgb*=1-mark[:,:,None]*.055
        # Water descends from the upper edge. Lichen is isolated dull speckling.
        cloud(21) # Keep subsequent lichen/spall seeds stable when changing only water colour.
        water=np.random.default_rng(2000+slot).random(24)
        columns=np.interp(u[0],np.linspace(0,1,24),water)[None,:]
        streak=np.clip((columns-.58)*1.9,0,.40)*np.clip(v*1.35,0,1)*(.8+.2*np.cos(v*5+u*3))
        rgb*=1-streak[:,:,None]*np.array((.18,.16,.10));rough+=streak*.18
        lichen=np.clip((cloud(37)-.70)*4,0,.33)*(large>.47)
        if slot!=10:rgb=rgb*(1-lichen[:,:,None]*.38)+np.array((.34,.35,.28))*lichen[:,:,None]*.38
        # Small spalls at discontinuous edges, never a polygon web across every face.
        edge=np.minimum.reduce((u,v,1-u,1-v));spall=np.clip((.027-edge)/.027,0,1)*np.clip((cloud(19)-.47)*3,0,1)
        h-=spall*.009;rgb*=1-spall[:,:,None]*.10;ao*=1-spall*.20
        rough+=spall*.12+(micro-.5)*.07
    return np.clip(rgb,0,1).astype(np.float32),h.astype(np.float32),np.clip(rough,.22,.98).astype(np.float32),ao.astype(np.float32)

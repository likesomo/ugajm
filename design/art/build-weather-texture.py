"""Original tileable weather channels, no image libraries or third-party assets.
R: broken mortar and stone wear, G: irregular moss islands, B: warped timber grain.
"""
import math,random,struct,zlib
from pathlib import Path
size=256
out=Path('C:/unityjam/shared-assets/seven-steps/Assets/Art/RuinsHenge/Textures/RH_Weather256.png')
rng=random.Random(74)
grids={n:[[rng.random() for _ in range(n)] for _ in range(n)] for n in [2,4,8,16,32]}
def noise(u,v,n):
 x,y=(u%1)*n,(v%1)*n;ix,iy=int(x),int(y);a,b=x-ix,y-iy;a=a*a*(3-2*a);b=b*b*(3-2*b);g=grids[n]
 return (g[iy%n][ix%n]*(1-a)+g[iy%n][(ix+1)%n]*a)*(1-b)+(g[(iy+1)%n][ix%n]*(1-a)+g[(iy+1)%n][(ix+1)%n]*a)*b
def byte(x):return max(0,min(255,round(x*255)))
rows=[]
for y in range(size):
 row=bytearray([0]);v=y/size
 for x in range(size):
  u=x/size;fine=rng.random();wear=.5*noise(u,v,4)+.3*noise(u,v,8)+.2*noise(u,v,16)
  edge=(y%64)<(1+int(noise(u,v,32)*2)) or ((x+(y//64%2)*32)%64)<(1+int(noise(u,v,16)*2))
  r=(.12+.12*wear) if edge else .42+.28*wear+.12*fine
  g=.42*noise(u,v,4)+.32*noise(u,v,8)+.18*noise(u,v,16)+.08*fine
  # Broad, rough moss patches instead of repeating sinusoidal ornaments.
  g=max(0,min(1,(g-.25)*2))
  warp=.8*noise(u,v,4)+.3*noise(u,v,16)
  grain=math.sin(math.tau*(u*16+warp))
  b=.42+.12*grain+.22*wear+.06*fine
  row.extend([byte(r),byte(g),byte(b),255])
 rows.append(row)
def chunk(kind,data):return struct.pack('>I',len(data))+kind+data+struct.pack('>I',zlib.crc32(kind+data)&0xffffffff)
out.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',size,size,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows),9))+chunk(b'IEND',b''))
print(out)

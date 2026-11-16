"""Create original symbolic prototype UI assets and a Godot v2 resource pack."""
from pathlib import Path
from PIL import Image,ImageDraw
import re,struct,hashlib,json
root=Path(__file__).resolve().parents[1]
assets=root/'assets'/'ArtuMod'/'images';assets.mkdir(parents=True,exist_ok=True)
classes=[]
for path in (root/'src').glob('*.cs'):
 classes+=re.findall(r'public (?:sealed )?class (\w+)',path.read_text(encoding='utf8'))
classes=sorted(set(classes))
def texture(name):
 w,h=(250,190)
 image=Image.new('RGB',(w,h),(20,24,30));d=ImageDraw.Draw(image)
 grade=next((i for i,g in enumerate(['Stone','Bronze','Silver','Gold']) if name.startswith(g)),None)
 colors=[(125,130,131),(180,115,65),(194,205,213),(229,180,75)]
 c=colors[grade] if grade is not None else (206,173,109)
 for y in range(h):d.line((0,y,w,y),fill=(20+y//15,24+y//20,30+y//17))
 d.rounded_rectangle((12,12,w-13,h-13),radius=12,outline=c,width=3)
 d.ellipse((70,39,180,149),outline=c,width=3)
 if 'Slaughter' in name:
  d.polygon([(116,118),(140,61),(146,45),(128,55),(105,112)],fill=c);d.line((94,106,135,127),fill=c,width=7);d.line((112,118,100,139),fill=c,width=7)
 elif 'Conquest' in name or 'Tyrant' in name:
  d.polygon([(87,113),(83,77),(106,99),(125,60),(144,99),(167,77),(163,113)],fill=c);d.rectangle((87,118,163,128),fill=c)
 elif 'Luxury' in name or 'Coin' in name:
  d.ellipse((89,64,143,118),outline=c,width=5);d.ellipse((111,80,165,134),outline=c,width=5);d.line((138,93,138,121),fill=c,width=3)
 elif 'Lust' in name:
  d.arc((99,53,151,107),0,180,fill=c,width=5);d.line((99,80,109,53,141,53,151,80),fill=c,width=4);d.line((125,108,125,137),fill=c,width=4);d.line((109,137,141,137),fill=c,width=4)
 elif 'Box' in name:
  d.rectangle((85,80,165,130),outline=c,width=5);d.polygon([(85,80),(104,61),(145,61),(165,80)],outline=c);d.line((100,105,150,105),fill=c,width=2)
 else:
  d.polygon([(86,63),(122,71),(125,133),(87,124)],outline=c);d.polygon([(128,71),(164,63),(163,124),(128,133)],outline=c)
  for y in [82,93,104,115]:d.line((94,y,113,y+3),fill=c,width=2);d.line((138,y+3,157,y),fill=c,width=2)
 if grade is not None:
  for i in range(grade+1):d.ellipse((102+i*13,158,108+i*13,164),fill=c)
 image.save(assets/(name.lower()+'.png'))
 raw=', '.join(map(str,image.tobytes()))
 text=f'[gd_resource type="ImageTexture" load_steps=2 format=3]\n\n[sub_resource type="Image" id="Image_art"]\ndata = {{\n"data": PackedByteArray({raw}),\n"format": "RGB8",\n"height": {h},\n"mipmaps": false,\n"width": {w}\n}}\n\n[resource]\nimage = SubResource("Image_art")\n'
 (assets/(name.lower()+'.tres')).write_text(text,encoding='utf8')
for name in classes:texture(name)
files={f'ArtuMod/images/{p.relative_to(assets).as_posix()}':p.read_bytes() for p in assets.rglob('*.tres')}
pack=root/'package'/'ArtuMod'/'ArtuMod.pck'
paths=[(name.encode()+b'\0',data) for name,data in sorted(files.items())]
header_size=4*6+8+16*4+4
directory_size=sum(4+((len(p)+3)//4*4)+8+8+16+4 for p,data in paths)
offset=(header_size+directory_size+15)//16*16
records=[];blobs=[]
for p,data in paths:
 padded=p+b'\0'*((-len(p))%4)
 records.append(struct.pack('<I',len(padded))+padded+struct.pack('<QQ',offset,len(data))+hashlib.md5(data).digest()+struct.pack('<I',0))
 padding=(-len(data))%16;blobs.append(data+b'\0'*padding);offset+=len(data)+padding
with pack.open('wb') as f:
 f.write(struct.pack('<6IQ16II',0x43504447,2,4,5,1,0,0,*([0]*16),len(paths)))
 for record in records:f.write(record)
 f.write(b'\0'*((-f.tell())%16))
 for data in blobs:f.write(data)
manifest=root/'package'/'ArtuMod'/'ArtuMod.json';d=json.loads(manifest.read_text(encoding='utf8'));d['has_pck']=True;manifest.write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
print(f'Packed {len(paths)} original UI textures, {pack.stat().st_size} bytes')

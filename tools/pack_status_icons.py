"""Convert prepared PNGs to native ImageTexture resources; preserve all prior PCK entries."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, struct, hashlib, re

root = Path(__file__).resolve().parents[1]
assets = root / 'assets' / 'status-icons'
mapping = json.loads((assets / 'mapping.json').read_text(encoding='utf-8'))
resources = root / 'assets' / 'ArtuMod' / 'images' / 'status'
resources.mkdir(parents=True, exist_ok=True)

types = set()
for source in (root / 'src').glob('*.cs'):
    types.update(re.findall(r'public (?:sealed )?class (\w+Power)\(', source.read_text(encoding='utf-8-sig')))
missing = types - mapping.keys()
if missing:
    raise RuntimeError('Unmapped powers: ' + str(sorted(missing)))

for key in mapping:
    with Image.open(assets / (key.lower() + '.png')) as image:
        image = image.convert('RGBA')
        raw = ', '.join(map(str, image.tobytes()))
        text = ('[gd_resource type="ImageTexture" load_steps=2 format=3]\n\n'
                '[sub_resource type="Image" id="Image_icon"]\ndata = {\n'
                f'"data": PackedByteArray({raw}),\n"format": "RGBA8",\n'
                '"height": 128,\n"mipmaps": false,\n"width": 128\n}\n\n'
                '[resource]\nimage = SubResource("Image_icon")\n')
        (resources / (key.lower() + '.tres')).write_text(text, encoding='utf-8')

pack = root / 'package' / 'ArtuMod' / 'ArtuMod.pck'
original = pack.read_bytes()
magic, version = struct.unpack_from('<II', original)
if magic != 0x43504447 or version != 2:
    raise RuntimeError('Unsupported PCK; do not overwrite')
flags, base = struct.unpack_from('<IQ', original, 20)
if flags or base:
    raise RuntimeError('PCK flags/base unsupported; do not overwrite')
count, = struct.unpack_from('<I', original, 96)
pos = 100
files = {}
for _ in range(count):
    length, = struct.unpack_from('<I', original, pos); pos += 4
    name = original[pos:pos + length].rstrip(b'\0').decode('utf-8'); pos += length
    offset, size = struct.unpack_from('<QQ', original, pos); pos += 16
    digest = original[pos:pos + 16]; pos += 16
    entry_flags, = struct.unpack_from('<I', original, pos); pos += 4
    data = original[offset:offset + size]
    if entry_flags or hashlib.md5(data).digest() != digest:
        raise RuntimeError('Corrupt/unsupported original resource: ' + name)
    files[name] = data
old_hashes = {k:hashlib.sha256(v).hexdigest() for k,v in files.items() if '/status/' not in k}
for path in resources.glob('*.tres'):
    files['ArtuMod/images/status/' + path.name] = path.read_bytes()
paths = [(k.encode() + b'\0', v) for k,v in sorted(files.items())]
offset = (100 + sum(4 + (len(p)+3)//4*4 + 36 for p,v in paths) + 15)//16*16
records, blobs = [], []
for p, data in paths:
    padded = p + b'\0' * (-len(p) % 4)
    records.append(struct.pack('<I',len(padded))+padded+struct.pack('<QQ',offset,len(data))+hashlib.md5(data).digest()+struct.pack('<I',0))
    padding = -len(data) % 16
    blobs.append(data + b'\0'*padding); offset += len(data)+padding
with pack.open('wb') as stream:
    stream.write(struct.pack('<6IQ16II',0x43504447,2,4,5,1,0,0,*([0]*16),len(paths)))
    for data in records: stream.write(data)
    stream.write(b'\0'*(-stream.tell()%16))
    for data in blobs: stream.write(data)

rows = ['# 临时状态图标来源与对应表','',
        'Icons made by Lorc and Delapouite. Available on https://game-icons.net.',
        'License: CC BY 3.0 — https://creativecommons.org/licenses/by/3.0/',
        '上游许可原文：UPSTREAM-LICENSE.txt；逐项原始 SVG：source/。',
        '改动：去除原黑色背景、按状态调色、加圆角底板并缩小留白、转换为128×128 PNG及Godot ImageTexture。',
        '图形临时占位，不改变状态规则、层数、计时或存档。','',
        '| 状态 | 程序标识 | 图形 / 作者 / 来源 |','| --- | --- | --- |']
for key,(label,source,tone) in mapping.items():
    author, name = source.split('/')
    rows.append(f'| {label} | `{key}` | [{name} / {author.title()}](https://game-icons.net/1x1/{source}.html) |')
(assets / '来源与对应表.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')

# Contact sheet at both hover-size and small in-combat size.
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',17)
sheet = Image.new('RGB',(1000,((len(mapping)+4)//5)*170),(29,33,41))
draw = ImageDraw.Draw(sheet)
for i,(key,(label,_,_)) in enumerate(mapping.items()):
    x,y = (i%5)*200,(i//5)*170
    with Image.open(assets / (key.lower()+'.png')) as image:
        sheet.paste(image.resize((86,86)),(x+34,y+8),image.resize((86,86)))
        sheet.paste(image.resize((32,32)),(x+132,y+46),image.resize((32,32)))
    draw.text((x+10,y+101),label,font=font,fill=(233,230,219))
sheet.save(assets / '图标预览.png')
report = {'powers':len(types),'icons':len(mapping),'original_entries_preserved':len(old_hashes),
          'prior_resource_sha256':old_hashes,'pck_sha256':hashlib.sha256(pack.read_bytes()).hexdigest()}
(root/'verification'/'状态图标打包核验.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Packed {len(mapping)} differentiated icons; {len(old_hashes)} prior entries preserved.')

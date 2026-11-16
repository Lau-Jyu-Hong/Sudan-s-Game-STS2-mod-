from pathlib import Path
import json,hashlib,zipfile
root=Path(__file__).resolve().parents[1]
verification=root/'verification';verification.mkdir(exist_ok=True)
report=Path('work/runtime-final.json')
data=json.loads(report.read_text(encoding='utf8'))
if data['failed']:raise RuntimeError(data['errors'])
(verification/'runtime-final.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf8')
(verification/'build.log').write_bytes(Path('work/build.log').read_bytes())
manifest={}
for path in (root/'package'/'ArtuMod').iterdir():
 if path.is_file():manifest[path.name]={'size':path.stat().st_size,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(verification/'package-hashes.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
for filename,full in [('阿尔图_Mod_v0.1_安装包.zip',False),('阿尔图_Mod_v0.1_源码工程.zip',True)]:
 with zipfile.ZipFile(root.parent/filename,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  if full:
   for path in root.rglob('*'):
    if not path.is_file():continue
    rel=path.relative_to(root)
    if any(x in rel.parts for x in ['bin','obj','backups']):continue
    z.write(path,Path('ArtuMod')/rel)
  else:
   for path in (root/'package'/'ArtuMod').iterdir():
    if path.is_file():z.write(path,Path('ArtuMod')/path.name)
   z.write(root/'README_zh-CN.md','README_zh-CN.md')
   for path in verification.iterdir():
    if path.is_file():z.write(path,Path('verification')/path.name)
 print(filename,(root.parent/filename).stat().st_size)
print('Verified checks:',data['passed'])

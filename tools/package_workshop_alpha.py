"""Local delivery only. The Workshop content folder remains limited to six release files."""
from pathlib import Path
import json, zipfile, hashlib
root=Path(__file__).resolve().parents[1]
out=Path(__file__).resolve().parents[4]/'outputs'/'ArtuMod_workshop_alpha'
out.mkdir(parents=True,exist_ok=True)
package=root/'package'/'ArtuMod'
release={p.name:{'size':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in package.iterdir() if p.is_file()}
(out/'release-hashes.json').write_text(json.dumps(release,ensure_ascii=False,indent=2),encoding='utf-8')
with zipfile.ZipFile(out/'阿尔图_v0.4.5-alpha_安装包.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in package.iterdir():
        if p.is_file():z.write(p,'ArtuMod/'+p.name)
    z.write(root/'README_zh-CN.md','README_zh-CN.md')
    z.write(root/'verification'/'发布前更新报告_v0.4.5.md','发布前更新报告_v0.4.5.md')
with zipfile.ZipFile(out/'阿尔图_v0.4.5-alpha_源码工程.zip','w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in root.rglob('*'):
        if p.is_file() and not any(x in p.relative_to(root).parts for x in ('bin','obj','backups','.git')):
            z.write(p,'ArtuMod/'+p.relative_to(root).as_posix())
print('Packaged release and source locally; no test saves, normal user settings or game libraries added.')

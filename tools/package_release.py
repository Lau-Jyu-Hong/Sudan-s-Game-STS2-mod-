"""Package original mod, portable source, and verified evidence. No game/dependency binaries."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import json, hashlib
project=Path(__file__).resolve().parents[1]
output=project.parent
version="v0.4"
manifest=json.loads((project/"verification/release-manifest.json").read_text("utf-8"))
for name,digest in manifest["files"].items():
    assert hashlib.sha256((project/"package/ArtuMod"/name).read_bytes()).hexdigest()==digest,name
with ZipFile(output/f"阿尔图_Mod_{version}_安装包.zip","w",ZIP_DEFLATED) as archive:
    for path in sorted((project/"package").rglob("*")):
        if path.is_file():archive.write(path,path.relative_to(project/"package"))
    archive.write(project/"README_zh-CN.md","README_zh-CN.md")
    for name in ["v0.4更新报告.md","v0.4奖励池80张.md","release-manifest.json"]:
        archive.write(project/"verification"/name,name)
excluded={"bin","obj","backups","__pycache__"}
preliminary={"v04-first","v04-check","v04-check2","v04-check3","v04-check4","v04-check5","v04-check6","v04-live","v04-live-final","v04-final","v04-end-act1"}
with ZipFile(output/f"阿尔图_Mod_{version}_源码工程.zip","w",ZIP_DEFLATED) as archive:
    for path in sorted(project.rglob("*")):
        relative=path.relative_to(project)
        if path.is_file() and not (excluded|preliminary).intersection(relative.parts):archive.write(path,Path("ArtuMod")/relative)
print(f"Packaged {version}: verified runtime + portable source")

"""Build a verified source/binary delta without machine-local config or game assets."""
import hashlib
import json
import sys
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

project = Path(__file__).resolve().parents[1]
base_archive, output = map(Path, sys.argv[1:3])
changed_sources = ["Main.cs", "Initialization.cs", "Relics.cs", "RewardCards.cs", "SultanCards.cs", "Powers.cs", "V03Verification.cs"]
paths = [f"src/{name}" for name in changed_sources] + [
    "package/ArtuMod/ArtuMod.dll", "package/ArtuMod/ArtuMod.json",
    "verification/v0.3.1修复报告.md", "verification/v031-smoke/runtime.json",
]
sha256 = lambda data: hashlib.sha256(data).hexdigest().upper()
files = []
with ZipFile(base_archive) as base:
    pck_hash = sha256(base.read("ArtuMod/package/ArtuMod/ArtuMod.pck"))
    assert sha256((project / "package/ArtuMod/ArtuMod.pck").read_bytes()) == pck_hash
    for relative in paths:
        entry = {"path": relative, "sha256": sha256((project / relative).read_bytes())}
        base_name = "ArtuMod/" + relative
        entry["baseSha256"] = sha256(base.read(base_name)) if base_name in base.namelist() else None
        entry["gameRelativePath"] = Path(relative).name if relative.startswith("package/") else None
        files.append(entry)
manifest = {"schemaVersion": 1, "fromVersion": "v0.3.0", "toVersion": "v0.3.1", "requiredPckSha256": pck_hash, "files": files}
output.parent.mkdir(parents=True, exist_ok=True)
if output.exists():
    raise FileExistsError(output)
with ZipFile(output, "w", ZIP_DEFLATED, compresslevel=9) as archive:
    for relative in paths:
        archive.write(project / relative, relative)
    for name in ["README_增量更新.md", "Apply-Update.ps1"]:
        archive.write(project / "tools/increment_v031" / name, name)
    archive.writestr("update-manifest.json", json.dumps(manifest, ensure_ascii=False, indent=2))
with ZipFile(output) as archive:
    assert archive.testzip() is None
    for entry in files:
        assert sha256(archive.read(entry["path"])) == entry["sha256"]
print(f"Created and verified: {output} ({output.stat().st_size} bytes)")

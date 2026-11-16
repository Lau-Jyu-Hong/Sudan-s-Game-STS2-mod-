"""Package only original mod files, source, and verification artifacts."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

project = Path(__file__).resolve().parents[1]
output = project.parent
version = "v0.3"
with ZipFile(output / f"阿尔图_Mod_{version}_安装包.zip", "w", ZIP_DEFLATED) as archive:
    for path in sorted((project / "package").rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(project / "package"))
    archive.write(project / "README_zh-CN.md", "README_zh-CN.md")
    archive.write(project / "verification" / "圆桌茶会.md", "圆桌茶会.md")
    archive.write(project / "verification" / "v0.3更新报告.md", "v0.3更新报告.md")

excluded = {"bin", "obj", "backups", "__pycache__"}
with ZipFile(output / f"阿尔图_Mod_{version}_源码工程.zip", "w", ZIP_DEFLATED) as archive:
    for path in sorted(project.rglob("*")):
        relative = path.relative_to(project)
        if path.is_file() and not excluded.intersection(relative.parts):
            archive.write(path, Path("ArtuMod") / relative)
print(f"Packaged {version}")

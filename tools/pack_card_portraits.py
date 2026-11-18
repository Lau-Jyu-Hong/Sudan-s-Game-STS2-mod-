"""Add the latest ordinary card portraits. Does not touch Sultan full-card art."""
import hashlib
from pathlib import Path

from PIL import Image, ImageOps

from build_art_preview import read_pack, texture, write_pack

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(
    r"C:\Users\Administrator\.cursor\projects"
    r"\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha\assets"
)
INSTALLED = Path(r"F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ArtuMod\ArtuMod.pck")
OUTPUT = ROOT / "design" / "cursor_art_task" / "outputs" / "ArtuMod-card-portraits-20261118.pck"
PORTRAITS = ROOT / "assets" / "card-ui" / "portraits"
NAMES = (
    "fearofsultan",
    "backroomdeal",
    "obeyorders",
    "returnonspending",
    "stateaffairs",
    "weighoptions",
    "temporaryrecruitment",
    "allarmyattack",
    "blockreprisal",
    "destructivestrike",
    "tribunal",
    "sharekingsburden",
    "allbykingsgrace",
    "hollowauthority",
    "bowtosultan",
    "indulgentlife",
    "elitetroops",
    "warsong",
    "trainarmy",
    "revolutionaryacademy",
    "armymight",
    "chivalry",
)


def main() -> None:
    PORTRAITS.mkdir(parents=True, exist_ok=True)
    entries = read_pack(INSTALLED)
    print("installed", hashlib.sha256(INSTALLED.read_bytes()).hexdigest()[:12], "files", len(entries))
    for name in NAMES:
        key = f"ArtuMod/images/{name}.tres"
        image = Image.open(SOURCE / f"{name}.jpg")
        fitted = ImageOps.fit(image.convert("RGB"), (250, 190), Image.Resampling.LANCZOS)
        fitted.save(PORTRAITS / f"{name}.png")
        entries[key] = texture(fitted, (250, 190))
        print("packed", key, fitted.size)
    if OUTPUT.exists():
        raise SystemExit(f"refuse overwrite {OUTPUT}")
    write_pack(OUTPUT, entries)
    print("wrote", OUTPUT.name, hashlib.sha256(OUTPUT.read_bytes()).hexdigest()[:12])


if __name__ == "__main__":
    main()

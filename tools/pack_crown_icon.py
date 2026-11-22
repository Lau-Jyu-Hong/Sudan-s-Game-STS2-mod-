"""Insert the prismatic crown relic icon and the three new status icons into a copy of the installed pack."""
import sys
from collections import deque
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from build_art_preview import read_pack, texture, write_pack

root = Path(__file__).resolve().parents[1]
source = Path(r"F:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\ArtuMod\ArtuMod.pck")
picture = Path(r"C:\Users\Administrator\.cursor\projects\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha\assets\usurpers-crown.jpg")
out_png = root / "assets" / "relics" / "usurperscrown.png"
out_pack = root / "package" / "ArtuMod" / "ArtuMod.ecology.pck"


def clear_corner_white(image):
    image = image.convert("RGBA")
    pixels = image.load()
    width, height = image.size
    seen = set()
    queue = deque([(0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)])
    while queue:
        x, y = queue.popleft()
        if (x, y) in seen or x < 0 or y < 0 or x >= width or y >= height:
            continue
        red, green, blue, _alpha = pixels[x, y]
        if red < 236 or green < 236 or blue < 236:
            continue
        seen.add((x, y))
        pixels[x, y] = (red, green, blue, 0)
        queue.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))
    return image


def main():
    entries = read_pack(source)
    crown = clear_corner_white(Image.open(picture))
    out_png.parent.mkdir(parents=True, exist_ok=True)
    crown.save(out_png)
    entries["ArtuMod/images/usurperscrown.tres"] = texture(crown, (128, 128), alpha=True)
    copies = {
        "ArtuMod/images/status/usurpercrownpower.tres": "ArtuMod/images/status/kingsgracepower.tres",
        "ArtuMod/images/status/scaleswaiverpower.tres": "ArtuMod/images/status/tribunalpower.tres",
        "ArtuMod/images/status/pardondecreepower.tres": "ArtuMod/images/status/passpower.tres",
    }
    for dest, src in copies.items():
        entries[dest] = entries[src]
    if out_pack.exists():
        out_pack.unlink()
    write_pack(out_pack, entries)
    print(f"packed {len(entries)} entries -> {out_pack}")


if __name__ == "__main__":
    main()

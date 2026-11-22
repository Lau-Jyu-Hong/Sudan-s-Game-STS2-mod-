"""Cut the chosen tablet frame to the game's 598 by 844 card frame.

The portrait window stays empty because the frame is drawn over the portrait.
The lower area stays opaque so the rules text has a plate. The title ribbon and
energy socket are removed; the game draws those itself, including the rarity edge.
"""

from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = Path(
    r"C:\Users\Administrator\.cursor\projects"
    r"\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha\assets"
    r"\artu_frame_tablet.jpg"
)
VANILLA = ROOT / "design" / "cursor_art_task" / "outputs" / "_preview" / "vanilla_skill_frame.png"
OUT = ROOT / "assets" / "card-ui" / "artu_card_frame.png"
# Portrait rect inside the 598x844 frame texture. The card control is 300x422.
PORTRAIT = (50, 86, 548, 466)
PLATE = (46, 34, 26, 255)


def _flood(pixels, width, height, start, test):
    if not test(pixels[start]):
        return 0
    seen = {start}
    queue = deque([start])
    while queue:
        x, y = queue.popleft()
        color = pixels[x, y]
        pixels[x, y] = (color[0], color[1], color[2], 0)
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                continue
            if test(pixels[nx, ny]):
                seen.add((nx, ny))
                queue.append((nx, ny))
    return len(seen)


def _parchment(color) -> bool:
    r, g, b = color[:3]
    return r > 145 and g > 105 and b > 70 and r + 8 >= g >= b - 10 and r > b + 25


def _ring_gold(color) -> bool:
    r, g, b = color[:3]
    return r > 120 and g > 75 and 30 < b < 150 and r > g - 5 and r > b + 20


def _clear_rect(pixels, box) -> None:
    left, top, right, bottom = box
    for y in range(top, bottom):
        for x in range(left, right):
            r, g, b, _ = pixels[x, y]
            pixels[x, y] = (r, g, b, 0)


def _clear_circle(pixels, center, radius) -> None:
    cx, cy = center
    for y in range(cy - radius, cy + radius + 1):
        for x in range(cx - radius, cx + radius + 1):
            if (x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius:
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)


def _cut_source() -> Image.Image:
    image = Image.open(SOURCE).convert("RGBA")
    pixels = image.load()
    width, height = image.size
    _flood(pixels, width, height, (0, 0), lambda p: max(p[:3]) < 48)
    left, top, right, bottom = image.getbbox()
    _flood(pixels, width, height, (width // 2, height // 2), lambda p: max(p[:3]) < 48)
    # Keep the leather rim. The parchment ribbon and its gold ends sit inside it,
    # and the game draws the title banner in that same place.
    rim = 36
    _clear_rect(pixels, (left + rim, top + 28, right - rim, top + 188))
    for y in range(top + 188, top + 270):
        for x in range(left + rim, right - rim):
            if _parchment(pixels[x, y]) or _ring_gold(pixels[x, y]):
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
    # The gold energy socket is covered by the game's own cost orb.
    _clear_circle(pixels, (148, 158), 54)
    box = image.getbbox()
    if box is None:
        raise SystemExit("frame became empty")
    return image.crop(box)


def _outside(vanilla: Image.Image) -> Image.Image:
    mask = vanilla.getchannel("A").point(lambda v: 255 if v < 12 else 0)
    pixels = mask.load()
    width, height = mask.size
    seen = {(0, 0)}
    queue = deque([(0, 0)])
    while queue:
        x, y = queue.popleft()
        pixels[x, y] = 0
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height or (nx, ny) in seen:
                continue
            if pixels[nx, ny]:
                seen.add((nx, ny))
                queue.append((nx, ny))
    # seen pixels were outside and are now 0. Interior holes stayed 255, then invert
    # is wrong: outside is 0, the portrait hole is still 255 because it was not
    # connected to the corner. Both the border and the hole should count as inside.
    inside = Image.new("L", (width, height), 0)
    inside_pixels = inside.load()
    for y in range(height):
        for x in range(width):
            if (x, y) not in seen:
                inside_pixels[x, y] = 255
    return inside


def main() -> None:
    frame = _cut_source().resize((598, 844), Image.Resampling.LANCZOS)
    vanilla = Image.open(VANILLA).convert("RGBA")
    if vanilla.size != frame.size:
        raise SystemExit(f"vanilla frame is {vanilla.size}, expected {frame.size}")
    inside = _outside(vanilla)
    alpha = frame.getchannel("A")
    alpha_pixels = alpha.load()
    inside_pixels = inside.load()
    for y in range(frame.height):
        for x in range(frame.width):
            if inside_pixels[x, y] == 0:
                alpha_pixels[x, y] = 0
    frame.putalpha(alpha)

    pixels = frame.load()
    left, top, right, bottom = PORTRAIT
    # Drop the ribbon end that the rectangle clear left on the top-right rim.
    for y in range(0, top):
        for x in range(24, frame.width - 24):
            if pixels[x, y][3] > 0 and (_parchment(pixels[x, y]) or _ring_gold(pixels[x, y])):
                r, g, b, _ = pixels[x, y]
                pixels[x, y] = (r, g, b, 0)
    for y in range(top, bottom):
        for x in range(left, right):
            r, g, b, _ = pixels[x, y]
            pixels[x, y] = (r, g, b, 0)
    # Paint only the rules-text part of the open window. A flood stops at the
    # leather, so the plate cannot spill past the frame onto the table.
    hole = set()
    queue = deque([(frame.width // 2, (bottom + frame.height) // 2)])
    while queue:
        x, y = queue.popleft()
        if (x, y) in hole or x < 0 or y < 0 or x >= frame.width or y >= frame.height:
            continue
        if pixels[x, y][3] >= 24 or inside_pixels[x, y] == 0:
            continue
        hole.add((x, y))
        queue.extend(((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)))
    for x, y in hole:
        if y >= bottom:
            pixels[x, y] = PLATE

    OUT.parent.mkdir(parents=True, exist_ok=True)
    frame.save(OUT)
    print(OUT, frame.size)


if __name__ == "__main__":
    main()

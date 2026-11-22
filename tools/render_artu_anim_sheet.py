"""Paint the same joint poses the Godot scene uses, so the keyframes can be checked without a GPU."""

import math
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
from artu_rig_scene import PART_DRAW, PART_FILE, PIVOTS, SPRITE_REST

PARTS = ROOT / "assets" / "combat-visuals" / "artu_rig"
OUT = ROOT / "design" / "cursor_art_task" / "outputs" / "_preview" / "anim"
OFFSET = (130, 80)
FEET = (250 + OFFSET[0], 499 + OFFSET[1])


def canvas_point(world):
    return (FEET[0] + world[0], FEET[1] + world[1])


def rotate_around(image: Image.Image, pivot, radians: float) -> Image.Image:
    if abs(radians) < 1e-6:
        return image
    # Godot's positive rotation is clockwise; Pillow's is counter-clockwise.
    return image.rotate(-math.degrees(radians), resample=Image.Resampling.BICUBIC, center=pivot)


def scale_around(image: Image.Image, pivot, scale) -> Image.Image:
    if abs(scale[0] - 1) < 1e-6 and abs(scale[1] - 1) < 1e-6:
        return image
    resized = image.resize(
        (max(1, round(image.width * scale[0])), max(1, round(image.height * scale[1]))),
        Image.Resampling.BICUBIC,
    )
    placed = Image.new("RGBA", image.size)
    placed.alpha_composite(resized, (round(pivot[0] * (1 - scale[0])), round(pivot[1] * (1 - scale[1]))))
    return placed


def modulate(image: Image.Image, color) -> Image.Image:
    if color == (1, 1, 1, 1):
        return image
    red, green, blue, alpha = image.split()
    red = red.point(lambda value: int(value * color[0]))
    green = green.point(lambda value: int(value * color[1]))
    blue = blue.point(lambda value: int(value * color[2]))
    alpha = alpha.point(lambda value: int(value * color[3]))
    return Image.merge("RGBA", (red, green, blue, alpha))


def pose(parts, rig) -> Image.Image:
    canvas = Image.new("RGBA", (760, 760))
    for part in PART_DRAW:
        local = parts[part].copy()
        state = rig.get(part, {})
        pivot = (PIVOTS[part][0] + 250, PIVOTS[part][1] + 499)
        local = scale_around(local, pivot, state.get("scale", (1, 1)))
        local = rotate_around(local, pivot, state.get("rotation", 0.0))
        canvas.alpha_composite(local, OFFSET)
    canvas = modulate(canvas, rig.get("modulate", (1, 1, 1, 1)))
    canvas = rotate_around(canvas, FEET, rig.get("rotation", 0.0))
    shift = rig.get("position", (0, 0))
    if shift != (0, 0):
        moved = Image.new("RGBA", canvas.size)
        moved.alpha_composite(canvas, (round(shift[0]), round(shift[1])))
        canvas = moved
    return canvas


def sheet(frames, labels) -> Image.Image:
    width, height = 250, 320
    board = Image.new("RGBA", (width * len(frames), height + 28), (38, 32, 30, 255))
    for index, (frame, label) in enumerate(zip(frames, labels)):
        thumb = frame.resize((width, height), Image.Resampling.BICUBIC)
        board.alpha_composite(thumb, (index * width, 28))
    return board


def paste_pose(drawing: Image.Image) -> Image.Image:
    canvas = Image.new("RGBA", (760, 760))
    canvas.alpha_composite(drawing, (FEET[0] - drawing.width // 2, FEET[1] - 499))
    return canvas


def main() -> None:
    parts = {part: Image.open(PARTS / f"{PART_FILE[part]}.png").convert("RGBA") for part in PART_DRAW}
    poses = ROOT / "assets" / "combat-visuals" / "artu_poses"
    names = [
        "idle",
        "attack_windup",
        "attack_strike",
        "attack_follow",
        "cast_mid",
        "cast_raise",
        "hit_recoil",
        "die_fall",
        "die_down",
    ]
    OUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for name in names:
        if name == "idle":
            frame = pose(parts, {})
        else:
            frame = paste_pose(Image.open(poses / f"{name}.png").convert("RGBA"))
        frame.save(OUT / f"{name}.png")
        frames.append(frame)
    sheet(frames, names).save(OUT / "contact_sheet.png")
    merchant = Image.open(ROOT / "assets" / "combat-visuals" / "artu_merchant.png").convert("RGBA")
    board = Image.new("RGBA", (512, 560), (38, 32, 30, 255))
    board.alpha_composite(merchant, (0, 40))
    board.save(OUT / "merchant_idle.png")
    print("wrote", OUT)


if __name__ == "__main__":
    main()

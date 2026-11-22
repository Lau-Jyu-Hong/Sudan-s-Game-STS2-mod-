"""Key the painted action poses and pin them to the combat rig's feet.

Standing poses match the rig's shoulder-to-foot height, so a sword raised
overhead does not shrink the body. Kneeling poses match the rig's torso
width instead, so a shorter silhouette is not scaled up to a standing height.
Every frame shares one canvas and the chest stays on the same vertical line.
"""

from __future__ import annotations

import statistics
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "design" / "cursor_art_task" / "tools"))
sys.path.insert(0, str(ROOT / "tools"))

from artu_rig_scene import PART_DRAW, PART_FILE, POSE_NAMES
from fit_sprites import key_magenta

MASTERS = Path(
    r"C:\Users\Administrator\.cursor\projects\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha\assets"
)
RIG = ROOT / "assets" / "combat-visuals" / "artu_rig"
OUT = ROOT / "assets" / "combat-visuals" / "artu_poses"
PREVIEW = ROOT / "design" / "cursor_art_task" / "outputs" / "_preview" / "anim"
KNEELING = {"die_fall", "die_down"}
# Idle already faces the enemy. Flipping a whole attack clip also turns its
# windup and recovery the other way, so the pose pops against the standing rig.
FLIP = set()
CANVAS_H = 500
FOOT_Y = 499


def _alpha_mask(image: Image.Image) -> list[list[int]]:
    alpha = image.getchannel("A")
    width, height = image.size
    pixels = alpha.load()
    rows = []
    for y in range(height):
        xs = [x for x in range(width) if pixels[x, y] > 20]
        rows.append(xs)
    return rows


def _torso(rows: list[list[int]]) -> tuple[int, int, float, float]:
    counts = [len(row) for row in rows]
    peak = max(counts)
    torso_rows = [index for index, count in enumerate(counts) if count >= peak * 0.55]
    top = min(torso_rows)
    bottom = max(index for index, count in enumerate(counts) if count >= peak * 0.12)
    centers = [statistics.median(rows[index]) for index in torso_rows]
    return top, bottom, statistics.median(centers), float(peak)


def _measure(image: Image.Image) -> dict:
    rows = _alpha_mask(image)
    top, bottom, center, peak = _torso(rows)
    height = bottom - top
    # A head is about a quarter of the torso. Capping it keeps a raised sword out of the scale.
    head = min(int(height * 0.28), top)
    band_end = top + max(1, int(height * 0.4))
    chest_widths = [len(rows[index]) for index in range(top, band_end) if len(rows[index]) > peak * 0.25]
    return {
        "body": height + head,
        "peak": peak,
        "chest": statistics.median(chest_widths) if chest_widths else peak,
        "center": center,
        "foot": max(index for index, row in enumerate(rows) if row),
    }


def _reference() -> dict:
    canvas = Image.new("RGBA", (500, 500))
    for part in PART_DRAW:
        with Image.open(RIG / f"{PART_FILE[part]}.png") as image:
            canvas.alpha_composite(image.convert("RGBA"))
    return _measure(canvas)


def _fit(image: Image.Image, measure: dict, scale: float, canvas_w: int) -> Image.Image:
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.Resampling.LANCZOS,
    )
    anchor_x = round(measure["center"] * scale)
    foot = round(measure["foot"] * scale)
    paste_x = canvas_w // 2 - anchor_x
    paste_y = FOOT_Y - foot
    if paste_x < 0 or paste_y < 0 or paste_x + resized.width > canvas_w or paste_y + resized.height > CANVAS_H:
        raise SystemExit(
            f"pose does not fit {canvas_w}x{CANVAS_H}: paste=({paste_x}, {paste_y}) size={resized.size}"
        )
    canvas = Image.new("RGBA", (canvas_w, CANVAS_H), (0, 0, 0, 0))
    canvas.alpha_composite(resized, (paste_x, paste_y))
    return canvas


def _needed_width(image: Image.Image, measure: dict, scale: float) -> int:
    width = round(image.width * scale)
    anchor_x = round(measure["center"] * scale)
    left = anchor_x + 8
    right = width - anchor_x + 8
    return max(left, right) * 2


def main() -> None:
    reference = _reference()
    keyed = {}
    measures = {}
    scales = {}
    for name in POSE_NAMES:
        source = MASTERS / f"artu_{name}.jpg"
        image = key_magenta(Image.open(source))
        bbox = image.getbbox()
        if bbox is None:
            raise SystemExit(f"{name} keyed to nothing")
        image = image.crop(bbox)
        measure = _measure(image)
        if name in KNEELING:
            # A kneel is shorter than the stand, but the chest has to stay the same person.
            # Matching the widest row shrinks him, because the sword and spread knee dominate it.
            target = 340 if name == "die_fall" else 300
            scale = target / image.height
        else:
            scale = reference["body"] / measure["body"]
        keyed[name] = image
        measures[name] = measure
        scales[name] = scale
        print(
            f"{name:16} body={measure['body']:4} chest={measure['chest']:5.0f} "
            f"scale={scale:.3f} foot={measure['foot']}"
        )

    canvas_w = max(_needed_width(keyed[name], measures[name], scales[name]) for name in POSE_NAMES)
    canvas_w = max(500, canvas_w + canvas_w % 2)
    print(
        "canvas", canvas_w, CANVAS_H,
        "reference body", reference["body"], "chest", round(reference["chest"]),
    )

    # A raised sword can still make a standing pose taller than the canvas. Shrink only that frame.
    for name in POSE_NAMES:
        image = keyed[name]
        measure = measures[name]
        scale = scales[name]
        height = round(image.height * scale)
        if height > CANVAS_H - 4:
            scales[name] *= (CANVAS_H - 8) / height
            print(f"{name} capped to {scales[name]:.3f}")

    OUT.mkdir(parents=True, exist_ok=True)
    frames = []
    for name in POSE_NAMES:
        fitted = _fit(keyed[name], measures[name], scales[name], canvas_w)
        if name in FLIP:
            fitted = fitted.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        corners = [fitted.getpixel(point)[3] for point in (
            (0, 0), (canvas_w - 1, 0), (0, CANVAS_H - 1), (canvas_w - 1, CANVAS_H - 1)
        )]
        if any(corners):
            raise SystemExit(f"{name} corners are not clear: {corners}")
        fitted.save(OUT / f"{name}.png")
        frames.append(fitted)
        print(f"wrote {name} bbox={fitted.getbbox()}")

    rig = Image.new("RGBA", (canvas_w, CANVAS_H))
    rig_x = (canvas_w - 500) // 2
    for part in PART_DRAW:
        with Image.open(RIG / f"{PART_FILE[part]}.png") as image:
            rig.alpha_composite(image.convert("RGBA"), (rig_x, 0))
    frames.insert(0, rig)
    PREVIEW.mkdir(parents=True, exist_ok=True)
    thumb_w, thumb_h = 180, 250
    board = Image.new("RGBA", (thumb_w * len(frames), thumb_h), (38, 32, 30, 255))
    for index, frame in enumerate(frames):
        thumb = frame.resize((thumb_w, thumb_h), Image.Resampling.BICUBIC)
        board.alpha_composite(thumb, (index * thumb_w, 0))
    board.save(PREVIEW / "pose_sheet.png")
    print("wrote", PREVIEW / "pose_sheet.png")


if __name__ == "__main__":
    main()

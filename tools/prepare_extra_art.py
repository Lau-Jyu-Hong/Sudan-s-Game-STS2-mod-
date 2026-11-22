"""Fit the stump-free rest pose, enemy action drawings, and skill effects."""

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "design" / "cursor_art_task" / "tools"))
from fit_sprites import key_magenta, trim

MASTERS = Path(
    r"C:\Users\Administrator\.cursor\projects"
    r"\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha\assets"
)
ASSETS = ROOT / "assets" / "combat-visuals"


def _keyed(name: str) -> Image.Image:
    return trim(key_magenta(Image.open(MASTERS / name)))


def _place(sprite: Image.Image, canvas: tuple[int, int], foot_y: int, center_x: int, scale: float) -> Image.Image:
    resized = sprite.resize(
        (max(1, round(sprite.width * scale)), max(1, round(sprite.height * scale))),
        Image.Resampling.LANCZOS,
    )
    image = Image.new("RGBA", canvas, (0, 0, 0, 0))
    x = center_x - resized.width // 2
    y = foot_y - resized.height
    if x < 0 or y < 0 or x + resized.width > canvas[0] or y + resized.height > canvas[1]:
        raise SystemExit(f"does not fit {canvas} paste=({x},{y}) size={resized.size}")
    image.alpha_composite(resized, (x, y))
    return image


def rest() -> None:
    sprite = _keyed("artu_rest_seat.jpg")
    # Boots sit on the camp log. The log is already in the room, so no stump is drawn.
    fitted = _place(sprite, (512, 512), 500, 256, min(430 / sprite.height, 480 / sprite.width))
    fitted.save(ASSETS / "artu_rest_site.png")
    print("rest", fitted.getbbox())


def enemies() -> None:
    jobs = {
        "crown_knight": ("crown_knight_combat.png", "knight_attack.jpg", "knight_cast.jpg"),
        "warlord": ("warlord_combat.png", "warlord_attack.jpg", "warlord_cast.jpg"),
        "mercenary_captain": ("mercenary_captain_combat.png", "captain_attack.jpg", "captain_cast.jpg"),
    }
    out = ASSETS / "enemies"
    out.mkdir(parents=True, exist_ok=True)
    for key, (body_name, attack_name, cast_name) in jobs.items():
        source = Image.open(ASSETS / body_name).convert("RGBA")
        body = source.crop(source.getbbox())
        attack = _keyed(attack_name)
        cast = _keyed(cast_name)
        scale = body.height / attack.height
        cast_scale = body.height / cast.height
        width = max(body.width + 16, round(attack.width * scale) + 16, round(cast.width * cast_scale) + 16)
        height = max(body.height + 8, round(attack.height * scale) + 8, round(cast.height * cast_scale) + 8)
        center = width // 2
        foot = height - 2
        body_canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        body_canvas.alpha_composite(body, (center - body.width // 2, foot - body.height))
        body_canvas.save(out / f"{key}.png")
        _place(attack, (width, height), foot, center, scale).save(out / f"{key}_attack.png")
        _place(cast, (width, height), foot, center, cast_scale).save(out / f"{key}_cast.png")
        print(key, width, height)


def effects() -> None:
    out = ASSETS / "enemies"
    out.mkdir(parents=True, exist_ok=True)
    for name in ("fx_royal_guard", "fx_blockade", "fx_requisition", "fx_recruit"):
        sprite = _keyed(name + ".jpg")
        scale = min(220 / sprite.width, 220 / sprite.height)
        fitted = _place(sprite, (256, 256), 128 + round(sprite.height * scale) // 2, 128, scale)
        fitted.save(out / f"{name}.png")
        print(name, fitted.getbbox())


if __name__ == "__main__":
    rest()
    enemies()
    effects()

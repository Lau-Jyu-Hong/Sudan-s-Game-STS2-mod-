"""Add the layered combat scene and shop pose to a copy of an existing ArtuMod.pck.

Reads the baseline pack and writes a new file. It does not modify the baseline.
"""

import io
import re
import sys
from pathlib import Path

from PIL import Image

from artu_rig_scene import PART_DRAW, PART_FILE, POSE_NAMES, combat_scene, enemy_scene, merchant_scene
from build_art_preview import apply_rounded_corners, read_pack, texture, write_pack

PORTRAITS = {
    f"{grade}{suit}"
    for grade in ("stone", "bronze", "silver", "gold")
    for suit in ("luxury", "slaughter", "conquest", "lust")
}
ENEMIES = {
    "crown_knight": {"RoyalGuard": "fx_royal_guard"},
    "warlord": {"Blockade": "fx_blockade", "Requisition": "fx_requisition"},
    "mercenary_captain": {"Recruit": "fx_recruit", "Expand": "fx_recruit"},
}

ROOT = Path(__file__).resolve().parents[1]


def _enemies(entries: dict) -> None:
    folder = ROOT / "assets" / "combat-visuals" / "enemies"
    packed_fx = {}
    for name, effects in ENEMIES.items():
        body = folder / f"{name}.png"
        attack = folder / f"{name}_attack.png"
        cast = folder / f"{name}_cast.png"
        with Image.open(body) as image:
            height = image.height
            body_key = f"ArtuMod/images/combat/enemies/{name}.ctex"
            entries[body_key] = texture(image, image.size, alpha=True, compressed=True)
        with Image.open(attack) as image:
            attack_key = f"ArtuMod/images/combat/enemies/{name}_attack.ctex"
            entries[attack_key] = texture(image, image.size, alpha=True, compressed=True)
        with Image.open(cast) as image:
            cast_key = f"ArtuMod/images/combat/enemies/{name}_cast.ctex"
            entries[cast_key] = texture(image, image.size, alpha=True, compressed=True)
        effect_paths = {}
        for anim, fx in effects.items():
            if fx not in packed_fx:
                with Image.open(folder / f"{fx}.png") as image:
                    fx_key = f"ArtuMod/images/combat/enemies/{fx}.ctex"
                    entries[fx_key] = texture(image, image.size, alpha=True, compressed=True)
                packed_fx[fx] = "res://" + fx_key
            effect_paths[anim] = packed_fx[fx]
        scene = enemy_scene(
            "res://" + body_key,
            "res://" + attack_key,
            "res://" + cast_key,
            effect_paths,
            canvas_h=height,
            game_script=True,
        )
        resources = scene.count("[ext_resource ") + scene.count("[sub_resource ")
        declared = int(re.search(r"load_steps=(\d+)", scene).group(1))
        if resources != declared:
            raise SystemExit(f"{name} scene load_steps {declared} but counted {resources}")
        entries[f"ArtuMod/scenes/creature_visuals/{name}.tscn"] = scene.encode()
        print("enemy", name, height)


def _image_from_tres(data: bytes) -> Image.Image:
    text = data.decode("utf-8")
    fmt = re.search(r'"format": "(\w+)"', text).group(1)
    width = int(re.search(r'"width": (\d+)', text).group(1))
    height = int(re.search(r'"height": (\d+)', text).group(1))
    raw = re.search(r"PackedByteArray\((.*?)\)", text, re.S).group(1)
    pixels = bytes(int(part) for part in raw.split(",") if part.strip())
    mode = "RGBA" if fmt.startswith("RGBA") else "RGB"
    return Image.frombytes(mode, (width, height), pixels)


def _image_from_ctex(data: bytes) -> Image.Image:
    start = data.find(b"\x89PNG")
    if start < 0:
        raise SystemExit("compressed texture has no PNG")
    return Image.open(io.BytesIO(data[start:]))


def _round_cards(entries: dict) -> None:
    # The live frame is rounded. The portrait window needs a matching curve,
    # larger than the earlier 28px mask, which still read as a square in play.
    for name in sorted(PORTRAITS):
        key = f"ArtuMod/images/{name}.tres"
        if key not in entries:
            raise SystemExit(f"missing portrait {key}")
        image = _image_from_tres(entries[key])
        radius = round(72 * image.width / 384)
        rounded = apply_rounded_corners(image, radius)
        entries[key] = texture(rounded, rounded.size, alpha=True)
        corner = rounded.getpixel((0, 0))[3]
        inner = rounded.getpixel((min(40, rounded.width - 1), min(40, rounded.height - 1)))[3]
        print(f"portrait {name} radius={radius} corner={corner} inner={inner}")
    for name in sorted(PORTRAITS):
        key = f"ArtuMod/images/art_preview/{name}_full.ctex"
        if key not in entries:
            print("no full art", name)
            continue
        image = _image_from_ctex(entries[key])
        radius = round(56 * image.width / 448)
        rounded = apply_rounded_corners(image, radius)
        entries[key] = texture(rounded, rounded.size, alpha=True, compressed=True)
        print(f"full {name} radius={radius} corner={rounded.getpixel((0, 0))[3]}")


def main() -> None:
    baseline = Path(sys.argv[1])
    output = Path(sys.argv[2])
    if output.exists():
        raise SystemExit(f"refusing to overwrite {output}")
    entries = read_pack(baseline)
    rig = ROOT / "assets" / "combat-visuals" / "artu_rig"
    paths = {}
    for part in PART_DRAW:
        key = f"ArtuMod/images/combat/{PART_FILE[part]}.ctex"
        with Image.open(rig / f"{PART_FILE[part]}.png") as image:
            entries[key] = texture(image, (500, 500), alpha=True, compressed=True)
        paths[part] = "res://" + key
    pose_dir = ROOT / "assets" / "combat-visuals" / "artu_poses"
    pose_paths = {}
    for name in POSE_NAMES:
        key = f"ArtuMod/images/combat/poses/{name}.ctex"
        with Image.open(pose_dir / f"{name}.png") as image:
            entries[key] = texture(image, image.size, alpha=True, compressed=True)
        pose_paths[name] = "res://" + key
    entries["ArtuMod/scenes/creature_visuals/artu.tscn"] = combat_scene(
        paths, game_script=True, pose_paths=pose_paths).encode()
    rest = ROOT / "assets" / "combat-visuals" / "artu_rest_site.png"
    with Image.open(rest) as image:
        entries["ArtuMod/images/combat/artu_rest_site.ctex"] = texture(
            image, image.size, alpha=True, compressed=True)
    merchant = ROOT / "assets" / "combat-visuals" / "artu_merchant.png"
    with Image.open(merchant) as image:
        entries["ArtuMod/images/combat/artu_merchant.ctex"] = texture(
            image, image.size, alpha=True, compressed=True)
    entries["ArtuMod/scenes/merchant/artu_merchant.tscn"] = merchant_scene(
        "res://ArtuMod/images/combat/artu_merchant.ctex", game_script=True).encode()
    _enemies(entries)
    _round_cards(entries)
    output.parent.mkdir(parents=True, exist_ok=True)
    write_pack(output, entries)
    print("wrote", output)


if __name__ == "__main__":
    main()
"""Key the shop painting and stage the Godot preview project. Does not touch the game."""

import shutil
import sys
from pathlib import Path

from collections import deque

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "design" / "cursor_art_task" / "tools"))
from artu_rig_scene import PART_DRAW, PART_FILE, POSE_NAMES, combat_scene, merchant_scene
from fit_sprites import key_magenta, place

MASTER = Path(
    r"C:\Users\Administrator\.cursor\projects"
    r"\c-Users-Administrator-Documents-Codex-2026-07-23-new-chat-work-artu-public-v0-4-7-alpha"
    r"\assets\artu_merchant_master.jpg"
)


def keep_largest_component(limb: Image.Image, body: Image.Image) -> None:
    src, dst = limb.load(), body.load()
    width, height = limb.size
    seen = [[False] * width for _ in range(height)]
    components: list[list[tuple[int, int]]] = []
    for y in range(height):
        for x in range(width):
            if seen[y][x] or src[x, y][3] <= 40:
                continue
            queue = deque([(x, y)])
            seen[y][x] = True
            pixels = []
            while queue:
                cx, cy = queue.popleft()
                pixels.append((cx, cy))
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = cx + dx, cy + dy
                    if 0 <= nx < width and 0 <= ny < height and not seen[ny][nx] and src[nx, ny][3] > 40:
                        seen[ny][nx] = True
                        queue.append((nx, ny))
            components.append(pixels)
    if not components:
        return
    largest = max(components, key=len)
    keep = set(largest)
    for pixels in components:
        if pixels is largest:
            continue
        for x, y in pixels:
            if dst[x, y][3] < 40:
                dst[x, y] = src[x, y]
            src[x, y] = (0, 0, 0, 0)
    # Duplicate the joint so the limb can rotate without opening a hole.
    top = min(y for _, y in keep)
    for x, y in keep:
        if y <= top + 36 and dst[x, y][3] < 40:
            dst[x, y] = src[x, y]
    print(limb.size, "kept", len(keep), "moved", sum(len(item) for item in components) - len(keep), "joint_y", top)


def main() -> None:
    placed = place(key_magenta(Image.open(MASTER)), (512, 512), 460, bottom_pad=2)
    corners = [placed.getpixel(point)[3] for point in ((0, 0), (511, 0), (0, 511), (511, 511))]
    print("merchant", placed.size, "bbox", placed.getbbox(), "corners", corners)
    if any(corners):
        raise SystemExit("merchant canvas corners are not transparent")
    merchant_out = ROOT / "design" / "cursor_art_task" / "outputs" / "merchant" / "artu_merchant.png"
    merchant_out.parent.mkdir(parents=True, exist_ok=True)
    placed.save(merchant_out)
    assets = ROOT / "assets" / "combat-visuals"
    shutil.copyfile(merchant_out, assets / "artu_merchant.png")
    rig = assets / "artu_rig"
    rig.mkdir(parents=True, exist_ok=True)
    source = ROOT / "design" / "cursor_art_task" / "outputs" / "combat_artu"
    for part in PART_DRAW:
        shutil.copyfile(source / f"{PART_FILE[part]}.png", rig / f"{PART_FILE[part]}.png")
    # Loose specks in the arm cuts orbit away from the limb. Keep each arm's
    # largest piece, and leave the specks on the body so the idle pose is unchanged.
    body = Image.open(rig / "artu_body.png").convert("RGBA")
    for limb_name in ("artu_weapon_arm.png", "artu_off_arm.png"):
        limb = Image.open(rig / limb_name).convert("RGBA")
        keep_largest_component(limb, body)
        limb.save(rig / limb_name)
    body.save(rig / "artu_body.png")

    project = ROOT / "design" / "cursor_art_task" / "godot_preview"
    part_dir = project / "parts"
    part_dir.mkdir(parents=True, exist_ok=True)
    for part in PART_DRAW:
        shutil.copyfile(rig / f"{PART_FILE[part]}.png", part_dir / f"{PART_FILE[part]}.png")
    shutil.copyfile(merchant_out, project / "artu_merchant.png")
    textures = {part: f"res://parts/{PART_FILE[part]}.png" for part in PART_DRAW}
    pose_src = assets / "artu_poses"
    pose_dst = project / "poses"
    pose_dst.mkdir(parents=True, exist_ok=True)
    pose_paths = {}
    for name in POSE_NAMES:
        shutil.copyfile(pose_src / f"{name}.png", pose_dst / f"{name}.png")
        pose_paths[name] = f"res://poses/{name}.png"
    (project / "artu_combat.tscn").write_text(
        combat_scene(textures, game_script=False, pose_paths=pose_paths), encoding="utf-8")
    (project / "artu_merchant.tscn").write_text(
        merchant_scene("res://artu_merchant.png", game_script=False), encoding="utf-8")
    (project / "project.godot").write_text(
        "config_version=5\n\n[application]\nconfig/name=\"ArtuAnimPreview\"\n"
        "config/features=PackedStringArray(\"4.7\")\n\n[display]\n"
        "window/size/viewport_width=560\nwindow/size/viewport_height=640\n\n[rendering]\n"
        "textures/canvas_textures/default_texture_filter=1\n",
        encoding="utf-8",
    )
    print("project", project)


if __name__ == "__main__":
    main()

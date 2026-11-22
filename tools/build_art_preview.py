"""Merge the approved art into a separate candidate PCK; don't touch the input pack."""
import argparse
import hashlib
import io
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageOps

from artu_rig_scene import PART_DRAW, PART_FILE, POSE_NAMES, combat_scene, merchant_scene


def read_pack(path):
    data = path.read_bytes()
    assert struct.unpack_from("<II", data) == (0x43504447, 2)
    assert struct.unpack_from("<IQ", data, 20) == (0, 0)
    cursor = 100
    entries = {}
    for _ in range(struct.unpack_from("<I", data, 96)[0]):
        length, = struct.unpack_from("<I", data, cursor)
        cursor += 4
        name = data[cursor:cursor + length].rstrip(b"\0").decode()
        cursor += length
        start, size, md5, flags = struct.unpack_from("<QQ16sI", data, cursor)
        cursor += 36
        value = data[start:start + size]
        assert not flags and len(value) == size and hashlib.md5(value).digest() == md5, name
        assert name not in entries
        entries[name] = value
    return entries


def write_pack(path, entries):
    names = [(k.encode() + b"\0", v) for k, v in sorted(entries.items())]
    keys = [(k + b"\0" * (-len(k) % 4), v) for k, v in names]
    offset = 100 + sum(40 + len(k) for k, _ in keys)
    offset += -offset % 16
    stream = io.BytesIO()
    stream.write(struct.pack("<6IQ16II", 0x43504447, 2, 4, 5, 1, 0, 0, *([0] * 16), len(keys)))
    for k, v in keys:
        stream.write(struct.pack("<I", len(k)) + k + struct.pack("<QQ", offset, len(v)))
        stream.write(hashlib.md5(v).digest() + struct.pack("<I", 0))
        offset += len(v) + (-len(v) % 16)
    stream.write(b"\0" * (-stream.tell() % 16))
    for _, v in keys:
        stream.write(v + b"\0" * (-len(v) % 16))
    with path.open("xb") as out:
        out.write(stream.getbuffer())


def texture(picture, size, *, alpha=False, center=(0.5, 0.5), compressed=False):
    mode = "RGBA" if alpha else "RGB"
    if alpha:
        final = ImageOps.contain(picture.convert(mode), size, Image.Resampling.LANCZOS)
        framed = Image.new(mode, size)
        framed.paste(final, ((size[0] - final.width) // 2, (size[1] - final.height) // 2))
        final = framed
    else:
        final = ImageOps.fit(picture.convert(mode), size, Image.Resampling.LANCZOS, centering=center)
    if compressed:
        # Godot 4.5 GST2 header + one lossless PNG image (no mipmaps).
        payload = io.BytesIO()
        final.save(payload, format="PNG")
        raw = b"PNG " + payload.getvalue()
        return (b"GST2" + struct.pack("<8I", 1, *size, 0, 0, 0, 0, 0)
                + struct.pack("<I2HIII", 1, *size, 0, 5 if alpha else 4, len(raw)) + raw)
    pixels = ",".join(map(str, final.tobytes()))
    return (f'[gd_resource type="ImageTexture" load_steps=2 format=3]\n'
            f'[sub_resource type="Image" id="ImageArt"]\n'
            f'data = {{"data": PackedByteArray({pixels}), "format": "{mode}8", "height": {size[1]}, '
            f'"mipmaps": false, "width": {size[0]}}}\n'
            '[resource]\nimage = SubResource("ImageArt")\n').encode()


def apply_rounded_corners(img, radius=34):
    """Generate high-quality antialiased rounded rectangle mask using 4x supersampling."""
    w, h = img.size
    sw, sh = w * 4, h * 4
    smask = Image.new("L", (sw, sh), 0)
    sdraw = ImageDraw.Draw(smask)
    sdraw.rounded_rectangle([(0, 0), (sw - 1, sh - 1)], radius=radius * 4, fill=255)
    mask = smask.resize((w, h), Image.Resampling.LANCZOS)
    
    rgba = img.convert("RGBA")
    if "A" in img.getbands():
        orig_a = img.getchannel("A")
        mask_pix = mask.load()
        orig_pix = orig_a.load()
        comb = Image.new("L", (w, h))
        cpix = comb.load()
        for y in range(h):
            for x in range(w):
                cpix[x, y] = int(orig_pix[x, y] * mask_pix[x, y] / 255.0)
        rgba.putalpha(comb)
    else:
        rgba.putalpha(mask)
    return rgba


def full_card_canvas(picture):
    """Extend the approved illustration into the live-label card template."""
    w, h = 448, 672
    picture = picture.convert("RGB")
    top = ImageOps.contain(picture, (w-32, 294), Image.Resampling.LANCZOS)
    # Derive the dark text area from the actual background palette of this card.
    swatch = picture.crop((0, picture.height * 9 // 10, picture.width, picture.height))
    backing = swatch.resize((w,h), Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(13))
    backing = Image.blend(backing, Image.new("RGB",(w,h),"#080f12"),0.68)
    y = 106
    rgba = top.convert("RGBA")
    fade = Image.new("L", (1, top.height))
    fade.putdata([min(255, int(255 * min((j + 1)/24, (top.height-j)/34))) for j in range(top.height)])
    rgba.putalpha(fade.resize(top.size))
    backing.paste(rgba, ((w-top.width)//2,y), rgba.getchannel("A"))
    return apply_rounded_corners(backing, radius=34)


def build(source, baseline, output, layout="pilot"):
    assert baseline.resolve() != output.resolve() and not output.exists()
    original = read_pack(baseline)
    updates = {}
    images = "ArtuMod/images/"
    preview = images + "art_preview/"

    def add(key, name, size, **options):
        with Image.open(source / (name + ".png")) as im:
            compressed = key.startswith(preview)
            updates[key + (".ctex" if compressed else ".tres")] = texture(im, size, compressed=compressed, **options)

    grades = ("stone", "bronze", "silver", "gold")
    for grade in grades:
        for suit in ("slaughter", "luxury", "lust"):
            with Image.open(source / (grade + suit + ".png")) as im:
                rounded_im = apply_rounded_corners(im.resize((384, 292), Image.Resampling.LANCZOS), radius=28)
                updates[images + grade + suit + ".tres"] = texture(rounded_im, (384, 292), alpha=True)
            if layout=="full" and grade+suit!="goldslaughter":
                with Image.open(source / (grade+suit+".png")) as im:
                    updates[preview+grade+suit+"_full.ctex"] = texture(full_card_canvas(im),(448,672),compressed=True)
    with Image.open(source / "conquest-approved-sheet.png") as sheet:
        w, h = sheet.size
        for i, grade in enumerate(grades):
            panel = sheet.crop((i*w//4+5, 0, (i+1)*w//4-5, h))
            rounded_panel = apply_rounded_corners(panel.resize((384, 292), Image.Resampling.LANCZOS), radius=28)
            updates[images + grade + "conquest.tres"] = texture(rounded_panel, (384, 292), alpha=True, center=(0.5, 0.43))
            if layout=="full":
                framed = ImageOps.fit(panel, (384,292), centering=(0.5,0.43))
                updates[preview+grade+"conquest_full.ctex"] = texture(full_card_canvas(framed),(448,672),compressed=True)

    add(images + "witchcardbox", "witch-box", (256, 256), alpha=True)
    add(preview + "artu_standard", "artu-standard", (128, 128), alpha=True)
    add(preview + "artu_top", "artu-top", (128, 128), alpha=True)
    add(preview + "artu_button", "artu-button", (160, 240))
    with Image.open(source / "artu-button.png") as portrait:
        updates[preview + "artu_locked.ctex"] = texture(ImageOps.grayscale(portrait), (160, 240), compressed=True)
    add(preview + "artu_select", "artu-select", (1280, 720))
    add(preview + "leaderless", "leaderless", (896, 504))
    add(preview + "map", "map-marker", (64, 64), alpha=True)
    add(preview + "energy", "energy", (128, 128), alpha=True)
    add(preview + "energy_text", "energy", (40, 40), alpha=True)
    add(images + "status/allarmypower", "all-army", (128, 128), alpha=True)
    add(images + "status/armymightpower", "army-might", (128, 128), alpha=True)
    for gesture in ("arm_pointing", "arm_rock", "arm_paper", "arm_scissors"):
        gfile = source / f"{gesture}.png"
        if gfile.exists():
            add(preview + gesture, gesture, (256, 256), alpha=True)

    if layout!="curse":
        with Image.open(source / "goldslaughter-full.png") as im:
            updates[preview + "goldslaughter_full.ctex"] = texture(apply_rounded_corners(im.resize((448, 672), Image.Resampling.LANCZOS), radius=34), (448, 672), compressed=True)
    updates["ArtuMod/scenes/artu_art_preview_bg.tscn"] = f'''[gd_scene load_steps=2 format=3]

[ext_resource type="CompressedTexture2D" path="res://{preview}artu_select.ctex" id="1"]

[node name="ArtuSelectArt" type="TextureRect"]
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
mouse_filter = 2
texture = ExtResource("1")
expand_mode = 1
stretch_mode = 6
'''.encode()
    updates["ArtuMod/scenes/artu_art_preview_icon.tscn"] = f'''[gd_scene load_steps=2 format=3]

[ext_resource type="CompressedTexture2D" path="res://{preview}artu_top.ctex" id="1"]

[node name="ArtuIcon" type="TextureRect"]
custom_minimum_size = Vector2(48, 48)
mouse_filter = 2
texture = ExtResource("1")
expand_mode = 1
stretch_mode = 5
'''.encode()

    combat_dir = source.parent / "combat-visuals"
    if combat_dir.exists():
        rest_file = combat_dir / "artu_rest_site.png"
        if rest_file.exists():
            with Image.open(rest_file) as im:
                updates["ArtuMod/images/combat/artu_rest_site.ctex"] = texture(im, (520, 520), alpha=True, compressed=True)
            updates["ArtuMod/scenes/rest_site/artu_rest_site.tscn"] = f'''[gd_scene load_steps=5 format=3]

[ext_resource type="Script" path="res://src/Core/Nodes/RestSite/NRestSiteCharacter.cs" id="1_script"]
[ext_resource type="CompressedTexture2D" path="res://ArtuMod/images/combat/artu_rest_site.ctex" id="2_tex"]
[ext_resource type="PackedScene" path="res://scenes/ui/selection_reticle.tscn" id="3_reticle"]

[sub_resource type="Animation" id="Animation_idle"]
resource_name = "idle"
length = 2.4
loop_mode = 1
tracks/0/type = "value"
tracks/0/imported = false
tracks/0/enabled = true
tracks/0/path = NodePath("Sprite2D:scale")
tracks/0/interp = 2
tracks/0/loop_wrap = true
tracks/0/keys = {{
"times": PackedFloat32Array(0, 1.2, 2.4),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Vector2(1, 1), Vector2(1.015, 0.985), Vector2(1, 1)]
}}

[sub_resource type="AnimationLibrary" id="AnimationLibrary_rest"]
_data = {{
"idle": SubResource("Animation_idle")
}}

[node name="ArtuRestSite" type="Node2D"]
position = Vector2(-2, 42)
scale = Vector2(0.76, 0.76)
script = ExtResource("1_script")

[node name="Sprite2D" type="Sprite2D" parent="."]
texture = ExtResource("2_tex")
position = Vector2(0, -210)

[node name="ControlRoot" type="Control" parent="."]
layout_mode = 3
anchors_preset = 0

[node name="SelectionReticle" parent="ControlRoot" instance=ExtResource("3_reticle")]
unique_name_in_owner = true

[node name="Hitbox" type="Control" parent="ControlRoot"]
unique_name_in_owner = true
layout_mode = 3
anchors_preset = 0
offset_left = -280.0
offset_top = -280.0
offset_right = 280.0
offset_bottom = 280.0

[node name="ThoughtBubbleRight" type="Control" parent="ControlRoot"]
unique_name_in_owner = true
layout_mode = 3
anchors_preset = 0
offset_left = 140.0
offset_top = -260.0
offset_right = 140.0
offset_bottom = -260.0

[node name="ThoughtBubbleLeft" type="Control" parent="ControlRoot"]
unique_name_in_owner = true
layout_mode = 3
anchors_preset = 0
offset_left = -140.0
offset_top = -260.0
offset_right = -140.0
offset_bottom = -260.0

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
libraries = {{
"": SubResource("AnimationLibrary_rest")
}}
autoplay = "idle"
'''.encode()

        combat_specs = {
            "crown_knight": {"size": (586, 720), "bounds_w": 400, "intent_y": -750, "center_y": -360, "talk_x": -220, "talk_y": -580},
            "warlord": {"size": (473, 610), "bounds_w": 320, "intent_y": -640, "center_y": -305, "talk_x": -180, "talk_y": -500},
            "mercenary_captain": {"size": (410, 550), "bounds_w": 300, "intent_y": -580, "center_y": -275, "talk_x": -160, "talk_y": -450},
            "artu": {"size": (424, 580), "bounds_w": 300, "intent_y": -610, "center_y": -290, "talk_x": 160, "talk_y": -480}
        }
        for cname, spec in combat_specs.items():
            img_file = combat_dir / f"{cname}.png"
            if img_file.exists():
                with Image.open(img_file) as im:
                    updates[f"ArtuMod/images/combat/{cname}.ctex"] = texture(im, spec["size"], alpha=True, compressed=True)
                ctex_path = f"res://ArtuMod/images/combat/{cname}.ctex"
                node_name = "".join(part.capitalize() for part in cname.split("_")) + "Visuals"
                h = spec["size"][1]
                bw = spec["bounds_w"]
                
                rig_dir = combat_dir / "artu_rig"
                pose_dir = combat_dir / "artu_poses"
                rig_ready = cname == "artu" and all(
                    (rig_dir / f"{PART_FILE[part]}.png").exists() for part in PART_DRAW
                ) and all((pose_dir / f"{name}.png").exists() for name in POSE_NAMES)
                if rig_ready:
                    texture_paths = {}
                    for part in PART_DRAW:
                        key = f"ArtuMod/images/combat/{PART_FILE[part]}.ctex"
                        with Image.open(rig_dir / f"{PART_FILE[part]}.png") as im:
                            updates[key] = texture(im, (500, 500), alpha=True, compressed=True)
                        texture_paths[part] = "res://" + key
                    pose_paths = {}
                    for name in POSE_NAMES:
                        pose_file = pose_dir / f"{name}.png"
                        key = f"ArtuMod/images/combat/poses/{name}.ctex"
                        with Image.open(pose_file) as im:
                            updates[key] = texture(im, im.size, alpha=True, compressed=True)
                        pose_paths[name] = "res://" + key
                    updates[f"ArtuMod/scenes/creature_visuals/{cname}.tscn"] = combat_scene(
                        texture_paths, game_script=True, pose_paths=pose_paths).encode()
                elif cname == "artu":
                    updates[f"ArtuMod/scenes/creature_visuals/{cname}.tscn"] = f'''[gd_scene load_steps=7 format=3]

[ext_resource type="Script" path="res://src/Core/Nodes/Combat/NCreatureVisuals.cs" id="1_script"]
[ext_resource type="CompressedTexture2D" path="{ctex_path}" id="2_tex"]

[sub_resource type="Animation" id="Animation_idle"]
resource_name = "idle"
length = 2.0
loop_mode = 1
tracks/0/type = "value"
tracks/0/imported = false
tracks/0/enabled = true
tracks/0/path = NodePath("Visuals/Sprite:position")
tracks/0/interp = 2
tracks/0/loop_wrap = true
tracks/0/keys = {{
"times": PackedFloat32Array(0, 1.0, 2.0),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Vector2(0, {-h / 2.0}), Vector2(0, {-h / 2.0 + 6.0}), Vector2(0, {-h / 2.0})]
}}
tracks/1/type = "value"
tracks/1/imported = false
tracks/1/enabled = true
tracks/1/path = NodePath("Visuals/Sprite:scale")
tracks/1/interp = 2
tracks/1/loop_wrap = true
tracks/1/keys = {{
"times": PackedFloat32Array(0, 1.0, 2.0),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Vector2(1, 1), Vector2(1.02, 0.98), Vector2(1, 1)]
}}

[sub_resource type="Animation" id="Animation_attack"]
resource_name = "attack"
length = 0.45
tracks/0/type = "value"
tracks/0/imported = false
tracks/0/enabled = true
tracks/0/path = NodePath("Visuals/Sprite:position")
tracks/0/interp = 2
tracks/0/loop_wrap = true
tracks/0/keys = {{
"times": PackedFloat32Array(0, 0.15, 0.45),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Vector2(0, {-h / 2.0}), Vector2(55, {-h / 2.0}), Vector2(0, {-h / 2.0})]
}}
tracks/1/type = "value"
tracks/1/imported = false
tracks/1/enabled = true
tracks/1/path = NodePath("Visuals/Sprite:rotation")
tracks/1/interp = 2
tracks/1/loop_wrap = true
tracks/1/keys = {{
"times": PackedFloat32Array(0, 0.15, 0.45),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [0.0, 0.12, 0.0]
}}

[sub_resource type="Animation" id="Animation_hit"]
resource_name = "hit"
length = 0.35
tracks/0/type = "value"
tracks/0/imported = false
tracks/0/enabled = true
tracks/0/path = NodePath("Visuals/Sprite:position")
tracks/0/interp = 2
tracks/0/loop_wrap = true
tracks/0/keys = {{
"times": PackedFloat32Array(0, 0.08, 0.35),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Vector2(0, {-h / 2.0}), Vector2(-35, {-h / 2.0}), Vector2(0, {-h / 2.0})]
}}
tracks/1/type = "value"
tracks/1/imported = false
tracks/1/enabled = true
tracks/1/path = NodePath("Visuals/Sprite:modulate")
tracks/1/interp = 2
tracks/1/loop_wrap = true
tracks/1/keys = {{
"times": PackedFloat32Array(0, 0.08, 0.35),
"transitions": PackedFloat32Array(1, 1, 1),
"update": 0,
"values": [Color(1, 1, 1, 1), Color(1, 0.35, 0.35, 1), Color(1, 1, 1, 1)]
}}

[sub_resource type="AnimationLibrary" id="AnimationLibrary_artu"]
_data = {{
"idle": SubResource("Animation_idle"),
"attack": SubResource("Animation_attack"),
"hit": SubResource("Animation_hit"),
"hurt": SubResource("Animation_hit")
}}

[node name="{node_name}" type="Node2D"]
scale = Vector2(0.72, 0.72)
script = ExtResource("1_script")

[node name="Bounds" type="Control" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2({bw}, {h})
layout_mode = 3
anchors_preset = 0
offset_left = {-bw / 2.0}
offset_top = {-h}.0
offset_right = {bw / 2.0}
offset_bottom = 0.0

[node name="Visuals" type="Node2D" parent="."]
unique_name_in_owner = true

[node name="Sprite" type="Sprite2D" parent="Visuals"]
texture = ExtResource("2_tex")
position = Vector2(0, {-h / 2.0})

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
unique_name_in_owner = true
libraries = {{
"": SubResource("AnimationLibrary_artu")
}}
autoplay = "idle"

[node name="IntentPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["intent_y"]})

[node name="CenterPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["center_y"]})

[node name="TalkPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2({spec["talk_x"]}, {spec["talk_y"]})

[node name="FormVfx" type="Control" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["center_y"]})
'''.encode()
                else:
                    updates[f"ArtuMod/scenes/creature_visuals/{cname}.tscn"] = f'''[gd_scene load_steps=3 format=3]

[ext_resource type="Script" path="res://src/Core/Nodes/Combat/NCreatureVisuals.cs" id="1_script"]
[ext_resource type="CompressedTexture2D" path="{ctex_path}" id="2_tex"]

[node name="{node_name}" type="Node2D"]
scale = Vector2(0.72, 0.72)
script = ExtResource("1_script")

[node name="Bounds" type="Control" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2({bw}, {h})
layout_mode = 3
anchors_preset = 0
offset_left = {-bw / 2.0}
offset_top = {-h}.0
offset_right = {bw / 2.0}
offset_bottom = 0.0

[node name="Visuals" type="Node2D" parent="."]
unique_name_in_owner = true

[node name="Sprite" type="Sprite2D" parent="Visuals"]
texture = ExtResource("2_tex")
offset = Vector2(0, {-h / 2.0})

[node name="IntentPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["intent_y"]})

[node name="CenterPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["center_y"]})

[node name="TalkPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2({spec["talk_x"]}, {spec["talk_y"]})

[node name="FormVfx" type="Control" parent="."]
unique_name_in_owner = true
position = Vector2(0, {spec["center_y"]})
'''.encode()
        merchant_file = combat_dir / "artu_merchant.png"
        if merchant_file.exists():
            with Image.open(merchant_file) as im:
                updates["ArtuMod/images/combat/artu_merchant.ctex"] = texture(
                    im, im.size, alpha=True, compressed=True)
            updates["ArtuMod/scenes/merchant/artu_merchant.tscn"] = merchant_scene(
                "res://ArtuMod/images/combat/artu_merchant.ctex", game_script=True).encode()
    output.parent.mkdir(parents=True, exist_ok=True)
    expected = original | updates
    write_pack(output, expected)
    assert read_pack(output) == expected
    print(f"{len(updates)} candidate entries; {len(original.keys() - updates.keys())} unchanged baseline entries")
    print(f"SHA256 {hashlib.sha256(output.read_bytes()).hexdigest()}")


if __name__ == "__main__":
    p = argparse.ArgumentParser()
    p.add_argument("source", type=Path)
    p.add_argument("baseline", type=Path)
    p.add_argument("output", type=Path)
    p.add_argument("--layout", choices=("pilot","full","curse"), default="pilot")
    a = p.parse_args()
    build(a.source, a.baseline, a.output, a.layout)

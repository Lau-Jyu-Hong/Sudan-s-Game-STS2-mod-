"""Merge the approved art into a separate candidate PCK; don't touch the input pack."""
import argparse
import hashlib
import io
import struct
from pathlib import Path

from PIL import Image, ImageFilter, ImageOps


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
    return backing


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
            add(images + grade + suit, grade + suit, (384, 292))
            if layout=="full" and grade+suit!="goldslaughter":
                with Image.open(source / (grade+suit+".png")) as im:
                    updates[preview+grade+suit+"_full.ctex"] = texture(full_card_canvas(im),(448,672),compressed=True)
    with Image.open(source / "conquest-approved-sheet.png") as sheet:
        w, h = sheet.size
        for i, grade in enumerate(grades):
            panel = sheet.crop((i*w//4+5, 0, (i+1)*w//4-5, h))
            updates[images + grade + "conquest.tres"] = texture(panel, (384, 292), center=(0.5, 0.43))
            if layout=="full":
                framed = ImageOps.fit(panel, (384,292), centering=(0.5,0.43))
                updates[preview+grade+"conquest_full.ctex"] = texture(full_card_canvas(framed),(448,672),compressed=True)

    add(images + "witchcardbox", "witch-box", (256, 256), alpha=True)
    add(preview + "artu_standard", "artu-standard", (128, 128), alpha=True)
    add(preview + "artu_top", "artu-button", (128, 128), center=(0.5, 0.19))
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
    if layout!="curse":
        add(preview + "goldslaughter_full", "goldslaughter-full", (448, 672))
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

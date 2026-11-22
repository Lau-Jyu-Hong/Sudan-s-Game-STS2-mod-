"""Godot scenes for the layered Artu combat rig and the shop pose.

The five part textures share a 500x500 canvas. Feet sit on y=499 and the body
center is x=250, so a sprite centered at (0, -249) stands on the node origin.
Each limb is parented at its joint; at zero rotation the sprite lands on that
same rest position, so the idle pose matches the uncut painting.
"""

from __future__ import annotations

SPRITE_REST = (0.0, -249.0)

# Joints in the character's own space: origin at the feet, +y down.
PIVOTS = {
    "Cape": (30.0, -314.0),
    "Body": (26.0, -198.0),
    "OffArm": (50.0, -303.0),
    "WeaponArm": (-21.0, -237.0),
    "Head": (28.0, -324.0),
}

PART_DRAW = ("Cape", "Body", "OffArm", "WeaponArm", "Head")
PART_FILE = {
    "Cape": "artu_cape",
    "Body": "artu_body",
    "OffArm": "artu_off_arm",
    "WeaponArm": "artu_weapon_arm",
    "Head": "artu_head",
}


# Painted in-betweens. Attack, cast, hit and death swap these whole drawings.
# Idle stays on the five-part rig, because that motion is only a breath.
POSE_NAMES = (
    "attack_windup",
    "attack_strike",
    "attack_follow",
    "cast_mid",
    "cast_raise",
    "hit_recoil",
    "die_fall",
    "die_down",
)


def _fmt(value) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, str):
        return value
    if isinstance(value, tuple) and len(value) == 2:
        return f"Vector2({value[0]:.4f}, {value[1]:.4f})"
    if isinstance(value, tuple) and len(value) == 4:
        return f"Color({value[0]:.3f}, {value[1]:.3f}, {value[2]:.3f}, {value[3]:.3f})"
    if isinstance(value, float):
        return f"{value:.4f}"
    return str(value)


def _track(index: int, path: str, times: list[float], values: list, interp: int = 1, update: int = 0) -> str:
    time_text = ", ".join(f"{item:.3f}" for item in times)
    value_text = ", ".join(_fmt(item) for item in values)
    transition = ", ".join(["1"] * len(times))
    return f'''tracks/{index}/type = "value"
tracks/{index}/imported = false
tracks/{index}/enabled = true
tracks/{index}/path = NodePath("{path}")
tracks/{index}/interp = {interp}
tracks/{index}/loop_wrap = true
tracks/{index}/keys = {{
"times": PackedFloat32Array({time_text}),
"transitions": PackedFloat32Array({transition}),
"update": {update},
"values": [{value_text}]
}}'''


def _animation(resource_id: str, name: str, length: float, tracks: list[str], loop: bool = False) -> str:
    body = "\n".join(tracks)
    loop_line = "\nloop_mode = 1" if loop else ""
    return f'''[sub_resource type="Animation" id="{resource_id}"]
resource_name = "{name}"
length = {length:.3f}{loop_line}
{body}
'''


def _pose_ref(name: str) -> str:
    return f'ExtResource("pose_{name}")'


def _show_pose(length: float) -> list[str]:
    # The rig returns on the last key. Idle also forces it back, so a queued idle cannot leave the painting up.
    return [
        _track(0, "Visuals/Rig:visible", [0, length], [False, True], 0, 1),
        _track(1, "Visuals/Pose:visible", [0, length], [True, False], 0, 1),
    ]


def combat_animations() -> str:
    # Bob the whole rig together. Scaling the body or turning the head opens the neck.
    idle = _animation("Animation_idle", "idle", 2.4, [
        _track(0, "Visuals/Rig:position", [0, 1.2, 2.4], [(0, 0), (0, -4), (0, 0)], 2),
        _track(1, "Visuals/Rig/Cape:rotation", [0, 1.2, 2.4], [0.01, -0.035, 0.01], 2),
        _track(2, "Visuals/Rig:visible", [0], [True], 0, 1),
        _track(3, "Visuals/Pose:visible", [0], [False], 0, 1),
        _track(4, "Visuals/Pose:modulate", [0], [(1, 1, 1, 1)], 0, 1),
    ], loop=True)
    # The strike stays up long enough to read. It is on screen at 0.45s, Artu.AttackAnimDelay.
    attack = _animation("Animation_attack", "attack", 1.15, [
        *_show_pose(1.15),
        _track(2, "Visuals/Pose:texture", [0, 0.45, 0.78], [
            _pose_ref("attack_windup"), _pose_ref("attack_strike"), _pose_ref("attack_follow"),
        ], 0, 1),
    ])
    hit = _animation("Animation_hit", "hit", 0.36, [
        *_show_pose(0.36),
        _track(2, "Visuals/Pose:texture", [0], [_pose_ref("hit_recoil")], 0, 1),
        _track(3, "Visuals/Pose:modulate", [0, 0.08, 0.36], [
            (1, 0.42, 0.38, 1), (1, 0.42, 0.38, 1), (1, 1, 1, 1),
        ]),
    ])
    # The raised scroll is readable at 0.25s, the character's CastAnimDelay.
    cast = _animation("Animation_cast", "cast", 0.7, [
        *_show_pose(0.7),
        _track(2, "Visuals/Pose:texture", [0, 0.22], [
            _pose_ref("cast_mid"), _pose_ref("cast_raise"),
        ], 0, 1),
    ])
    # Death stays on the painted corpse. Restoring the standing rig would pop him upright.
    die = _animation("Animation_die", "die", 1.2, [
        _track(0, "Visuals/Rig:visible", [0], [False], 0, 1),
        _track(1, "Visuals/Pose:visible", [0], [True], 0, 1),
        _track(2, "Visuals/Pose:texture", [0, 0.42], [
            _pose_ref("die_fall"), _pose_ref("die_down"),
        ], 0, 1),
        _track(3, "Visuals/Pose:modulate", [0, 0.72, 1.2], [
            (1, 1, 1, 1), (1, 1, 1, 1), (0.55, 0.5, 0.48, 0),
        ]),
    ])
    library = '''[sub_resource type="AnimationLibrary" id="AnimationLibrary_artu"]
_data = {
"Attack": SubResource("Animation_attack"),
"Cast": SubResource("Animation_cast"),
"Dead": SubResource("Animation_die"),
"Die": SubResource("Animation_die"),
"Hit": SubResource("Animation_hit"),
"Hurt": SubResource("Animation_hit"),
"Idle": SubResource("Animation_idle"),
"Relaxed": SubResource("Animation_idle"),
"attack": SubResource("Animation_attack"),
"cast": SubResource("Animation_cast"),
"dead": SubResource("Animation_die"),
"die": SubResource("Animation_die"),
"hit": SubResource("Animation_hit"),
"hurt": SubResource("Animation_hit"),
"idle": SubResource("Animation_idle"),
"relaxed": SubResource("Animation_idle")
}
'''
    return "\n".join([idle, attack, hit, cast, die, library])


def _bone(part: str, texture_id: str) -> str:
    pivot = PIVOTS[part]
    local = (SPRITE_REST[0] - pivot[0], SPRITE_REST[1] - pivot[1])
    # The head cut meets the collar on a single pixel row. Overlap it so the neck cannot open.
    if part == "Head":
        local = (local[0], local[1] + 10.0)
    return f'''[node name="{part}" type="Node2D" parent="Visuals/Rig"]
position = Vector2({pivot[0]:.1f}, {pivot[1]:.1f})

[node name="Sprite" type="Sprite2D" parent="Visuals/Rig/{part}"]
texture = ExtResource("{texture_id}")
position = Vector2({local[0]:.1f}, {local[1]:.1f})
'''


def _flat_parts(parent: str, texture_ids: dict[str, str]) -> str:
    blocks = []
    for part in PART_DRAW:
        blocks.append(f'''[node name="{part}" type="Sprite2D" parent="{parent}"]
texture = ExtResource("{texture_ids[part]}")
position = Vector2({SPRITE_REST[0]:.1f}, {SPRITE_REST[1]:.1f})
''')
    return "\n".join(blocks)


def combat_scene(texture_paths: dict[str, str], *, game_script: bool, pose_paths: dict[str, str]) -> str:
    resources = []
    ids = {}
    for index, part in enumerate(PART_DRAW, start=1):
        resource_id = f"tex_{index}"
        ids[part] = resource_id
        kind = "CompressedTexture2D" if texture_paths[part].endswith(".ctex") else "Texture2D"
        resources.append(
            f'[ext_resource type="{kind}" path="{texture_paths[part]}" id="{resource_id}"]'
        )
    for name in POSE_NAMES:
        kind = "CompressedTexture2D" if pose_paths[name].endswith(".ctex") else "Texture2D"
        resources.append(
            f'[ext_resource type="{kind}" path="{pose_paths[name]}" id="pose_{name}"]'
        )
    script = ""
    if game_script:
        resources.insert(
            0,
            '[ext_resource type="Script" path="res://src/Core/Nodes/Combat/NCreatureVisuals.cs" id="script_visuals"]',
        )
        script = '\nscript = ExtResource("script_visuals")'
    steps = len(resources) + 6
    bones = "\n".join(_bone(part, ids[part]) for part in PART_DRAW)
    flat = _flat_parts("PhobiaModeVisuals", ids)
    return f'''[gd_scene load_steps={steps} format=3]

{chr(10).join(resources)}

{combat_animations()}
[node name="ArtuVisuals" type="Node2D"]{script}

[node name="Bounds" type="Control" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2(220, 400)
layout_mode = 3
anchors_preset = 0
offset_left = -110.0
offset_top = -400.0
offset_right = 110.0
offset_bottom = 0.0
mouse_filter = 2

[node name="Visuals" type="Node2D" parent="."]
unique_name_in_owner = true

[node name="Rig" type="Node2D" parent="Visuals"]

{bones}
[node name="Pose" type="Sprite2D" parent="Visuals"]
visible = false
texture = ExtResource("pose_attack_windup")
position = Vector2(0, -249)

[node name="PhobiaModeVisuals" type="Node2D" parent="."]
unique_name_in_owner = true
visible = false

{flat}
[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
unique_name_in_owner = true
libraries = {{
"": SubResource("AnimationLibrary_artu")
}}
autoplay = "idle"

[node name="IntentPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, -460)

[node name="CenterPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, -200)

[node name="TalkPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(150, -320)

[node name="OrbPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(36, -210)

[node name="FormVfx" type="Control" parent="."]
unique_name_in_owner = true
mouse_filter = 2
offset_left = -20.0
offset_top = -220.0
offset_right = 20.0
offset_bottom = -180.0
'''


def merchant_scene(texture_path: str, *, game_script: bool) -> str:
    kind = "CompressedTexture2D" if texture_path.endswith(".ctex") else "Texture2D"
    resources = [f'[ext_resource type="{kind}" path="{texture_path}" id="tex_merchant"]']
    script = ""
    steps = 5
    if game_script:
        resources.insert(
            0,
            '[ext_resource type="Script" path="res://src/Core/Nodes/Screens/Shops/NMerchantCharacter.cs" id="script_merchant"]',
        )
        script = '\nscript = ExtResource("script_merchant")'
        steps += 1
    idle = _animation("Animation_idle", "idle", 2.6, [
        _track(0, "Rig:position", [0, 1.3, 2.6], [(0, 0), (0, -5), (0, 0)], 2),
        _track(1, "Rig:scale", [0, 1.3, 2.6], [(1, 1), (1.01, 0.985), (1, 1)], 2),
        _track(2, "Rig:rotation", [0, 1.3, 2.6], [-0.012, 0.018, -0.012], 2),
    ], loop=True)
    die = _animation("Animation_die", "die", 0.8, [
        _track(0, "Rig:rotation", [0, 0.8], [0.0, 0.35]),
        _track(1, "Rig:modulate", [0, 0.8], [(1, 1, 1, 1), (1, 1, 1, 0.0)]),
    ])
    return f'''[gd_scene load_steps={steps} format=3]

{chr(10).join(resources)}

{idle}
{die}
[sub_resource type="AnimationLibrary" id="AnimationLibrary_shop"]
_data = {{
"Die": SubResource("Animation_die"),
"Idle": SubResource("Animation_idle"),
"dead": SubResource("Animation_die"),
"die": SubResource("Animation_die"),
"idle": SubResource("Animation_idle"),
"relaxed_loop": SubResource("Animation_idle")
}}

[node name="ArtuMerchant" type="Node2D"]{script}

[node name="Rig" type="Node2D" parent="."]

[node name="Sprite" type="Sprite2D" parent="Rig"]
texture = ExtResource("tex_merchant")
position = Vector2(0, -256)

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
unique_name_in_owner = true
libraries = {{
"": SubResource("AnimationLibrary_shop")
}}
autoplay = "idle"
'''


def enemy_scene(
    body: str,
    attack: str,
    cast: str,
    effects: dict[str, str],
    *,
    canvas_h: int,
    game_script: bool,
) -> str:
    """One painted body, plus attack and cast drawings. Attacks face screen-left.

    Skill names play the cast drawing and a short effect. NRestSiteCharacter is unrelated;
    monster scenes only need the creature visual nodes.
    """
    y = -canvas_h / 2.0
    resources = [
        f'[ext_resource type="CompressedTexture2D" path="{body}" id="tex_body"]',
        f'[ext_resource type="CompressedTexture2D" path="{attack}" id="tex_attack"]',
        f'[ext_resource type="CompressedTexture2D" path="{cast}" id="tex_cast"]',
    ]
    for index, (name, path) in enumerate(effects.items()):
        resources.append(
            f'[ext_resource type="CompressedTexture2D" path="{path}" id="fx_{index}"]'
        )
    fx_ids = {name: f"fx_{index}" for index, name in enumerate(effects)}
    script = ""
    if game_script:
        resources.insert(
            0,
            '[ext_resource type="Script" path="res://src/Core/Nodes/Combat/NCreatureVisuals.cs" id="script_visuals"]',
        )
        script = '\nscript = ExtResource("script_visuals")'
    rest = (0.0, y)
    idle = _animation("Animation_idle", "idle", 2.2, [
        _track(0, "Visuals:position", [0, 1.1, 2.2], [rest, (0, y - 6), rest], 2),
        _track(1, "Visuals/Body:visible", [0], [True], 0, 1),
        _track(2, "Visuals/Pose:visible", [0], [False], 0, 1),
        _track(3, "Visuals/Fx:visible", [0], [False], 0, 1),
        _track(4, "Visuals:modulate", [0], [(1, 1, 1, 1)], 0, 1),
    ], loop=True)
    # The blow is readable at 0.25s, the same delay the tea-party attacks already wait.
    attack_anim = _animation("Animation_attack", "attack", 0.9, [
        _track(0, "Visuals/Body:visible", [0, 0.9], [False, True], 0, 1),
        _track(1, "Visuals/Pose:visible", [0, 0.9], [True, False], 0, 1),
        _track(2, "Visuals/Pose:texture", [0], ['ExtResource("tex_attack")'], 0, 1),
        _track(3, "Visuals:position", [0, 0.25, 0.9], [rest, (-36, y), rest]),
    ])
    hit = _animation("Animation_hit", "hit", 0.36, [
        _track(0, "Visuals:position", [0, 0.08, 0.36], [rest, (28, y), rest]),
        _track(1, "Visuals:modulate", [0, 0.08, 0.36], [(1, 0.42, 0.38, 1), (1, 0.42, 0.38, 1), (1, 1, 1, 1)]),
        _track(2, "Visuals/Body:visible", [0], [True], 0, 1),
        _track(3, "Visuals/Pose:visible", [0], [False], 0, 1),
        _track(4, "Visuals/Fx:visible", [0], [False], 0, 1),
    ])
    cast_anim = _animation("Animation_cast", "cast", 0.7, [
        _track(0, "Visuals/Body:visible", [0, 0.7], [False, True], 0, 1),
        _track(1, "Visuals/Pose:visible", [0, 0.7], [True, False], 0, 1),
        _track(2, "Visuals/Pose:texture", [0], ['ExtResource("tex_cast")'], 0, 1),
    ])
    die = _animation("Animation_die", "die", 1.2, [
        _track(0, "Visuals:rotation", [0, 0.7, 1.2], [0.0, 0.9, 1.2]),
        _track(1, "Visuals:modulate", [0, 0.7, 1.2], [(1, 1, 1, 1), (1, 1, 1, 1), (0.45, 0.42, 0.4, 0)]),
        _track(2, "Visuals/Body:visible", [0], [True], 0, 1),
        _track(3, "Visuals/Pose:visible", [0], [False], 0, 1),
        _track(4, "Visuals/Fx:visible", [0], [False], 0, 1),
    ])
    extras = []
    for index, name in enumerate(effects):
        extras.append(_animation(f"Animation_fx_{index}", name, 0.8, [
            _track(0, "Visuals/Body:visible", [0, 0.8], [False, True], 0, 1),
            _track(1, "Visuals/Pose:visible", [0, 0.8], [True, False], 0, 1),
            _track(2, "Visuals/Pose:texture", [0], ['ExtResource("tex_cast")'], 0, 1),
            _track(3, "Visuals/Fx:visible", [0, 0.8], [True, False], 0, 1),
            _track(4, "Visuals/Fx:texture", [0], [f'ExtResource("{fx_ids[name]}")'], 0, 1),
            _track(5, "Visuals/Fx:modulate", [0, 0.16, 0.8], [(1, 1, 1, 0), (1, 1, 1, 1), (1, 1, 1, 0)]),
        ]))
    names = {
        "Attack": "Animation_attack",
        "AttackSword": "Animation_attack",
        "Cast": "Animation_cast",
        "Dead": "Animation_die",
        "Die": "Animation_die",
        "Hit": "Animation_hit",
        "Hurt": "Animation_hit",
        "Idle": "Animation_idle",
        "RamAttack": "Animation_attack",
        "attack": "Animation_attack",
        "cast": "Animation_cast",
        "dead": "Animation_die",
        "die": "Animation_die",
        "hit": "Animation_hit",
        "hurt": "Animation_hit",
        "idle": "Animation_idle",
    }
    for index, name in enumerate(effects):
        names[name] = f"Animation_fx_{index}"
    library_body = ",\n".join(f'"{key}": SubResource("{value}")' for key, value in names.items())
    library = f'''[sub_resource type="AnimationLibrary" id="AnimationLibrary_enemy"]
_data = {{
{library_body}
}}
'''
    steps = len(resources) + 6 + len(effects)
    half = canvas_h * 0.55
    return f'''[gd_scene load_steps={steps} format=3]

{chr(10).join(resources)}

{idle}
{attack_anim}
{hit}
{cast_anim}
{die}
{chr(10).join(extras)}
{library}
[node name="EnemyVisuals" type="Node2D"]{script}
scale = Vector2(0.72, 0.72)

[node name="Bounds" type="Control" parent="."]
unique_name_in_owner = true
custom_minimum_size = Vector2({half:.0f}, {canvas_h})
offset_left = {-half / 2:.0f}
offset_top = {-canvas_h}
offset_right = {half / 2:.0f}
offset_bottom = 0
mouse_filter = 2

[node name="Visuals" type="Node2D" parent="."]
unique_name_in_owner = true

[node name="Body" type="Sprite2D" parent="Visuals"]
texture = ExtResource("tex_body")
position = Vector2(0, {y:.1f})

[node name="Pose" type="Sprite2D" parent="Visuals"]
visible = false
texture = ExtResource("tex_attack")
position = Vector2(0, {y:.1f})

[node name="Fx" type="Sprite2D" parent="Visuals"]
visible = false
position = Vector2(-20, {y * 0.72:.1f})
scale = Vector2(0.55, 0.55)

[node name="PhobiaModeVisuals" type="Node2D" parent="."]
unique_name_in_owner = true
visible = false

[node name="Body" type="Sprite2D" parent="PhobiaModeVisuals"]
texture = ExtResource("tex_body")
position = Vector2(0, {y:.1f})

[node name="AnimationPlayer" type="AnimationPlayer" parent="."]
unique_name_in_owner = true
libraries = {{
"": SubResource("AnimationLibrary_enemy")
}}
autoplay = "idle"

[node name="IntentPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {-canvas_h - 30:.0f})

[node name="CenterPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(0, {y * 0.7:.0f})

[node name="TalkPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(-120, {y * 0.8:.0f})

[node name="OrbPos" type="Marker2D" parent="."]
unique_name_in_owner = true
position = Vector2(30, {y * 0.55:.0f})

[node name="FormVfx" type="Control" parent="."]
unique_name_in_owner = true
mouse_filter = 2
offset_left = -20
offset_top = {y - 20:.0f}
offset_right = 20
offset_bottom = {y + 20:.0f}
'''

"""Jev decision driver for autoSpire.

This process is deliberately outside the C# mod:

    Slay the Spire 2 -> GET /state -> Jev Choice -> action key -> POST /action

Jev never receives an arbitrary action schema.  This file builds the currently
legal candidates, asks Jev to choose one candidate key, and maps that key back
to the existing autoSpire action protocol.

Safe defaults:
  - no API key in source code (uses TYPESAFE_API_KEY)
  - dry-run unless --execute is supplied
  - stop instead of acting when confidence is below the threshold
  - one decision in dry-run mode; --once limits an executing run to one action

Examples:
  python mcp/jev_driver.py
  python mcp/jev_driver.py --execute --once
  python mcp/jev_driver.py --execute
"""

from __future__ import annotations

import argparse
import json
import os
import re
import ssl
import sys
import time
import traceback
from dataclasses import dataclass
from typing import Any

import httpx
import httpx2
from typesafe_sdk import Choice, TypeSafeClient


DEFAULT_PORT = 8770
MAX_PORT_ATTEMPTS = 10
MAX_JEV_CHOICES = 255
DEFAULT_POLL_INTERVAL = 0.25
DEFAULT_HTTP_TIMEOUT = 10.0
DEFAULT_ACTION_SETTLE_TIMEOUT = 8.0
STATE_STABLE_POLLS = 2
DEFAULT_LOG_FORMAT = "human"
DEFAULT_VERBOSE = False
DEFAULT_SHOW_OPTIONS = 8
DEFAULT_DESCRIPTION_LENGTH = 180
# Jev already chooses from a state-filtered legal-action set.  Confidence is
# diagnostic by default; callers may opt into an abstention gate explicitly.
DEFAULT_MIN_CONFIDENCE = 0.0

JEV_INSTRUCTIONS = """你是《杀戮尖塔 2》的单步决策器。

游戏是回合制爬塔卡牌游戏：玩家通过出牌、使用药水和选择地图节点推进牌局。
战斗中需要管理能量、生命、格挡、敌人意图、手牌和药水；非战斗阶段需要在奖励、
休息、事件、商店、宝箱和地图路线之间做选择。

决策目标：
1. 优先避免当前死亡或受到不可接受的伤害。
2. 战斗中综合考虑敌人意图、当前生命、格挡、能量和手牌。
3. 非战斗中综合考虑当前资源、牌组质量和未来路线收益。
4. 不要为了短期收益破坏稳定的牌组或生存能力。
5. 奖励主界面选择卡牌奖励后，必须等待并完成 CardSelection，不能直接选择地图路线。
6. 最终跳过/继续奖励后，必须等到奖励界面关闭，再选择地图路线。

硬性约束：
1. 只能选择 criteria 中提供的候选 key。
2. 只能执行一个动作；执行后会重新获得新的游戏状态。
3. 不要创造新的 key、参数或动作。
4. 不要假设状态 JSON 中不存在的信息。
5. 候选描述中的目标名称、HP、格挡和意图是给你的人类可读索引；仍须以状态 JSON 和候选 key 为准。
6. 优先选择当前合法、可立即执行且能改善生存或长期收益的动作。"""


JsonObject = dict[str, Any]


@dataclass(frozen=True)
class Candidate:
    """One legal game action exposed as one Jev Choice option."""

    key: str
    description: str
    action: JsonObject


@dataclass(frozen=True)
class JevDecision:
    key: str
    confidence: float
    probabilities: dict[str, float]
    model: str | None


def _one_line_text(value: Any, max_length: int) -> str:
    """Normalize a game description for a compact terminal log."""

    text = _clean_game_text(value)
    if max_length <= 0 or len(text) <= max_length:
        return text
    if max_length <= 3:
        return text[:max_length]
    return text[: max_length - 3].rstrip() + "..."


def _state_summary(state: JsonObject) -> str:
    """Return useful high-level context without dumping the full snapshot."""

    phase = _text(state.get("Phase"), "unknown")
    run = _as_dict(state.get("Run"))
    parts = [f"phase={phase}"]
    if run:
        if run.get("CurrentAct") is not None or run.get("CurrentFloor") is not None:
            parts.append(
                f"floor={run.get('CurrentAct', '?')}/{run.get('CurrentFloor', '?')}"
            )
        if run.get("Gold") is not None:
            parts.append(f"gold={run.get('Gold')}")
        deck = _as_list(run.get("DeckCards"))
        relics = _as_list(run.get("Relics"))
        if deck:
            parts.append(f"deck={len(deck)}种")
        if relics:
            parts.append(f"relics={len(relics)}")

    if phase == "combat":
        combat = _as_dict(state.get("Combat"))
        if combat.get("Energy") is not None:
            parts.append(f"energy={combat.get('Energy')}/{combat.get('MaxEnergy', '?')}")
        player = _as_dict(combat.get("Player"))
        health = next(
            (player.get(field) for field in ("CurrentHp", "CurrentHealth", "Hp", "Health")
             if player.get(field) is not None),
            None,
        )
        if health is not None:
            parts.append(f"hp={health}")
        if combat.get("RoundNumber") is not None:
            parts.append(f"round={combat.get('RoundNumber')}")

    return " | ".join(parts)


def _format_action(action: JsonObject) -> str:
    """Render an autoSpire action in a short human-readable form."""

    action_type = _text(action.get("action"), "unknown")
    labels = {
        "play_card": "出牌",
        "multi_play": "连续出牌",
        "end_turn": "结束回合",
        "use_potion": "使用药水",
        "pick_card": "选择卡牌",
        "confirm_selection": "确认选牌",
        "move_to_map_coord": "选择地图路线",
        "pick_reward": "领取奖励",
        "rest_action": "休息点操作",
        "shop_action": "商店操作",
        "treasure_action": "宝箱操作",
        "event_action": "事件选项",
        "menu_action": "菜单操作",
    }
    label = labels.get(action_type, action_type)
    parameters = {key: value for key, value in action.items() if key != "action"}
    if not parameters:
        return label
    detail = ", ".join(f"{key}={value}" for key, value in parameters.items())
    return f"{label}（{detail}）"


def _format_probability_lines(
    decision: JevDecision,
    candidates: list[Candidate],
    max_items: int = 3,
    description_length: int = DEFAULT_DESCRIPTION_LENGTH,
) -> list[str]:
    """Map Jev's top probabilities back to option names and descriptions."""

    if not decision.probabilities:
        return ["无概率分布"]
    by_key = {candidate.key: candidate for candidate in candidates}
    ranked = sorted(
        decision.probabilities.items(), key=lambda item: item[1], reverse=True
    )[:max_items]
    lines: list[str] = []
    for key, probability in ranked:
        candidate = by_key.get(key)
        description = (
            _one_line_text(candidate.description, description_length)
            if candidate is not None
            else "未注册候选"
        )
        lines.append(f"{key}={probability:.2f}（{description}）")
    return lines


def _format_minimal_decision_line(
    decision: JevDecision,
    response_time_ms: float,
) -> str:
    """Render the default one-line decision log."""

    return (
        f"[Jev] 选择={decision.key} | 置信度={decision.confidence:.2f}"
        f" | 响应时间={response_time_ms:.0f} ms"
    )


def _as_dict(value: Any) -> JsonObject:
    return value if isinstance(value, dict) else {}


def _as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def _text(value: Any, fallback: str = "") -> str:
    if value is None:
        return fallback
    return str(value)


_GAME_TAG_RE = re.compile(r"\[/?[A-Za-z][^\]]*\]")


def _clean_game_text(value: Any, fallback: str = "") -> str:
    """Remove Godot formatting tags and normalize text used in Jev criteria."""

    text = _text(value, fallback)
    text = _GAME_TAG_RE.sub("", text)
    return " ".join(text.replace("\r", "\n").split())


def _number(value: Any) -> int | float | None:
    if isinstance(value, bool) or value is None:
        return None
    if isinstance(value, (int, float)):
        return value
    match = re.fullmatch(r"\s*-?\d+(?:\.\d+)?\s*", str(value))
    if not match:
        return None
    number = float(match.group())
    return int(number) if number.is_integer() else number


def _first_number(mapping: JsonObject, fields: tuple[str, ...]) -> int | float | None:
    for field in fields:
        number = _number(mapping.get(field))
        if number is not None:
            return number
    return None


def _extract_damage(card: JsonObject) -> int | float | None:
    """Find a reliable single-hit damage value for target outcome hints."""

    direct_damage = _first_number(card, ("Damage", "BaseDamage", "AttackDamage"))
    if direct_damage is not None:
        return direct_damage

    description = _clean_game_text(card.get("Description"))
    for pattern in (
        r"造成\s*(\d+(?:\.\d+)?)",
        r"(?:deal|deals)\s*(\d+(?:\.\d+)?)",
        r"damage\s*(\d+(?:\.\d+)?)",
    ):
        match = re.search(pattern, description, flags=re.IGNORECASE)
        if match:
            return _number(match.group(1))
    return None


def _compact_intent(value: Any) -> str:
    """Keep the useful intent facts while avoiding repeated prose."""

    text = _clean_game_text(value)
    if not text:
        return ""
    attack_match = re.search(r"攻击.*?(\d+(?:\.\d+)?)点伤害", text)
    if attack_match:
        return f"攻击{attack_match.group(1)}"
    return text


def _enemy_description(enemy: JsonObject) -> str:
    """Render an enemy target with enough context to distinguish duplicates."""

    name = _clean_game_text(enemy.get("Name"), "未知敌人")
    combat_id = enemy.get("CombatId")
    parts = [name]
    if combat_id is not None:
        parts.append(f"CombatId={combat_id}")

    current_hp = _first_number(enemy, ("CurrentHp", "CurrentHealth", "Hp", "Health"))
    max_hp = _first_number(enemy, ("MaxHp", "MaxHealth"))
    if current_hp is not None and max_hp is not None:
        parts.append(f"HP={current_hp}/{max_hp}")
    elif current_hp is not None:
        parts.append(f"HP={current_hp}")

    block = _first_number(enemy, ("Block", "CurrentBlock"))
    if block is not None:
        parts.append(f"格挡={block}")

    intent = enemy.get("IntentLabel")
    if intent is None:
        intent_value = enemy.get("Intent")
        if isinstance(intent_value, dict):
            intent = intent_value.get("Label") or intent_value.get("Name")
        else:
            intent = intent_value
    intent_text = _compact_intent(intent)
    if intent_text:
        parts.append(f"意图={intent_text}")

    buffs = []
    for raw_buff in _as_list(enemy.get("Buffs")):
        buff = _as_dict(raw_buff)
        buff_name = _clean_game_text(buff.get("Name"))
        stack_count = _number(buff.get("StackCount"))
        if buff_name:
            buffs.append(
                f"{buff_name}{stack_count if stack_count is not None else ''}"
            )
    if buffs:
        parts.append(f"状态={','.join(buffs)}")
    return "，".join(parts)


def _target_outcome_description(card: JsonObject, enemy: JsonObject) -> str:
    """Describe the likely result of a direct-damage card against one target."""

    damage = _extract_damage(card)
    current_hp = _first_number(enemy, ("CurrentHp", "CurrentHealth", "Hp", "Health"))
    block = _first_number(enemy, ("Block", "CurrentBlock"))
    if damage is None:
        return ""

    outcome = [f"预计基础伤害={damage}"]
    if block is not None:
        effective_damage = max(0, damage - block)
        outcome.append(f"扣除格挡后={effective_damage}")
    else:
        effective_damage = damage

    if current_hp is not None:
        remaining_hp = max(0, current_hp - effective_damage)
        outcome.append("预计击杀" if effective_damage >= current_hp else f"预计剩余HP={remaining_hp}")
    return "；".join(outcome)


def _state_digest(state: JsonObject) -> str:
    return json.dumps(state, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def _has_card_selection(state: JsonObject, phase: str) -> bool:
    """Return whether a phase exposes a concrete card-selection overlay."""

    section_name = {
        "reward": "Reward",
        "shop": "Shop",
    }.get(phase)
    if section_name is None:
        return False
    section = _as_dict(state.get(section_name))
    selection = _as_dict(section.get("CardSelection"))
    return bool(_as_list(selection.get("Options")))


def _requires_card_selection(action: JsonObject) -> str | None:
    """Return the phase whose selection overlay must appear after an action."""

    if action.get("action") == "pick_reward" and action.get("choice_type") == "card":
        return "reward"
    if action.get("action") == "shop_action" and action.get("shop_action") == "remove_card":
        return "shop"
    return None


def _requires_reward_exit(action: JsonObject) -> bool:
    """Return whether a successful action must close the reward overlay."""

    return action.get("action") == "pick_reward" and action.get("choice_type") == "skip"


def _detect_game_url() -> str:
    explicit_url = os.environ.get("AUTOSPIRE_URL")
    if explicit_url:
        return explicit_url.rstrip("/")

    explicit_port = os.environ.get("AUTOSPIRE_PORT")
    if explicit_port:
        return f"http://localhost:{explicit_port}"

    for port in range(DEFAULT_PORT, DEFAULT_PORT + MAX_PORT_ATTEMPTS):
        try:
            response = httpx.get(
                f"http://localhost:{port}/state",
                timeout=0.5,
                trust_env=False,
            )
            if response.status_code == 200:
                data = response.json()
                if isinstance(data, dict) and "Phase" in data:
                    return f"http://localhost:{port}"
        except Exception:
            pass

    return f"http://localhost:{DEFAULT_PORT}"


def _candidate(key: str, description: str, action: JsonObject) -> Candidate:
    return Candidate(key=key, description=description, action=action)


def _card_description(card: JsonObject, prefix: str = "") -> str:
    name = _clean_game_text(card.get("Name"), "未知卡牌")
    cost = card.get("Cost", "?")
    card_type = {
        "Attack": "攻击",
        "Skill": "技能",
        "Power": "能力",
        "Status": "状态",
        "Curse": "诅咒",
    }.get(_text(card.get("Type")), _text(card.get("Type"), "卡牌"))
    description = _clean_game_text(card.get("Description"), "无描述")
    return f"{prefix}{name}（{card_type}，费用 {cost}）：{description}"


def _build_card_selection_candidates(
    selection: JsonObject,
    action_prefix: str = "pick_card",
) -> list[Candidate]:
    """Map CardSelection or HandCardSelection to pick_card/confirm actions."""

    options = selection.get("Options")
    if not isinstance(options, list):
        options = selection.get("SelectableCards")
    options = _as_list(options)

    # Grid overlays retain the original options after a card is previewed.
    # Confirm the selection once the UI exposes an enabled confirm button.
    if selection.get("CanConfirm") is True and "SelectableCards" not in selection:
        return [
            _candidate(
                f"{action_prefix}_confirm",
                "确认当前已经选中的卡牌",
                {"action": "confirm_selection"},
            )
        ]

    candidates: list[Candidate] = []
    for index, raw_card in enumerate(options):
        card = _as_dict(raw_card)
        candidates.append(
            _candidate(
                f"{action_prefix}_{index}",
                _card_description(card, f"选择第 {index} 张牌；"),
                {"action": "pick_card", "card_index": index},
            )
        )

    # HandCardSelection can require a separate confirmation after selecting
    # one or more cards.  It is a legal candidate only when the UI enables it.
    if selection.get("CanConfirm") is True:
        candidates.append(
            _candidate(
                f"{action_prefix}_confirm",
                "确认当前已经选中的手牌",
                {"action": "confirm_selection"},
            )
        )
    return candidates


def _build_combat_candidates(combat: JsonObject) -> list[Candidate]:
    hand_selection = _as_dict(combat.get("HandCardSelection"))
    if hand_selection:
        return _build_card_selection_candidates(hand_selection, "hand_select")

    card_selection = _as_dict(combat.get("CardSelection"))
    if card_selection:
        return _build_card_selection_candidates(card_selection, "combat_select")

    if combat.get("IsPlayPhase") is False:
        return []

    candidates: list[Candidate] = []
    hand = _as_list(combat.get("Hand"))
    enemies = [
        _as_dict(enemy)
        for enemy in _as_list(combat.get("Enemies"))
        if _as_dict(enemy).get("IsAlive") is not False
        and _as_dict(enemy).get("IsHittable") is not False
    ]
    enemies_by_id = {
        enemy.get("CombatId"): enemy
        for enemy in enemies
        if enemy.get("CombatId") is not None
    }

    for hand_index, raw_card in enumerate(hand):
        card = _as_dict(raw_card)
        if card.get("CanPlay") is not True:
            continue

        needs_target = card.get("NeedsTarget") is True
        target_ids = [int(x) for x in _as_list(card.get("ValidTargetIds"))]
        if needs_target and not target_ids:
            continue

        targets: list[int | None] = target_ids if needs_target else [None]
        for target_id in targets:
            target_suffix = "" if target_id is None else f"_target_{target_id}"
            key = f"play_card_{hand_index}{target_suffix}"
            target_text = ""
            outcome_text = ""
            if target_id is not None:
                enemy = enemies_by_id.get(target_id)
                if enemy is None:
                    target_text = f"；目标敌人 CombatId={target_id}"
                else:
                    target_text = f"；目标：{_enemy_description(enemy)}"
                    outcome_text = _target_outcome_description(card, enemy)
            if outcome_text:
                outcome_text = f"；{outcome_text}"
            description = _card_description(
                card,
                f"打出手牌槽位 {hand_index}{target_text}；",
            )
            description += outcome_text
            action: JsonObject = {
                "action": "play_card",
                "hand_index": hand_index,
            }
            if target_id is not None:
                action["target_id"] = target_id
            candidates.append(_candidate(key, description, action))

    for raw_potion in _as_list(combat.get("Potions")):
        potion = _as_dict(raw_potion)
        name = _clean_game_text(potion.get("Name"), "未知药水")
        slot_index = potion.get("SlotIndex")
        target_type = _text(potion.get("TargetType"), "Unknown")
        description = _clean_game_text(potion.get("Description"), "无描述")

        if target_type == "AnyEnemy":
            for enemy in enemies:
                target_id = enemy.get("CombatId")
                if target_id is None:
                    continue
                key = f"use_potion_{slot_index}_target_{target_id}"
                candidates.append(
                    _candidate(
                        key,
                        f"使用{name}（{description}），目标：{_enemy_description(enemies_by_id.get(target_id, {'CombatId': target_id}))}",
                        {
                            "action": "use_potion",
                            "slot_index": slot_index,
                            "target_id": target_id,
                        },
                    )
                )
        else:
            candidates.append(
                _candidate(
                    f"use_potion_{slot_index}",
                    f"使用{name}（目标类型 {target_type}）：{description}",
                    {"action": "use_potion", "slot_index": slot_index},
                )
            )

    candidates.append(
        _candidate(
            "end_turn",
            "结束当前回合，不再执行其他动作",
            {"action": "end_turn"},
        )
    )
    return candidates


def _build_map_candidates(map_state: JsonObject) -> list[Candidate]:
    candidates: list[Candidate] = []
    for index, raw_node in enumerate(_as_list(map_state.get("AvailableNodes"))):
        node = _as_dict(raw_node)
        col = node.get("Col")
        row = node.get("Row")
        if col is None or row is None:
            continue
        node_type = _text(node.get("NodeType"), "Unknown")
        candidates.append(
            _candidate(
                f"map_node_{index}",
                f"选择地图节点 {node_type}（坐标 col={col}, row={row}）",
                {"action": "move_to_map_coord", "col": col, "row": row},
            )
        )
    return candidates


def _build_reward_candidates(reward: JsonObject, *, potion_slots_full: bool = False) -> list[Candidate]:
    card_selection = _as_dict(reward.get("CardSelection"))
    if card_selection:
        return _build_card_selection_candidates(card_selection, "reward_select")

    candidates: list[Candidate] = []
    for index, raw_item in enumerate(_as_list(reward.get("Items"))):
        item = _as_dict(raw_item)
        choice_index = item.get("Index", index)
        reward_type = _text(item.get("Type"), "unknown")
        if reward_type == "potion" and potion_slots_full:
            continue
        name = _clean_game_text(item.get("Name"), reward_type)
        description = _clean_game_text(item.get("Description"), "")
        card_options = [
            _clean_game_text(_as_dict(card).get("Name"), "未知卡牌")
            for card in _as_list(item.get("CardOptions"))
        ]
        if card_options:
            description += f" 可选卡牌：{', '.join(card_options)}"
        candidates.append(
            _candidate(
                f"reward_{choice_index}_{reward_type}",
                f"选择奖励 {name}（类型 {reward_type}）：{description}",
                {
                    "action": "pick_reward",
                    "choice_index": choice_index,
                    "choice_type": reward_type,
                },
            )
        )

    if reward.get("CanSkip") is True:
        candidates.append(
            _candidate(
                "reward_skip",
                "跳过当前奖励并继续前进",
                {"action": "pick_reward", "choice_type": "skip"},
            )
        )
    return candidates


def _build_rest_candidates(rest: JsonObject) -> list[Candidate]:
    card_selection = _as_dict(rest.get("CardSelection"))
    if card_selection:
        return _build_card_selection_candidates(card_selection, "rest_select")

    target_selection = _as_dict(rest.get("TargetSelection"))
    if target_selection:
        candidates: list[Candidate] = []
        for index, raw_target in enumerate(_as_list(target_selection.get("Targets"))):
            target = _as_dict(raw_target)
            candidates.append(
                _candidate(
                    f"rest_target_{index}",
                    f"选择休息点目标玩家 {_clean_game_text(target.get('PlayerName'), str(index))}",
                    {"action": "rest_action", "card_index": index},
                )
            )
        return candidates

    candidates = []
    for index, raw_option in enumerate(_as_list(rest.get("Options"))):
        option = _as_dict(raw_option)
        option_index = option.get("Index", index)
        candidates.append(
            _candidate(
                f"rest_{option_index}",
                f"休息点操作 {_clean_game_text(option.get('Name'), str(option_index))}：{_clean_game_text(option.get('Description'))}",
                {"action": "rest_action", "option_index": option_index},
            )
        )

    if not candidates and rest.get("CanProceed") is True:
        candidates.append(
            _candidate(
                "rest_proceed",
                "休息点选项已用完，继续前进",
                {"action": "rest_action"},
            )
        )
    return candidates


def _build_event_candidates(event: JsonObject) -> list[Candidate]:
    card_selection = _as_dict(event.get("CardSelection"))
    if card_selection:
        return _build_card_selection_candidates(card_selection, "event_select")

    if event.get("IsFinished") is True:
        return [
            _candidate(
                "event_proceed",
                "事件已完成，离开事件房间",
                {"action": "event_action"},
            )
        ]

    candidates: list[Candidate] = []
    for index, raw_option in enumerate(_as_list(event.get("Options"))):
        option = _as_dict(raw_option)
        if option.get("IsLocked") is True:
            continue
        option_index = option.get("Index", index)
        text = _clean_game_text(option.get("Text"), f"选项 {option_index}")
        detail = _clean_game_text(option.get("DetailDescription"), "")
        candidates.append(
            _candidate(
                f"event_{option_index}",
                f"选择事件选项 {text}：{detail}",
                {"action": "event_action", "option_index": option_index},
            )
        )
    return candidates


def _build_shop_candidates(shop: JsonObject, *, potion_slots_full: bool = False) -> list[Candidate]:
    card_selection = _as_dict(shop.get("CardSelection"))
    if card_selection:
        return _build_card_selection_candidates(card_selection, "shop_select")

    candidates: list[Candidate] = []
    if shop.get("InventoryOpen") is False:
        candidates.append(
            _candidate(
                "shop_open_inventory",
                "打开商店商品面板",
                {"action": "shop_action", "shop_action": "open_inventory"},
            )
        )
    groups = (
        ("CharacterCards", "character_card"),
        ("ColorlessCards", "colorless_card"),
        ("Relics", "relic"),
        ("Potions", "potion"),
    )
    for field_name, entry_type in groups:
        if entry_type == "potion" and potion_slots_full:
            continue
        for index, raw_item in enumerate(_as_list(shop.get(field_name))):
            item = _as_dict(raw_item)
            if item.get("IsStocked") is not True or item.get("EnoughGold") is not True:
                continue
            item_index = item.get("Index", index)
            name = _clean_game_text(item.get("Name"), entry_type)
            description = _clean_game_text(item.get("Description"))
            candidates.append(
                _candidate(
                    f"buy_{entry_type}_{item_index}",
                    f"购买{entry_type}商品 {name}，价格 {item.get('Price', '?')}：{description}",
                    {
                        "action": "shop_action",
                        "shop_action": "buy",
                        "choice_type": entry_type,
                        "item_index": item_index,
                    },
                )
            )

    gold = shop.get("Gold")
    remove_cost = shop.get("RemoveCardCost")
    can_remove_card = (
        isinstance(gold, (int, float))
        and not isinstance(gold, bool)
        and isinstance(remove_cost, (int, float))
        and not isinstance(remove_cost, bool)
        and remove_cost > 0
        and gold >= remove_cost
    )
    if can_remove_card:
        candidates.append(
            _candidate(
                "shop_remove_card",
                f"支付 {remove_cost} 金币并移除一张牌",
                {"action": "shop_action", "shop_action": "remove_card"},
            )
        )
    if shop.get("InventoryOpen") is True:
        candidates.append(
            _candidate(
                "shop_close_inventory",
                "关闭商店商品面板",
                {"action": "shop_action", "shop_action": "leave"},
            )
        )
    elif shop.get("CanLeave") is not False:
        candidates.append(
            _candidate(
                "shop_leave",
                "离开商店",
                {"action": "shop_action", "shop_action": "leave"},
            )
        )
    return candidates


def _build_treasure_candidates(treasure: JsonObject) -> list[Candidate]:
    if treasure.get("ChestOpened") is not True:
        return [
            _candidate(
                "treasure_open",
                "打开宝箱",
                {"action": "treasure_action", "treasure_action": "open_chest"},
            )
        ]

    if treasure.get("IsPicking") is True:
        candidates: list[Candidate] = []
        for index, raw_relic in enumerate(_as_list(treasure.get("Relics"))):
            relic = _as_dict(raw_relic)
            relic_index = relic.get("Index", index)
            candidates.append(
                _candidate(
                    f"treasure_relic_{relic_index}",
                    f"选择遗物 {_clean_game_text(relic.get('Name'), str(relic_index))}：{_clean_game_text(relic.get('Description'))}",
                    {
                        "action": "treasure_action",
                        "treasure_action": "pick_relic",
                        "choice_index": relic_index,
                    },
                )
            )
        candidates.append(
            _candidate(
                "treasure_skip",
                "跳过遗物选择",
                {"action": "treasure_action", "treasure_action": "skip"},
            )
        )
        return candidates

    if treasure.get("CanLeave") is True:
        return [
            _candidate(
                "treasure_leave",
                "离开宝箱房间",
                {"action": "treasure_action", "treasure_action": "leave"},
            )
        ]
    return []


def _menu_candidate(
    key: str,
    description: str,
    menu_action: str,
    **parameters: Any,
) -> Candidate:
    action: JsonObject = {"action": "menu_action", "menu_action": menu_action}
    action.update(parameters)
    return _candidate(key, description, action)


def _build_menu_candidates(menu: JsonObject, allow_destructive: bool = False) -> list[Candidate]:
    """Map every menu/game-over action understood by GameHookServer."""

    screen = _text(menu.get("Screen"))
    is_submenu = menu.get("IsSubmenu") is True
    candidates: list[Candidate] = []

    if screen == "main_menu":
        if menu.get("CanContinue") is True:
            candidates.append(_menu_candidate("continue_run", "继续未完成的牌局", "continue_run"))
            if allow_destructive:
                candidates.append(_menu_candidate("abandon_run", "放弃当前存档并开始新的牌局", "abandon_run"))
        candidates.extend(
            [
                _menu_candidate("singleplayer", "打开单机模式菜单", "singleplayer"),
                _menu_candidate("multiplayer", "打开多人模式菜单", "multiplayer"),
                _menu_candidate("settings", "打开设置", "settings"),
                _menu_candidate("compendium", "打开百科", "compendium"),
            ]
        )
        if allow_destructive:
            candidates.append(_menu_candidate("quit", "退出游戏", "quit"))
        return candidates

    if screen == "singleplayer_submenu":
        return [
            _menu_candidate("standard", "开始标准单机游戏", "standard"),
            _menu_candidate("daily", "打开每日挑战", "daily"),
            _menu_candidate("custom", "打开自定义游戏", "custom"),
            _menu_candidate("back", "返回主菜单", "back"),
        ]

    if screen == "multiplayer_submenu":
        return [
            _menu_candidate("host_standard", "创建标准多人房间", "host_standard"),
            _menu_candidate("host_daily", "创建每日挑战房间", "host_daily"),
            _menu_candidate("host_custom", "创建自定义多人房间", "host_custom"),
            _menu_candidate("join_friend", "加入好友房间", "join_friend"),
            _menu_candidate("back", "返回主菜单", "back"),
        ]

    if screen == "multiplayer_host":
        return [
            _menu_candidate("standard", "创建标准多人游戏", "standard"),
            _menu_candidate("daily", "创建每日挑战多人游戏", "daily"),
            _menu_candidate("custom", "创建自定义多人游戏", "custom"),
            _menu_candidate("back", "返回多人菜单", "back"),
        ]

    if screen in {"character_select", "custom_run"}:
        indices = menu.get("AvailableCharacterIndices")
        if isinstance(indices, list):
            for raw_index in indices:
                if not isinstance(raw_index, int):
                    continue
                candidates.append(
                    _menu_candidate(
                        f"select_character_{raw_index}",
                        f"选择角色 index={raw_index}",
                        "select_character",
                        character_index=raw_index,
                    )
                )
        max_ascension = menu.get("MaxAscension")
        if not isinstance(max_ascension, int):
            max_ascension = 20
        for level in range(max(0, max_ascension) + 1):
            candidates.append(
                _menu_candidate(
                    f"set_ascension_{level}",
                    f"设置进阶等级为 {level}",
                    "set_ascension",
                    ascension_level=level,
                )
            )
        if menu.get("CanEmbark") is not False:
            candidates.append(
                _menu_candidate("embark", "确认角色与进阶并开始游戏", "embark")
            )
        candidates.append(_menu_candidate("back", "返回上一层菜单", "back"))
        return candidates

    if screen == "daily_run":
        candidates = []
        if menu.get("CanEmbark") is not False:
            candidates.append(_menu_candidate("daily_embark", "开始每日挑战", "embark"))
        candidates.append(_menu_candidate("back", "返回上一层菜单", "back"))
        return candidates

    if screen == "game_over":
        return [_menu_candidate("game_over_main_menu", "返回主菜单", "main_menu")]

    if screen in {"modal", "modal_confirm"}:
        candidates = [
            _menu_candidate("modal_confirm", "确认弹窗操作", "confirm"),
            _menu_candidate("modal_cancel", "取消弹窗操作", "cancel"),
        ]
        if screen == "modal":
            candidates.extend(
                [
                    _menu_candidate("modal_proceed", "关闭提示弹窗并继续", "proceed"),
                    _menu_candidate("modal_ok", "确认提示弹窗", "ok"),
                ]
            )
        return candidates

    if is_submenu:
        return [_menu_candidate("back", f"从 {screen} 返回上一层", "back")]
    return []


def build_candidates(state: JsonObject, allow_destructive_menu: bool = False) -> list[Candidate]:
    """Build only actions that the current snapshot says are legal."""

    # Older mods do not expose capacity: unknown must not be treated as full.
    empty_slots = _as_dict(state.get("Run")).get("EmptyPotionSlots")
    potion_slots_full = type(empty_slots) is int and empty_slots == 0
    phase = state.get("Phase")
    if phase == "combat":
        return _build_combat_candidates(_as_dict(state.get("Combat")))
    if phase == "map":
        return _build_map_candidates(_as_dict(state.get("Map")))
    if phase == "reward":
        return _build_reward_candidates(_as_dict(state.get("Reward")), potion_slots_full=potion_slots_full)
    if phase == "rest":
        return _build_rest_candidates(_as_dict(state.get("Rest")))
    if phase == "event":
        return _build_event_candidates(_as_dict(state.get("Event")))
    if phase == "shop":
        return _build_shop_candidates(_as_dict(state.get("Shop")), potion_slots_full=potion_slots_full)
    if phase == "treasure":
        return _build_treasure_candidates(_as_dict(state.get("Treasure")))
    if phase in {"menu", "game_over"}:
        return _build_menu_candidates(
            _as_dict(state.get("Menu")),
            allow_destructive=allow_destructive_menu,
        )
    return []


def compact_state_for_jev(state: JsonObject) -> JsonObject:
    """Keep the decision context small while preserving game semantics."""

    phase = state.get("Phase")
    view: JsonObject = {
        "phase": phase,
        "waiting_for_input": state.get("WaitingForInput"),
        "run": state.get("Run"),
    }

    if phase == "combat":
        combat = _as_dict(state.get("Combat"))
        view["combat"] = {
            "round": combat.get("RoundNumber"),
            "is_play_phase": combat.get("IsPlayPhase"),
            "energy": combat.get("Energy"),
            "max_energy": combat.get("MaxEnergy"),
            "player": combat.get("Player"),
            "hand": combat.get("Hand"),
            "enemies": combat.get("Enemies"),
            "potions": combat.get("Potions"),
            "card_selection": combat.get("CardSelection"),
            "hand_card_selection": combat.get("HandCardSelection"),
        }
    elif phase == "map":
        map_state = _as_dict(state.get("Map"))
        view["map"] = {
            "current_act": map_state.get("CurrentAct"),
            "current_floor": map_state.get("CurrentFloor"),
            "current_node_type": map_state.get("CurrentNodeType"),
            "available_nodes": map_state.get("AvailableNodes"),
        }
    elif phase in {"reward", "rest", "event", "shop", "treasure"}:
        field = {
            "reward": "Reward",
            "rest": "Rest",
            "event": "Event",
            "shop": "Shop",
            "treasure": "Treasure",
        }[phase]
        view[phase] = state.get(field)
        if phase == "reward":
            view["phase_rule"] = "必须先完成当前奖励或选牌界面，不能执行地图移动。"
    elif phase in {"menu", "game_over"}:
        view["menu"] = state.get("Menu")

    return view


def _create_typesafe_client() -> TypeSafeClient:
    """Create the reusable TypeSafe client used by one driver process."""

    # httpx2 defaults to truststore.SSLContext on Windows.  With Python 3.13,
    # truststore can recurse while changing verify_mode during TLS setup
    # through an HTTPS proxy.  A regular stdlib context keeps proxy support
    # while avoiding that recursion path.
    typesafe_http = httpx2.Client(verify=ssl.create_default_context())
    return TypeSafeClient(http_client=typesafe_http)


def _ask_jev_with_client(
    client: TypeSafeClient,
    state: JsonObject,
    candidates: list[Candidate],
) -> JevDecision:
    if not candidates:
        raise RuntimeError("没有可供 Jev 选择的合法动作")
    if len(candidates) > MAX_JEV_CHOICES:
        raise RuntimeError(
            f"候选动作数量为 {len(candidates)}，超过 Jev Choice 上限 {MAX_JEV_CHOICES}"
        )

    criteria = {candidate.key: candidate.description for candidate in candidates}
    response = client.system_one(
        # TypeSafe accepts structured JSON state natively.  Keep it as a
        # dict instead of converting it to a JSON string, which can send
        # the SDK through a recursive text/JSON normalization path.
        state=compact_state_for_jev(state),
        questions={
            "next_action": Choice(
                instructions=JEV_INSTRUCTIONS,
                criteria=criteria,
            )
        },
    )

    answer = response.answers["next_action"]
    key = str(answer.choice)
    probabilities = {
        str(option): float(probability)
        for option, probability in (getattr(answer, "probabilities", {}) or {}).items()
    }
    confidence = float(getattr(answer, "confidence", 0.0) or 0.0)
    model = getattr(response, "model", None)
    return JevDecision(key, confidence, probabilities, model)


def ask_jev(
    state: JsonObject,
    candidates: list[Candidate],
    *,
    client: TypeSafeClient | None = None,
) -> JevDecision:
    """Ask Jev, optionally using a caller-owned persistent client.

    The standalone form remains backward-compatible for small scripts. The
    long-running driver passes its process-scoped client so TLS connections
    and the HTTP connection pool can be reused across decisions.
    """

    if client is not None:
        return _ask_jev_with_client(client, state, candidates)
    with _create_typesafe_client() as owned_client:
        return _ask_jev_with_client(owned_client, state, candidates)


class JevDriver:
    def __init__(
        self,
        game_url: str,
        min_confidence: float,
        poll_interval: float,
        settle_timeout: float,
        log_format: str = DEFAULT_LOG_FORMAT,
        show_options: int = DEFAULT_SHOW_OPTIONS,
        description_length: int = DEFAULT_DESCRIPTION_LENGTH,
        verbose: bool = DEFAULT_VERBOSE,
        jev_client: TypeSafeClient | None = None,
    ) -> None:
        self.game_url = game_url.rstrip("/")
        self.min_confidence = min_confidence
        self.poll_interval = poll_interval
        self.settle_timeout = settle_timeout
        self.log_format = log_format
        self.show_options = show_options
        self.description_length = description_length
        self.verbose = verbose
        self.http = httpx.Client(timeout=DEFAULT_HTTP_TIMEOUT, trust_env=False)
        self.jev_client = jev_client if jev_client is not None else _create_typesafe_client()

    def _emit_json(self, payload: JsonObject) -> None:
        if self.log_format in {"json", "both"}:
            print(json.dumps(payload, ensure_ascii=False), flush=True)

    def _emit_human(self, lines: list[str]) -> None:
        if self.log_format in {"human", "both"}:
            print("\n".join(lines), file=sys.stderr, flush=True)

    def _print_candidates(
        self,
        decision_number: int,
        state: JsonObject,
        candidates: list[Candidate],
    ) -> None:
        if not self.verbose:
            return
        shown = candidates[: self.show_options]
        lines = [
            f"\n[决策 #{decision_number}] 发送给 Jev",
            f"  输入摘要: {_state_summary(state)} | 候选数={len(candidates)}",
            "  可选项:",
        ]
        for index, candidate in enumerate(shown, start=1):
            description = _one_line_text(candidate.description, self.description_length)
            lines.append(f"    {index:>2}. {candidate.key} — {description}")
        remaining = len(candidates) - len(shown)
        if remaining > 0:
            lines.append(f"    ... 其余 {remaining} 项未显示（可用 --show-options 调整）")
        self._emit_human(lines)

    def _print_decision(
        self,
        decision_number: int,
        state: JsonObject,
        candidates: list[Candidate],
        decision: JevDecision,
        response_time_ms: float,
        execute: bool,
    ) -> None:
        if not self.verbose:
            self._emit_human([_format_minimal_decision_line(decision, response_time_ms)])
            return
        by_key = {candidate.key: candidate for candidate in candidates}
        selected = by_key.get(decision.key)
        selected_description = (
            _one_line_text(selected.description, self.description_length)
            if selected is not None
            else "未注册候选"
        )
        self._emit_human(
            [
                f"  Jev 返回: {decision.key} — {selected_description}",
                f"  模型: {decision.model or 'unknown'} | 置信度: {decision.confidence:.2f}"
                f" | 响应时间: {response_time_ms:.0f} ms",
                "  概率 Top 3: "
                + "；".join(
                    _format_probability_lines(
                        decision,
                        candidates,
                        description_length=self.description_length,
                    )
                ),
                f"  计划动作: {_format_action(selected.action) if selected else '无'}"
                f" | 模式: {'execute' if execute else 'dry-run'}",
            ]
        )

    def _print_action_result(
        self,
        result: JsonObject,
        action_time_ms: float,
    ) -> None:
        if not self.verbose:
            return
        success = result.get("Success") is True
        detail = result.get("Error") or result.get("Message")
        status = "成功" if success else "失败"
        suffix = f" | {detail}" if detail else ""
        self._emit_human([f"  执行结果: {status} | 动作耗时: {action_time_ms:.0f} ms{suffix}"])

    def close(self) -> None:
        if self.jev_client is not None:
            self.jev_client.close()
            self.jev_client = None
        self.http.close()

    def get_state(self) -> JsonObject:
        response = self.http.get(f"{self.game_url}/state")
        response.raise_for_status()
        state = response.json()
        if not isinstance(state, dict):
            raise RuntimeError("/state 返回的不是 JSON object")
        return state

    def post_action(self, action: JsonObject) -> JsonObject:
        response = self.http.post(f"{self.game_url}/action", json=action)
        try:
            result = response.json()
        except ValueError:
            response.raise_for_status()
            raise RuntimeError("/action 返回的不是 JSON object")
        if not isinstance(result, dict):
            raise RuntimeError("/action 返回的不是 JSON object")
        # GameHookServer uses 422 for a valid request whose action was rejected
        # by the current game state.  Preserve its ActionResult body so the
        # caller can inspect the reason and refresh the snapshot.
        if response.is_error and "Success" not in result:
            response.raise_for_status()
        return result

    def wait_for_state_change(
        self,
        before_digest: str,
        *,
        before_phase: str | None,
        action: JsonObject,
    ) -> JsonObject:
        deadline = time.monotonic() + self.settle_timeout
        last_digest: str | None = None
        stable_polls = 0
        expected_selection_phase = _requires_card_selection(action)
        expected_reward_exit = _requires_reward_exit(action)
        last_state: JsonObject | None = None
        while time.monotonic() < deadline:
            time.sleep(self.poll_interval)
            try:
                state = self.get_state()
            except httpx.HTTPError:
                continue
            last_state = state
            digest = _state_digest(state)
            if digest == before_digest:
                continue
            phase = state.get("Phase")
            if expected_selection_phase is not None:
                if phase != expected_selection_phase or not _has_card_selection(
                    state, expected_selection_phase
                ):
                    continue
            elif expected_reward_exit and phase == "reward":
                continue
            elif before_phase == "combat" and phase == "map":
                # A combat victory should expose its reward before map routing.
                # During the animation the map can briefly be visible behind the
                # reward overlay; never accept that transient map as settled.
                continue
            if digest == last_digest:
                stable_polls += 1
                if stable_polls >= STATE_STABLE_POLLS:
                    return state
            else:
                last_digest = digest
                stable_polls = 1

        if expected_selection_phase is not None:
            raise RuntimeError(
                f"动作成功后未出现预期的 {expected_selection_phase} 选牌界面；"
                f"最后状态 phase={last_state.get('Phase') if last_state else 'unknown'}"
            )
        if expected_reward_exit and last_state is not None and last_state.get("Phase") == "reward":
            raise RuntimeError("跳过/完成奖励后奖励界面仍未关闭，已停止以避免重复选择 reward_skip")
        if before_phase == "combat" and last_state is not None and last_state.get("Phase") == "map":
            raise RuntimeError("战斗结束后在等待奖励时直接进入地图，已停止以避免跳过奖励")

        raise RuntimeError("动作完成后状态未稳定")

    def run(
        self,
        execute: bool,
        once: bool,
        allow_destructive_menu: bool = False,
        max_decisions: int | None = None,
    ) -> None:
        # Dry-run is intentionally one-shot so it cannot print the same
        # decision forever while the user is inspecting it.
        if not execute:
            once = True

        decision_count = 0
        while True:
            if max_decisions is not None and decision_count >= max_decisions:
                message = {
                    "status": "max_decisions_reached",
                    "decisions": decision_count,
                    "limit": max_decisions,
                }
                self._emit_json(message)
                if self.verbose:
                    self._emit_human(
                        [f"\n达到决策上限：{decision_count}/{max_decisions}，驱动结束。"]
                    )
                return

            state = self.get_state()
            phase = state.get("Phase")
            if phase == "loading":
                if once:
                    message = {"status": "not_in_run", "phase": phase}
                    self._emit_json(message)
                    if self.verbose:
                        self._emit_human([f"当前阶段为 {phase}，没有可执行决策。"])
                    return
                time.sleep(self.poll_interval)
                continue

            if state.get("WaitingForInput") is not True:
                if once:
                    message = {"status": "not_waiting", "phase": phase}
                    self._emit_json(message)
                    if self.verbose:
                        self._emit_human([f"当前阶段 {phase} 尚未等待输入，单步模式结束。"])
                    return
                time.sleep(self.poll_interval)
                continue

            candidates = build_candidates(state, allow_destructive_menu=allow_destructive_menu)
            if not candidates:
                message = {
                    "status": "unsupported_or_no_legal_action",
                    "phase": phase,
                }
                self._emit_json(message)
                if self.verbose:
                    self._emit_human(
                        [f"当前阶段 {phase} 没有可用候选，等待下一次状态刷新。"]
                    )
                if once:
                    return
                time.sleep(self.poll_interval)
                continue

            next_decision_number = decision_count + 1
            self._print_candidates(next_decision_number, state, candidates)
            request_started = time.perf_counter()
            decision = ask_jev(state, candidates, client=self.jev_client)
            response_time_ms = (time.perf_counter() - request_started) * 1000.0
            by_key = {candidate.key: candidate for candidate in candidates}
            selected = by_key.get(decision.key)
            if selected is None:
                raise RuntimeError(f"Jev 返回了未注册的候选 key: {decision.key}")
            decision_count += 1

            decision_message = {
                "status": "decision",
                "phase": phase,
                "model": decision.model,
                "choice": decision.key,
                "choice_description": selected.description,
                "confidence": decision.confidence,
                "probabilities": decision.probabilities,
                "action": selected.action,
                "execute": execute,
                "decision_index": decision_count,
                "decision_limit": max_decisions,
                "candidate_count": len(candidates),
                "response_time_ms": round(response_time_ms, 1),
            }
            self._emit_json(decision_message)
            self._print_decision(
                decision_count,
                state,
                candidates,
                decision,
                response_time_ms,
                execute,
            )

            if self.min_confidence > 0.0 and decision.confidence < self.min_confidence:
                message = (
                    f"低于置信度阈值 {self.min_confidence:.2f}，停止执行。"
                )
                if self.verbose:
                    self._emit_human([f"  {message}"])
                self._emit_json(
                    {
                        "status": "confidence_gate",
                        "confidence": decision.confidence,
                        "threshold": self.min_confidence,
                        "message": message,
                    }
                )
                return

            if not execute:
                if self.verbose:
                    self._emit_human(["  dry-run：仅展示决策，不向游戏发送动作。"])
                return

            before_digest = _state_digest(state)
            action_started = time.perf_counter()
            result = self.post_action(selected.action)
            action_time_ms = (time.perf_counter() - action_started) * 1000.0
            self._emit_json(
                {
                    "status": "action_result",
                    "result": result,
                    "action_time_ms": round(action_time_ms, 1),
                }
            )
            self._print_action_result(result, action_time_ms)
            if result.get("Success") is not True:
                # A 422 can be caused by a stale transitional snapshot (most
                # notably immediately after a map vote).  In continuous mode,
                # refresh state and ask Jev again instead of terminating.
                if once:
                    return
                time.sleep(self.poll_interval)
                continue
            if once:
                if self.verbose:
                    self._emit_human(["  单步模式：动作已发送，不等待后续状态。"])
                return
            settle_started = time.perf_counter()
            settled_state = self.wait_for_state_change(
                before_digest,
                before_phase=phase,
                action=selected.action,
            )
            settle_time_ms = (time.perf_counter() - settle_started) * 1000.0
            settled_phase = settled_state.get("Phase")
            self._emit_json(
                {
                    "status": "state_settled",
                    "before_phase": phase,
                    "after_phase": settled_phase,
                    "settle_time_ms": round(settle_time_ms, 1),
                }
            )
            if self.verbose:
                self._emit_human(
                    [
                        f"  状态已稳定: {phase} -> {settled_phase}"
                        f" | 等待耗时: {settle_time_ms:.0f} ms",
                        f"  新状态摘要: {_state_summary(settled_state)}",
                    ]
                )


def main() -> int:
    parser = argparse.ArgumentParser(description="Use TypeSafe Jev to drive autoSpire")
    parser.add_argument(
        "--execute",
        action="store_true",
        help="actually POST the selected action; without it, only print the decision",
    )
    parser.add_argument(
        "--once",
        action="store_true",
        help="execute at most one decision, then exit",
    )
    parser.add_argument(
        "--max-decisions",
        type=int,
        default=None,
        help="execute at most N decisions, then exit; useful for bounded live tests",
    )
    parser.add_argument(
        "--game-url",
        default=None,
        help="game server URL; defaults to AUTOSPIRE_URL or auto-detected port 8770-8779",
    )
    parser.add_argument(
        "--allow-destructive-menu",
        action="store_true",
        help="also expose menu actions that abandon a run or quit the game",
    )
    parser.add_argument(
        "--min-confidence",
        type=float,
        default=float(os.environ.get("JEV_MIN_CONFIDENCE", DEFAULT_MIN_CONFIDENCE)),
        help=(
            "optional confidence gate; 0 disables blocking and only logs confidence "
            f"(default: {DEFAULT_MIN_CONFIDENCE})"
        ),
    )
    parser.add_argument(
        "--poll-interval",
        type=float,
        default=DEFAULT_POLL_INTERVAL,
        help=f"poll interval in seconds (default: {DEFAULT_POLL_INTERVAL})",
    )
    parser.add_argument(
        "--log-format",
        choices=("human", "json", "both"),
        default=os.environ.get("JEV_LOG_FORMAT", DEFAULT_LOG_FORMAT),
        help=(
            "console output format: human (default), json (JSON Lines), "
            "or both (human stderr + JSON stdout)"
        ),
    )
    parser.add_argument(
        "--show-options",
        type=int,
        default=DEFAULT_SHOW_OPTIONS,
        help=f"number of candidate options to print (default: {DEFAULT_SHOW_OPTIONS})",
    )
    parser.add_argument(
        "--description-length",
        type=int,
        default=DEFAULT_DESCRIPTION_LENGTH,
        help=(
            "maximum characters per option description in human logs "
            f"(default: {DEFAULT_DESCRIPTION_LENGTH})"
        ),
    )
    parser.add_argument(
        "--verbose",
        action="store_true",
        help="显示候选列表、概率、执行结果和状态转换；默认只打印每次 Jev 决策一行",
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="print the full traceback when the driver fails",
    )
    args = parser.parse_args()
    if args.max_decisions is not None and args.max_decisions <= 0:
        parser.error("--max-decisions must be a positive integer")
    if args.show_options <= 0:
        parser.error("--show-options must be a positive integer")
    if args.description_length < 40:
        parser.error("--description-length must be at least 40")
    if args.log_format not in {"human", "json", "both"}:
        parser.error("--log-format must be human, json, or both")

    if not os.environ.get("TYPESAFE_API_KEY"):
        print("缺少环境变量 TYPESAFE_API_KEY；不会读取硬编码密钥。", file=sys.stderr)
        return 2
    if not 0.0 <= args.min_confidence <= 1.0:
        print("--min-confidence 必须在 0 到 1 之间。", file=sys.stderr)
        return 2

    game_url = args.game_url or _detect_game_url()
    if args.verbose:
        print(f"[jev_driver] game server: {game_url}", file=sys.stderr)
        print(f"[jev_driver] mode: {'execute' if args.execute else 'dry-run'}", file=sys.stderr)
        print(
            f"[jev_driver] log format: {args.log_format}, show options: {args.show_options}",
            file=sys.stderr,
        )

    driver = JevDriver(
        game_url=game_url,
        min_confidence=args.min_confidence,
        poll_interval=args.poll_interval,
        settle_timeout=DEFAULT_ACTION_SETTLE_TIMEOUT,
        log_format=args.log_format,
        show_options=args.show_options,
        description_length=args.description_length,
        verbose=args.verbose,
    )
    try:
        driver.run(
            execute=args.execute,
            once=args.once,
            allow_destructive_menu=args.allow_destructive_menu,
            max_decisions=args.max_decisions,
        )
    except httpx.HTTPError as exc:
        print(f"HTTP error: {exc}", file=sys.stderr)
        if args.debug:
            traceback.print_exc()
        return 1
    except Exception as exc:
        print(f"jev_driver error: {exc}", file=sys.stderr)
        if args.debug:
            traceback.print_exc()
        return 1
    finally:
        driver.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

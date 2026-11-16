"""Offline regression tests for the Jev-to-autoSpire action mapping.

These tests never call TypeSafe and never touch the running game.  They verify
that a snapshot can produce the same atomic action families exposed by the
MCP adapter, while deliberately keeping multi_play out of Jev's choice space.
"""

from __future__ import annotations

import unittest
from unittest.mock import Mock, patch

from mcp.jev_driver import (
    DEFAULT_MIN_CONFIDENCE,
    JEV_INSTRUCTIONS,
    JevDecision,
    JevDriver,
    _requires_reward_exit,
    _format_action,
    _format_minimal_decision_line,
    _format_probability_lines,
    _one_line_text,
    build_candidates,
    compact_state_for_jev,
)


def _state(phase: str, payload: dict) -> dict:
    return {"Phase": phase, "WaitingForInput": True, **payload}


class JevDriverMappingTests(unittest.TestCase):
    def test_event_grid_preview_requires_confirmation(self) -> None:
        selection = {"Options": [{"Name": "打击"}, {"Name": "防御"}], "CanConfirm": False}
        state = _state("event", {"Event": {"CardSelection": selection}})
        self.assertEqual(
            [candidate.key for candidate in build_candidates(state)],
            ["event_select_0", "event_select_1"],
        )
        selection["CanConfirm"] = True
        candidates = build_candidates(state)
        self.assertEqual([candidate.key for candidate in candidates], ["event_select_confirm"])
        self.assertEqual(candidates[0].action, {"action": "confirm_selection"})

    def test_full_potion_slots_filter_rewards_and_shop_without_reindexing(self) -> None:
        for slots in (0, 1, None):
            with self.subTest(empty_slots=slots):
                run = {} if slots is None else {"EmptyPotionSlots": slots}
                rewards = build_candidates(_state("reward", {
                    "Run": run,
                    "Reward": {"Items": [
                        {"Index": 0, "Type": "potion"},
                        {"Index": 2, "Type": "gold"},
                    ], "CanSkip": True},
                }))
                keys = {c.key for c in rewards}
                self.assertEqual("reward_0_potion" in keys, slots != 0)
                self.assertIn("reward_skip", keys)
                gold = next(c for c in rewards if c.key == "reward_2_gold")
                self.assertEqual(gold.action["choice_index"], 2)
                shop = build_candidates(_state("shop", {
                    "Run": run,
                    "Shop": {"Potions": [
                        {"Index": 3, "IsStocked": True, "EnoughGold": True},
                    ]},
                }))
                self.assertEqual("buy_potion_3" in {c.key for c in shop}, slots != 0)

    def test_prompt_and_confidence_defaults_are_non_blocking(self) -> None:
        self.assertEqual(DEFAULT_MIN_CONFIDENCE, 0.0)
        self.assertIn("只能选择 criteria 中提供的候选 key", JEV_INSTRUCTIONS)
        self.assertIn("只能执行一个动作", JEV_INSTRUCTIONS)
        self.assertIn("不能直接选择地图路线", JEV_INSTRUCTIONS)
        self.assertIn("奖励界面关闭", JEV_INSTRUCTIONS)

    def test_reward_skip_requires_leaving_reward_phase(self) -> None:
        self.assertTrue(
            _requires_reward_exit(
                {"action": "pick_reward", "choice_type": "skip"}
            )
        )
        self.assertFalse(
            _requires_reward_exit(
                {"action": "pick_reward", "choice_type": "card"}
            )
        )

    def test_combat_exposes_atomic_actions_only(self) -> None:
        state = _state(
            "combat",
            {
                "Combat": {
                    "IsPlayPhase": True,
                    "Energy": 3,
                    "Hand": [
                        {
                            "Name": "打击",
                            "Cost": 1,
                            "CanPlay": True,
                            "NeedsTarget": True,
                            "ValidTargetIds": [7],
                        },
                        {
                            "Name": "防御",
                            "Cost": 1,
                            "CanPlay": True,
                            "NeedsTarget": False,
                            "ValidTargetIds": [],
                        },
                    ],
                    "Enemies": [{"CombatId": 7, "IsAlive": True, "IsHittable": True}],
                    "Potions": [
                        {"SlotIndex": 0, "Name": "攻击药水", "TargetType": "AnyEnemy"},
                        {"SlotIndex": 1, "Name": "能量药水", "TargetType": "AnyPlayer"},
                    ],
                }
            },
        )

        actions = [candidate.action for candidate in build_candidates(state)]
        action_types = {action["action"] for action in actions}
        self.assertEqual(action_types, {"play_card", "use_potion", "end_turn"})
        self.assertIn(
            {"action": "play_card", "hand_index": 0, "target_id": 7},
            actions,
        )
        self.assertIn(
            {"action": "use_potion", "slot_index": 0, "target_id": 7},
            actions,
        )
        self.assertIn({"action": "use_potion", "slot_index": 1}, actions)

    def test_combat_candidate_describes_target_outcome(self) -> None:
        state = _state(
            "combat",
            {
                "Combat": {
                    "IsPlayPhase": True,
                    "Hand": [
                        {
                            "Name": "完美打击",
                            "Cost": 2,
                            "Type": "Attack",
                            "Description": "造成[green]26[/green]点伤害。",
                            "CanPlay": True,
                            "NeedsTarget": True,
                            "ValidTargetIds": [2],
                        }
                    ],
                    "Enemies": [
                        {
                            "CombatId": 2,
                            "Name": "盛碗虫（卵）",
                            "CurrentHp": 3,
                            "MaxHp": 22,
                            "Block": 7,
                            "IntentLabel": "这个敌人将要[gold]攻击[/gold]造成[blue]7[/blue]点伤害。",
                            "Buffs": [],
                            "IsAlive": True,
                            "IsHittable": True,
                        }
                    ],
                }
            },
        )

        candidate = build_candidates(state)[0]
        self.assertIn("盛碗虫（卵）", candidate.description)
        self.assertIn("CombatId=2", candidate.description)
        self.assertIn("HP=3/22", candidate.description)
        self.assertIn("格挡=7", candidate.description)
        self.assertIn("意图=攻击7", candidate.description)
        self.assertIn("扣除格挡后=19", candidate.description)
        self.assertIn("预计击杀", candidate.description)
        self.assertNotIn("[green]", candidate.description)

    def test_all_in_run_phases_have_candidates(self) -> None:
        states = [
            _state("map", {"Map": {"AvailableNodes": [{"Col": 1, "Row": 2, "NodeType": "Monster"}]}}),
            _state("reward", {"Reward": {"Items": [{"Index": 0, "Type": "gold", "Name": "金币"}], "CanSkip": True}}),
            _state("rest", {"Rest": {"Options": [{"Index": 0, "Name": "回复"}]}}),
            _state("event", {"Event": {"Options": [{"Index": 0, "Text": "接受", "IsLocked": False}]}}),
            _state(
                "shop",
                {
                    "Shop": {
                        "InventoryOpen": False,
                        "CanLeave": True,
                        "RemoveCardCost": 0,
                        "CharacterCards": [],
                        "ColorlessCards": [],
                        "Relics": [],
                        "Potions": [],
                    }
                },
            ),
            _state("treasure", {"Treasure": {"ChestOpened": False}}),
        ]

        for state in states:
            with self.subTest(phase=state["Phase"]):
                candidates = build_candidates(state)
                self.assertTrue(candidates)
                self.assertNotIn("multi_play", {candidate.action["action"] for candidate in candidates})

    def test_selection_and_shop_actions_remain_atomic(self) -> None:
        reward_selection = _state(
            "reward",
            {
                "Reward": {
                    "CardSelection": {
                        "Prompt": "选择一张牌",
                        "Options": [{"Name": "打击", "Cost": 1}],
                    }
                }
            },
        )
        reward_actions = build_candidates(reward_selection)
        self.assertEqual(reward_actions[0].action, {"action": "pick_card", "card_index": 0})

        shop = _state(
            "shop",
            {
                "Shop": {
                    "Gold": 100,
                    "InventoryOpen": False,
                    "CanLeave": True,
                    "RemoveCardCost": 75,
                    "CharacterCards": [
                        {"Index": 0, "Name": "打击", "IsStocked": True, "EnoughGold": True, "Price": 50}
                    ],
                    "ColorlessCards": [],
                    "Relics": [],
                    "Potions": [],
                }
            },
        )
        shop_actions = {candidate.key: candidate.action for candidate in build_candidates(shop)}
        self.assertEqual(
            shop_actions["shop_open_inventory"],
            {"action": "shop_action", "shop_action": "open_inventory"},
        )
        self.assertEqual(
            shop_actions["buy_character_card_0"],
            {
                "action": "shop_action",
                "shop_action": "buy",
                "choice_type": "character_card",
                "item_index": 0,
            },
        )
        self.assertEqual(
            shop_actions["shop_remove_card"],
            {"action": "shop_action", "shop_action": "remove_card"},
        )

    def test_shop_filters_unaffordable_removal_and_can_close_inventory(self) -> None:
        shop = _state(
            "shop",
            {
                "Shop": {
                    "Gold": 7,
                    "InventoryOpen": True,
                    "CanLeave": False,
                    "RemoveCardCost": 75,
                    "CharacterCards": [],
                    "ColorlessCards": [],
                    "Relics": [],
                    "Potions": [],
                }
            },
        )
        actions = {candidate.key: candidate.action for candidate in build_candidates(shop)}
        self.assertNotIn("shop_remove_card", actions)
        self.assertEqual(
            actions["shop_close_inventory"],
            {"action": "shop_action", "shop_action": "leave"},
        )

    def test_menu_and_game_over_actions(self) -> None:
        main_menu = _state(
            "menu",
            {"Menu": {"Screen": "main_menu", "CanContinue": True, "IsSubmenu": False}},
        )
        safe_keys = {candidate.key for candidate in build_candidates(main_menu)}
        self.assertIn("continue_run", safe_keys)
        self.assertNotIn("abandon_run", safe_keys)
        self.assertNotIn("quit", safe_keys)

        destructive_keys = {
            candidate.key
            for candidate in build_candidates(main_menu, allow_destructive_menu=True)
        }
        self.assertIn("abandon_run", destructive_keys)
        self.assertIn("quit", destructive_keys)

        not_ready = _state(
            "menu",
            {
                "Menu": {
                    "Screen": "character_select",
                    "IsSubmenu": True,
                    "AvailableCharacterIndices": [0, 1],
                    "MaxAscension": 2,
                    "CanEmbark": False,
                }
            },
        )
        self.assertNotIn("embark", {candidate.key for candidate in build_candidates(not_ready)})

        game_over = _state(
            "game_over",
            {"Menu": {"Screen": "game_over", "CanContinue": False, "IsSubmenu": False}},
        )
        self.assertEqual(
            build_candidates(game_over)[0].action,
            {"action": "menu_action", "menu_action": "main_menu"},
        )

    def test_treasure_leave_is_gated_by_snapshot(self) -> None:
        pending = _state(
            "treasure",
            {"Treasure": {"ChestOpened": True, "IsPicking": False, "CanLeave": False}},
        )
        self.assertEqual(build_candidates(pending), [])

        ready = _state(
            "treasure",
            {"Treasure": {"ChestOpened": True, "IsPicking": False, "CanLeave": True}},
        )
        self.assertEqual(build_candidates(ready)[0].action["treasure_action"], "leave")

    def test_compact_menu_state_is_sent_as_context(self) -> None:
        state = _state(
            "menu",
            {"Menu": {"Screen": "singleplayer_submenu", "IsSubmenu": True}},
        )
        compact = compact_state_for_jev(state)
        self.assertEqual(compact["menu"]["Screen"], "singleplayer_submenu")

    def test_compact_reward_state_forbids_map_before_reward_completion(self) -> None:
        state = _state(
            "reward",
            {"Reward": {"Items": [], "CanSkip": True}},
        )
        compact = compact_state_for_jev(state)
        self.assertEqual(
            compact["phase_rule"],
            "必须先完成当前奖励或选牌界面，不能执行地图移动。",
        )

    def test_human_log_helpers_include_option_names_and_actions(self) -> None:
        state = _state(
            "reward",
            {
                "Run": {"CurrentAct": 1, "CurrentFloor": 2, "Gold": 113},
                "Reward": {
                    "Items": [
                        {
                            "Index": 0,
                            "Type": "card",
                            "Name": "选牌",
                            "Description": "选择一张卡牌",
                            "CardOptions": [{"Name": "完美打击"}],
                        }
                    ],
                    "CanSkip": True,
                },
            },
        )
        candidates = build_candidates(state)
        decision = JevDecision(
            "reward_0_card",
            0.75,
            {"reward_0_card": 0.75, "reward_skip": 0.25},
            "jev-test",
        )
        probability_lines = _format_probability_lines(decision, candidates)

        self.assertIn("完美打击", probability_lines[0])
        self.assertIn("reward_0_card=0.75", probability_lines[0])
        self.assertEqual(
            _format_action(
                {"action": "pick_reward", "choice_index": 0, "choice_type": "card"}
            ),
            "领取奖励（choice_index=0, choice_type=card）",
        )
        self.assertTrue(_one_line_text("第一行\n第二行", 20).startswith("第一行 第二行"))
        self.assertTrue(_one_line_text("很长的描述" * 30, 40).endswith("..."))

    def test_default_decision_log_is_one_line(self) -> None:
        line = _format_minimal_decision_line(
            JevDecision("play_card_1_target_2", 0.62, {}, "jev-test"),
            843.4,
        )
        self.assertEqual(
            line,
            "[Jev] 选择=play_card_1_target_2 | 置信度=0.62 | 响应时间=843 ms",
        )

    def test_driver_reuses_and_closes_process_scoped_jev_client(self) -> None:
        state = _state(
            "map",
            {"Map": {"AvailableNodes": [{"Col": 1, "Row": 2, "NodeType": "Monster"}]}},
        )
        jev_client = Mock()
        decision = JevDecision("map_node_0", 0.9, {}, "jev-test")
        driver = JevDriver(
            game_url="http://localhost:8770",
            min_confidence=0.0,
            poll_interval=0.01,
            settle_timeout=1.0,
            jev_client=jev_client,
        )
        driver.get_state = Mock(return_value=state)

        with patch("mcp.jev_driver.ask_jev", return_value=decision) as ask:
            driver.run(execute=False, once=True)

        ask.assert_called_once()
        self.assertIs(ask.call_args.kwargs["client"], jev_client)
        driver.close()
        jev_client.close.assert_called_once_with()


if __name__ == "__main__":
    unittest.main()

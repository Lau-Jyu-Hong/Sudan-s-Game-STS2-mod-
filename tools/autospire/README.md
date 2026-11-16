# autoSpire — AI 自动爬塔

[杀戮尖塔 2](https://store.steampowered.com/app/2868840) 的 AI 自走 mod。嵌入式 HTTP 服务 + MCP 协议，让 AI Agent（Claude Code / Codex / OpenCode）驱动游戏决策。

## 架构

```
AI Agent (Claude Code / Codex / OpenCode) ←─ MCP stdio ─→ mcp/server.py ←─ HTTP ─→ C# GameHookServer (游戏内)
```

- **C# GameHookServer**：嵌入游戏进程，主线程每帧刷新状态快照 + 消费 HTTP 动作队列
- **Python MCP 适配器**：将 HTTP API 封装为 MCP 工具（`get_state` / `take_action`），纯透传无业务逻辑
- **Godot Mod**：`[ModInitializer]` 入口，注册 Harmony patch + 启动 HTTP 服务

## 快速开始

### 前置条件

- [Slay the Spire 2](https://store.steampowered.com/app/2868840)
- Python 3.10+
- [Claude Code](https://claude.ai/code)（推荐）、[Codex](https://github.com/openai/codex) 或 [OpenCode](https://github.com/ssttuu/opencode)

### 三步上手

**① 解压安装**

从 [Releases](../../releases) 下载 `autoSpire-vX.X.X.zip` 解压。将 `autoSpire/` 放入游戏 mod 目录，然后安装 Python 依赖：

```
将 autoSpire/ 复制到 <Steam库>\...\Slay the Spire 2\mods\autoSpire\
pip install -r mcp/requirements.txt
```

解压后结构：
```
├── .mcp.json              # MCP 配置（Claude Code 自动发现 / Codex 可读）
├── autoSpire/
│   ├── autoSpire.dll      # Mod 本体
│   └── autoSpire.json     # Mod 清单
└── mcp/
    ├── server.py           # MCP 适配器
    ├── jev_driver.py       # 可选的 Jev 决策驱动
    └── requirements.txt    # Python 依赖
```

**② 把 MCP 接入 Agent**

解压目录已包含 `.mcp.json`，Claude Code 打开文件夹即自动发现 MCP Server；较新的 Codex CLI 在项目目录下也会读取它。

- **Claude Code**：无需额外配置，直接打开解压目录即可。
- **Codex**：推荐用 CLI 注册到全局配置（所有项目可用）：

  ```bash
  codex mcp add autospire -- python "<解压目录>\mcp\server.py"
  ```

  等价于在 `~/.codex/config.toml` 中写入：

  ```toml
  [mcp_servers.autospire]
  command = "python"
  args = ["<解压目录>\\mcp\\server.py"]
  ```

  也可以写在项目的 `.codex/config.toml`（仅受信项目生效）。修改配置后需**新建** Codex 对话（已存在的旧对话不会补发 MCP 工具），启动游戏后即可看到 `get_state` / `take_action` 工具。
- **OpenCode 等其他 Agent**：在各自的 MCP 配置中注册同一个 stdio server：命令 `python`，参数 `mcp/server.py`（或绝对路径）。

> **双人联机**：`.mcp.json` 已内置 `autospire2`（端口 8771）。Codex 可执行 `codex mcp add autospire2 --env AUTOSPIRE_PORT=8771 -- python "<解压目录>\mcp\server.py"` 添加第二个服务；启动第二个游戏客户端即可双人爬塔。

**③ 开始使用**

启动游戏，确认日志 `[autoSpire] autoSpire mod initialized!`，然后在 Agent 中：

```
> 开始一局标准单机游戏
```

### 自行编译

```bash
# 修改 autoSpire.csproj 中的 <Sts2Dir> 为游戏路径，然后：
dotnet build
```

如果游戏正在运行并锁定已加载的 DLL，可只编译不复制：`dotnet build -p:SkipModCopy=true`；重新启动游戏后再执行普通 `dotnet build` 部署。

### Jev 决策驱动（可选）

`mcp/jev_driver.py` 是一个独立的 Python 控制器：读取游戏 `/state`，把当前合法动作映射为 Jev 的 `Choice` 候选，收到候选 key 后再调用游戏 `/action`。它不修改 C# mod，也不会把 API Key 放进仓库。

先安装依赖并设置 TypeSafe API Key：

```powershell
python -m pip install -r mcp/requirements.txt
$env:TYPESAFE_API_KEY = "你的 TypeSafe API Key"
```

`mcp/requirements.txt` 将 MCP SDK 限定在 `1.x`，因为当前 `mcp/server.py` 使用的是 MCP Python SDK v1 的 `Server.list_tools()` 接口；直接安装 `mcp 2.x` 会导致 stdio server 启动后无法列出工具。

默认是 dry-run，只打印 Jev 决策，不操作游戏：

```powershell
python mcp/jev_driver.py
```

默认控制台为极简模式，每次决策只输出一行：候选操作 key、置信度和响应时间。需要查看候选描述、Top 概率、执行结果和阶段转换时，加 `--verbose`；也可以用 `--log-format json` 输出 JSON Lines 供脚本处理。

```powershell
python mcp/jev_driver.py --verbose --once
```

确认决策后执行一次动作：

```powershell
python mcp/jev_driver.py --execute --once
```

持续驱动当前对局：

```powershell
python mcp/jev_driver.py --execute
```

当前覆盖 MCP 的全部 Jev 所需原子动作类别：战斗单张出牌、选牌、地图、奖励、休息点、事件、商店、宝箱、菜单和结算页。`multi_play` 保留给传统大语言模型的高吞吐调用，不暴露给 Jev。默认不会把“放弃存档”和“退出游戏”暴露给 Jev；确需自动化时加 `--allow-destructive-menu`。Jev 默认只记录置信度、不因置信度停止执行；如需启用安全闸门，可使用 `--min-confidence 0.45` 或设置 `JEV_MIN_CONFIDENCE`。

## API

### GET `/state` — 游戏状态快照

返回 `GameStateSnapshot` JSON。根据 `phase` 字段，仅当前场景对应子快照非 null：

| Phase | 字段 | 内容 |
|-------|------|------|
| `combat` | `combat` | 手牌/能量/敌人(意图+buff)/药水/牌堆计数/卡牌选牌状态 |
| `map` | `map` | 当前节点 + 下一层可选 + 全地图节点 |
| `shop` | `shop` | 金币 + 商品列表(卡牌/遗物/药水/删牌) |
| `reward` | `reward` | 奖励项列表 + 可选卡牌/遗物 |
| `rest` | `rest` | 休息选项 + 升级/移除选牌界面 |
| `event` | `event` | 事件名称/描述/选项(含关联卡牌/遗物预览) |
| `treasure` | `treasure` | 宝箱状态/可选遗物/投票 |
| `game_over` | `menu` | 游戏结束结算界面（死亡/通关），`menu.screen="game_over"`，可用 `menu_action=main_menu` 返回主菜单 |
| `menu` | `menu` | 游戏外界面(主菜单/角色选择/子菜单等) |

所有阶段均有 `run` 字段：章节/层数/金币/牌组。

### POST `/action` — 提交动作

返回 `ActionResult` JSON：

```json
{
  "success": true,
  "error": null,
  "played_card_name": "打击",          // 仅 play_card
  "played_card_target": {              // 仅 play_card
    "type": "enemy",                   // enemy / self / none
    "combat_id": 2,
    "name": "小啃兽"
  },
  "hand_card_selection": { ... },      // 仅 pick_card / confirm_selection
  "card_selection": { ... }            // 仅 pick_card / confirm_selection
}
```

| 动作 | 必填参数 | 可选参数 | 说明 |
|------|----------|----------|------|
| `play_card` | `hand_index` | `target_id` | 出牌。支持 AOE/随机目标（无需 target_id）。返回 `played_card_name` + `played_card_target` |
| `multi_play` | `cards` | — | 一次出多张牌。`cards` 为 `[{hand_index, target_id?}]` 数组。返回 `played_card_name`（尝试列表） |
| `end_turn` | — | — | 结束回合 |
| `use_potion` | `slot_index` | `target_id` | 使用药水。`target_id` 仅对 `AnyEnemy` 类药水必填；全体/随机/自身类药水请勿传 |
| `pick_card` | `card_index` | — | 通用选牌（手牌选择 / overlay 选牌）。返回最新选牌状态 |
| `confirm_selection` | — | — | 确认手牌选择（净化等多选手动确认） |
| `move_to_map_coord` | `col`, `row` | — | 选路线 |
| `pick_reward` | `choice_index`, `choice_type` | `card_index` | 选奖励（card/relic/gold/skip）；有 CardSelection 子界面时传 card_index。在选牌子界面用 `pick_card` 选完牌后，需再调用一次 `pick_reward choice_type=skip`（或继续拿金币/药水）关闭奖励界面 |
| `rest_action` | — | `option_index`, `card_index` | 休息点操作；选项用完（`rest.options=[]`、`can_proceed=true`）时不带 `option_index` 调用即可继续前进 |
| `shop_action` | — | — | 商店操作（buy/remove_card/leave） |
| `treasure_action` | — | — | 宝箱操作（open_chest/pick_relic/skip/leave；pick_relic 时配合 choice_index） |
| `event_action` | `option_index` | — | 事件选项（支持多人投票、选牌） |
| `menu_action` | — | — | 游戏外操作：continue_run/singleplayer/multiplayer/standard/daily/custom/select_character/set_ascension/embark/back/abandon_run/confirm/cancel 等；结算页（game_over）支持 `main_menu` 返回主菜单；弹窗（如“进阶难度已解锁”）可用 `proceed` 关闭 |

## 功能状态

### 已完成 & 已测试（主客机双端）

- 战斗状态获取：手牌(含 Damage/Block/描述)/能量/敌人(HP+意图+buff)/药水(含描述)/牌堆计数/选牌状态
- 出牌：支持有目标(单敌)、自目标(技能/能力)、AOE/随机目标(AllEnemies/RandomEnemy/AllAllies)、卡牌选牌触发(净化/头槌/洁净)
- **批量出牌**：`multi_play` 一次调用打出多张牌，减少 AI 调用次数。两阶段设计（先解析全部 CardModel 引用再入队）避免 index 偏移问题
- 用药水：自目标 + 敌方目标（快照含 `target_type` 字段供 AI 判断是否需传 `target_id`），药水选牌触发(灰水)
- 选牌：Overlay 选牌/手牌选择模式/奖励选牌/商店删牌选牌，描述含正确数值
- 选牌状态即时返回：`pick_card` 后 ActionResult 含更新后牌列表
- 出牌返回值：`played_card_name` + `played_card_target`（含目标类型/ID/名称）
- 地图状态 + 选路线(VoteForMapCoordAction)
- 奖励选择：card/gold/relic/potion/skip
- 休息点：回复/锻造/移除 + 升级/移除选牌界面
- **商店全链路**：商品列表（角色卡/无色卡/遗物/药水/删牌含价格库存）+ buy / remove_card / leave（自动开/关界面）
- **事件全链路**：事件描述 + 选项（含关联卡牌/遗物预览）+ 单人/合作多人投票 + 事件中选牌 + 古之民事件
- **宝箱房间**：宝箱开启 + 遗物选择（单人/多人投票，自动分配）
- **游戏结束检测**：死亡/通关后正确识别 `game_over` 阶段（不再误判为 map），并支持 `menu_action=main_menu` 从结算页返回主菜单
- **游戏外界面检测**：主菜单 / 角色选择 / 单机子菜单 / 多人子菜单 / 建房 / 每日 / 自定义局 / 弹窗等 `menu` 阶段，20+ 种界面类型识别
- **游戏外操作**：`menu_action` 支持从主菜单到开始游戏全流程（角色选择/进阶设置/每日挑战/自定义局），含放弃存档确认弹窗处理
- 图标清理：`[img]` 能量/星图标标签自动替换为文字
- Run 级别信息：遗物(名称+描述)、牌组(合并计数)
- JSON 输出优化：省略所有 null 字段，显著减少 token 消耗

## 多端支持

同一份 MCP 适配器通过 `AUTOSPIRE_PORT` 环境变量区分：

- `autospire`（默认 8770）— 主机
- `autospire2`（8771）— 客机

游戏内 GameHookServer 默认从 8770 开始，端口被占用时自动递增（最多尝试 10 个端口）。Python 适配器未设置 `AUTOSPIRE_PORT` 时会自动探测 8770–8779，找到游戏返回的 `/state` 即连接，所以即使 8770 被其他程序占用，也会自动改用实际端口（例如 8771）。

## 已知限制

- **药水使用是异步入队**：`use_potion` 返回成功仅代表请求已入队，实际结算在后续帧完成；AI 应随后 `get_state` 确认药水槽与敌人状态
- **附魔选牌确认偶发卡住（待确认归因）**：在事件“自助指南”的攻击牌附魔流程中，`pick_card` 选择 `NCardGridSelectionScreen` 内的牌后，界面可能停留在附魔预览/确认状态，继续点击确认无效；退出该选牌界面后重新选择卡牌可恢复。需要后续确认这是游戏 UI 偶发 bug，还是 MCP 对附魔类 `NCardGridSelectionScreen` 缺少预览确认/状态刷新处理。
- **奖励选牌 `card_index` 疑似未生效（待复现）**：在奖励选牌界面中，调用 `pick_reward` 并传入 `card_index=1` 期望选择第二张牌时，实际进入牌组的是第一张牌。需要后续复现并确认是 MCP 参数转发/执行逻辑忽略了 `card_index`，还是该次界面状态导致的索引偏移。
- **出牌异步**：`play_card` 通过 `ActionQueueSynchronizer.RequestEnqueue` 入队，卡牌选牌触发在后续帧处理，AI 需随后 `get_state` 检查
- **卡牌选牌触发后手牌索引变化**：如净化每选一张牌后手牌索引重排，需依赖 `pick_card` 返回值中的最新索引
- **卡牌描述**：`:energyIcons` / `:starIcons` 等特殊 formatter 输出 `[img]` 标签被替换为文字
- **事件选项**：部分动态变量（如 `{BatheCurses}`）在选项描述中可能未注入，`SafeFormat` 回退到原始文本
- **多人联机**：建房/加入的菜单操作已实现，但实际联网需要 Steam 网络层，尚未在真实多人环境下验证
- **`multi_play` 出牌是异步的**：`RequestEnqueue` 入队后卡牌异步处理，`played_card_name` 只反映尝试列表而非实际打出结果。AI 应在 `multi_play` 后紧跟 `get_state` 确认真实手牌和选牌状态
- **`multi_play` + 选牌触发**：若批中包含触发选牌的牌（生存者/净化等），选牌触发后同一批中的后续卡牌会被阻塞。AI 应单独 `play_card` 处理这类牌，或将它们放在批末尾

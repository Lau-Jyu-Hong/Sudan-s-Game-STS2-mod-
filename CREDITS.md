# 阿尔图 Mod · 鸣谢与素材来源（CREDITS）

> 项目：基于《杀戮尖塔 2》（Slay the Spire 2）的阿尔图角色 Mod  
> 人物与世界观灵感来源：《苏丹的游戏》（Sultan's Game）  
> 本文件用于记录原作鸣谢、第三方素材作者及其来源和使用条件。请随项目持续维护，并在 Steam 创意工坊页面提供相应鸣谢。

## 一、原作、开发商与许可信息

### Mega Crit Games — 《杀戮尖塔 2》（Slay the Spire 2）

感谢 **Mega Crit Games** 创作《杀戮尖塔 2》，为本 Mod 提供游戏本体与玩法基础，并通过公开政策支持社区 Mod 创作。

- 游戏 Steam 页面：https://store.steampowered.com/app/2868840/
- 官方内容政策：https://megacrit.com/content-policy/
- 政策说明：Mega Crit 的公开 Mod 政策允许符合其条件及相应平台条款的 Mod 创作；不应暗示本 Mod 与 Mega Crit 存在官方关联。除接受捐赠外，不得以付费墙、收费 Mod 或其他方式将 Mod 商业化。
- **此项感谢及公开 Mod 政策并不等于获得了单独使用、再分发《杀戮尖塔 2》游戏素材的无限制许可。**

### 双头龙工作室（Double Cross）

感谢双头龙工作室创作《苏丹的游戏》。本 Mod 的人物、故事背景与部分玩法构思受到该作品启发。

- 原作 Steam 页面：https://store.steampowered.com/app/3117820/
- 身份：《苏丹的游戏》开发团队。

### 2P Games

感谢《苏丹的游戏》发行商 **2P Games** 就非商业粉丝 Mod 的制作与发布提供答复。

- 据项目开发者此前收到的邮件回复：在**完全免费、非商业、不使用官方美术素材**的条件下，可以制作与发布相关粉丝 Mod。
- 上述表述仅记录已沟通的条件，**不代表获得官方美术、音频、商标或其他素材的额外使用许可**；如后续取得明确的补充许可，须另行更新本节。
- 授权沟通记录：由项目维护者私下留存原始邮件，不在公开版本中公布私人通信内容。

### 非官方作品声明

本 Mod 是独立制作、免费发布的非官方粉丝作品，并非 Mega Crit、双头龙工作室或 2P Games 官方开发、发行、赞助或认可的产品。各作品名称、人物与相关权利归各自权利人所有。

## 二、第三方音效

### Joseph SARDIN — BigSoundBank / LaSonothèque

感谢 **Joseph SARDIN** 提供音效素材。

**推荐英文鸣谢：** Additional sounds: Joseph SARDIN - BigSoundBank.com

- 素材网站：https://BigSoundBank.com
- 授权说明：https://bigsoundbank.com/licenses.html
- 使用条件：网站中**具体下载页面标注“Free and Royalty Free”**的音效可依其公开许可使用；网站将这些免费音效的使用条件描述为 CC0 1.0 等同或类似的自由使用条件。
- 署名要求：网站说明署名**并非强制**，但作者欢迎鸣谢及附上网站链接。
- 实际采用的四个音效及对应页面见下方登记表，文件位于 `assets/becky/`，原始 MP3 未裁剪或改变音高，嵌入 `ArtuMod.dll`。更详细的素材记录见 `assets/becky/来源与许可.md`。

## 三、第三方美术与动画

### pzUH — Cat & Dog - Free Sprites

感谢 **pzUH** 提供猫狗角色精灵与动画素材。

- 素材名称：Cat & Dog - Free Sprites
- 原始页面：https://opengameart.org/content/cat-dog-free-sprites
- 发布作者：pzUH（OpenGameArt 页面署名）
- 授权协议：[Creative Commons CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
- 署名要求：**不强制署名**；本项目自愿鸣谢并保留来源链接。
- 允许在许可范围内修改、裁剪、制作动画并随 Mod 发布；不得暗示素材作者对本 Mod 作出官方认可或背书。
- 实际使用猫的 Jump 1～8 与 Idle 1～10，共 18 张 PNG，位于 `assets/becky/Jump-1.png`～`Jump-8.png`、`Idle-1.png`～`Idle-10.png`，嵌入 `ArtuMod.dll`。仅调整文件名，未重画或裁剪原图；狗及其他动作未纳入游戏。这是非像素卡通猫，不是写实素材。

## 四、技术依赖与 AI 辅助开发

### 临时状态图标 — Game-icons.net / Lorc 与 Delapouite

Icons made by **Lorc** and **Delapouite**. Available on https://game-icons.net.

- 许可：[CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)；允许免费使用和修改，须保留作者署名、许可及来源，不暗示作者认可本 Mod。
- 上游原文：https://github.com/game-icons/icons/blob/master/license.txt 。本次于 2026-10-11 查阅网站与上游许可，素材按作者目录逐项选取。
- 用途：67 个自定义战斗状态及 6 个常驻状态入口（罪证共用对应战斗图标），共 72 个图标资源。不替换原版力量、易伤等通用状态。
- 修改：去除原黑色背景、调色、增加圆角底板和留白，转换为 128×128 PNG 及 Godot ImageTexture。玩法与存档不变；这是临时素材，不代表最终美术。
- 创意工坊封面由项目自行编排的SVG文字与这些已登记图标组成，同样保留Lorc、Delapouite及CC BY 3.0署名；不使用《苏丹的游戏》官方美术。
- 逐项标题、作者、来源及对应状态：源码 `assets/status-icons/来源与对应表.md`；安装目录 `ICON_CREDITS.md`。原始 SVG 保留于 `assets/status-icons/source/`，上游许可原文随安装包以 `LICENSE_ICONS.txt` 提供。

### Alchyr — BaseLib（《杀戮尖塔 2》Mod 基础库）

感谢 **Alchyr** 开发与维护 **BaseLib**，为《杀戮尖塔 2》社区 Mod 提供自定义内容注册及相关开发支持。

- 项目仓库：https://github.com/Alchyr/BaseLib-StS2
- 项目作者：Alchyr。
- 开源许可：MIT License；许可原文：https://github.com/Alchyr/BaseLib-StS2/blob/master/LICENSE.txt
- **工程核验状态：**2026-10-11 已核查：`src/ArtuMod.csproj` 引用 BaseLib 且设置 `Private="false"`；`package/ArtuMod/ArtuMod.json` 声明 BaseLib 依赖，最低版本为 3.3.8。本机使用 3.4.7。当前安装目录不包含 BaseLib 副本，玩家需另行安装；本次未重新在线核验许可证文本。
- **许可证合规：**如随 Mod 分发 BaseLib 软件副本或其重要部分，应按 MIT License 要求一并保留其版权及许可声明。若仅要求玩家自行安装 BaseLib，应在创意工坊页面清楚标注运行依赖，无须将依赖库误称为项目原创代码。

### OpenAI Codex — AI 辅助编程工具

感谢 **OpenAI Codex** 在本 Mod 的代码实现、调试与迭代中提供辅助支持。

- 官方介绍：https://openai.com/codex/
- 本项目由 Mod 维护者主导玩法设计、需求取舍、实际测试及发布决策；通过 Codex 辅助完成程序开发。设计讨论及任务整理过程中也使用了 ChatGPT 辅助。
- 以上表述仅为如实说明工具的参与，不代表 OpenAI 为本 Mod 的共同作者、发行商、赞助者或官方认可方；项目维护者对最终作品承担责任。
- 不应将 AI 辅助生成的内容谎称为完全由人工编写，也不应将所有创意或代码都归于 AI。

## 五、素材使用与维护规则

1. **新增素材先确认来源。** 每次加入第三方图片、动画、音效、字体或代码时，记录作者、作品名称、原始链接、许可类型及适用条件。
2. **逐项核查。** 不得因为同一个网站上的某些素材采用 CC0，就推断该网站所有素材均可自由使用。对于 BigSoundBank，尤其要核对实际音效下载页上的授权标识。
3. **区分致谢、公开政策与单独授权。** 鸣谢不等于获得许可；Mega Crit 的公开 Mod 政策、2P Games 的有条件邮件回复与具体素材的授权范围应分别记录。不得将任何一项扩大表述，也不得宣称原开发商或素材作者赞助、认可或参与了本 Mod。
4. **同步维护。** 增删或替换素材、工具与第三方代码依赖时同步修订本文件；保留仍在发行版本中使用的素材信息。必要时更新 Steam 创意工坊描述与版本更新日志。
5. **保护通信隐私。** 私下授权邮件保留供项目维护者查证；公开文件只陈述许可范围，不公开邮箱地址、邮件全文或其他个人信息。
6. **不确定则暂缓使用。** 来源或授权无法核实时，应标记待核对，不要直接打包发布。

## 六、素材登记表（持续补充）

> 以下文件及依赖已与当前工程核对；许可页面记录沿用已有素材来源清单，本次文件整理未重新在线核验。其他来源不虚构。

| 类别 | 作者／来源 | 作品或来源页 | 授权与鸣谢状态 | 项目内文件及用途 |
| --- | --- | --- | --- | --- |
| 短猫叫 #2 | Joseph SARDIN / BigSoundBank | https://bigsoundbank.com/little-meow-of-a-cat-2-s1472.html | 来源清单记录 CC0；自愿鸣谢 | `assets/becky/1472.mp3`；贝姬出牌演出 |
| 短猫叫 #3 | Joseph SARDIN / BigSoundBank | https://bigsoundbank.com/little-meow-of-a-cat-3-s1473.html | 来源清单记录 CC0；自愿鸣谢 | `assets/becky/1473.mp3`；贝姬出牌演出 |
| 短猫叫 #9 | Joseph SARDIN / BigSoundBank | https://bigsoundbank.com/little-meow-of-a-cat-9-s1479.html | 来源清单记录 CC0；自愿鸣谢 | `assets/becky/1479.mp3`；贝姬出牌演出 |
| 长猫叫 #2 | Joseph SARDIN / BigSoundBank | https://bigsoundbank.com/meow-cat-2-s1890.html | 来源清单记录 CC0；自愿鸣谢 | `assets/becky/1890.mp3`；贝姬收尾演出 |
| 猫精灵及动画 | pzUH / OpenGameArt | https://opengameart.org/content/cat-dog-free-sprites | CC0 1.0；自愿鸣谢 | `assets/becky/Jump-*.png`（8帧）、`Idle-*.png`（10帧）；贝姬演出 |
| 技术依赖 | Alchyr / BaseLib | https://github.com/Alchyr/BaseLib-StS2 | MIT（许可链接见上文）；已核对依赖，不随包分发 | `src/ArtuMod.csproj`、`package/ArtuMod/ArtuMod.json`；玩家单独安装 |
| 临时状态图标 | Lorc、Delapouite / Game-icons.net | https://game-icons.net/ | CC BY 3.0；按作者署名及逐项链接分发 | `assets/status-icons/`、`assets/ArtuMod/images/status/`；67 战斗状态及6常驻入口 |
| 其他第三方素材 | 待补充 | 待补充 | 待核对 | 待补充 |

---

*首次整理：2026-10-11；同日加入工程，补齐实际音效、猫动画与 BaseLib 依赖登记。原作政策及私人邮件条件沿用维护者提供的说明，不代表本次独立核验或额外授权。后续版本请以实际打包素材、技术依赖和许可记录为准更新。*
# 本轮资源复用

军威如山状态图标复用已署名的 Lorc / Game-icons.net 的 backup 图形（CC BY 3.0）。对应 `assets/status-icons/armymightpower.png` 与 `assets/ArtuMod/images/status/armymightpower.tres`；沿用现有调色、底板和尺寸转换。完整来源见 `assets/status-icons/来源与对应表.md`。

# v0.4.9-alpha 美术更新（2026-11-17）

- `assets/art-preview/`：OpenAI GPT Image 2（Codex 内置绘图）生成的阿尔图头像和选角图、苏丹牌母图、女术士的牌盒、群龙无首、地图和能量徽记、两个军队状态图标。黄金杀戮全卡背景以已确认的横图为参考扩画。运行图经裁切、缩放后由 `tools/build_art_preview.py` 打包。
- `assets/combat-visuals/artu_merchant.png`：同一绘图流程生成的商店购物站姿，手持钱袋与卷轴，不复用战斗构图。`assets/combat-visuals/artu_rig/` 是战斗立绘的五层切图，待机仍由这些切图做轻微呼吸。
- `assets/combat-visuals/artu_poses/`：同一绘图流程生成的衔接立绘。攻击分蓄力、挥出、收势，三帧都与待机同一朝向，挥出不留胡须。施法分半举和举卷，另有受击、跪倒和伏身。跪倒与伏身不留胡须。这些动作播放时整张换画。
- `assets/card-ui/artu_card_frame.png`：同一绘图流程生成的金边书板卡框。只替换阿尔图普通、罕见、稀有牌的外框；插图窗口和文字底板按游戏原卡框的位置留出，稀有度边、标题横幅和费用仍用游戏自己的。苏丹牌和先古牌不用这张框。
- `assets/card-ui/portraits/`：同一绘图流程生成的插图，裁成 250×190 放进插图窗。已装入调兵遣将、御用通行、金币弹弓、上吧铁头、整兵待战、思潮翻涌、忠犬护主、快脚小圆、妻子的茶会、小狗与月亮之书、招兵买马、罪犯的投奔、操练、奉诏出征、纯洁的思想、不洁的思绪，以及 2026-11-18 追加的对苏丹的恐惧、暗箱操作、顺应皇命、有去有回、处理政要、权衡利弊、临时征募、全军出击、格挡反震、毁灭打击、审判庭、为君分忧、全凭君恩、色厉内茬、俯首称臣、犬马声色、精锐部队、强军战歌、训练兵队、革命军校、军威如山、行侠仗义。原作已有的人物和物件按《苏丹的游戏》原设立绘；其余按已确认的场面。不带卡框、标题和费用。苏丹牌全卡图未改。
- `assets/combat-visuals/artu_rest_site.png`：同一绘图流程生成的篝火坐姿，只画读卷轴的人，不另画木桩。左侧座位朝向火堆；右侧座位沿用游戏原有的水平翻转。
- `assets/combat-visuals/enemies/`：同一绘图流程生成的王冠骑士、军阀、佣兵队长攻击与施法立绘，以及王室护卫、封锁、征调、征募四张技能演出。攻击朝画面左侧。扩军与征募共用征募演出。
- 安装包中的 `allarmypower.tres` 与 `armymightpower.tres` 使用新生成的三矛与红旗。Game-icons.net 的 backup 登记仍对应基础资源包与历史版本，其余临时状态图标的署名和许可不变。
- 不重新分发用来查看游戏显示效果的原版截图，不把参考图计作 Mod 新增资源。


# 苏丹的游戏 — 阿尔图 Mod（v0.4.5-alpha）

本次发布前增量：罪证改为跨战斗持续，休息处可消耗一次行动移除1层；兵种自身伤害/格挡不吃力量、敏捷、虚弱、易伤、脆弱、活力，仍可被格挡。兵种专属联动与卡牌自身攻击处理保留。下文为已有功能与历史验证范围，本轮验收另见发布前更新报告。

以当前实际可运行代码为基线，不按任务书标题版本号回退。本次在v0.4.3上实施第二批截图S13～S18任务及追加的罪犯的投奔7/10，保留猫咪演出与前期修复。当前详情见 `verification/第二批逐项更新报告.md`；历史报告不代表本轮所有项目已实测。

贝姬先抽4张再扫描。单人默认启用猫演出：待打出的牌上方播放跳跃动画及轮换短猫叫，1.8秒后打牌，完整结算后继续；两轮结束，待机猫和长猫叫收尾3.2秒，再结束回合。战斗提前结束会停止剩余演出并清理。`ARTU_BECKY_PRESENTATION=0`可禁用，联机仍不启用以免引入同步风险。音量跟随游戏主音量与音效音量。猫是非像素卡通风，不是写实风。

常规奖励池实际注册84张：23普通、36罕见、25稀有，含上轮格挡反震/毁灭打击和本轮临时征募/征兵告示。本机游戏v0.111.0，BaseLib 3.4.7。主角、圆桌敌人及卡图占位状态保留；本轮两张新牌使用既有通用卡图，未声称补齐美术。

六张三/四阶思潮改为1费。杂兵补给按本轮任务执行，罪犯的投奔为7/10。状态非零时独立显示在角色下方，战斗外无角色模型时使用左下角入口，不再塞入牌盒。苏丹牌的期限/逾期独立提示依原牌实例，黄金回合期限与全凭君恩冻结保持。

格挡提前消失暂未在隔离环境复现，兵种/原生格挡和纵欲数值均未改。需要完整模组环境取证时，可在启动游戏前设置 `ARTU_TRACE_BLOCK_ORDER=1`，正常游戏godot.log会记录 `[Artu block order]` 各阶段。默认关闭；测试辅助模组仍只在隔离环境使用。

v0.4.1规格、机制验证及安装信息见 `verification/v0.4.1防御补充更新报告.md`；历史v0.4报告及80张清单保留为旧版记录。

## 在其他电脑安装

关闭游戏，将安装包中的 **ArtuMod整个文件夹** 放进游戏的 `mods` 目录，最终应为：

```
Slay the Spire 2/mods/ArtuMod/ArtuMod.dll
Slay the Spire 2/mods/ArtuMod/ArtuMod.pck
Slay the Spire 2/mods/ArtuMod/ArtuMod.json
Slay the Spire 2/mods/ArtuMod/CREDITS.md
Slay the Spire 2/mods/ArtuMod/ICON_CREDITS.md
Slay the Spire 2/mods/ArtuMod/LICENSE_ICONS.txt
```

不要把压缩包文件直接留在mods里，也不要多套一层同名目录。安装并启用兼容BaseLib，重启启用阿尔图，新开一局。已有Mod用整个ArtuMod文件夹覆盖，保留鸣谢与许可证文件，建议先备份。安装包不附带游戏或BaseLib。

维护请给另一台电脑的Codex **源码工程包**，安装包只有编译产物。工程包含源码、设计、验证脚本、报告和安装文件；提供当地游戏与BaseLib路径，运行Build.ps1后复制package/ArtuMod。安装脚本会备份旧版。

## 本版更新

- 七张新罕见、十四张新稀有，含三阶可用思潮；三条三场达标进化连通最终先古能力。
- 全凭君恩冻结苏丹履行期限和状态，增加免费重放；生命权杖提供每回合一次可选替代代价。螺旋圆舞曲复制能力效果；其余新增牌按任务书实施。
- 罪证累计1～8阶梯：3层少抽，不再掉血；6/8层用原版虚无晕眩，8层每次一张；7层第四张起随机弃牌。兵种自身伤害/格挡保留原处理。
- 字条改为0费能力、读物/思潮、保留、无消耗；六革命任务和七完成旗标跨战斗保存，构思/计划均去重发放。
- 两个第二/三幕专属事件，三个原事件隐藏选项，保留旧选项。额外读物奖励使用原生奖励UI。
- 同场实际使用构思与弑君后，革命计划加入手牌；打出触发独立“革命成功/胜利！”文字界面、原生整局胜利和历史徽章。
- 16苏丹牌、怒气阶梯、嘲弄十层死亡、白银逾期、青铜奢靡加法重放、贝姬实时ADGC、圆桌三怪/各玩家奖励均保留。

完整清单、持久化字段与测试范围见 `verification/v0.4更新报告.md`，实际注册ID见 `verification/v0.4奖励池80张.md`。

## 验证范围

v0.3.1先通过241项机制、22项原生出牌检查。v0.4最终通过393项机制检查，并在隔离游戏中验证新21张牌、能力复制、费用替换、原生事件/奖励流程及革命结局。逐项计数与日志以更新报告为准。

测试存档目录为ArtuModVerification，人工增加HP、金币、能量以检查功能；不是合理构筑通关或难度验收。未完成真实双客户端联机、重连或联机共同结局验收。游戏版本更新后需重新核对API并编译。Godot退出时仍有资源释放警告，具体见日志。

## 编译

安装.NET 9 SDK或兼容新版本：

```powershell
.\Build.ps1 -GameDir '你的游戏目录' -BaseLibPath '你的BaseLib.dll完整路径'
```

默认采用本机E盘路径；可传参更改。只引用本地程序集，不分发它们；卡面资源可用Python+Pillow运行tools/build_assets.py生成。验证辅助模组源码位于verification/harness，仅供隔离测试，不放进正常游戏mods。

## 维护入口

- `CREDITS.md`：正式鸣谢、素材来源及依赖记录，纳入持续维护；源码和安装包均携带。素材或依赖变化时同步更新，安装脚本会复制此文件。
- `assets/status-icons/`：临时状态图标，Game-icons.net / Lorc、Delapouite，CC BY 3.0。`mapping.json` 对应状态；原始SVG保留在source。用 `tools/prepare_status_icons.cjs` 调色转换，再运行 `tools/pack_status_icons.py` 将图标加入资源包，不改已有资源。安装包携带 `ICON_CREDITS.md` 和 `LICENSE_ICONS.txt`，不得漏掉署名。

- V04Cards.cs、V04Mechanics.cs：新增卡、原生重放和费用整合。
- Revolution.cs、RevolutionEvents.cs：任务、奖励UI、事件与结局。
- SultanCards.cs、SultanUsage.cs、Relics.cs：16苏丹牌、代价和永久状态。
- Powers.cs、Thoughts.cs：兵种/罪证/低阶思潮。
- Events.cs、TeaParty*.cs：原有事件和圆桌战斗。
- V03Verification.cs、V04Verification.cs、SmokeTests.cs：命令机制回归。

技术依据：本机游戏v0.111.0程序集，C#/.NET、Godot、Harmony及BaseLib。开发资料保留在tools及历史报告中；历史v0.3报告描述旧状态，不覆盖本版规格。

## 鸣谢

1. 感谢双头龙工作室许可。
2. 感谢 Joseph Sardin 提供音效。

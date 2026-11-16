# 苏丹的游戏 — 阿尔图 Mod

《杀戮尖塔 2》的阿尔图角色 Mod。当前源码对应 **v0.4.9-alpha**，适配游戏 v0.113.1。原作、素材来源与各自许可见 [CREDITS.md](CREDITS.md)；这是非官方、非商业粉丝作品。

## 源码与构建

- `src/`：角色、卡牌、遗物、机制与游戏内验证。
- `design/`：设计文档；未定稿条目不代表已经实装。
- `assets/`：已登记许可的猫咪动画、音效与状态图标。
- `verification/harness/`：隔离回归用辅助代码，不用于日常安装或平衡性统计。
- `tools/autospire/`：可选本地试玩工具，源自 [LightEnding/autoSpire](https://github.com/LightEnding/autoSpire)，保留其 MIT 许可，不是 Mod 运行依赖。

用 .NET 9 SDK 编译；需自行准备《杀戮尖塔 2》v0.113.1 和 BaseLib v3.4.8（及其声明的依赖）。旧版游戏请使用对应历史标签。

```powershell
.\Build.ps1 -GameDir '<游戏安装目录>' -BaseLibPath '<BaseLib.dll 完整路径>'
```

脚本生成 `package/ArtuMod/ArtuMod.dll`，**源码仓库本身不是安装包**。运行还需配套的 `ArtuMod.pck`。请从 [Steam 创意工坊条目](https://steamcommunity.com/sharedfiles/filedetails/?id=3817779845) 取得同版本资源包。也可用 Python 和 Pillow 基于 v0.4.8-alpha 的 PCK（SHA-256 `185177b628d0aedd94260977f56412e55d6df687877b86548980b1d125f0e556`）重建：`python tools/build_art_preview.py assets/art-preview <旧版PCK> <新PCK> --layout full`。先输出到新路径，再与 DLL 配套安装。仓库不含游戏或依赖 DLL。若编译试玩桥，请显式传入 `-p:Sts2Dir=<游戏安装目录>`，只编译时加 `-p:SkipModCopy=true`。

## v0.4.9-alpha 美术更新

- 16 张苏丹牌均采用专属全卡美术。保留原有类型、稀有度、费用、动态期限和效果；其余牌继续使用原排版。
- 更新阿尔图选角图、头像、牌盒、「群龙无首」封面，以及地图、能量、「全军出击」和「军威如山」图标。
- 实机截图复核选角构图、地图标记、选牌、手牌和整套苏丹牌。回归 780 项通过；原生显示及截图检查 30 项通过。战斗模型、其他事件美术及联机验收不在本次范围。

## 版本历史

Git 标签按各快照原 manifest 命名：v0.3.0、v0.3.1、v0.4.0、v0.4.1、v0.4.2-alpha、v0.4.3-alpha、v0.4.4-alpha、v0.4.5-alpha、v0.4.6-alpha、v0.4.7-alpha。它们从原源码包、工作副本与源码备份重建，不是原开发提交历史。v0.4.4-alpha 的备份只保存了部分工程文件，标签主要保留 C# 源码和清单。各版均排除了私人本机路径、安装二进制和私人数据；未确认可单独再分发的资源不纳入。

## v0.4.8-alpha 兼容性验证

游戏 v0.113.1（d09d48e5）、BaseLib v3.4.8。主工程和测试辅助工程编译 0 错误、0 警告。隔离引擎中，777 项机制检查全部通过；原生复合场景 86 项、圆桌茶会 34 项、思潮保留与进化各 7 项、界面与军校 9 项、AutoSpire 商店 7 项均通过。包含原存档依赖的隔离环境中，已有对局副本载入、战后奖励和新存档读取共 5 项断言通过。用例会注入测试资源，不代表联机、完整混装或平衡性实测。

1. 感谢双头龙工作室许可。
2. 感谢 Joseph Sardin 提供音效。

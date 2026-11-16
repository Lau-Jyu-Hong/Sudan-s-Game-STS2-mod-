# 苏丹的游戏 — 阿尔图 Mod

《杀戮尖塔 2》的阿尔图角色 Mod。当前源码对应 **v0.4.7-alpha**。原作、素材来源与各自许可见 [CREDITS.md](CREDITS.md)；这是非官方、非商业粉丝作品。

## 源码与构建

- `src/`：角色、卡牌、遗物、机制与游戏内验证。
- `design/`：设计文档；未定稿条目不代表已经实装。
- `assets/`：已登记许可的猫咪动画、音效与状态图标。
- `verification/harness/`：隔离回归用辅助代码，不用于日常安装或平衡性统计。
- `tools/autospire/`：可选本地试玩工具，源自 [LightEnding/autoSpire](https://github.com/LightEnding/autoSpire)，保留其 MIT 许可，不是 Mod 运行依赖。

用 .NET 9 SDK 编译；需自行准备对应版本的《杀戮尖塔 2》和 BaseLib。本版已在游戏 v0.111.0、BaseLib v3.4.7 上重新编译通过；manifest 声明的 BaseLib 最低版本是 3.3.8。游戏 v0.113.1 / BaseLib v3.4.8 下，`RoyalProtectionPower.AfterDeath` 的重写签名不兼容（CS0115），本次未做适配。

```powershell
.\Build.ps1 -GameDir '<游戏安装目录>' -BaseLibPath '<BaseLib.dll 完整路径>'
```

脚本生成 `package/ArtuMod/ArtuMod.dll`，**源码仓库本身不是安装包**。运行还需配套的 `ArtuMod.pck`；请从对应版本的 [Steam 创意工坊条目](https://steamcommunity.com/sharedfiles/filedetails/?id=3817779845) 或已有合法安装包取得，不要从源码仓库下载游戏或依赖 DLL。仓库未为全部历史版本逐一复测。若编译试玩桥，请显式传入 `-p:Sts2Dir=<游戏安装目录>`，只编译时加 `-p:SkipModCopy=true`。

## 版本历史

Git 标签按各快照原 manifest 命名：v0.3.0、v0.3.1、v0.4.0、v0.4.1、v0.4.2-alpha、v0.4.3-alpha、v0.4.4-alpha、v0.4.5-alpha、v0.4.6-alpha、v0.4.7-alpha。它们从原源码包、工作副本与源码备份重建，不是原开发提交历史。v0.4.4-alpha 的备份只保存了部分工程文件，标签主要保留 C# 源码和清单。各版均排除了私人本机路径、安装二进制和私人数据；未确认可单独再分发的资源不纳入。

# 隔离验证辅助工程

不要安装到日常游戏。先编译主工程，再编译本目录src/ArtuAutoplayLab.csproj（传-p:GameDir及-p:BaseLibPath）。项目通过相对路径引用主工程Release DLL，没有分发测试DLL、游戏或第三方程序集。

复制你自己的完整游戏到独立测试目录，清理副本mods使其只启用BaseLib、ArtuMod和ArtuAutoplayLab。将本目录override.cfg放到副本exe旁，使用ArtuModVerification独立用户目录；辅助模组会强制检查此目录。将测试DLL、manifest放副本mods/ArtuAutoplayLab。

PowerShell先清除其他ARTU_*模式，设置ARTU_AUTOPLAY_REPORT为绝对输出目录。机制回归：仅设ARTU_SMOKE_OUTPUT为绝对runtime.json路径，以--headless --force-steam off启动副本。机制回归不需要辅助模组。

实机场景设置ARTU_V04_SCENARIO=full（第二幕全流程），或0/2（第一/第三幕注入革命计划测试）。以--force-steam off --max-fps 60启动副本；会人工增加资源、使用原生出牌动作、选择界面和奖励同步，然后保存结果并退出。旧流程用ARTU_V03_SCENARIO=1。本工具改动测试存档，不能拿它检验正常玩家存档兼容或平衡。

V04Scenario以原生奖励按钮处理方法和选牌回调自动选择UI中的首项；没有修改生产模组奖励逻辑。双客户端联机尚未实现自动验收。

v0.113.1：在隔离副本的离线设置中先启用模组，并沿用配套资源包。新增 `ARTU_V04_SCENARIO=compat-recovery`（从隔离档位读取已有对局副本，验证战后结算和存档可读；包含外部模组字段时须装对应依赖）和 `compat-shop`（需隔离运行 AutoSpire，并独占 localhost:8770；验证同步商店买牌、移除与禁购）。它们是人工回归场景，不修改正常存档，不代表完整混装或联机验证。

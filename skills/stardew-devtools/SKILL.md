---
name: stardew-devtools
description: 开发、调试或测试星露谷物语 (Stardew Valley 1.6) 模组（C# / Content Patcher / .fx 着色器 / .tmx 地图）时使用，通过 sdv-dev CLI 完成资料检索、静态体检与实机自动化验收。
---

# stardew-devtools 规范

1. **以 `--help` 为唯一用法源**：本技能不重复介绍子命令与参数；调用前或不确定选项时，直接运行 `sdv-dev --help` 或 `sdv-dev help <命令>`，禁止盲猜参数。
2. **跨轮热驻守护**：同步命令（`IsDaemon: false`）结束时宿主作业组会回收派生的子进程；需跨轮高频调用或保持游戏会话存活时，先以 `IsDaemon: true` 启动一次 `sdv-dev --daemon`。
3. **开发纪律与安全红线**：
   - **先查后写**：原版/SMAPI/MonoGame/xTile 接口、1.6 数据字段、音效与前置 API 一律先检索或反编译核实，不凭记忆硬写。
   - **先检后跑**：进档或热刷前先通过静态体检，实机验收后核查错误日志。
   - **会话与存档保护**：启动、重启或关闭前先跑 `sdv-dev live`，若存在 `bridgeConnected=false` 的玩家手动会话，未经授权严禁强关；严禁关闭 `steam.exe`；优先使用隔离环境与专属测试档，禁止污染玩家个人存档（真实存档破坏性验证后需不保存退回主菜单）。

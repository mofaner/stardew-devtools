---
name: stardew-devtools
description: 使用 sdv-dev CLI 进行星露谷物语 (Stardew Valley 1.6) 模组开发、项目依赖管理、1.6 语义/Harmony/MonoGame 渲染静态体检、内置 .fx 着色器与 Publicizer 免 SDK 编译、地图/精灵图离线渲染与实机自动化测试。需要检索原版/SMAPI/MonoGame/xTile 接口、反编译源码、编译 HLSL 着色器、生成 GMCM 配置菜单、校验/预览 .tmx 地图或精灵图、隔离启动进档、免重启改模组配置、A/B 光影对比与动态四宫格截图、排演 Event 剧情、跨日推进世界、分屏联调或执行主线程 eval 时使用。
---

# SDV-DEV

- 先查看 `sdv-dev --help`；具体子命令参数与示例用 `sdv-dev <命令> --help` 或 `sdv-dev help <命令>` 查询（如 `help shader`、`help config`、`help screenshot`、`help deps`、`help check`、`help sprite`、`help asset`、`help event-play`、`help eval`）。
- **跨轮极速热驻与进程存活约定**：普通同步命令（`IsDaemon: false`）结束时宿主作业组会回收派生的子进程；若需跨多轮高频检索、反编译或保持实机游戏跨轮存活，先以守护模式（`IsDaemon: true`）显式启动一次 `sdv-dev --daemon`（闲置 10 分钟自退），后续命令会自动路由至该热驻守护进程。
- **不盲猜接口、数据、音效与前置 API**：未知逻辑用 `sdv-dev api` 搜索（支持 `--asm sdv|smapi|gamedata|monogame|xtile|harmony|toolkit`），查源码用 `sdv-dev decompile <Type.Member>`（大函数支持 `--grep <关键词> -C 15` 局部切片，覆盖原版、SMAPI 内部类、`MonoGame.Framework`、`xTile` 与 `0Harmony`）；1.6 数据与原版音效用 `schema`/`data`（如 `--asset Audio/Cues`）/`doc` 核查；第三方前置用 `sdv-dev deps add` 与 `sdv-dev deps api <前置ID>`。
- **内置 `.fx` 着色器编译、Publicizer、GMCM 生成与深度静态体检**：`sdv-dev build` 自动将工程内全部 `*.fx` HLSL 着色器编译为星露谷 1.6 DesktopGL 对齐的 `.mgfxo` 字节码（也可单独跑 `sdv-dev shader <file.fx>`），并默认启用 `BepInEx.AssemblyPublicizer` + `IgnoresAccessChecksTo`；配置菜单用 `sdv-dev gen-gmcm` 自动生成；进游戏前必跑 `sdv-dev check`（覆盖 1.6 `GameData`、跨表 ID 断链、`Event`/NPC 日程对话、`.fx` 着色器预检、Harmony 补丁签名、MonoGame/SMAPI 渲染坑如 `Rendered*` 内重复 `SpriteBatch.Begin` / 每帧 `new Texture2D` 泄漏 / `SetRenderTarget` 未还原 / 非预乘 `new Color(r,g,b,a)` 泛白、以及分屏静态字段隐患），发布用 `sdv-dev pack`。
- **地图、精灵图与视觉特效验收**：精灵图先用 `sdv-dev sprite` 校验包围盒与居中；地图用 `tilesheet --grid`、`map-inspect --rect x,y,w,h`（默认精简输出局部图块与引用贴图）、`map-autotile`、`map-prefab`/`map-extract`/`map-stamp` 及 `map-render --cp . --auto-crop --debug-overlay` 离线预览并跑 `map-check`；只改 `.fx` 着色器或贴图/地图时用 `sdv-dev reload-assets --project <目录>` 自动重编 `.mgfxo` 并 0.2s 免重启热刷内存 `Effect`；调大视野严禁手写 `eval` 改缩放，直接用 `sdv-dev world --zoom 0.8` 或 `screenshot --zoom 0.8`（自动调 `SetWindowSize` 重建 `Game1.screen` 防右侧 UV 撕裂）；调参用 `sdv-dev config <ModId> --set Key=Val` 或 `--reset`，并用 `screenshot --ab-compare <ModId>` 与 `screenshot --burst 4 --pan-test` 验收视觉与动态效果。
- **模组隔离、桌面可见/后台控制与交互测试闭环**：日常自测用 `sdv-dev start/restart --isolated --project <目录> --test-world`（若玩家桌面已开可见窗口，`restart` 默认原位无焦点同桌面刷新；若不想弹桌面窗口，加 `--background` 切为离屏无打扰隐藏模式）；世界交互用 `sdv-dev interact` 配合 `world --advance-days N` 与 `checkpoint save/load` 秒级回档；NPC 日程用 `schedule-check <NPC>`；排错与性能用 `asset --cp-trace`、`harmony <Method> --il`、`profile --seconds 5` 与 `logs --level Error`。
- **安全红线**：启动前跑 `sdv-dev live`，若存在 `bridgeConnected=false` 的玩家手动会话禁止强杀；绝不关闭 `steam.exe`；优先使用 `--test-world` 专属测试槽位避免污染个人存档，在真实存档执行破坏性操作后用 `sdv-dev menu` 不保存退回主菜单。

---
name: stardew-devtools
description: 使用 sdv-dev CLI 进行《星露谷物语》(Stardew Valley 1.6) 模组开发、项目依赖管理、1.6 语义/Harmony 静态体检、Publicizer 免 SDK 编译、地图/精灵图离线渲染与实机自动化测试。需要检索原版/SMAPI 接口、反编译源码、生成 GMCM 配置菜单、校验/预览 .tmx 地图或 NPC/物品精灵图、打包发布、隔离启动进档、透视实机资产/Harmony 补丁链、排演 Event 剧情、跨日推进世界、分屏联调或执行主线程 eval 时使用。
---

# SDV-DEV

- 先查看 `sdv-dev --help`；具体子命令参数与示例用 `sdv-dev <命令> --help` 或 `sdv-dev help <命令>` 查询（如 `help deps`、`help check`、`help sprite`、`help asset`、`help event-play`、`help eval`）。
- **跨轮极速热驻与进程存活约定**：普通同步命令（`IsDaemon: false`）结束时宿主作业组会回收派生的子进程；若需跨多轮高频检索、反编译或保持实机游戏跨轮存活，先以守护模式（`IsDaemon: true`）显式启动一次 `sdv-dev --daemon`（闲置 10 分钟自退），后续 `api`/`decompile`/`start`/`restart` 会自动路由至该热驻守护进程。
- **不盲猜接口、数据、音效与前置 API**：未知逻辑用 `sdv-dev api` 搜索，打 Harmony 补丁前用 `sdv-dev decompile` 核对源码；1.6 数据与原版音效/BGM 名用 `schema`/`data`（如 `--asset Audio/Cues`）/`doc` 核查；接入第三方前置模组时用 `sdv-dev deps add` 管理依赖（优先扫本地 `Mods`、缺失从 GitHub Releases 兜底拉取，不碰 N 网）并用 `sdv-dev deps api <前置ID>` 查看真实 `I*Api` 接口。
- **内置 Publicizer、GMCM 生成与 1.6 深度静态体检**：`sdv-dev build` 与 `eval` 默认启用 `BepInEx.AssemblyPublicizer` + `IgnoresAccessChecksTo`，C# 模组可直接访问原版私有/内部字段与方法（无需写慢速反射，且保留 `protected override` 兼容）；配置菜单用 `sdv-dev gen-gmcm` 从 `ModConfig.cs` 自动生成接口、绑定代码与双语 `i18n`；每次提交或进游戏前必跑 `sdv-dev check`（覆盖 1.6 `GameData` 强类型字段、`GSQ`/`TriggerAction`/`Event` 语法及音频 Cue 拼写、`EditImage`/`SpriteIndex` 越界、`i18n` 漏译、Harmony 目标/重载/参数静态验签及分屏联机静态变量隐患），发布用 `sdv-dev pack` 生成标准结构 `.zip`。
- **地图与精灵图视觉开发先离线后实机**：物品/大件/NPC 行走图 (`16x32`)/肖像 (`64x64`) 先用 `sdv-dev sprite` 导出包围盒与居中线校验图；地图用 `tilesheet --grid`、`map-inspect`、`map-autotile`、`map-prefab`/`map-extract`/`map-stamp` 拼图，修改后必跑 `map-check` 与 `map-render --debug-overlay` 1 秒离线预览；实机视觉验收配合 `warp`、`map-find-space`、`reload-assets` 与 `screenshot --mode screen|map` 看图确认。
- **模组隔离、世界交互、性能采样与分屏测试闭环**：日常自测优先用 `sdv-dev start/restart --isolated --project <目录> --test-world` 秒开；世界地块/机器/农具/NPC 交互直接用 `sdv-dev interact`（`--place`/`--drop-in`/`--right-click`/`--use-tool`）配合 `sdv-dev world --advance-days N` 跑完投放->投料->跨夜->收获闭环；排查 CP 覆盖、Transpiler 指令与掉帧卡顿用 `asset --cp-trace`、`harmony <Method> --il` 与 `profile --seconds 5`；调剧情用 `event-play`；测联机用 `mp add-farmhand` + `--screen host|farmhand`；多行 `eval` 用 `@' ... '@ | sdv-dev eval --stdin`，并必查 `sdv-dev logs --level Error` 确认零报错。
- **安全红线**：启动前跑 `sdv-dev live`，若存在 `bridgeConnected=false` 的玩家手动会话禁止强杀；绝不关闭 `steam.exe`；优先使用 `--test-world` 专属测试槽位避免污染个人存档，在真实存档执行破坏性操作后用 `sdv-dev menu` 不保存退回主菜单。

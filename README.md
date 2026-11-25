# stardew-devtools (`sdv-dev`)

面向 AI Agent 与开发者的星露谷物语 1.6 模组开发资料库、静态体检与实机自动化测试工具（`sdv-dev` CLI + AI Skill）。

内置原版、SMAPI、`MonoGame` 与 `xTile` 接口检索及源码反编译、1.6 数据表结构、中英原版条目、XACT 音效与官方 Wiki 离线资料库；支持 C# 工程与 `.fx` HLSL 着色器免安装工具链编译、GMCM 配置菜单生成、1.6 语义/跨表外键/NPC/Harmony/MonoGame 渲染静态校验、`.tmx` 与 CP `EditMap` 地图贴图离线渲染，以及隔离进档交互、免重启改配置、A/B 光影对比与动态四宫格截图、快照回档和帧耗时/IL 实机自动化测试。

---

## 一、安装

环境要求：Windows 10 / 11 (x64)，已安装 PC 版 **星露谷物语 1.6+** 与 **[SMAPI 4.0+](https://smapi.io/)**。

> **如果不想看命令行，直接复制下面这句话发给你的 AI 工具（Claude Code / Cursor / Codex / Gemini / Antigravity 等）让它代装：**
> ```text
> 请帮我下载并安装 https://github.com/mofaner/stardew-devtools 的最新 Release 包（运行里面的 install.cmd），装完用 sdv-dev status 验证一下。
> ```

手动安装只需 3 步：
1. 在 **[Releases](https://github.com/mofaner/stardew-devtools/releases)** 下载最新 `stardew-devtools-<version>-win-x64.zip` 并解压；
2. 双击运行 **`install.cmd`**（会自动把 `sdv-dev` 加入用户 `PATH`，并把 `stardew-devtools` 技能同步到本机已安装的 AI 工具目录）；
3. 重开终端或 AI 工具即可使用。

如果游戏不在默认 Steam / GOG 库里，首次绑定一下路径即可：
```powershell
sdv-dev sync --offline --game-path "D:\你的游戏路径\Stardew Valley"
```

---

## 二、主要功能

### 1. 查原版/SMAPI/MonoGame/xTile 源码、1.6 数据结构与音效库
内置预建好的 SQLite FTS5 知识库（覆盖 `Stardew Valley`、`StardewModdingAPI` 含内部框架类、`MonoGame.Framework`、`xTile`、`0Harmony`、168 张 1.6 数据表结构、6,200+ 中英原版条目与 XACT 音效名、官方 Wiki 文档），支持按类名或方法名实时反编译并用 `--grep` 局部切片：
```powershell
sdv-dev api "RenderTarget2D" --asm monogame
sdv-dev decompile Game1.DrawWorld --grep lighting -C 12
sdv-dev decompile SModHooks.OnRendered
sdv-dev schema Data/Machines
sdv-dev data "小桶" --asset Data/BigCraftables
sdv-dev data dwop --asset Audio/Cues
sdv-dev docs "TriggerActions"
```

### 2. C# 与 `.fx` 着色器免 SDK 构建、GMCM 生成与深度静态查错
内置 Roslyn 编译器、`AssemblyPublicizer` 与对齐星露谷 1.6 (MonoGame 3.8.0.1641) 的 `.fx -> .mgfxo` OpenGL 着色器编译器，支持从 `ModConfig.cs` 生成 GMCM 配置菜单，并在进游戏前做全套静态体检：
```powershell
sdv-dev new csharp --name MyShaderMod --author You
sdv-dev shader .\MyShaderMod\assets\water.fx     # 单文件编译 HLSL -> MGFX v9 (.mgfxo)，build 也会自动编译全部 .fx
sdv-dev gen-gmcm .\MyShaderMod
sdv-dev build .\MyShaderMod --deploy
sdv-dev check .\MyShaderMod
sdv-dev pack .\MyShaderMod
```
`sdv-dev check` 会同时检查：
- 1.6 `GameData` 字段名与类型（含 `Data/TriggerActions` 等 `List<T>` 数组表与食谱斜杠表）；
- 跨表物品 / 信件 / Buff / 任务 ID 断链与 `(O)` / `(BC)` 前缀错配（防 Error Item）；
- `Event` 剧本前置条件、第 3 段演员四元组站位、指令名与原版音效拼写；
- 自定义 NPC 的 `Characters/schedules/*` 时间递增、动作名、落点阻挡，以及 `Characters/Dialogue/*` 控制符（`$q`/`$r`/`$p`）与肖像 PNG 帧数越界；
- `.fx` 着色器 HLSL 语法、OpenGL `ps_3_0`/`vs_3_0` MojoShader 兼容性与漏编 `.mgfxo` 提醒；
- C# `[HarmonyPatch]` 目标存在性、重载歧义、`Prefix`/`Postfix` 返回值与注入参数类型/`ref` 匹配、MonoGame/SMAPI 渲染坑（`Rendered*` 内重复 `SpriteBatch.Begin` 崩溃、每帧无缓存 `new Texture2D/RenderTarget2D` 显存泄漏、离屏 `SetRenderTarget` 未还原、非预乘 `new Color(r,g,b,a)` 泛白），以及分屏静态字段隐患。

### 3. 离线地图与贴图预览
不用启动游戏，直接在命令行切分校验精灵图、拼装 `.tmx` 地图，或把 Content Patcher 的 `EditMap` 补丁叠到原版地图上秒出局部预览图：
```powershell
sdv-dev sprite assets/portrait.png --preset portrait --out portrait_check.png
sdv-dev map-autotile --spec terrain.txt --out assets/IslandBay.tmx --theme island
sdv-dev map-stamp assets/IslandBay.tmx --prefab @island-palm --at 6,4
sdv-dev map-render assets/IslandBay.tmx --out preview.png --debug-overlay
sdv-dev map-render Maps/Town --cp .\MyCpMod --auto-crop --debug-overlay --out town_patch.png
sdv-dev map-check assets/IslandBay.tmx --entry 10,2
```

### 4. 隔离进档实测、免重启调参、视觉对比/连拍、时光机快照与性能采样
通过 `--isolated --test-world` 在独立 `--mods-path` 下秒进测试档（不碰个人存档；玩家已开可见窗口时 `restart` 默认同桌面原位刷新，不想弹窗口则加 `--background` 切为后台隐藏模式）：
```powershell
sdv-dev start --isolated --project .\MyShaderMod --test-world
sdv-dev reload-assets --project .\MyShaderMod              # 改完 .fx 着色器免重启 0.2s 自动重编并热刷内存 Effect！
sdv-dev world --zoom 0.80 --time 1800                      # 安全切换视野缩放（自动重建 Game1.screen 防右侧 UV 撕裂）
sdv-dev config Author.MyShaderMod --set SunsetWarmth=1.4   # 0.05 秒免重启实时改内存配置（加 --reset 一键恢复默认）
sdv-dev screenshot --ab-compare Author.MyShaderMod -o ab.png # 同机位一键拍「关闭 vs 开启」左右对比图
sdv-dev screenshot --burst 4 --pan-test -o motion_grid.png   # 动态连拍 2x2 时序四宫格（审波纹动画与镜头残影）
sdv-dev warp Farm 64 15
sdv-dev interact --place "(BC)12" 65,16
sdv-dev interact --drop-in "(O)258" 65,16
sdv-dev world --advance-days 7
sdv-dev checkpoint save ready_to_harvest       # 存快照，改完代码重启也能 1 秒回档反复测收获
sdv-dev interact --right-click 65,16
sdv-dev ui-click --response 0                  # 秒点问答选项（自动跳过 750ms 保护锁）
sdv-dev screenshot --mode screen --ui-overlay --out menu_debug.png
sdv-dev schedule-check Abigail --key spring    # 实机跑原版寻路校验 NPC 全天日程是否可达
sdv-dev harmony StardewValley.Object:performObjectDropInAction --il
sdv-dev profile --seconds 5 --top 10           # 采样帧率、GC 速率及 SMAPI 事件 / Harmony 耗时热点
sdv-dev logs --level Error
sdv-dev stop
```

完整子命令说明可运行 `sdv-dev --help` 或 `sdv-dev <子命令> --help`。

---

## 三、仓库结构与本地开发

- `client/`：`sdv-dev.exe` 薄客户端（NativeAOT）
- `src/`：`sdv-dev-worker.exe` 后端热驻服务（知识库、编译、静态分析、地图与贴图渲染）
- `bridge/`：`SdvDevBridge.dll` 游戏内自动化桥接模组（编译后内嵌进 worker）
- `tools/mgfxc-3.8.0/`：内置对齐星露谷 1.6 (MonoGame 3.8.0.1641) 的 `.fx -> .mgfxo` 着色器编译器依赖
- `skills/stardew-devtools/`：AI Agent 技能文件
- `tests/`：离线与实机端到端回归测试集

本地从源码编译与运行测试（需 `.NET 8 SDK`）：
```powershell
.\build.ps1                        # 编译并安装到本机 ~/.local/bin
.\tests\run-all.ps1                # 运行离线全量回归测试
.\tests\run-all.ps1 -IncludeLive   # 额外启动隔离测试档运行实机端到端验收
.\pack-release.ps1 -Version v1.1.0 # 生成 releases/*.zip 发布包
```

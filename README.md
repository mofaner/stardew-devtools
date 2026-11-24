# stardew-devtools (`sdv-dev`)

面向 AI Agent 与开发者的 **《星露谷物语》（Stardew Valley 1.6）模组全闭环开发、离线地图/精灵图工作台、1.6 语义/Harmony 静态深检与无焦点实机自动化测试工具（CLI + AI Skill）**。

无需安装 Visual Studio、无需安装 `.NET SDK` 或运行时，解压即用，完整支持 **Windows 10 / Windows 11 (x64)** 以及含**中文、空格、`[CP]` 方括号、括号等特殊字符**的系统用户名与目录路径。

---

## 一、零依赖绿色三文件架构

发布包核心由 **3 个绿色便携文件 + 1 个 AI Skill** 组成：

| 组件 | 说明 |
| :--- | :--- |
| `bin/sdv-dev.exe` | **`.NET 8` NativeAOT 纯原生机器码薄壳**（零外部 DLL，冷启动 ~3–8ms，毫秒级转发命名管道请求） |
| `bin/sdv-dev-worker.exe` | **`.NET 8` 自包含单文件热驻引擎**（10 分钟空闲自退；内置内存版 Roslyn C# 编译器、`BepInEx.AssemblyPublicizer`、`ILSpy` 反编译器、`SQLite FTS5`、`SdvDevBridge.dll` 游戏桥接层与 106 篇官方 Wiki 离线兜底包） |
| `bin/sdv-dev.db` | **满血预置本地知识库**（同级目录自动直连、免首次 `sync`；收录 29,000+ 原版/SMAPI API、168 个 1.6 `GameData` Schema、6,200+ 中英原版条目与 XACT 音效 Cue、1,250+ 篇官方与实战文档） |
| `skills/stardew-devtools/` | **AI Agent 专用技能包**（适配 Claude Code、Codex、Cursor、Gemini CLI、Antigravity 等支持 `SKILL.md` 的主流智能体） |

---

## 二、安装教程（Windows 10 / Windows 11）

### 前置要求
- 操作系统：**Windows 10 (1809+) 或 Windows 11 (x64)**（无需管理员权限、无需安装 Visual Studio 或 `.NET`）
- 游戏环境：已安装 PC 版 **《星露谷物语》1.6+** 及 **[SMAPI 4.0+](https://smapi.io/)**（实机测试与本机 DLL 编译引用需要）

---

### 方式一：下载 Release 压缩包一键安装（推荐）

1. 前往本仓库 **Releases** 页面下载最新 `stardew-devtools-<version>-win-x64.zip` 并解压到任意目录（支持中文与空格路径）。
2. **双击运行解压目录中的 `install.cmd`**（自动切换 UTF-8 代码页、自动绕过 Windows 10 默认 PowerShell `Restricted` 执行策略限制、自动解除浏览器下载压缩包的 SmartScreen 锁定标记）。
   - 或者在 PowerShell（兼容 Win10 自带 PowerShell 5.1 与 PowerShell 7）中执行：
     ```powershell
     powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
     ```

`install.cmd` / `install.ps1` 会自动完成三件事：
1. 将 `sdv-dev.exe`、`sdv-dev-worker.exe`、`sdv-dev.db` 安装到 `%USERPROFILE%\.local\bin`；
2. 将 `%USERPROFILE%\.local\bin` 写入当前用户的 `PATH` 环境变量；
3. 将 `stardew-devtools` 技能包自动安装到 `~/.agents/skills/stardew-devtools`，并自动同步到本机已存在的 AI 工具目录（`~/.claude/skills`、`~/.codex/skills`、`~/.cursor/skills`、`~/.gemini/skills`、`~/.gemini/config/skills`）。

> **自定义安装参数（可选）**：
> ```powershell
> # 自定义 CLI 安装目录 + 手动指定非常规星露谷游戏目录
> powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -InstallDir "D:\Tools\sdv-dev" -GamePath "E:\我的游戏\Stardew Valley"
>
> # 仅把 AI Skill 安装到某个指定模组工程工作区 (.agents/skills 与 .claude/skills)
> powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -SkillOnly -ProjectDir "D:\Mods\[CP] 珊瑚湾物语"
> ```

---

### 方式二：纯绿色便携免安装模式

如果不希望修改系统环境变量，解压后直接将 `bin/` 目录下的 3 个文件（`sdv-dev.exe`、`sdv-dev-worker.exe`、`sdv-dev.db`）放在同一文件夹内即可直接调用：

```powershell
.\bin\sdv-dev.exe status
.\bin\sdv-dev.exe api "Farmer stamina"
```
程序优先自动直连同级目录的 `sdv-dev.db`，并通过注册表与 `libraryfolders.vdf` 自动识别 Steam（含自定义中文盘符库路径）与 GOG 版《星露谷物语》安装位置。

---

### Windows 10 与中文/特殊字符路径注意事项

1. **Windows 10 PowerShell 执行策略报错（`因为在此系统上禁止运行脚本`）**：
   - 请直接双击 `install.cmd`，或加上 `-ExecutionPolicy Bypass` 参数执行 `install.ps1`。
2. **Windows SmartScreen 拦截提示**：
   - 首次双击若弹出“Windows 已保护你的电脑”，点击 **更多信息 → 仍要运行**；或在 PowerShell 中对压缩包执行 `Unblock-File -LiteralPath .\stardew-devtools-v1.0.0-win-x64.zip` 后再解压。
3. **模组目录含 `[CP]` 方括号、空格、中文、`&` 或括号**：
   - 星露谷模组目录广泛使用 `[CP] 模组名` 命名。在 PowerShell 中手动传参时请用双引号包裹路径（如 `sdv-dev check "D:\Mods\[CP] 星露谷扩展 (测试)"`）；在自定义 PowerShell 脚本中操作 `[CP]` 目录时请使用 `-LiteralPath` 而非 `-Path`，避免方括号被识别为通配符。
4. **中文 Windows 用户名兼容**：
   - 工具对命名管道、互斥锁及 `.NET` 单文件原生库缓存目录均做了纯 ASCII 确定性哈希与 `C:\ProgramData\sdv-dev` 自动分流处理，`C:\Users\中文名` 账户在 Windows 10 下可零报错运行。
5. **非 Steam/GOG 绿色目录未自动识别游戏路径**：
   - 首次执行一次 `sdv-dev sync --offline --game-path "D:\你的路径\Stardew Valley"` 即可永久记住游戏目录（不会覆盖预置的 106 篇官方 Wiki 文档）。

---

## 三、核心功能速览

### 1. 毫秒级离线知识库与实时反编译
```powershell
sdv-dev api "checkAction"                            # 模糊搜索原版 & SMAPI API（含 private 方法）
sdv-dev decompile StardewValley.Object.placementAction # ILSpy 实时反编译 C# 源码 + 输出 Harmony 签名提示
sdv-dev schema Data/Machines                         # 查询 1.6 GameData 强类型字段定义
sdv-dev data "小桶" --asset Data/BigCraftables       # 按中英文检索原版 JSON 数据模板
sdv-dev data dwop --asset Audio/Cues                 # 按名称或类别 (Music/Sound/Ambient) 检索原版 XACT 音效/BGM Cue
sdv-dev docs "TriggerActions"                        # 检索内置官方 Wiki + 实战排错手册
```

### 2. 免 VS / 免 SDK 工程构建、前置管理、1.6 语义与 Harmony 静态体检
```powershell
sdv-dev new csharp --name CoralMachine --author You  # 生成 SMAPI C# 或 CP 模组脚手架
sdv-dev deps add spacechase0.SpaceCore               # 解析本地嵌套 Mods 前置，缺失时自动从 GitHub Releases 拉取
sdv-dev gen-gmcm .\CoralMachine                      # 解析 ModConfig.cs 自动生成 GMCM 配置菜单与双语 i18n
sdv-dev build .\CoralMachine --deploy                # 内置内存版 Roslyn + AssemblyPublicizer 秒级编译 + 编译后 Harmony 熔断校验
sdv-dev check .\CoralMachine                         # 1.6 数据模型/GSQ/Event 剧本/原版音效拼写/贴图越界/i18n/Harmony/分屏静态全检
sdv-dev pack .\CoralMachine                          # 构建并通过全检后，导出标准单顶层目录发布 .zip 与 SHA256
```

### 3. `.tmx` 地图与精灵图离线工作台（免进游戏 1 秒出图）
```powershell
sdv-dev tilesheet Maps/island_tilesheet_1 --grid --out sheet_grid.png
sdv-dev map-autotile --spec terrain.txt --out assets/IslandBay.tmx --theme island
sdv-dev map-stamp assets/IslandBay.tmx --prefab @island-palm --at 6,4
sdv-dev map-render assets/IslandBay.tmx --out preview.png --season summer --debug-overlay
sdv-dev map-check assets/IslandBay.tmx --entry 10,2  # 连通性孤岛、多格实体 Action 可达性、水面属性与图块越界深检
sdv-dev sprite assets/npc_walk.png --mode npc        # 物品/大件/NPC 四向行走图与 64x64 肖像尺寸体检与切分预览
```

### 4. 无焦点隔离实机闭环、世界交互、实时帧采样与 Transpiler IL 透视
```powershell
sdv-dev start --isolated --test-world                # 独立 --mods-path 隔离秒进专属测试档（绝不触碰个人存档）
sdv-dev warp Farm 64 15                              # 瞬时无黑屏传送并同步落位
sdv-dev interact --place "(BC)12" 65,16              # 摆放机器（小桶）
sdv-dev interact --drop-in "(O)258" 65,16            # 向机器投料（蓝莓）
sdv-dev world --advance-days 7                       # 驱动真实 Game1.NewDay 跨夜结算管线推进 7 天
sdv-dev interact --right-click 65,16                 # 右键收获产物（自动汇报背包增量 +1 蓝莓果酒）
sdv-dev interact --use-tool Hoe 64,16                # 模拟挥动农具/武器（锄头/水壶/十字镐/斧头等）
sdv-dev event-play --stdin < my_event.txt            # 免改 JSON 实时沙盒排演 Event 剧情脚本
sdv-dev asset Data/Machines --key "(BC)12" --cp-trace # 透视运行时合并资产与 Content Patcher 补丁命中链路
sdv-dev harmony StardewValley.Game1:Update --il      # 导出实时 Harmony 补丁链与叠加全部 Transpiler 后的逐条 IL 归属
sdv-dev profile --seconds 5 --top 10                 # 平时零开销、按需动态挂载采样 FPS / 1% Low / GC 速率 / 模组事件与补丁耗时 Top 10
sdv-dev mp add-farmhand                              # 同进程拉起本地分屏客机，全命令支持 --screen host|farmhand 联机验证
sdv-dev logs --level Error                           # 按级别或模组筛选 SMAPI 日志
sdv-dev stop                                         # 安全退出测试实例
```

查看全部命令详细参数与示例：
```powershell
sdv-dev --help
```

---

## 四、从源码构建与制作 Release 包（仅维护者需要）

源码构建仅需 `.NET 8 SDK`（可通过 `winget install Microsoft.DotNet.SDK.8` 安装，若装有 MSVC C++ 工具集则自动启用 `sdv-dev.exe` NativeAOT 编译，否则自动降级为单文件 ReadyToRun）：

```powershell
# 编译三文件绿色产物至 dist/ 并安装到本机
.\build.ps1

# 重新从本机星露谷提取并刷新预置知识库 dist/sdv-dev.db
.\build.ps1 -RefreshDb

# 打包 CLI + Skill 一体化发布压缩包 (releases/stardew-devtools-<version>-win-x64.zip)
.\pack-release.ps1 -Version v1.0.0 -SkipBuild
```

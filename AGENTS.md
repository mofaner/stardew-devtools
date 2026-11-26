# AGENTS.md

本文件面向维护 `stardew-devtools` 仓库本身的开发者与 AI Agent（使用 `sdv-dev` 开发星露谷模组请直接看 `skills/stardew-devtools/SKILL.md` 与 `sdv-dev --help`）。

## 1. 仓库结构

所有中间产物统一输出至根下 `obj\`，绿色三件套输出至 `dist\`：
- `bridge/`：`.NET 6` 游戏内桥接模组（`SdvDevBridge.dll`，编译后内嵌进 worker）
- `src/`：`.NET 8` 单文件后端与守护进程（`sdv-dev-worker.exe`，内嵌 Bridge、Wiki 与 mgfxc）
- `client/`：`.NET 8` NativeAOT 原生薄壳（`sdv-dev.exe`，自动校对 worker 指纹淘汰旧守护进程）
- `tools/mgfxc-3.8.0/`：对齐星露谷 1.6（`MonoGame 3.8.0.1641`）的着色器编译器，**禁止升级至 3.8.1+**（MGFX v10 字节码不兼容）
- `skills/stardew-devtools/SKILL.md`：配套 AI 技能；`RELEASE_README.md`：随发布包分发的简明说明

## 2. 构建、测试与打包

```powershell
.\build.ps1                         # 编译三端并安装到 ~/.local/bin 与全局 skills（知识库逻辑变更时加 -RefreshDb）
.\tests\run-all.ps1                 # 离线回归测试
.\tests\run-all.ps1 -IncludeLive    # 离线 + 实机验收（跑前先确认无玩家正在游玩的 StardewModdingAPI 进程！）
.\pack-release.ps1 -Version <版本号>  # 自动同步 Directory.Build.props / manifest.json 并生成 releases/*.zip
```

## 3. 核心维护原则

- **源码注释写原因**：新增功能、绕坑逻辑或特殊兼容分支时，在对应 C# 类/方法上方用中文注释写清真实痛点与引擎原因（底层细节注释留在源码里，不堆进本文档）。
- **纯新手裸机零依赖**：不得假定用户装有 `.NET SDK`、`pwsh`（需保留 `powershell.exe` 回退）、或 `Content (unpacked)`（需保持 `.xnb` 按需直读回退）；所有 `.ps1` 必须以 **UTF-8 BOM** 保存；第三方前置走在线拉取，不内嵌进二进制。
- **`--help` 唯一主力原则**：命令清单、参数用法与具体防踩坑提示一律写进 `src/CliApp.cs` 的 `PrintHelp` / `PrintTopicHelp`；`SKILL.md` 保持 ~1 KB，只写入口指引、宿主守护约定与安全红线，禁止重复堆砌命令说明。
- **新增 CLI 参数同步**：新增子命令或 `--flag` 时，同步更新 `GlobalKnownOptions` + `EnsureCommandOptions`、`PrintHelp` / `PrintTopicHelp`、`README.md` 与 `tests/run-all.ps1`。
- **运行期与存档安全**：严禁对运行中的模组目录直接调 `Directory.Delete`（用改名腾空或 `.staging` 退后替换）；严禁关闭 `steam.exe`；自动化测试仅限操作 `SdvDevTest_999999999`，禁止触碰个人存档或未托管的玩家游戏窗口。

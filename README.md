# stardew-devtools (`sdv-dev`)

让 AI 编程助手（Claude Code、Cursor、Codex、Gemini 等）真正看懂并实机测试 **星露谷物语 1.6 模组** 的全能工具包（CLI + AI Skill）。

无需安装 Visual Studio，无需配置 `.NET` 环境，支持 **Windows 10 / Windows 11**，中文用户名、空格及 `[CP]` 文件夹路径均可直接使用。

---

## 一、超简单安装（新手推荐）

> 💡 **如果看不懂下面的步骤，直接让你的 AI 帮你装！**  
> 打开你的 AI 编程工具（Claude Code / Cursor / Codex / Gemini / Antigravity 等），直接复制这句话发给它即可：
> ```text
> 请帮我下载并安装 https://github.com/mofaner/stardew-devtools 的最新 Release 包（运行里面的 install.cmd 或 install.ps1），装完用 sdv-dev status 验证一下。
> ```

### 手动 3 步安装

**准备条件**：Windows 10 / 11（64 位），且已安装 PC 版 **星露谷物语 1.6+** 与 **[SMAPI](https://smapi.io/)**。
1. 打开右侧 **[Releases](https://github.com/mofaner/stardew-devtools/releases)** 页面，下载最新的 `stardew-devtools-v1.0.0-win-x64.zip` 并**完整解压**。
2. 双击运行文件夹里的 **`install.cmd`**。
   > 若弹出蓝框“Windows 已保护你的电脑”，点击 **更多信息 → 仍要运行** 即可。
3. **重新打开**你的 AI 工具或终端窗口，安装完成！

现在你可以直接用大白话让 AI 干活了，例如：
- *“帮我做一个星露谷 1.6 的新机器模组，可以把草莓加工成草莓干，做完自己进游戏测一下能不能正常产出。”*
- *“帮我画一张海岛沙滩小地图，摆两棵椰子树，先生成预览图给我看。”*
- *“帮我检查当前文件夹里的模组有没有报错或漏翻译的地方。”*

---

## 二、安装后包含什么？

`install.cmd` 会自动帮你配好两样东西：
1. **`sdv-dev` 命令行工具**（含预置好的星露谷全量数据库，开箱即用，不用首次联网同步）；
2. **`stardew-devtools` AI 技能包**（自动装入本机已有的 Claude Code、Cursor、Codex、Gemini / Antigravity 等工具目录，AI 会自动调用）。

---

## 三、常见问题

**1. 怎么确认安装成功了？**  
新开一个终端（CMD 或 PowerShell），输入：
```powershell
sdv-dev status
```
看到 `Database ... (Exists: True)` 和 `Game Path ... (Exists: True)` 即代表一切就绪。

**2. 提示没找到星露谷游戏目录怎么办？**  
Steam 和 GOG 正版会自动识别。如果你的游戏放在特殊自定义位置，运行一次下面的命令绑定即可：
```powershell
sdv-dev sync --offline --game-path "D:\你的游戏路径\Stardew Valley"
```

**3. 不想改系统环境变量，能直接绿色运行吗？**  
可以。保持 `bin` 文件夹里的 `sdv-dev.exe`、`sdv-dev-worker.exe`、`sdv-dev.db` 三个文件放在一起，直接用 `.\bin\sdv-dev.exe` 调用即可。

**4. 只想把 AI 技能装进某一个项目文件夹？**  
```powershell
.\install.cmd -SkillOnly -ProjectDir "D:\你的模组工程目录"
```

---

## 四、它能帮 AI 和你做什么？

| 场景 | 功能说明 | 常用命令示例 |
| :--- | :--- | :--- |
| **查资料 / 看源码** | 离线查原版代码、1.6 数据格式、物品 ID、音效名、官方 Wiki | `sdv-dev api` / `decompile` / `data` / `docs` |
| **建项目 / 免 VS 编译** | 一键生成 CP 或 C# 模组模板、生成配置菜单、秒级编译与打包 | `sdv-dev new` / `gen-gmcm` / `build` / `pack` |
| **静态找错** | 进游戏前提前揪出 JSON 写错字段、剧本拼写错误、Harmony 崩游戏隐患 | `sdv-dev check .\你的模组目录` |
| **离线做地图 / 查贴图** | 不用开游戏，1 秒生成 `.tmx` 地图与人物行走图预览 | `sdv-dev map-render` / `map-check` / `sprite` |
| **全自动进档实测** | 自动隔离启动测试档（不碰个人存档），摆机器、跨夜、收产物、抓截图 | `sdv-dev start --isolated --test-world` / `interact` |

想手动查看所有命令用法，在终端输入：
```powershell
sdv-dev --help
```

---

## 五、从源码编译（仅开发者维护用）

需安装 `.NET 8 SDK`（`winget install Microsoft.DotNet.SDK.8`）：
```powershell
.\build.ps1                # 编译并安装到本机
.\pack-release.ps1         # 打包生成 releases/*.zip 发布包
```

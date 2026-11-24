# stardew-devtools 使用说明

星露谷物语 1.6 模组开发资料库与实机自动化测试工具包（包含命令行工具 `sdv-dev` 和配套 AI 技能）。装好之后，Claude Code、Cursor、Codex、Gemini、Antigravity 等 AI 助手就能直接离线检索原版源码与 Wiki 资料、编译检查模组、预览地图贴图，并自动开测试档进游戏完成实机验收。

---

## 一、安装方法（二选一）

前提：电脑是 64 位 Windows 10 / 11，并且已经装好了 PC 版 **星露谷物语 1.6+** 和 **[SMAPI](https://smapi.io/)**。

### 方法 A：直接让 AI 帮你装（最省事）
先把整个 `.zip` 压缩包解压出来，打开你的 AI 编程工具，复制下面这句话发给它（把末尾换成你解压出来的文件夹路径）：
```text
请帮我运行这个文件夹里的 install.cmd 完成 stardew-devtools 安装，装完跑一下 sdv-dev status 检查是否成功：<这里粘贴你解压后的文件夹路径>
```

### 方法 B：自己双击安装
1. 把整个 `.zip` 压缩包完整解压到任意文件夹；
2. 双击里面的 **`install.cmd`**（若弹出蓝框拦截提示，点 **更多信息 → 仍要运行**）；
3. 看到“安装完成”后，关掉并重新打开你的 AI 工具即可。

---

## 二、常见问题

**1. 怎么确认装好了？**  
新开一个 CMD 或 PowerShell 窗口输入 `sdv-dev status`，能看到 `Database ... (Exists: True)` 和 `Game Path ... (Exists: True)` 就正常了。

**2. 没自动找到星露谷游戏目录怎么办？**  
Steam 和 GOG 版会自动识别。如果游戏装在特殊位置，让 AI 帮你绑一下，或者在终端跑一行：
```powershell
sdv-dev sync --offline --game-path "D:\你的星露谷目录\Stardew Valley"
```

**3. 只想把技能装进某一个模组项目里？**  
```powershell
.\install.cmd -SkillOnly -ProjectDir "D:\你的模组项目文件夹"
```

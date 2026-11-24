param(
    [string]$InstallDir = '',
    [string]$GamePath = '',
    [switch]$CliOnly,
    [switch]$SkillOnly,
    [string]$ProjectDir = ''
)

$ErrorActionPreference = 'Stop'

try {
    [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
} catch { }

function Resolve-SourceBinDir {
    foreach ($rel in @('bin', 'dist', '.')) {
        $cand = if ($rel -eq '.') { $PSScriptRoot } else { Join-Path $PSScriptRoot $rel }
        if ((Test-Path -LiteralPath (Join-Path $cand 'sdv-dev.exe')) -and
            (Test-Path -LiteralPath (Join-Path $cand 'sdv-dev-worker.exe'))) {
            return $cand
        }
    }
    return $null
}

# 1. 自动解除 Windows 10/11 浏览器下载压缩包附带的 Zone.Identifier (SmartScreen 锁定标记)
Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
    try { Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue } catch { }
}

$targetBin = if ([string]::IsNullOrWhiteSpace($InstallDir)) {
    Join-Path $HOME '.local\bin'
} else {
    [System.IO.Path]::GetFullPath($InstallDir)
}

# 2. 安装 CLI 三文件绿色套件 (sdv-dev.exe + sdv-dev-worker.exe + sdv-dev.db)
if (-not $SkillOnly) {
    $srcBin = Resolve-SourceBinDir
    if (-not $srcBin) {
        throw "未找到 CLI 可执行文件 (sdv-dev.exe / sdv-dev-worker.exe)。若处于源码仓库，请先运行 .\build.ps1。"
    }

    # 更新前先平滑关闭后台热驻 worker，释放文件与 SQLite 锁
    $existingCli = Join-Path $targetBin 'sdv-dev.exe'
    if (Test-Path -LiteralPath $existingCli) {
        try { & $existingCli daemon-stop *> $null } catch { }
    }
    Get-Process -Name 'sdv-dev-worker','sdv-dev' -ErrorAction SilentlyContinue | ForEach-Object {
        try { $_.Kill(); $_.WaitForExit(3000) } catch { }
    }
    Start-Sleep -Milliseconds 100

    [System.IO.Directory]::CreateDirectory($targetBin) | Out-Null

    foreach ($fileName in @('sdv-dev.exe', 'sdv-dev-worker.exe', 'sdv-dev.db')) {
        $srcFile = Join-Path $srcBin $fileName
        if (Test-Path -LiteralPath $srcFile) {
            $dstFile = Join-Path $targetBin $fileName
            Copy-Item -LiteralPath $srcFile -Destination $dstFile -Force
            try { Unblock-File -LiteralPath $dstFile -ErrorAction SilentlyContinue } catch { }
        }
    }

    Write-Host "[1/3] 已安装 CLI 绿色三件套到: $targetBin" -ForegroundColor Green

    # 注册用户级 PATH 环境变量（兼容含中文、空格、括号及特殊字符的路径）
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @()
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $entries = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    $normTarget = $targetBin.TrimEnd('\', '/')
    $alreadyInPath = $entries | Where-Object { $_.Trim().TrimEnd('\', '/') -ieq $normTarget }
    if (-not $alreadyInPath) {
        $entries += $targetBin
        [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'User')
        Write-Host "[2/3] 已将 '$targetBin' 写入当前用户 PATH 环境变量（新开终端自动生效）" -ForegroundColor Green
    } else {
        Write-Host "[2/3] 用户 PATH 环境变量已包含 '$targetBin'" -ForegroundColor Cyan
    }

    if (($env:Path -split ';' | Where-Object { $_.Trim().TrimEnd('\', '/') -ieq $normTarget }).Count -eq 0) {
        $env:Path = "$targetBin;$env:Path"
    }

    # 若显式传入了星露谷游戏路径，写入全局配置
    if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
        $fullGamePath = [System.IO.Path]::GetFullPath($GamePath)
        $cfgDir = Join-Path $HOME '.local\share\sdv-dev'
        [System.IO.Directory]::CreateDirectory($cfgDir) | Out-Null
        $cfgFile = Join-Path $cfgDir 'config.json'
        $cfgJson = @{ GamePath = $fullGamePath; DbPath = '' } | ConvertTo-Json
        [System.IO.File]::WriteAllText($cfgFile, $cfgJson, [System.Text.UTF8Encoding]::new($false))
        Write-Host "      已绑定星露谷游戏目录: $fullGamePath" -ForegroundColor Green
    }
}

# 3. 安装 AI Agent Skill (stardew-devtools)
if (-not $CliOnly) {
    $skillSrc = Join-Path $PSScriptRoot 'skills\stardew-devtools'
    if (-not (Test-Path -LiteralPath $skillSrc)) {
        $skillSrc = Join-Path $PSScriptRoot 'skill\stardew-devtools'
    }
    if (-not (Test-Path -LiteralPath $skillSrc)) {
        throw "未找到技能目录 skills\stardew-devtools。"
    }

    $skillTargets = New-Object System.Collections.Generic.List[string]

    if (-not [string]::IsNullOrWhiteSpace($ProjectDir)) {
        # 安装到指定项目工作区
        $projFull = [System.IO.Path]::GetFullPath($ProjectDir)
        $skillTargets.Add((Join-Path $projFull '.agents\skills'))
        $skillTargets.Add((Join-Path $projFull '.claude\skills'))
    } else {
        # 始终安装通用全局技能目录 ~/.agents/skills，并自动同步已安装的各大 AI 编程助手目录
        $skillTargets.Add((Join-Path $HOME '.agents\skills'))

        $agentRoots = @(
            @{ Root = (Join-Path $HOME '.claude');        Skills = (Join-Path $HOME '.claude\skills') },
            @{ Root = (Join-Path $HOME '.codex');         Skills = (Join-Path $HOME '.codex\skills') },
            @{ Root = (Join-Path $HOME '.cursor');        Skills = (Join-Path $HOME '.cursor\skills') },
            @{ Root = (Join-Path $HOME '.gemini\config'); Skills = (Join-Path $HOME '.gemini\config\skills') },
            @{ Root = (Join-Path $HOME '.gemini');        Skills = (Join-Path $HOME '.gemini\skills') }
        )

        foreach ($ag in $agentRoots) {
            if (Test-Path -LiteralPath $ag.Root) {
                if (-not $skillTargets.Contains($ag.Skills)) {
                    $skillTargets.Add($ag.Skills)
                }
            }
        }
    }

    Write-Host "[3/3] 正在安装 AI 技能包 (stardew-devtools):" -ForegroundColor Green
    foreach ($rootDir in $skillTargets) {
        [System.IO.Directory]::CreateDirectory($rootDir) | Out-Null
        $dstSkill = Join-Path $rootDir 'stardew-devtools'
        if (Test-Path -LiteralPath $dstSkill) {
            Remove-Item -LiteralPath $dstSkill -Recurse -Force
        }
        Copy-Item -LiteralPath $skillSrc -Destination $dstSkill -Recurse -Force
        Write-Host "      -> $dstSkill"
    }
}

# 4. 自检验证
if (-not $SkillOnly) {
    $installedCli = Join-Path $targetBin 'sdv-dev.exe'
    if (Test-Path -LiteralPath $installedCli) {
        Write-Host ""
        Write-Host "---------------- 环境自检 (sdv-dev status) ----------------" -ForegroundColor Cyan
        & $installedCli status
        Write-Host "-----------------------------------------------------------" -ForegroundColor Cyan
        Write-Host "安装完成！重新打开任意终端（CMD / PowerShell / VS Code / AI Agent）即可直接使用 sdv-dev。" -ForegroundColor Green
    }
}

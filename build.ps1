param(
    [string]$GamePath = '',
    [switch]$NoAot,
    [switch]$RefreshDb,
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'

# 0. 自动定位 .NET 8+ SDK（优先找 PATH 中的 dotnet SDK，其次检查 ~/.dotnet/dotnet.exe，彻底摆脱对 Visual Studio 的依赖）
$dotnetExe = $null
$cmdDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($cmdDotnet) {
    $sdks = & $cmdDotnet.Source --list-sdks 2>$null
    if ($sdks -match '^([89]|\d{2})\.') {
        $dotnetExe = $cmdDotnet.Source
    }
}
if (-not $dotnetExe) {
    $userDotnet = Join-Path $HOME '.dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $userDotnet) {
        $dotnetExe = $userDotnet
    }
}
if (-not $dotnetExe) {
    throw "未找到 .NET 8+ SDK。请运行 'winget install Microsoft.DotNet.SDK.8' 或使用官方 dotnet-install.ps1 安装 .NET 8 SDK。"
}

$dotnetDir = Split-Path -Parent $dotnetExe
$env:PATH = "$dotnetDir;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'

# 1. 构建覆盖前先停止正在运行的热驻 Daemon 以释放文件与 SQLite 锁
$existingClient = Join-Path $HOME '.local\bin\sdv-dev.exe'
if (Test-Path -LiteralPath $existingClient) {
    try { & $existingClient daemon-stop *> $null } catch { }
}
Get-Process -Name 'sdv-dev-worker','sdv-dev' -ErrorAction SilentlyContinue | ForEach-Object {
    try { $_.Kill(); $_.WaitForExit(3000) } catch { }
}
Start-Sleep -Milliseconds 80

# 清理旧版 obj 与 dist 目录
$distDir = Join-Path $PSScriptRoot 'dist'
$objDir = Join-Path $PSScriptRoot 'obj'
$bridgeStaging = Join-Path $objDir 'bridge-out'
Remove-Item -LiteralPath $distDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $objDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
New-Item -ItemType Directory -Path $bridgeStaging -Force | Out-Null

# 1.5 将清洗好的 106 篇 Wiki Markdown 打包为 obj\WikiDocsBundle.zip 内嵌进 worker（兜底防外部用户跑 sync 清空 Wiki）
$wikiDir = Join-Path $PSScriptRoot 'docs\custom\wiki'
$wikiZip = Join-Path $objDir 'WikiDocsBundle.zip'
if (Test-Path -LiteralPath $wikiDir) {
    $mdFiles = Get-ChildItem -LiteralPath $wikiDir -Filter '*.md'
    if ($mdFiles.Count -gt 0) {
        $mdFiles | Compress-Archive -DestinationPath $wikiZip -CompressionLevel Optimal -Force
        Write-Host "[预备] 已打包 $($mdFiles.Count) 篇 Wiki 离线文档至内嵌资源包 ($([math]::Round((Get-Item -LiteralPath $wikiZip).Length / 1KB, 1)) KB)"
    }
}

# 2. 使用官方 Pathoschild.Stardew.ModBuildConfig 编译 .NET 6.0 游戏桥接层 (SdvDevBridge.dll)
Write-Host "[1/3] 编译 .NET 6.0 桥接模组 SdvDevBridge.dll (ModBuildConfig)..."
$bridgeArgs = @('build', "$PSScriptRoot\bridge\SdvDevBridge.csproj", '-c', 'Release', '-o', $bridgeStaging, '--nologo', '-v', 'minimal')
if (-not [string]::IsNullOrWhiteSpace($GamePath)) {
    $bridgeArgs += "-p:GamePath=$GamePath"
}
& $dotnetExe @bridgeArgs
if ($LASTEXITCODE -ne 0) {
    throw "SdvDevBridge.csproj 构建失败 (exit=$LASTEXITCODE)"
}

# 3. 单文件自包含打包 sdv-dev-worker.exe（内嵌 SdvDevBridge.dll + WikiDocsBundle.zip + Roslyn + Publicizer + ILSpy + SQLite）
Write-Host "[2/3] 单文件自包含打包后端引擎 sdv-dev-worker.exe (.NET 8 Self-Contained SingleFile)..."
$workerPubDir = Join-Path $objDir 'pub-worker'
& $dotnetExe publish "$PSScriptRoot\src\SdvDev.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $workerPubDir `
    --nologo -v minimal
if ($LASTEXITCODE -ne 0) {
    throw "SdvDev.csproj 单文件发布失败 (exit=$LASTEXITCODE)"
}
Copy-Item -LiteralPath (Join-Path $workerPubDir 'sdv-dev-worker.exe') -Destination (Join-Path $distDir 'sdv-dev-worker.exe') -Force

# 4. 自适应编译前端极速薄壳 sdv-dev.exe（有 C++ 链接器走 NativeAOT，纯 .NET SDK 机器自动走 SingleFile + ReadyToRun）
$canNativeAot = $false
if (-not $NoAot) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path -LiteralPath $vswhere) {
        $vcPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2>$null
        if (-not [string]::IsNullOrWhiteSpace($vcPath) -and (Test-Path -LiteralPath (Join-Path $vcPath 'VC\Tools\MSVC'))) {
            $canNativeAot = $true
        }
    }
    if (-not $canNativeAot -and (Get-Command cl.exe -ErrorAction SilentlyContinue)) {
        $canNativeAot = $true
    }
}

$clientPubDir = Join-Path $objDir 'pub-client'
$builtAot = $false
if ($canNativeAot) {
    Write-Host "[3/3] 检测到 C++ 链接器，正在使用 .NET 8 NativeAOT 编译纯原生薄壳 sdv-dev.exe..."
    & $dotnetExe publish "$PSScriptRoot\client\SdvDevClient.csproj" `
        -c Release `
        -r win-x64 `
        -p:PublishAot=true `
        -o $clientPubDir `
        --nologo -v minimal
    if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath (Join-Path $clientPubDir 'sdv-dev.exe'))) {
        $builtAot = $true
    } else {
        Write-Warning "NativeAOT 链接未通过，自动切换至零 VS 依赖的单文件 ReadyToRun 模式..."
        Remove-Item -LiteralPath $clientPubDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not $builtAot) {
    Write-Host "[3/3] 正在打包零 VS 依赖的单文件薄壳 sdv-dev.exe (.NET 8 SingleFile + ReadyToRun)..."
    & $dotnetExe publish "$PSScriptRoot\client\SdvDevClient.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:EnableCompressionInSingleFile=true `
        -o $clientPubDir `
        --nologo -v minimal
    if ($LASTEXITCODE -ne 0) {
        throw "SdvDevClient.csproj 单文件发布失败 (exit=$LASTEXITCODE)"
    }
}
Copy-Item -LiteralPath (Join-Path $clientPubDir 'sdv-dev.exe') -Destination (Join-Path $distDir 'sdv-dev.exe') -Force

# 5. 同步满血版便携知识库 sdv-dev.db 到 dist/sdv-dev.db（凑齐绿色便携三件套，拷走即用免首次 sync！）
$shareDb = Join-Path $HOME '.local\share\sdv-dev\sdv-dev.db'
if ($RefreshDb -or (-not (Test-Path -LiteralPath $shareDb))) {
    Write-Host "[数据] 正在执行离线知识库刷新 (sync --offline)..."
    & (Join-Path $distDir 'sdv-dev-worker.exe') sync --offline --db $shareDb
}
if (Test-Path -LiteralPath $shareDb) {
    Copy-Item -LiteralPath $shareDb -Destination (Join-Path $distDir 'sdv-dev.db') -Force
}

$clientSizeMb = [math]::Round((Get-Item (Join-Path $distDir 'sdv-dev.exe')).Length / 1MB, 2)
$workerSizeMb = [math]::Round((Get-Item (Join-Path $distDir 'sdv-dev-worker.exe')).Length / 1MB, 2)
$dbSizeMb = if (Test-Path -LiteralPath (Join-Path $distDir 'sdv-dev.db')) { [math]::Round((Get-Item (Join-Path $distDir 'sdv-dev.db')).Length / 1MB, 2) } else { 0 }

Write-Host ""
Write-Host "构建完成！绿色便携三文件产物目录 ($distDir):"
Write-Host "  - sdv-dev.exe        : $clientSizeMb MB ($(if ($builtAot) { 'NativeAOT 纯原生机器码' } else { 'SingleFile ReadyToRun' }))"
Write-Host "  - sdv-dev-worker.exe : $workerSizeMb MB (Self-Contained 单文件，已内嵌 Bridge + Wiki + Roslyn + ILSpy + SQLite)"
if ($dbSizeMb -gt 0) {
    Write-Host "  - sdv-dev.db         : $dbSizeMb MB (满血预置知识库，同级目录自动直连免首次 sync)"
}

if (-not $SkipInstall) {
    $binDir = Join-Path $HOME '.local\bin'
    New-Item -ItemType Directory -Path $binDir -Force | Out-Null

    Copy-Item -LiteralPath "$distDir\sdv-dev.exe" -Destination (Join-Path $binDir 'sdv-dev.exe') -Force
    Copy-Item -LiteralPath "$distDir\sdv-dev-worker.exe" -Destination (Join-Path $binDir 'sdv-dev-worker.exe') -Force
    if (Test-Path -LiteralPath "$distDir\sdv-dev.db") {
        Copy-Item -LiteralPath "$distDir\sdv-dev.db" -Destination (Join-Path $binDir 'sdv-dev.db') -Force
    }
    Remove-Item -LiteralPath (Join-Path $binDir 'sdv-dev.exe.config') -Force -ErrorAction SilentlyContinue
    $legacyLibDir = Join-Path $HOME '.local\lib\sdv-dev'
    if (Test-Path -LiteralPath $legacyLibDir) {
        Remove-Item -LiteralPath $legacyLibDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Host "已安装三文件绿色便携套件到: $binDir"

    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $entries = @()
    if (-not [string]::IsNullOrWhiteSpace($userPath)) {
        $entries = @($userPath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
    $changedPath = $false
    foreach ($dirToEnsure in @($binDir, $distDir, $dotnetDir)) {
        $exists = $entries | Where-Object { $_.TrimEnd('\') -ieq $dirToEnsure.TrimEnd('\') }
        if (-not $exists) {
            $entries += $dirToEnsure
            $changedPath = $true
        }
    }
    if ($changedPath) {
        [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'User')
        Write-Host "已同步用户环境变量 Path: $binDir; $dotnetDir"
    }

    $skillSrc = Join-Path $PSScriptRoot 'skills\stardew-devtools'
    if (Test-Path -LiteralPath $skillSrc) {
        foreach ($skillRoot in @(
            (Join-Path $HOME '.gemini\config\skills'),
            (Join-Path $HOME '.agents\skills')
        )) {
            New-Item -ItemType Directory -Path $skillRoot -Force | Out-Null
            $skillDst = Join-Path $skillRoot 'stardew-devtools'
            if (Test-Path -LiteralPath $skillDst) {
                Remove-Item -LiteralPath $skillDst -Recurse -Force
            }
            Copy-Item -LiteralPath $skillSrc -Destination $skillDst -Recurse -Force
            Write-Host "已安装全局技能到: $skillDst"
        }
    }
}

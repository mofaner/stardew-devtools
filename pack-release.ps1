param(
    [string]$Version = 'v1.1.0',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot 'build.ps1') -SkipInstall
}

$distDir = Join-Path $PSScriptRoot 'dist'
foreach ($req in @('sdv-dev.exe', 'sdv-dev-worker.exe', 'sdv-dev.db')) {
    if (-not (Test-Path -LiteralPath (Join-Path $distDir $req))) {
        throw "缺少构建产物 dist\$req，请先运行 .\build.ps1 -RefreshDb。"
    }
}

$releasesDir = Join-Path $PSScriptRoot 'releases'
$stageRoot = Join-Path $PSScriptRoot 'obj\release-stage'
$pkgFolder = Join-Path $stageRoot 'stardew-devtools'

Remove-Item -LiteralPath $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
[System.IO.Directory]::CreateDirectory((Join-Path $pkgFolder 'bin')) | Out-Null
[System.IO.Directory]::CreateDirectory((Join-Path $pkgFolder 'skills')) | Out-Null
[System.IO.Directory]::CreateDirectory($releasesDir) | Out-Null

# 1. 拷贝三文件绿色 CLI 套件
foreach ($f in @('sdv-dev.exe', 'sdv-dev-worker.exe', 'sdv-dev.db')) {
    Copy-Item -LiteralPath (Join-Path $distDir $f) -Destination (Join-Path $pkgFolder "bin\$f") -Force
}

# 2. 拷贝 AI Agent Skill
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'skills\stardew-devtools') -Destination (Join-Path $pkgFolder 'skills\stardew-devtools') -Recurse -Force

# 3. 拷贝安装器与发布包专用简明说明文档（确保 .ps1 携带 UTF-8 BOM 兼容 Windows 10 PowerShell 5.1）
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'install.cmd') -Destination (Join-Path $pkgFolder 'install.cmd') -Force
$installPs1Text = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'install.ps1'), [System.Text.Encoding]::UTF8)
[System.IO.File]::WriteAllText((Join-Path $pkgFolder 'install.ps1'), $installPs1Text, [System.Text.UTF8Encoding]::new($true))
$relReadmeSrc = Join-Path $PSScriptRoot 'RELEASE_README.md'
if (-not (Test-Path -LiteralPath $relReadmeSrc)) {
    $relReadmeSrc = Join-Path $PSScriptRoot 'README.md'
}
Copy-Item -LiteralPath $relReadmeSrc -Destination (Join-Path $pkgFolder 'README.md') -Force

# 4. 使用 .NET ZipFile 打包（规避 PowerShell Compress-Archive 对特殊字符路径的通配符解析问题）
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipName = "stardew-devtools-$Version-win-x64.zip"
$zipPath = Join-Path $releasesDir $zipName
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
[System.IO.Compression.ZipFile]::CreateFromDirectory($stageRoot, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$sizeMb = [math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 2)

Write-Host ""
Write-Host "Release 发布包已生成:" -ForegroundColor Green
Write-Host "  路径   : $zipPath"
Write-Host "  大小   : $sizeMb MB"
Write-Host "  SHA256 : $hash"

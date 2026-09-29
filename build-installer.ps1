<#
.SYNOPSIS
    WinFloat 安装包 (EXE) 一键打包脚本

.DESCRIPTION
    1. 自动编译并发布 x64 单文件应用（dotnet publish）
    2. 自动检测 Inno Setup 编译器（ISCC.exe）
    3. 调用 Inno Setup 生成现代化的 Windows 安装程序（WinFloat-Setup-vX.X.X.exe）

.PARAMETER Configuration
    构建配置，默认为 Release。

.PARAMETER SelfContained
    是否包含独立 .NET 运行时。
    - 默认不包含（依赖系统 .NET 8 桌面运行时，安装包更小，约 5~8 MB）
    - 传入 -SelfContained 时包含运行时（可在未安装 .NET 8 的电脑上直接运行，安装包约 40~60 MB）

.PARAMETER Version
    自定义版本号。若未指定，默认从 WinFloat.csproj 中读取。

.PARAMETER SkipPublish
    跳过 dotnet publish 步骤，直接使用 artifacts/publish 下已有文件打包。

.EXAMPLE
    .\build-installer.ps1
    .\build-installer.ps1 -SelfContained
    .\build-installer.ps1 -Version "1.0.1"
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SelfContained = $false,
    [string]$Version = "",
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"

# 统一编码为 UTF-8，防止控制台乱码
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
} catch {}

# 获取脚本所在根目录（兼容 Windows PowerShell 5.1 和 PowerShell Core）
$ScriptDir = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($ScriptDir)) {
    if ($MyInvocation.MyCommand.Definition) {
        $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
    } else {
        $ScriptDir = (Get-Location).Path
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "     WinFloat 安装包构建工具 (x64)      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. 解析版本号
$CsprojPath = Join-Path $ScriptDir "WinFloat.csproj"
if (-not (Test-Path -LiteralPath $CsprojPath)) {
    Write-Error "找不到项目文件: $CsprojPath"
    exit 1
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $CsprojContent = Get-Content -LiteralPath $CsprojPath -Raw
    if ($CsprojContent -match '<Version>([^<]+)</Version>') {
        $Version = $Matches[1].Trim()
    } else {
        $Version = "1.0.0"
    }
}

Write-Host "[1/3] 目标版本: $Version (配置: $Configuration, 独立运行时: $SelfContained)" -ForegroundColor Green

# 2. 定位 Inno Setup 编译器 (ISCC.exe)
$IsccFromPath = $null
try {
    $Cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($Cmd) {
        $IsccFromPath = $Cmd.Source
    }
} catch {}

$IsccCandidates = @(
    $IsccFromPath,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:ProgramData\chocolatey\bin\iscc.exe",
    "$env:USERPROFILE\scoop\apps\inno-setup\current\ISCC.exe"
)

$IsccPath = $null
foreach ($Candidate in $IsccCandidates) {
    if (-not [string]::IsNullOrWhiteSpace($Candidate)) {
        if (Test-Path -LiteralPath $Candidate) {
            $IsccPath = $Candidate
            break
        }
    }
}

if (-not $IsccPath) {
    Write-Host ""
    Write-Host "[错误] 未检测到 Inno Setup 编译器 (ISCC.exe)！" -ForegroundColor Red
    Write-Host "打包 EXE 安装包需要安装 Inno Setup 6。" -ForegroundColor Yellow
    Write-Host "你可以通过以下命令快速安装：" -ForegroundColor Yellow
    Write-Host "    winget install JRSoftware.InnoSetup" -ForegroundColor White
    Write-Host "或者访问官网下载：https://jrsoftware.org/isdl.php" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "      找到 Inno Setup: $IsccPath" -ForegroundColor Gray

# 3. 发布应用
$PublishDir = Join-Path $ScriptDir "artifacts\publish"
$ArtifactsDir = Join-Path $ScriptDir "artifacts"

if (-not (Test-Path -LiteralPath $ArtifactsDir)) {
    New-Item -ItemType Directory -Path $ArtifactsDir | Out-Null
}

if ($SkipPublish) {
    Write-Host "[2/3] 跳过发布步骤，直接使用已有 publish 目录..." -ForegroundColor Yellow
} else {
    Write-Host "[2/3] 正在发布项目 (dotnet publish)..." -ForegroundColor Green
    if (Test-Path -LiteralPath $PublishDir) {
        Remove-Item -LiteralPath $PublishDir -Recurse -Force
    }

    $PublishArgs = @(
        "publish",
        "$CsprojPath",
        "-c", $Configuration,
        "-r", "win-x64",
        "--self-contained", "$SelfContained",
        "-p:PublishSingleFile=true",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-o", "$PublishDir"
    )

    & dotnet $PublishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish 失败，退出码: $LASTEXITCODE"
        exit $LASTEXITCODE
    }
}

# 4. 调用 Inno Setup 编译安装包
Write-Host "[3/3] 正在编译安装程序 (Inno Setup)..." -ForegroundColor Green

$IssScript = Join-Path $ScriptDir "installer.iss"
if (-not (Test-Path -LiteralPath $IssScript)) {
    Write-Error "找不到安装脚本: $IssScript"
    exit 1
}

$IsccArgs = @(
    "/Qp",
    "/DMyAppVersion=$Version",
    "/DSourceDir=$PublishDir",
    "/DOutputDir=$ArtifactsDir",
    "$IssScript"
)

& "$IsccPath" $IsccArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup 编译失败，退出码: $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 5. 校验并输出结果
$InstallerFile = Join-Path $ArtifactsDir "WinFloat-Setup-v$Version.exe"
if (Test-Path -LiteralPath $InstallerFile) {
    $FileSizeMb = [math]::Round((Get-Item -LiteralPath $InstallerFile).Length / 1MB, 2)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "          打包成功！                    " -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "安装包文件: $InstallerFile" -ForegroundColor Cyan
    Write-Host "文件大小  : $FileSizeMb MB" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Error "未找到预期的安装包文件: $InstallerFile"
    exit 1
}

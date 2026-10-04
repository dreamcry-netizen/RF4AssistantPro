[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$SkipPublish,

    [switch]$LaunchInteractive
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$solution = Join-Path $repoRoot "RF4AssistantPro.sln"
$testProject = Join-Path $repoRoot "RF4AssistantPro.Tests\RF4AssistantPro.Tests.csproj"
$appProject = Join-Path $repoRoot "RF4AssistantPro\RF4AssistantPro.csproj"
$artifactsRoot = Join-Path $repoRoot "artifacts\windows-qa"
$publishRoot = Join-Path $artifactsRoot "publish"
$logRoot = Join-Path $artifactsRoot "logs"
$summaryPath = Join-Path $artifactsRoot "qa-summary.json"
$requirementsLog = Join-Path $logRoot "checklist-requirements.log"
[xml]$projectXml = Get-Content -Path $appProject
$version = [string]$projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Не удалось определить Version из $appProject."
}

New-Item -ItemType Directory -Force -Path $artifactsRoot, $logRoot | Out-Null
Set-Content -Path $requirementsLog -Value @(
    "# RF4 Assistant Pro v$version — checklist requirements"
    "# Started: $(Get-Date -Format o)"
) -Encoding UTF8

function Write-Requirement {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("INFO", "PASS", "FAIL", "PENDING")]
        [string]$Status,

        [Parameter(Mandatory)]
        [string]$Requirement
    )

    Add-Content -Path $requirementsLog `
        -Value "$(Get-Date -Format o) [$Status] $Requirement" `
        -Encoding UTF8
}

function Invoke-CheckedStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Action
    )

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    try {
        & $Action
        if ($LASTEXITCODE -ne 0) {
            throw "$Name завершился с кодом $LASTEXITCODE."
        }
        Write-Requirement -Status PASS -Requirement $Name
    }
    catch {
        Write-Requirement `
            -Status FAIL `
            -Requirement "$($Name): $($_.Exception.Message)"
        throw
    }
}

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') завершился с кодом $LASTEXITCODE."
    }
}

if ($env:OS -ne "Windows_NT") {
    Write-Requirement -Status FAIL -Requirement "Запуск на Windows"
    throw "Этап Windows QA должен запускаться на Windows."
}
Write-Requirement -Status PASS -Requirement "Запуск на Windows"

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnetCommand) {
    Write-Requirement -Status FAIL -Requirement "Найден dotnet"
    throw "Не найден dotnet. Установите .NET 10 SDK и повторите запуск."
}
Write-Requirement -Status PASS -Requirement "Найден dotnet"

if (-not (Test-Path $solution) -or
    -not (Test-Path $testProject) -or
    -not (Test-Path $appProject)) {
    Write-Requirement `
        -Status FAIL `
        -Requirement "Файлы solution и проектов доступны"
    throw "Не найден один из файлов проекта или solution."
}
Write-Requirement `
    -Status PASS `
    -Requirement "Файлы solution и проектов доступны"

$sdkList = @(dotnet --list-sdks)
if ($LASTEXITCODE -ne 0 -or
    -not ($sdkList | Where-Object { $_ -match "^\s*10\." })) {
    Write-Requirement -Status FAIL -Requirement ".NET 10 SDK установлен"
    throw "Не найден .NET 10 SDK. Установите SDK и повторите запуск."
}
Write-Requirement -Status PASS -Requirement ".NET 10 SDK установлен"

$startedAt = Get-Date
$restoreLog = Join-Path $logRoot "restore.log"
$buildLog = Join-Path $logRoot "build.log"
$testLog = Join-Path $logRoot "tests.log"
$publishLog = Join-Path $logRoot "publish.log"

Invoke-CheckedStep "dotnet restore" {
    & dotnet restore $solution -r win-x64 2>&1 |
        Tee-Object -FilePath $restoreLog
}

Invoke-CheckedStep "dotnet build" {
    & dotnet build $solution -c $Configuration --no-restore 2>&1 |
        Tee-Object -FilePath $buildLog
}

Invoke-CheckedStep "интеграционные тесты" {
    & dotnet run --project $testProject -c $Configuration --no-restore 2>&1 |
        Tee-Object -FilePath $testLog
}

$publishedExe = Join-Path $publishRoot "RF4AssistantPro.exe"
if (-not $SkipPublish) {
    if (Test-Path $publishRoot) {
        Remove-Item $publishRoot -Recurse -Force
    }

    Invoke-CheckedStep "dotnet publish win-x64" {
        & dotnet publish $appProject `
            -c $Configuration `
            -r win-x64 `
            --self-contained true `
            -p:PublishSingleFile=false `
            -p:DebugType=None `
            -p:DebugSymbols=false `
            -o $publishRoot 2>&1 |
            Tee-Object -FilePath $publishLog
    }

    if (-not (Test-Path $publishedExe)) {
        Write-Requirement `
            -Status FAIL `
            -Requirement "Опубликованный RF4AssistantPro.exe существует"
        throw "После publish не найден $publishedExe."
    }
    Write-Requirement `
        -Status PASS `
        -Requirement "Опубликованный RF4AssistantPro.exe существует"

    $publishedAsset = Join-Path $publishRoot "Assets\Baits\worm.png"
    if (-not (Test-Path $publishedAsset)) {
        Write-Requirement `
            -Status FAIL `
            -Requirement "Обязательный asset скопирован в publish"
        throw "После publish не найден обязательный asset: $publishedAsset."
    }
    Write-Requirement `
        -Status PASS `
        -Requirement "Обязательный asset скопирован в publish"
}

Write-Requirement `
    -Status PENDING `
    -Requirement "Ручная WPF/OCR/hook/capture/ZIP-проверка из qa\WINDOWS_QA_CHECKLIST.md"

if ($LaunchInteractive) {
    if (-not (Test-Path $publishedExe)) {
        throw "Для -LaunchInteractive нужен publish. Не используйте -SkipPublish."
    }

    Write-Host @"

Интерактивный этап Windows QA.
Проверьте сценарии из qa\WINDOWS_QA_CHECKLIST.md.
После завершения ручной проверки нажмите Enter; приложение будет закрыто.
"@ -ForegroundColor Yellow

    $process = Start-Process -FilePath $publishedExe `
        -WorkingDirectory $publishRoot `
        -PassThru
    try {
        Read-Host "Нажмите Enter после ручной проверки"
    }
    finally {
        if (-not $process.HasExited) {
            $process.CloseMainWindow() | Out-Null
            if (-not $process.WaitForExit(5000)) {
                $process.Kill()
            }
        }
    }
}

$finishedAt = Get-Date
$summary = [ordered]@{
    Product = "RF4 Assistant Pro"
    Version = $version
    Configuration = $Configuration
    Runtime = "win-x64"
    StartedAt = $startedAt.ToUniversalTime().ToString("O")
    FinishedAt = $finishedAt.ToUniversalTime().ToString("O")
    Dotnet = (& dotnet --version).Trim()
    PublishExecuted = -not $SkipPublish
    InteractiveLaunched = [bool]$LaunchInteractive
    PreflightStatus = "PASS"
    ManualQaStatus = "PENDING"
    Status = "PRECHECK_PASS"
    ReleaseReady = $false
    ChecklistRequirementsLog = $requirementsLog
    Logs = @(
        $restoreLog,
        $buildLog,
        $testLog
    )
}

if (-not $SkipPublish) {
    $summary.Logs += $publishLog
    $summary.PublishDirectory = $publishRoot
}

$summary | ConvertTo-Json -Depth 5 | Set-Content `
    -Path $summaryPath `
    -Encoding UTF8

Write-Host "`nWindows preflight завершён успешно." -ForegroundColor Green
Write-Host "Сводка: $summaryPath"
Write-Host "Лог требований чек-листа: $requirementsLog"
if (-not $LaunchInteractive) {
    Write-Host "Для ручной проверки запустите с параметром -LaunchInteractive."
}
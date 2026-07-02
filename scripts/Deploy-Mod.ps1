param(
    [string]$Configuration = "Debug",
    [string]$GamePath = "D:\SteamLibrary\steamapps\common\Across the Obelisk"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$projectRoot = Split-Path -Parent $PSScriptRoot
$pluginsDir = Join-Path $GamePath "BepInEx\plugins"
$modDir = Join-Path $pluginsDir "AccessTheObelisk"

& "$PSScriptRoot\Build-Mod.ps1" -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Deploy skipped."
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $modDir)) {
    New-Item -ItemType Directory -Force -Path $modDir | Out-Null
}

$dllPath = Join-Path $projectRoot "bin\$Configuration\net472\AccessTheObelisk.dll"
Copy-Item -LiteralPath $dllPath -Destination $modDir -Force
Write-Host "Deployed AccessTheObelisk.dll to $modDir"

$prismDllPath = Join-Path $projectRoot "third_party\prism\v0.16.5\windows-x64\prism.dll"
if (Test-Path -LiteralPath $prismDllPath) {
    Copy-Item -LiteralPath $prismDllPath -Destination $GamePath -Force
    Write-Host "Deployed prism.dll to $GamePath"

    $legacyPrismPath = Join-Path $modDir "prism.dll"
    if (Test-Path -LiteralPath $legacyPrismPath) {
        Remove-Item -LiteralPath $legacyPrismPath -Force
        Write-Host "Removed duplicate prism.dll from $modDir"
    }
} else {
    Write-Error "Prism DLL not found at $prismDllPath."
}

$legacyDllPath = Join-Path $pluginsDir "AccessTheObelisk.dll"
if (Test-Path -LiteralPath $legacyDllPath) {
    Remove-Item -LiteralPath $legacyDllPath -Force
    Write-Host "Removed legacy AccessTheObelisk.dll from $pluginsDir"
}

$localizationSource = Join-Path $projectRoot "Localization"
if (Test-Path -LiteralPath $localizationSource) {
    $localizationTarget = Join-Path $modDir "Localization"
    if (-not (Test-Path -LiteralPath $localizationTarget)) {
        New-Item -ItemType Directory -Force -Path $localizationTarget | Out-Null
    }

    Copy-Item -Path (Join-Path $localizationSource "*") -Destination $localizationTarget -Force
    Write-Host "Deployed localization files to $localizationTarget"
}

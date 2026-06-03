param(
    [string]$Configuration = "Release",
    [string]$Version = "0.3.5",
    [string]$OutputDirectory = "",
    [string]$RussianLocalizationZip = "C:\Users\Incognitus\Downloads\AcrossTheObelisk_Russian_v1.2.1.zip"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "dist"
}

& "$PSScriptRoot\Build-Mod.ps1" -Configuration $Configuration
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Package skipped."
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $OutputDirectory)) {
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
}

$packageName = "AccessTheObelisk-v$Version"
$stageRoot = Join-Path $OutputDirectory $packageName
$zipPath = Join-Path $OutputDirectory "$packageName.zip"

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$modTarget = Join-Path $stageRoot "BepInEx\plugins\AccessTheObelisk"
$locTarget = Join-Path $modTarget "Localization"
New-Item -ItemType Directory -Force -Path $locTarget | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot "bin\$Configuration\net472\AccessTheObelisk.dll") -Destination $modTarget -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Localization\en.txt") -Destination $locTarget -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Localization\ru.txt") -Destination $locTarget -Force

$files = Get-ChildItem -LiteralPath $stageRoot -Recurse -File | ForEach-Object {
    $relativePath = $_.FullName.Substring($stageRoot.Length + 1).Replace('\', '/')
    [ordered]@{
        path = $relativePath
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
    }
}

$manifest = [ordered]@{
    packageId = "AccessTheObelisk"
    name = "AccessTheObelisk"
    version = $Version
    files = @($files)
}

$manifestJson = $manifest | ConvertTo-Json -Depth 5
[System.IO.File]::WriteAllText((Join-Path $stageRoot "manifest.json"), $manifestJson, $utf8NoBom)

Compress-Archive -Path (Join-Path $stageRoot "*") -DestinationPath $zipPath -Force
Write-Host "Created mod package: $zipPath"

if (Test-Path -LiteralPath $RussianLocalizationZip) {
    $russianTarget = Join-Path $OutputDirectory (Split-Path -Leaf $RussianLocalizationZip)
    Copy-Item -LiteralPath $RussianLocalizationZip -Destination $russianTarget -Force
    Write-Host "Copied Russian localization package: $russianTarget"
} else {
    Write-Host "Russian localization package not found: $RussianLocalizationZip"
}

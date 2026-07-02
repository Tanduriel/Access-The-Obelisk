param(
    [string]$Configuration = "Release",
    [string]$Version = "0.4",
    [string]$OutputDirectory = "",
    [string]$RussianLocalizationZip = "C:\Users\Incognitus\Downloads\AcrossTheObelisk_Russian_v1.2.1.zip",
    [string]$ReleaseDocsDirectory = ""
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
if ([string]::IsNullOrWhiteSpace($ReleaseDocsDirectory)) {
    $ReleaseDocsDirectory = Join-Path $projectRoot "docs"
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
$bepInExRuntime = Join-Path $projectRoot "third_party\bepinexpack-acrosstheobelisk\v5.4.23"

if (-not (Test-Path -LiteralPath $bepInExRuntime)) {
    Write-Error "BepInExPack_AcrossTheObelisk runtime not found at $bepInExRuntime"
    exit 1
}

New-Item -ItemType Directory -Force -Path $stageRoot | Out-Null
Get-ChildItem -LiteralPath $bepInExRuntime -Force | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $locTarget | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot "bin\$Configuration\net472\AccessTheObelisk.dll") -Destination $modTarget -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Localization\en.txt") -Destination $locTarget -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "Localization\ru.txt") -Destination $locTarget -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "third_party\prism\v0.16.5\windows-x64\prism.dll") -Destination $stageRoot -Force

if (Test-Path -LiteralPath $ReleaseDocsDirectory) {
    # Only the player-facing En/Ru subfolders (readme.txt, changelog.txt) ship in the
    # release package. Internal dev docs (game-api.md, release-guide.md) stay out.
    $docsStageRoot = Join-Path $stageRoot "docs"
    New-Item -ItemType Directory -Force -Path $docsStageRoot | Out-Null
    foreach ($locale in @("En", "Ru")) {
        $localeSource = Join-Path $ReleaseDocsDirectory $locale
        if (Test-Path -LiteralPath $localeSource) {
            Copy-Item -LiteralPath $localeSource -Destination $docsStageRoot -Recurse -Force
        } else {
            Write-Error "Release docs locale directory not found: $localeSource"
            exit 1
        }
    }
} else {
    Write-Error "Release docs directory not found: $ReleaseDocsDirectory"
    exit 1
}

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

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$projectRoot = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $projectRoot "installer\AccessTheObelisk.Installer\AccessTheObelisk.Installer.csproj"

dotnet publish $installerProject -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) {
    Write-Error "Installer build failed."
    exit $LASTEXITCODE
}

$exePath = Join-Path $projectRoot "installer\AccessTheObelisk.Installer\bin\$Configuration\net8.0-windows\win-x64\publish\AccessTheObelisk.Installer.exe"
Write-Host "Installer build succeeded: $exePath"

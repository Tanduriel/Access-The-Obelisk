param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "AccessTheObelisk.csproj"

dotnet build $projectPath -c $Configuration /p:CodePage=65001
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed."
    exit $LASTEXITCODE
}

$dllPath = Join-Path $projectRoot "bin\$Configuration\net472\AccessTheObelisk.dll"
Write-Host "Build succeeded: $dllPath"

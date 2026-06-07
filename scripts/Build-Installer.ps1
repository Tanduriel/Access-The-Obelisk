param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::InputEncoding = $utf8NoBom
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$projectRoot = Split-Path -Parent $PSScriptRoot
$installerProject = Join-Path $projectRoot "installer\AccessTheObelisk.Installer.Rust\Cargo.toml"
$distDir = Join-Path $projectRoot "dist"
$cargoBin = Join-Path $env:USERPROFILE ".cargo\bin"
if (Test-Path -LiteralPath $cargoBin) {
    $env:Path = "$cargoBin;$env:Path"
}

$cargo = Get-Command cargo -ErrorAction SilentlyContinue
if ($null -eq $cargo) {
    Write-Error "Cargo was not found. Install Rust first: winget install --id Rustlang.Rustup -e"
    exit 1
}

$cargoArgs = @("build", "--manifest-path", $installerProject)
if ($Configuration -ieq "Release") {
    $cargoArgs += "--release"
}

& cargo @cargoArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Installer build failed."
    exit $LASTEXITCODE
}

$profile = if ($Configuration -ieq "Release") { "release" } else { "debug" }
$builtExe = Join-Path $projectRoot "installer\AccessTheObelisk.Installer.Rust\target\$profile\access_the_obelisk_installer.exe"
if (-not (Test-Path -LiteralPath $builtExe)) {
    Write-Error "Installer executable was not found: $builtExe"
    exit 1
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$exePath = Join-Path $distDir "AccessTheObelisk.Installer.exe"
Copy-Item -LiteralPath $builtExe -Destination $exePath -Force

$sizeMb = [Math]::Round((Get-Item -LiteralPath $exePath).Length / 1MB, 2)
Write-Host "Installer build succeeded: $exePath ($sizeMb MB)"

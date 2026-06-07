# Release Guide

This project cannot build the BepInEx mod package fully in GitHub Actions because the mod references local Across the Obelisk game assemblies. Do not commit or upload decompiled game code or game DLLs.

## Local release steps

1. Build and package the mod:

   `.\scripts\Package-Release.ps1 -Configuration Release -Version 0.4`

2. Build the installer:

   `.\scripts\Build-Installer.ps1 -Configuration Release`

3. Create a GitHub release with tag:

   `v0.4`

4. Upload these assets:

   `D:\AtOAccess\dist\AccessTheObelisk-v0.4.zip`

   `D:\AtOAccess\dist\AcrossTheObelisk_Russian_v1.2.1.zip`

   `D:\AtOAccess\dist\AccessTheObelisk.Installer.exe`

5. Put the version changelog into the GitHub release body. The installer shows this text before install or update.

## Installer expectations

The installer reads versions from GitHub Releases in:

`tanduriel/Access-The-Obelisk`

The accessibility mod asset must be named like:

`AccessTheObelisk-v0.4.zip`

The optional Russian localization asset currently uses:

`AcrossTheObelisk_Russian_v1.2.1.zip`

The AccessTheObelisk release zip includes Thunderstore's game-specific BepInExPack_AcrossTheObelisk 5.4.23, Prism, and the mod. When installing an older mod release that did not bundle it, the installer downloads that pack directly from Thunderstore and extracts the inner `BepInExPack_AcrossTheObelisk` folder into the game directory.

## Installer toolchain

The installer is built with Rust and native Windows controls. If Rust is missing locally, install it once:

`winget install --id Rustlang.Rustup -e`

Then use:

`rustup default stable-x86_64-pc-windows-msvc`

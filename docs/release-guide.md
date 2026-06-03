# Release Guide

This project cannot build the BepInEx mod package fully in GitHub Actions because the mod references local Across the Obelisk game assemblies. Do not commit or upload decompiled game code or game DLLs.

## Local release steps

1. Build and package the mod:

   `.\scripts\Package-Release.ps1 -Configuration Release -Version 0.3.5`

2. Build the installer:

   `.\scripts\Build-Installer.ps1 -Configuration Release`

3. Create a GitHub release with tag:

   `v0.3.5`

4. Upload these assets:

   `D:\AtOAccess\dist\AccessTheObelisk-v0.3.5.zip`

   `D:\AtOAccess\dist\AcrossTheObelisk_Russian_v1.2.1.zip`

   `D:\AtOAccess\installer\AccessTheObelisk.Installer\bin\Release\net8.0-windows\win-x64\publish\AccessTheObelisk.Installer.exe`

5. Put the version changelog into the GitHub release body. The installer shows this text before install or update.

## Installer expectations

The installer reads versions from GitHub Releases in:

`tanduriel/Access-The-Obelisk`

The accessibility mod asset must be named like:

`AccessTheObelisk-v0.3.5.zip`

The optional Russian localization asset currently uses:

`AcrossTheObelisk_Russian_v1.2.1.zip`

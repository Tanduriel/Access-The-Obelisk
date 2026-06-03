# AccessTheObelisk

AccessTheObelisk is an accessibility mod for Across the Obelisk.

The mod targets blind screen reader users and works with the game's existing BepInEx 5.x mod loader setup.

## Install

Use the Windows installer from GitHub Releases when available.

The installer can:

- find or select the game folder;
- install AccessTheObelisk;
- update AccessTheObelisk from GitHub Releases;
- remove AccessTheObelisk safely;
- install the optional Russian game localization package.

## Manual Package

Release archives use this structure:

`BepInEx\plugins\AccessTheObelisk\AccessTheObelisk.dll`

`BepInEx\plugins\AccessTheObelisk\Localization\en.txt`

`BepInEx\plugins\AccessTheObelisk\Localization\ru.txt`

## Build

Build the mod:

`.\scripts\Build-Mod.ps1`

Package a release:

`.\scripts\Package-Release.ps1 -Version 0.3.5`

Build the installer:

`.\scripts\Build-Installer.ps1`

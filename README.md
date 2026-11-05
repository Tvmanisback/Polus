# Polus

**A custom-role mod and Windows patcher for Among Us (Steam).**

<div align="center">

<a href="#downloads"><img src="https://img.shields.io/badge/Downloads-Choose%20your%20platform-5865F2?style=for-the-badge" alt="Downloads"></a>
<a href="#features"><img src="https://img.shields.io/badge/Explore-Features-8A2BE2?style=for-the-badge" alt="Features"></a>
<a href="#installation"><img src="https://img.shields.io/badge/Setup-Installation-orange?style=for-the-badge" alt="Installation"></a>

</div>

---

## Downloads

| Platform | Status | Download |
| --- | --- | --- |
| **PC (Windows x64)** | EXE release | [**Download Polus.exe**](https://github.com/Tvmanisback/Polus/releases/latest/download/Polus.exe) |
| **Android** | APK in development | Not available yet |
| **iOS** | Unsupported | Not available |

**Windows:** Download the EXE directly. Releases contain the installer executable, not a ZIP file. If the direct download is not available yet, open [Polus Releases](https://github.com/Tvmanisback/Polus/releases) and download the Polus.exe asset from the latest release.

## Quick navigation

- [Downloads](#downloads)
- [Features](#features)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Disclaimer and credits](#disclaimer-and-credits)

## Features

### Windows patcher

- Detects Steam libraries, including secondary library folders.
- Checks that the installed Among Us executable is 64-bit.
- Installs the matching BepInEx IL2CPP x64 loader.
- Backs up files before replacing them.
- Installs the Polus plugin, role runtime, and role catalogue.
- Checks for updates and can update the patcher.
- Provides a restore option to undo the patcher's game-folder changes.

### Custom roles

- Includes 35 original role specifications.
- Role-specific ability labels and mechanics are implemented in the plugin.
- Uses a separate role ability button; the game's normal Kill button is not replaced.
- Quartermaster retains its separate emergency-meeting button.
- Custom role behavior requires compatible modded clients.

### Host panel

- Host-only panel opened with **P**.
- Player moderation controls.
- Private-lobby-only gameplay settings.
- Settings changes are restricted to the host and private lobbies.

## Compatibility

- **Game:** Among Us Steam, build 2026.9.29 / Build ID 25134893.
- **Architecture:** Windows x64.
- **Loader:** BepInEx 6 IL2CPP, build line 6.0.0-be.788.
- **Plugin target:** AmongUs.GameLibs.Steam 2026.9.29, Reactor 2.5.1-ci.400.

Game updates can break compatibility. All players who need custom role functionality must use a compatible Polus build. Vanilla mobile clients cannot run the custom plugin or custom role effects.

## Installation

1. Download **Polus.exe** from the [latest release](https://github.com/Tvmanisback/Polus/releases/latest/download/Polus.exe).
2. Run the downloaded EXE. **There is no ZIP file to extract.**
3. Select **Setup**. The patcher searches Steam libraries automatically; use Browse if needed.
4. Let Setup install BepInEx and create a backup.
5. Select **Patch** to install Polus.
6. Launch Among Us through Steam.
7. To undo the changes, open Polus and use **Restore** with the backup created during Setup.

## Disclaimer and credits

Polus is an independent community project and is not affiliated with, endorsed by, or sponsored by Innersloth. It does not bypass anti-cheat or disable game security protections.

Polus source code is MIT licensed. BepInEx, Reactor, and Among Us game libraries remain subject to their own licenses.

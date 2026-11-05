# Polus

**A custom-role mod and Windows patcher for Among Us (Steam).**

<div align="center">

<a href="#downloads"><img src="https://img.shields.io/badge/Downloads-Choose%20your%20platform-5865F2?style=for-the-badge" alt="Downloads"></a>
<a href="https://github.com/Tvmanisback/Polus/releases"><img src="https://img.shields.io/badge/GitHub-Releases-2ea44f?style=for-the-badge&logo=github" alt="Releases"></a>
<a href="https://github.com/Tvmanisback/Polus/actions"><img src="https://img.shields.io/badge/Build-GitHub%20Actions-blue?style=for-the-badge&logo=githubactions" alt="Builds"></a>
<a href="#features"><img src="https://img.shields.io/badge/Explore-Features-8A2BE2?style=for-the-badge" alt="Features"></a>
<a href="#installation"><img src="https://img.shields.io/badge/Setup-Installation-orange?style=for-the-badge" alt="Installation"></a>

</div>

---

## Downloads

**Download from [GitHub Releases](https://github.com/Tvmanisback/Polus/releases), or use the platform links below.** A platform build is only available once it has been published to Releases.

| Platform | Status | Download |
| --- | --- | --- |
| **Android** | Working on APK | [Download for Android](https://github.com/Tvmanisback/Polus/releases) |
| **PC (Windows x64)** | Working on EXE | [Download for PC](https://github.com/Tvmanisback/Polus/releases) |
| **iOS** | Currently impossible | Not available |

<a id="download"></a>
[<img src="https://img.shields.io/badge/Download%20from-GitHub%20Releases-2ea44f?style=for-the-badge&logo=github" alt="Download from GitHub Releases">](https://github.com/Tvmanisback/Polus/releases)

> **Note:** Android and PC downloads are not advertised as ready until their actual APK/EXE assets are published. iOS is currently unsupported.

## Quick navigation

- [Downloads](#downloads)
- [Features](#features)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Build from source](#build-from-source)
- [Project structure](#project-structure)
- [Disclaimer and credits](#disclaimer-and-credits)

## Features

### Windows patcher

- Detects Steam libraries, including secondary library folders.
- Checks that the installed Among Us executable is 64-bit.
- Installs the matching BepInEx IL2CPP x64 loader.
- Backs up files before replacing them.
- Installs the Polus plugin, role runtime, and role catalogue.
- Checks GitHub Releases for updates and can update the patcher.
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

1. Download the Windows build from [GitHub Releases](https://github.com/Tvmanisback/Polus/releases).
2. Extract the downloaded ZIP.
3. Run **Polus.exe**.
4. Select **Setup**. The patcher searches Steam libraries automatically; use Browse if needed.
5. Let Setup install BepInEx and create a backup.
6. Select **Patch** to install Polus.
7. Launch Among Us through Steam.
8. To undo the changes, open Polus and use **Restore** with the backup created during Setup.

## Build from source

The repository's GitHub Actions workflow builds the Windows x64 patcher.

Requirements:
- .NET 6 SDK
- .NET 8 SDK
- Node.js 20
- Python 3.12

Commands:

```powershell
dotnet restore .\src\Plugin\Polus.Plugin.csproj --configfile nuget.config
dotnet build .\src\Plugin\Polus.Plugin.csproj -c Release --no-restore
dotnet publish .\src\Patcher\Patcher.csproj -c Release -r win-x64 --self-contained true
```

To create a release, push a version tag such as `v1.0.1`. The workflow attaches the Windows patcher executable to the GitHub Release.

## Project structure

| Path | Purpose |
| --- | --- |
| `.github/workflows/build.yml` | Windows build and release automation |
| `src/Patcher/` | Windows patcher, setup, restore, and updater |
| `src/Plugin/` | BepInEx IL2CPP plugin |
| `src/RoleCore/` | Shared role models and runtime |
| `roles/roles.json` | Original role specifications |
| `assets/polus-icon.svg` | Source artwork for the Windows app icon |

## Disclaimer and credits

Polus is an independent community project and is not affiliated with, endorsed by, or sponsored by Innersloth. It does not bypass anti-cheat or disable game security protections.

Polus source code is MIT licensed. BepInEx, Reactor, and Among Us game libraries remain subject to their own licenses.

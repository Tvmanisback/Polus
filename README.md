# Polus

Polus is a Windows x64 patcher and an in-development custom-role mod for the Steam version of Among Us.

## Compatibility target

- Among Us Steam public build: **2026.9.29 / Build ID 25134893** (64-bit).
- BepInEx: **6.0.0-be.788 IL2CPP Windows x64** build line.
- Plugin compile references: AmongUs.GameLibs.Steam 2026.9.29 and Reactor 2.5.1-ci.400.

Game updates can break mods. Polus checks the architecture and installs the official BepInEx x64 IL2CPP build, but a loader version alone does not guarantee game API compatibility.

## Patcher features

- **Setup:** detects the Steam library (including secondary Steam libraries), verifies that Among Us.exe is 64-bit, downloads the latest matching BepInEx IL2CPP x64 build from the official BepInEx builds site, backs up overwritten files, and installs it beside Among Us.exe.
- **Patch:** installs the Polus plugin assembly, shared role runtime assembly, and role catalogue into BepInEx/plugins/Polus.
- **Restore:** restores files backed up by Setup and removes the Polus plugin folder.
- **Check for updates:** checks the latest GitHub release for this repository.
- **Update Polus:** downloads the Polus.exe release asset, checks the PE header and GitHub SHA-256 digest when provided, then replaces and restarts the running patcher.
- The patcher does not bypass anti-cheat, disable game protections, or modify matchmaking security.

## Current gameplay status

The plugin now contains role-specific gameplay hooks for all 35 role classes:
- Crewmate roles track their own task, map-sector, meeting, streak, ledger, or checkpoint mechanics.
- Impostor roles have distinct action handlers, including temporary visual effects, vote trading, player repositioning, position swapping, target marking, and private deception records.
- Neutral roles use different task-pattern objectives; their custom win check uses that role's objective progress rather than a generic task total.
- The shared action button displays a role-specific name and uses a cooldown. Quartermaster keeps its separate emergency-meeting button.

This is an initial gameplay implementation, not a claim of fully balanced or multiplayer-verified gameplay. The latest GitHub Actions build must pass, and the mod still needs testing in an actual modded lobby. All clients must have a compatible Polus build; vanilla mobile clients cannot execute the custom role code or display its custom effects.

## Build

GitHub Actions builds on pushes to main and on tags matching v*. Download the Windows x64 artifact from the workflow run.

To publish a release, push a version tag such as v1.0.1. The workflow attaches Polus.exe to that GitHub release so the in-app updater can find it.

Build on Windows with .NET 6 and .NET 8 SDKs:

    dotnet restore .\src\Plugin\Polus.Plugin.csproj --configfile nuget.config
    dotnet build .\src\Plugin\Polus.Plugin.csproj -c Release --no-restore
    dotnet publish .\src\Patcher\Patcher.csproj -c Release -r win-x64 --self-contained true

## Install

1. Run Polus.exe.
2. Press **Setup**. It detects Steam automatically; if it cannot find the game, use Browse.
3. Setup installs the x64 BepInEx loader and creates a backup.
4. Press **Patch** to install the Polus plugin payload.
5. Press **Check for updates** to check GitHub releases, then **Update Polus** if a newer release is available.
6. Use **Restore** and select the backup folder to undo the game-folder changes.

## Source layout

- `.github/workflows/build.yml` — build and release workflow.
- `src/Patcher/` — Windows patcher, setup, restore and self-updater.
- `src/Plugin/` — BepInEx IL2CPP plugin entry point.
- `src/RoleCore/` — game-independent role models and match runtime.
- `roles/roles.json` — 35 original role specifications.

Polus source is MIT licensed. BepInEx, Reactor, and Among Us game libraries remain under their respective licenses. Polus is not affiliated with or endorsed by Innersloth.

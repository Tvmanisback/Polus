# Polus Android

Polus Android is a standalone, host-only Among Us mod designed to run through a compatible Android mod loader such as Starlight.

## Design

- **No MiraAPI or Reactor dependency in the Android mod.**
- Uses a Polus-owned role registry, options, ability cooldowns, meeting abilities, and match state.
- Follows a Town-of-Host-style architecture: the host owns role assignment and role logic; unmodified clients do not need Polus installed.
- Vanilla mode leaves Polus gameplay hooks disabled.
- Modded mode enables Polus host-side hooks and the in-game host panel.

## Current Android project

- `Polus.Android`: Android patcher / companion app.
- `LemonLoader.Core`: APK patching and installation support.
- `Polus.Mod`: Android runtime plugin.
- `PolusHostRoles.cs`: standalone host-side role and option registry.
- `AndroidRoleMechanics.cs`: role-specific task, ability, and meeting state.
- `Roles.json`: role catalog embedded into the Android mod.

## Host-only compatibility

The Android mod does not require other players to install Polus just to join a lobby. Role assignment and ability validation are performed by the host. Features that require custom UI or custom code on remote clients are not advertised as vanilla-compatible. Each role mechanic must use game-native behavior to affect an unmodified client; a host-side status message alone is not a replicated in-game effect.

## Build

The Android workflow builds the runtime and patcher, launches the patcher in an Android emulator, captures its UI, and uploads the screenshot and build artifacts.

The project is independent of the upstream MiraAPI framework.
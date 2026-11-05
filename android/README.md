# Polus Android

Polus includes an Android ARM64 patcher app and a separate LemonLoader-based Among Us plugin.

## Android patcher

1. Install the official Among Us Android package (`com.innersloth.spacemafia`).
2. Open Polus on an ARM64 Android device.
3. Select **Patch installed Among Us**.
4. Polus copies the installed base and split APKs into its private cache, downloads the LemonLoader runtime payload, and runs the vendored patch pipeline.
5. Review the generated APK parts in the patcher. The original installation is not silently removed.
6. If you choose **Replace Among Us**, Android displays its own uninstall confirmation and then the install confirmation for the patched package.

The patcher does not use Shizuku. Replacing a Play Store-signed app with a differently signed build requires uninstalling the original first, which can remove local game data. Back up important data before proceeding.

## In-game Android plugin

The Android plugin targets LemonLoader/MelonLoader on Android ARM64 Unity IL2CPP. When the plugin loads, the game displays a **Vanilla / Polus Modded** mode selector. The Modded mode enables the Polus host panel, role actions, meeting hooks, task events, and host-observed vote events.

The Android MiraAPI compatibility surface provides:

- Role, action-button, and meeting-button registries.
- Role options and spawn-weight settings.
- Plugin registration through `IMiraPlugin` and `MiraApi.RegisterPlugin`.
- Match, role, task, meeting, vote, ability, and game-end events.
- Modifier, game-end-condition, compatibility, color, asset, keybind, and local-settings registries.
- Host-side role assignment and ability validation.

### Host-only behavior

This Android implementation intentionally does not add a custom client RPC protocol. The host can track Polus roles and execute host-side actions, but vanilla clients do not receive custom role UI, role state, or role-specific effects. Vote hooks observe votes; they do not rewrite the game's vote result.

This is an Android-native compatibility API, not a drop-in port of every upstream MiraAPI/BepInEx/Reactor feature. Upstream game patches, custom HUD rendering, cross-client role synchronization, and full custom win-condition integration still require Android-specific implementations.

## Build and screenshot

The `.github/workflows/android.yml` workflow builds the Android plugin and patcher APK, starts an Android emulator, launches the patcher, captures its UI as `polus-android-patcher.png`, and uploads the screenshot as the `Polus-Android-Patcher-Screenshot` artifact. The APK and plugin assemblies are uploaded separately.

## Compatibility

- Android ARM64 devices.
- Unity IL2CPP game builds only.
- The exact Among Us version and APK split layout must match the generated interop data and LemonLoader runtime.
- A successful patch does not guarantee that every game release is compatible.

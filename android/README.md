# Polus Android

Polus now contains a .NET Android host that references the vendored LemonLoader patch engine in `android/LemonLoader.Core`.

## Patch flow

1. Install the official Among Us Android package (`com.innersloth.spacemafia`).
2. Open Polus on an ARM64 Android device.
3. Press **Patch installed Among Us**.
4. Polus copies the installed base/split APKs into its private cache, downloads LemonLoader runtime data, and calls LemonLoader's `Patcher.Run()` pipeline directly.
5. The patched APK parts are written to Polus's private cache under `cache/polus-lemonloader/output`.

Polus does not open the separate LemonLoader Installer app and does not use Shizuku.

## Important installation limitation

The LemonLoader patch engine generates its own signing certificate. Android will not accept that output as an in-place update over the Play Store-signed Among Us package. This implementation deliberately leaves the original game installed and does not silently replace it. A user-approved uninstall/reinstall flow is still required to install the newly signed package and may affect game data/OBB handling.

## Current limitations

- The engine is called directly by the Android host; a user-facing export/install destination is not implemented yet.
- Split APK layouts need device testing. LemonLoader's core patch pipeline signs the base and ARM64 library split; additional configuration splits are copied but require signing/validation before installation.
- The Vanilla / Polus Modded in-game selector and Android Unity plugin are not implemented yet. The Windows BepInEx plugin is not Android-compatible.
- A successful patch does not guarantee compatibility with every Among Us release.

## Build

GitHub Actions builds the .NET Android app and uploads the APK as `Polus-Android-LemonLoader`.

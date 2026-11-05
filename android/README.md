# Polus Android

Polus Android is the Android setup and patcher front end. It targets the official Among Us package already installed on the device.

## Current app behavior

- Detects the installed Among Us package (`com.innersloth.spacemafia`).
- Inspects the base APK and split APKs for the expected ARM64 Unity IL2CPP files:
  - `lib/arm64-v8a/libil2cpp.so`
  - `lib/arm64-v8a/libmain.so`
  - `assets/bin/Data/Managed/Metadata/global-metadata.dat`
- Does not require Shizuku.
- Setup/preflight does not modify or uninstall the game.

## LemonLoader integration status

The current LemonLoader Patcher is a desktop CLI/GUI that generates game-specific Interop, injects a Loader release into an APK, and optionally aligns/signs the result. It is not an Android library that can be called directly from this Kotlin app. The Android-focused LemonLoader installer is a separate .NET MAUI application with its own Android package install flow.

The app currently performs only a compatibility preflight. It does not yet contain the LemonLoader patch engine, so the Patch button remains disabled rather than pretending to patch the installed game.

## In-game Vanilla / Modded selector

The requested mode selector must be implemented inside the Unity game through an Android-compatible Polus plugin loaded by LemonLoader:

- Vanilla: do not initialize Polus gameplay features or Polus role modules.
- Modded: initialize the Polus Android plugin and its supported features.

This is not the same as a launcher screen. The existing Windows plugin targets BepInEx IL2CPP x64 and depends on Windows-targeted game libraries, so it cannot be copied into the Android build unchanged. An Android plugin port and a game-version-matched Interop build are required.

## Important install constraint

LemonLoader Patcher does not change the target package identity. Replacing the installed app requires Android to accept the APK signing identity. The older on-device installer flow may uninstall/reinstall the target and warns that app data can be lost. Polus must not run that flow without a tested backup and restore path for Among Us data.

## Build

The workflow `.github/workflows/android.yml` builds a debug APK and uploads it as `Polus-Android-debug`.

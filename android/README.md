# Polus Android

Polus Android is the setup front end for the official Among Us package already installed on the device.

## Current behavior

- Detects Among Us (`com.innersloth.spacemafia`).
- Inspects the base APK and split APKs for the expected ARM64 Unity IL2CPP files:
  - `lib/arm64-v8a/libil2cpp.so`
  - `lib/arm64-v8a/libmain.so`
  - `assets/bin/Data/Managed/Metadata/global-metadata.dat`
- Detects the separate LemonLoader Installer app (`com.lavagang.melonloader.installer`).
- Opens the installed LemonLoader Installer directly from Polus.
- If the installer is not installed, opens the LemonLoader installer source repository.
- Does not require Shizuku.

## LemonLoader handoff

Polus now has a real handoff to the Android LemonLoader Installer. The installer is a separate application maintained by the LemonLoader project; Polus does not bundle or reimplement its patch engine. Select Among Us inside that installer and follow its own patch flow.

Official source: https://github.com/LemonLoader/MelonLoaderInstaller

Important: the documented on-device installer flow can remove the target app's data during patching. Back up anything important first. Polus does not claim that the current installer flow preserves Among Us save data or OBB files. The compatibility check in Polus is only a basic APK-layout check and does not guarantee that a particular Among Us release is supported.

## In-game Vanilla / Modded selector

The requested selector still needs a separate Android-compatible Unity plugin built for the exact Among Us game version and loaded by LemonLoader:

- Vanilla: start without Polus gameplay features.
- Polus Modded: initialize the Android Polus plugin and supported features.

The existing Windows plugin targets BepInEx IL2CPP x64 and depends on Windows-targeted game libraries. It cannot be copied into the Android build unchanged.

## Build

The workflow `.github/workflows/android.yml` builds a debug APK and uploads it as `Polus-Android-debug`.

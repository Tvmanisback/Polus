# Polus Android

Polus Android is the Android launcher/setup front end for the Polus project.

## Current Android app

- Detects the installed Among Us package (`com.innersloth.spacemafia`).
- Displays the installed game version and APK path.
- Launches the installed game in vanilla mode.
- Integrates the Shizuku API and requests permission through Shizuku's normal authorization prompt.
- Does not uninstall, replace, or modify the installed Among Us app during setup.

## Patching status

The Android UI is not yet a complete game patcher. The next required integration is the LemonLoader Android patch pipeline plus game-specific IL2CPP Interop generated for the exact installed Among Us build. The Android Polus plugin must also be ported from the Windows BepInEx IL2CPP x64 plugin.

The LemonLoader Android workflow documents ARM64 Unity IL2CPP support, but requires matching game-specific Interop and an external APK patch/sign/install stage. Its documentation also warns that replacing an installed APK can affect app data. Polus must back up required data and verify a restore path before enabling an install operation.

Shizuku is integrated as a permission API only. Polus does not run arbitrary shell commands or claim to have copied OBB files. OBB handling must be implemented as a checked backup/copy operation before any patch can be reported successful.

## Build

The workflow `.github/workflows/android.yml` builds a debug APK and uploads it as `Polus-Android-debug`.

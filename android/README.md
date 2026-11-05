# Polus Android

Polus Android is a companion app for checking whether a device can run the Android version of Among Us.

## Setup compatibility checks

- Requires Android 11 (API 30) or newer.
- Checks that the official Among Us Android package (`com.innersloth.spacemafia`) is installed.
- Checks that Android can launch Among Us.
- Does not require Google sign-in, Play Games Services, a Google Cloud project, or a Play Console account.
- Does not modify or replace the original Among Us installation.

## Android patching status

Android patching is not implemented yet. The existing Polus plugin targets Windows BepInEx IL2CPP x64. Those Windows assemblies are not Android-compatible and cannot be copied into the Android game.

A real Android patch requires an Android-compatible Polus plugin and a compatible, supported Android loading/integration method. The app keeps the Patch control disabled until that payload exists; it will not report a fake successful patch.

## Build

The workflow `.github/workflows/android.yml` builds a debug APK and uploads it as the `Polus-Android-debug` artifact. Run it from the repository's Actions tab, or push a change under `android/`.

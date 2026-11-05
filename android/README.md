# Polus Android companion

This is a separate Android companion app. It does not replace or modify the official Among Us installation.

## Current features

- Google sign-in using Android Credential Manager.
- Setup checks that the official Among Us Android package is installed.
- Open launches the separately installed Among Us app.
- Patch is intentionally disabled until an Android-compatible Polus plugin and patch implementation exist.

## Google sign-in configuration

1. Create or select a project in Google Cloud Console.
2. Configure the Google Identity / OAuth consent screen.
3. Create an **Android OAuth client** for package name:
   `com.tvmanisback.poluscompanion`
4. Add the SHA-1 signing certificate fingerprint for the build you will distribute.
5. Create a **Web application OAuth client** in the same project.
6. Replace `YOUR_WEB_OAUTH_CLIENT_ID.apps.googleusercontent.com` in `app/src/main/java/com/tvmanisback/poluscompanion/MainActivity.kt` with that Web client ID.

The Web client ID is public application configuration; never put a client secret in the Android app. A production service should validate the returned Google ID token on a trusted backend before treating the account as authenticated for server-side actions.

## Google Play account limitation

The app can ask the user to choose a Google account through Google sign-in. The user should select the same account they use in Play Store. Android does not provide a normal app with a way to take over Play Store's active session or force a separately installed game to use the selected account. Google sign-in also does not prove that the user owns Among Us.

## Build

The workflow `.github/workflows/android.yml` builds a debug APK and uploads it as the `Polus-Android-debug` artifact. Run it from the repository's Actions tab, or push a change under `android/`.

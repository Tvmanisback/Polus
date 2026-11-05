package com.tvmanisback.poluscompanion

import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import java.io.File
import java.util.zip.ZipFile

private const val AMONG_US_PACKAGE = "com.innersloth.spacemafia"
private const val LEMONLOADER_INSTALLER_PACKAGE = "com.lavagang.melonloader.installer"
private const val LEMONLOADER_SOURCE_URL = "https://github.com/LemonLoader/MelonLoaderInstaller"

data class GameInstall(
    val installed: Boolean,
    val version: String,
    val apkPaths: List<String>,
    val isArm64: Boolean,
    val hasIl2Cpp: Boolean,
    val hasUnityMain: Boolean,
    val hasMetadata: Boolean,
    val message: String
) {
    val loaderPreflightPassed: Boolean
        get() = installed && isArm64 && hasIl2Cpp && hasUnityMain && hasMetadata
}

class MainActivity : ComponentActivity() {
    private var gameInstall by mutableStateOf<GameInstall?>(null)
    private var lemonLoaderInstalled by mutableStateOf(false)
    private var statusMessage by mutableStateOf("Run setup to inspect the installed Among Us build.")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        refreshLemonLoaderStatus()
        setContent {
            MaterialTheme {
                PolusScreen(
                    gameInstall = gameInstall,
                    lemonLoaderInstalled = lemonLoaderInstalled,
                    statusMessage = statusMessage,
                    onSetup = { runSetup() },
                    onPatchCheck = { runPatchPreflight() },
                    onOpenLemonLoader = { openLemonLoader() }
                )
            }
        }
    }

    override fun onResume() {
        super.onResume()
        refreshLemonLoaderStatus()
    }

    private fun refreshLemonLoaderStatus() {
        lemonLoaderInstalled = try {
            packageManager.getPackageInfo(LEMONLOADER_INSTALLER_PACKAGE, 0)
            true
        } catch (_: PackageManager.NameNotFoundException) {
            false
        }
    }

    private fun openLemonLoader() {
        val launchIntent = packageManager.getLaunchIntentForPackage(LEMONLOADER_INSTALLER_PACKAGE)
        if (launchIntent != null) {
            startActivity(launchIntent)
            statusMessage = "Opened LemonLoader Installer. Select Among Us there and follow its patch prompts."
        } else {
            statusMessage = "LemonLoader Installer is not installed. Opening its source repository."
            startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(LEMONLOADER_SOURCE_URL)))
        }
    }

    private fun runSetup() {
        gameInstall = inspectAmongUs()
        refreshLemonLoaderStatus()
        statusMessage = when {
            Build.VERSION.SDK_INT < Build.VERSION_CODES.O ->
                "Android 8.0 or newer is required by the current LemonLoader Android runtime profile."
            gameInstall?.installed != true ->
                "Among Us was not found. Install the official game, then run setup again."
            else ->
                "Among Us detected. Polus inspected the installed APK; it has not modified the game."
        }
    }

    private fun runPatchPreflight() {
        val game = inspectAmongUs()
        gameInstall = game
        statusMessage = when {
            !game.installed -> "Among Us is not installed."
            !game.isArm64 -> "This installation/device does not expose the required ARM64 ABI."
            !game.hasIl2Cpp -> "The installed APK set does not contain libil2cpp.so in the expected ARM64 location."
            !game.hasUnityMain -> "The installed APK set does not contain libmain.so in the expected ARM64 location."
            !game.hasMetadata -> "Unity global-metadata.dat was not found in the inspected APK set."
            else -> "Basic LemonLoader prerequisites detected. The external installer still performs game-specific patching; this check does not guarantee Among Us compatibility."
        }
    }

    private fun inspectAmongUs(): GameInstall {
        return try {
            val appInfo = packageManager.getApplicationInfo(AMONG_US_PACKAGE, 0)
            val packageInfo = packageManager.getPackageInfo(AMONG_US_PACKAGE, 0)
            val paths = buildList {
                add(appInfo.sourceDir)
                appInfo.splitSourceDirs?.forEach { add(it) }
            }.distinct()

            var il2cpp = false
            var unityMain = false
            var metadata = false

            for (path in paths) {
                if (!File(path).isFile) continue
                try {
                    ZipFile(path).use { apk ->
                        il2cpp = il2cpp || apk.getEntry("lib/arm64-v8a/libil2cpp.so") != null
                        unityMain = unityMain || apk.getEntry("lib/arm64-v8a/libmain.so") != null
                        metadata = metadata || apk.getEntry("assets/bin/Data/Managed/Metadata/global-metadata.dat") != null
                    }
                } catch (_: Exception) {
                    // Continue checking remaining APK splits.
                }
            }

            val arm64 = Build.SUPPORTED_ABIS.any { it == "arm64-v8a" }
            GameInstall(
                installed = true,
                version = packageInfo.versionName ?: "Unknown",
                apkPaths = paths,
                isArm64 = arm64,
                hasIl2Cpp = il2cpp,
                hasUnityMain = unityMain,
                hasMetadata = metadata,
                message = "Among Us is installed."
            )
        } catch (_: PackageManager.NameNotFoundException) {
            GameInstall(false, "—", emptyList(), false, false, false, false, "Among Us is not installed.")
        }
    }
}

@Composable
private fun PolusScreen(
    gameInstall: GameInstall?,
    lemonLoaderInstalled: Boolean,
    statusMessage: String,
    onSetup: () -> Unit,
    onPatchCheck: () -> Unit,
    onOpenLemonLoader: () -> Unit
) {
    Surface(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(22.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Text("POLUS", style = MaterialTheme.typography.displaySmall)
            Text("Android setup & patcher", style = MaterialTheme.typography.titleMedium)
            Text("Prepare the installed Among Us build and hand off patching to LemonLoader.")
            HorizontalDivider()

            Text("1. Detect Among Us", style = MaterialTheme.typography.titleLarge)
            Button(onClick = onSetup, modifier = Modifier.fillMaxWidth()) {
                Text("Run setup")
            }
            gameInstall?.let { game ->
                Text(if (game.installed) "Among Us ${game.version} detected" else game.message)
                if (game.installed) {
                    Text("APK parts: ${game.apkPaths.size}", style = MaterialTheme.typography.bodySmall)
                    Text("ARM64 device: ${yesNo(game.isArm64)}")
                    Text("libil2cpp.so: ${yesNo(game.hasIl2Cpp)}")
                    Text("libmain.so: ${yesNo(game.hasUnityMain)}")
                    Text("global-metadata.dat: ${yesNo(game.hasMetadata)}")
                }
            }
            Text(statusMessage, color = MaterialTheme.colorScheme.secondary)

            HorizontalDivider()
            Text("2. LemonLoader", style = MaterialTheme.typography.titleLarge)
            Text(
                if (lemonLoaderInstalled)
                    "LemonLoader Installer is installed. Open it to select Among Us and run its patch process."
                else
                    "LemonLoader Installer is not detected. Open the project source to get the installer, then return here."
            )
            Button(onClick = onOpenLemonLoader, modifier = Modifier.fillMaxWidth()) {
                Text(if (lemonLoaderInstalled) "Open LemonLoader Installer" else "Get LemonLoader Installer")
            }
            Text(
                "Patching is handled by the official separate installer, not by Polus itself. Its documented on-device flow may remove Among Us app data. Back up anything important first; do not assume OBB or save data will be preserved.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error
            )

            HorizontalDivider()
            Text("3. Compatibility check", style = MaterialTheme.typography.titleLarge)
            Text("Checks the installed APK set for an ARM64 Unity IL2CPP layout. Passing does not guarantee that this specific Among Us build is supported.")
            OutlinedButton(
                onClick = onPatchCheck,
                enabled = gameInstall?.installed == true,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Check Among Us requirements")
            }

            HorizontalDivider()
            Text("4. Polus in-game mode selector", style = MaterialTheme.typography.titleLarge)
            Text("The Vanilla / Polus Modded selector is not implemented yet. It requires a separate Android-compatible Unity plugin built against the exact Among Us game version and loaded by LemonLoader.")
        }
    }
}

private fun yesNo(value: Boolean) = if (value) "Found" else "Not found"

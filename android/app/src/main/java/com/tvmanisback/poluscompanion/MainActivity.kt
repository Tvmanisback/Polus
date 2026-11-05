package com.tvmanisback.poluscompanion

import android.content.Intent
import android.content.pm.PackageManager
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
    private var statusMessage by mutableStateOf("Run setup to inspect the installed Among Us build.")

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                PolusScreen(
                    gameInstall = gameInstall,
                    statusMessage = statusMessage,
                    onSetup = { runSetup() },
                    onPatchCheck = { runPatchPreflight() }
                )
            }
        }
    }

    private fun runSetup() {
        gameInstall = inspectAmongUs()
        statusMessage = when {
            Build.VERSION.SDK_INT < Build.VERSION_CODES.O ->
                "Android 8.0 or newer is required by the current LemonLoader Android runtime profile."
            gameInstall?.installed != true ->
                "Among Us was not found. Install the official game, then run setup again."
            else ->
                "Among Us detected. Setup only inspected the installed APK; no game files were changed."
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
            else -> "Basic LemonLoader prerequisites detected. This is not a completed patch: game-specific Interop generation and APK injection/signing are still required."
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
                    // A split may not be a readable ZIP on some vendor builds; continue checking the remaining APKs.
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
    statusMessage: String,
    onSetup: () -> Unit,
    onPatchCheck: () -> Unit
) {
    Surface(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(22.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Text("POLUS", style = MaterialTheme.typography.displaySmall)
            Text("Android setup & patcher", style = MaterialTheme.typography.titleMedium)
            Text("Inspect the installed Among Us build and prepare it for the Android Polus loader.")
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
            Text("2. LemonLoader compatibility", style = MaterialTheme.typography.titleLarge)
            Text("Checks the installed APK set for the Android ARM64 Unity IL2CPP layout used by LemonLoader. Passing these checks does not prove the game is compatible; matching Interop assemblies and a successful test launch are still required.")
            Button(
                onClick = onPatchCheck,
                enabled = gameInstall?.installed == true,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Check patch requirements")
            }

            HorizontalDivider()
            Text("3. Install Polus", style = MaterialTheme.typography.titleLarge)
            Text("The Android LemonLoader patch pipeline and Android Polus game plugin are not integrated yet. Polus will not replace the installed game or show a success message until the patch, signature-preserving install, and first launch are verified.")
            OutlinedButton(
                onClick = {},
                enabled = false,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Patch installed Among Us — integration required")
            }

            HorizontalDivider()
            Text("4. In-game mode selector", style = MaterialTheme.typography.titleLarge)
            Text("The planned selector belongs inside Among Us: Vanilla starts without Polus gameplay features; Modded loads the Polus Android plugin. It must be implemented as a Unity-side plugin, not as a separate launcher screen.")
        }
    }
}

private fun yesNo(value: Boolean) = if (value) "Found" else "Not found"

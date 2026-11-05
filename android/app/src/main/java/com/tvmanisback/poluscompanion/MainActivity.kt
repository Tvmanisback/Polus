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
import rikka.shizuku.Shizuku

private const val AMONG_US_PACKAGE = "com.innersloth.spacemafia"
private const val SHIZUKU_REQUEST_CODE = 7401

data class GameInstall(
    val installed: Boolean,
    val version: String,
    val apkPath: String,
    val message: String
)

class MainActivity : ComponentActivity() {
    private var shizukuStatus by mutableStateOf("Shizuku has not been checked.")
    private var shizukuGranted by mutableStateOf(false)
    private var gameInstall by mutableStateOf<GameInstall?>(null)
    private var statusMessage by mutableStateOf("Run setup to inspect Among Us.")

    private val permissionListener = Shizuku.OnRequestPermissionResultListener { requestCode, grantResult ->
        if (requestCode == SHIZUKU_REQUEST_CODE) {
            shizukuGranted = grantResult == PackageManager.PERMISSION_GRANTED
            shizukuStatus = if (shizukuGranted) "Shizuku permission granted." else "Shizuku permission was not granted."
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        Shizuku.addRequestPermissionResultListener(permissionListener)
        refreshShizukuStatus()
        setContent {
            MaterialTheme {
                PolusScreen(
                    gameInstall = gameInstall,
                    shizukuStatus = shizukuStatus,
                    shizukuGranted = shizukuGranted,
                    statusMessage = statusMessage,
                    onSetup = { runSetup() },
                    onRequestShizuku = { requestShizukuPermission() },
                    onOpenVanilla = { openAmongUs() },
                    onOpenShizuku = { openShizukuManager() },
                    onPatch = { statusMessage = "Patch engine is not installed in this build yet. No game files have been changed." }
                )
            }
        }
    }

    override fun onDestroy() {
        Shizuku.removeRequestPermissionResultListener(permissionListener)
        super.onDestroy()
    }

    private fun runSetup() {
        gameInstall = inspectAmongUs()
        refreshShizukuStatus()
        statusMessage = when {
            Build.VERSION.SDK_INT < Build.VERSION_CODES.O -> "This Android version is too old for the Android LemonLoader runtime."
            !gameInstall!!.installed -> "Among Us was not found. Install the official game, then run setup again."
            else -> "Among Us detected. Original installation has not been modified."
        }
    }

    private fun inspectAmongUs(): GameInstall {
        return try {
            val info = packageManager.getApplicationInfo(AMONG_US_PACKAGE, 0)
            val packageInfo = packageManager.getPackageInfo(AMONG_US_PACKAGE, 0)
            GameInstall(true, packageInfo.versionName ?: "Unknown", info.sourceDir ?: "Unavailable", "Among Us is installed.")
        } catch (_: PackageManager.NameNotFoundException) {
            GameInstall(false, "—", "—", "Among Us is not installed.")
        }
    }

    private fun refreshShizukuStatus() {
        val connected = try { Shizuku.pingBinder() } catch (_: Exception) { false }
        shizukuGranted = try {
            connected && Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED
        } catch (_: Exception) { false }
        shizukuStatus = when {
            !connected -> "Shizuku service is not running."
            shizukuGranted -> "Shizuku is connected and permission is granted."
            else -> "Shizuku is running, but Polus has not been granted permission."
        }
    }

    private fun requestShizukuPermission() {
        if (!Shizuku.pingBinder()) {
            shizukuStatus = "Start Shizuku first, then return here and try again."
            return
        }
        if (Shizuku.checkSelfPermission() == PackageManager.PERMISSION_GRANTED) {
            shizukuGranted = true
            shizukuStatus = "Shizuku permission is already granted."
        } else {
            Shizuku.requestPermission(SHIZUKU_REQUEST_CODE)
        }
    }

    private fun openShizukuManager() {
        val intent = packageManager.getLaunchIntentForPackage("moe.shizuku.privileged.api")
        if (intent != null) startActivity(intent)
        else statusMessage = "Install Shizuku from its official source, start its service, then return to Polus."
    }

    private fun openAmongUs() {
        val intent = packageManager.getLaunchIntentForPackage(AMONG_US_PACKAGE)
        if (intent == null) {
            statusMessage = "Among Us is not installed or cannot be launched."
            return
        }
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        startActivity(intent)
    }
}

@Composable
private fun PolusScreen(
    gameInstall: GameInstall?,
    shizukuStatus: String,
    shizukuGranted: Boolean,
    statusMessage: String,
    onSetup: () -> Unit,
    onRequestShizuku: () -> Unit,
    onOpenVanilla: () -> Unit,
    onOpenShizuku: () -> Unit,
    onPatch: () -> Unit
) {
    Surface(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(22.dp),
            verticalArrangement = Arrangement.spacedBy(14.dp)
        ) {
            Text("POLUS", style = MaterialTheme.typography.displaySmall)
            Text("Android launcher & patcher", style = MaterialTheme.typography.titleMedium)
            Text("Set up the Android game, manage the mod loader, and choose how to launch Among Us.")
            HorizontalDivider()

            Text("1. Setup", style = MaterialTheme.typography.titleLarge)
            Text("Polus checks the installed Among Us package and records its APK location. Setup does not replace or uninstall the game.")
            Button(onClick = onSetup, modifier = Modifier.fillMaxWidth()) { Text("Run setup") }
            gameInstall?.let {
                Text(if (it.installed) "Among Us ${it.version} detected" else it.message)
                if (it.installed) Text("APK: ${it.apkPath}", style = MaterialTheme.typography.bodySmall)
            }
            Text(statusMessage, color = MaterialTheme.colorScheme.secondary)

            HorizontalDivider()
            Text("2. Shizuku access", style = MaterialTheme.typography.titleLarge)
            Text("Shizuku can provide user-authorized ADB-level access for protected file operations. Polus only requests permission; it does not silently grant itself access.")
            Text(shizukuStatus, color = if (shizukuGranted) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.secondary)
            OutlinedButton(onClick = onOpenShizuku, modifier = Modifier.fillMaxWidth()) { Text("Open Shizuku") }
            Button(onClick = onRequestShizuku, modifier = Modifier.fillMaxWidth()) {
                Text(if (shizukuGranted) "Refresh Shizuku permission" else "Authorize Polus in Shizuku")
            }

            HorizontalDivider()
            Text("3. Patch Among Us", style = MaterialTheme.typography.titleLarge)
            Text("The Android LemonLoader runtime is the intended loader. Its patcher needs game-specific IL2CPP Interop generated for the exact installed game build. That patching engine and Polus Android plugin are not bundled yet.")
            Button(onClick = onPatch, enabled = gameInstall?.installed == true, modifier = Modifier.fillMaxWidth()) {
                Text("Check patch requirements")
            }

            HorizontalDivider()
            Text("4. Play", style = MaterialTheme.typography.titleLarge)
            Text("Vanilla starts the installed game normally. Modded launch will become available after the Android loader and Polus plugin are successfully installed and verified.")
            Button(onClick = onOpenVanilla, enabled = gameInstall?.installed == true, modifier = Modifier.fillMaxWidth()) {
                Text("Play Vanilla Among Us")
            }
            OutlinedButton(onClick = { }, enabled = false, modifier = Modifier.fillMaxWidth()) {
                Text("Play Polus Modded — patch engine required")
            }
        }
    }
}

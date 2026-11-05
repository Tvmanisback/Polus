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

private const val AMONG_US_PACKAGE = "com.innersloth.spacemafia"
private const val ANDROID_11_API = Build.VERSION_CODES.R

data class CompatibilityResult(
    val compatible: Boolean,
    val message: String,
    val amongUsInstalled: Boolean
)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                PolusScreen(
                    checkCompatibility = { checkCompatibility() },
                    openAmongUs = { openAmongUs() }
                )
            }
        }
    }

    private fun checkCompatibility(): CompatibilityResult {
        if (Build.VERSION.SDK_INT < ANDROID_11_API) {
            return CompatibilityResult(
                false,
                "Not compatible: Polus Android requires Android 11 or newer. This device is running Android ${Build.VERSION.RELEASE}.",
                false
            )
        }

        val amongUsInstalled = try {
            packageManager.getPackageInfo(AMONG_US_PACKAGE, 0)
            true
        } catch (_: PackageManager.NameNotFoundException) {
            false
        }

        if (!amongUsInstalled) {
            return CompatibilityResult(
                false,
                "Android version supported, but the official Among Us app was not found. Install it from Google Play and run Setup again.",
                false
            )
        }

        val launchIntent = packageManager.getLaunchIntentForPackage(AMONG_US_PACKAGE)
        if (launchIntent == null) {
            return CompatibilityResult(
                false,
                "Among Us is installed, but Android did not provide a launchable activity for it.",
                true
            )
        }

        return CompatibilityResult(
            true,
            "Compatible device. Android 11+ is available and Among Us is installed.",
            true
        )
    }

    private fun openAmongUs() {
        packageManager.getLaunchIntentForPackage(AMONG_US_PACKAGE)?.let {
            it.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
            startActivity(it)
        }
    }
}

@Composable
private fun PolusScreen(
    checkCompatibility: () -> CompatibilityResult,
    openAmongUs: () -> Unit
) {
    var result by remember { mutableStateOf<CompatibilityResult?>(null) }

    Surface(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text("POLUS", style = MaterialTheme.typography.displaySmall)
            Text("Among Us Android companion", style = MaterialTheme.typography.titleMedium)
            HorizontalDivider()

            Text("1. Setup", style = MaterialTheme.typography.titleLarge)
            Text("Checks Android 11 or newer, verifies that Among Us is installed, and confirms that Android can launch it. Setup does not sign in, modify the original game, or require a Google Cloud project.")
            Button(
                modifier = Modifier.fillMaxWidth(),
                onClick = { result = checkCompatibility() }
            ) { Text("Check compatibility") }

            result?.let {
                Text(
                    it.message,
                    color = if (it.compatible) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.error
                )
            } ?: Text("Compatibility has not been checked yet.")

            HorizontalDivider()
            Text("2. Patch", style = MaterialTheme.typography.titleLarge)
            Text("Android patching is not available yet. This repository currently contains a Windows BepInEx IL2CPP plugin, which cannot run inside the Android version of Among Us. An Android-compatible plugin and loader integration are required before this button can safely install anything.")
            Button(
                onClick = {},
                enabled = false,
                modifier = Modifier.fillMaxWidth()
            ) { Text("Patch — Android plugin required") }

            HorizontalDivider()
            Text("3. Open Among Us", style = MaterialTheme.typography.titleLarge)
            Text("Launches the official Among Us app without changing its existing account or installation.")
            Button(
                enabled = result?.compatible == true,
                onClick = openAmongUs,
                modifier = Modifier.fillMaxWidth()
            ) { Text("Open Among Us") }
        }
    }
}

package com.tvmanisback.poluscompanion

import android.content.Intent
import android.content.pm.PackageManager
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
import com.google.android.gms.games.PlayGames
import com.google.android.gms.games.PlayGamesSdk

private const val AMONG_US_PACKAGE = "com.innersloth.spacemafia"

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        PlayGamesSdk.initialize(this)
        setContent {
            MaterialTheme {
                PolusScreen(
                    onCheckPlayGames = { callback -> checkPlayGamesSignIn(callback) },
                    isAmongUsInstalled = { isAmongUsInstalled() },
                    onOpenAmongUs = { openAmongUs() }
                )
            }
        }
    }

    private fun checkPlayGamesSignIn(callback: (Boolean, String?) -> Unit) {
        PlayGames.getGamesSignInClient(this)
            .isAuthenticated
            .addOnCompleteListener { task ->
                if (task.isSuccessful && task.result.isAuthenticated) {
                    callback(true, null)
                } else {
                    callback(
                        false,
                        task.exception?.localizedMessage
                            ?: "Play Games did not authenticate this app. The app may need Play Games Services configuration in Google Play Console."
                    )
                }
            }
    }

    private fun isAmongUsInstalled(): Boolean = try {
        packageManager.getPackageInfo(AMONG_US_PACKAGE, 0)
        true
    } catch (_: PackageManager.NameNotFoundException) {
        false
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
    onCheckPlayGames: ((Boolean, String?) -> Unit) -> Unit,
    isAmongUsInstalled: () -> Boolean,
    onOpenAmongUs: () -> Unit
) {
    var playGamesStatus by remember { mutableStateOf<String?>(null) }
    var error by remember { mutableStateOf<String?>(null) }
    var busy by remember { mutableStateOf(false) }
    var setupComplete by remember { mutableStateOf(false) }
    var installed by remember { mutableStateOf(isAmongUsInstalled()) }

    Surface(Modifier.fillMaxSize()) {
        Column(
            Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Text("POLUS", style = MaterialTheme.typography.displaySmall)
            Text("Android companion", style = MaterialTheme.typography.titleMedium)
            HorizontalDivider()

            Text("1. Google Play Games", style = MaterialTheme.typography.titleLarge)
            Text("Polus checks the Play Games profile already active on this device using Google Play Games Services.")
            Button(
                enabled = !busy,
                modifier = Modifier.fillMaxWidth(),
                onClick = {
                    busy = true
                    error = null
                    onCheckPlayGames { authenticated, message ->
                        playGamesStatus = if (authenticated) "Play Games profile authenticated" else null
                        error = message
                        busy = false
                    }
                }
            ) { Text(if (busy) "Checking…" else "Check Play Games account") }
            playGamesStatus?.let { Text(it) }
            error?.let { Text(it, color = MaterialTheme.colorScheme.error) }

            HorizontalDivider()
            Text("2. Setup", style = MaterialTheme.typography.titleLarge)
            Text("Checks for the official Among Us Android app. The original installation is not modified.")
            Button(
                enabled = !busy,
                modifier = Modifier.fillMaxWidth(),
                onClick = {
                    installed = isAmongUsInstalled()
                    setupComplete = installed
                }
            ) { Text("Setup") }
            Text(
                if (setupComplete) "Setup complete — Among Us was found."
                else if (installed) "Among Us is installed. Press Setup to check it."
                else "Among Us was not found. Install the official game from Google Play first."
            )

            HorizontalDivider()
            Text("3. Patch", style = MaterialTheme.typography.titleLarge)
            Text("The Android Polus game plugin and patch payload are not implemented yet. Patching is disabled instead of pretending it succeeded.")
            Button(onClick = {}, enabled = false, modifier = Modifier.fillMaxWidth()) {
                Text("Patch — Android build not ready")
            }

            HorizontalDivider()
            Text("4. Open", style = MaterialTheme.typography.titleLarge)
            Text("Opens the separately installed official Among Us app. Its own account sign-in remains managed by Among Us.")
            Button(
                enabled = installed,
                onClick = onOpenAmongUs,
                modifier = Modifier.fillMaxWidth()
            ) { Text("Open Among Us") }

            Text(
                "Play Games authentication identifies the profile for Polus. It does not transfer that profile into Among Us or sign the game in.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

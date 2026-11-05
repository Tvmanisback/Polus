package com.tvmanisback.poluscompanion

import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.lifecycle.lifecycleScope
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.credentials.Credential
import androidx.credentials.CredentialManager
import androidx.credentials.CustomCredential
import androidx.credentials.GetCredentialRequest
import com.google.android.libraries.identity.googleid.GetGoogleIdOption
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential
import kotlinx.coroutines.launch

private const val AMONG_US_PACKAGE = "com.innersloth.spacemafia"

// Replace with the Web OAuth client ID from the same Google Cloud project.
private const val GOOGLE_WEB_CLIENT_ID = "YOUR_WEB_OAUTH_CLIENT_ID.apps.googleusercontent.com"

class MainActivity : ComponentActivity() {
    private val credentialManager by lazy { CredentialManager.create(this) }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            MaterialTheme {
                PolusScreen(
                    onGoogleSignIn = { callback -> signInWithGoogle(callback) },
                    isAmongUsInstalled = { isAmongUsInstalled() },
                    onOpenAmongUs = { openAmongUs() }
                )
            }
        }
    }

    private fun signInWithGoogle(callback: (String?, String?) -> Unit) {
        if (GOOGLE_WEB_CLIENT_ID.startsWith("YOUR_")) {
            callback(null, "Google OAuth client ID has not been configured yet.")
            return
        }
        val option = GetGoogleIdOption.Builder()
            .setServerClientId(GOOGLE_WEB_CLIENT_ID)
            .setFilterByAuthorizedAccounts(false)
            .setAutoSelectEnabled(false)
            .build()
        val request = GetCredentialRequest.Builder().addCredentialOption(option).build()

        lifecycleScope.launch {
            try {
                val result = credentialManager.getCredential(this@MainActivity, request)
                val credential: Credential = result.credential
                if (credential is CustomCredential &&
                    credential.type == GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL
                ) {
                    val google = GoogleIdTokenCredential.createFrom(credential.data)
                    callback(google.id, null)
                } else {
                    callback(null, "Google returned an unsupported credential.")
                }
            } catch (error: Exception) {
                callback(null, error.localizedMessage ?: "Google sign-in failed or was cancelled.")
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
    onGoogleSignIn: ((String?, String?) -> Unit) -> Unit,
    isAmongUsInstalled: () -> Boolean,
    onOpenAmongUs: () -> Unit
) {
    var account by remember { mutableStateOf<String?>(null) }
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

            Text("1. Google Play account", style = MaterialTheme.typography.titleLarge)
            Text("Sign in with the Google account you use for Play Store. This connects your Google identity to Polus; it does not take over the Play Store session.")
            Button(
                enabled = !busy,
                modifier = Modifier.fillMaxWidth(),
                onClick = {
                    busy = true
                    error = null
                    onGoogleSignIn { email, message ->
                        account = email
                        error = message
                        busy = false
                    }
                }
            ) { Text(if (busy) "Signing in…" else if (account == null) "Sign in with Google" else "Switch Google account") }
            account?.let { Text("Google account connected: $it") }
            error?.let { Text(it, color = MaterialTheme.colorScheme.error) }

            HorizontalDivider()
            Text("2. Setup", style = MaterialTheme.typography.titleLarge)
            Text("Checks for the official Among Us Android app. The original installation is not modified.")
            Button(
                enabled = !busy,
                modifier = Modifier.fillMaxWidth(),
                onClick = {
                    if (account == null) {
                        busy = true
                        error = null
                        onGoogleSignIn { email, message ->
                            account = email
                            error = message
                            busy = false
                            if (email != null) {
                                installed = isAmongUsInstalled()
                                setupComplete = installed
                            }
                        }
                    } else {
                        installed = isAmongUsInstalled()
                        setupComplete = installed
                    }
                }
            ) { Text(if (busy) "Setting up…" else "Setup") }
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
            Text("Opens the separately installed official Among Us app. It does not sign the game into the Google account selected above.")
            Button(
                enabled = installed,
                onClick = onOpenAmongUs,
                modifier = Modifier.fillMaxWidth()
            ) { Text("Open Among Us") }

            Text(
                "Google sign-in identifies the account selected in Polus. It cannot prove ownership of Among Us or force the game to use that account.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
    }
}

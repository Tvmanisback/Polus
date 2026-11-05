# Polus browser terminal

Polus is configured for GitHub Codespaces. Open the repository on GitHub, select **Code → Codespaces → Create codespace on main**. Codespaces opens VS Code in your browser with a real Linux terminal.

The environment includes .NET SDKs 6, 8, and 9 plus Java 17.

## First commands

    dotnet --list-sdks
    java -version
    git status

## Important networking limitation

Codespaces port forwarding supports **TCP**, not UDP. Among Us uses an HTTP matchmaking service and a separate UDP gameplay service. This means Codespaces is suitable for editing and compiling Polus, but a server running there cannot directly accept an iOS game's UDP packets.

For a real iOS device test, run the server on a computer on the same Wi-Fi as the iOS device. Follow [the local iOS server guide](../ios/server/README.md). The HTTPS tunnel in that guide exposes matchmaking only; UDP gameplay stays on the local network.

## Free usage

- Codespaces is a development terminal, not permanent hosting.
- Personal GitHub Free accounts have a limited monthly Codespaces allowance. Check the current quota in GitHub Settings → Billing and plans.
- Do not enable paid overages just to keep a test server running.
- Codespaces may stop when idle. A server running inside it is available only while the Codespace is active.

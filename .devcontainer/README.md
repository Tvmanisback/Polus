# Polus browser terminal

Polus is configured for GitHub Codespaces. Open the repository on GitHub, select **Code → Codespaces → Create codespace on main**. Codespaces opens VS Code in your browser with a real Linux terminal.

The environment includes .NET SDKs 6, 8, and 9 plus Java 17. Ports 22023 and 22024 are reserved for the future iOS game server and status endpoint.

## First commands

```bash
dotnet --list-sdks
java -version
git status
```

## Important

- Codespaces is a development/test terminal, not permanent hosting.
- Personal GitHub Free accounts currently include 120 core-hours and 15 GB-month of Codespaces storage. A 2-core machine uses two core-hours per hour.
- Set a zero spending limit / do not enable paid overages. If the free allowance runs out, wait for the monthly reset.
- Codespaces may stop when idle. A server running inside it is available only while the Codespace is active.
- The Polus iOS server implementation is still in development. This terminal setup does not mean the game server is already implemented or iOS-compatible.

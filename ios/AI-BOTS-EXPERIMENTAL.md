# Polus AI Bots — isolated experiment

This branch is a **separate build experiment**. It does not change the official Impostor server currently running on Altare.

## Build it

1. Open the [Experimental AI Bots Server workflow](../../actions/workflows/experimental-ai-bots.yml).
2. Choose **Run workflow** on branch `experimental/ai-bots`.
3. Wait for the test and publish steps to finish.
4. Download the `Polus-AI-Bots-Experimental-linux-x64` artifact.

The build uses the upstream AI-bot implementation at the pinned GPL-3.0 revision listed in the workflow. It runs the upstream automated tests and publishes a separate Linux server package. The bots use the built-in heuristic brain by default; no OpenRouter key is required.

## Compatibility status

The upstream project documents Among Us 18.0 support. The current Polus iPad client is Among Us 19.0.1. The package enables Impostor's `AllowFutureGameVersions` setting, but that is only a compatibility bypass and **does not prove** the 19.0.1 protocol still works. Real-client testing is still required.

## Do not deploy to the live server

The current Altare allocation is already used by the official Impostor process. This artifact is not a plugin and cannot be loaded as `plugins/Polus.ServerPlugin.dll`. It is a separate server executable that needs its own test host and UDP allocation. Do not overwrite the live root files, the root `plugins/` folder, the current `start.sh`, or the current Caddy setup.

Only after a separate-host test succeeds should we consider porting the bot implementation to the exact Among Us 19.0.1 protocol. No live deployment is performed by this workflow.

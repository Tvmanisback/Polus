# Polus AI Bots — Among Us 19.0+ port

This experiment ports the bot subsystem onto the **official Impostor 1.10.9 source**, rather than building the older Impostor fork used by the bot project.

The official 1.10.9 compatibility manager includes Among Us 19.0 PC and mobile entries. The included configuration also enables `AllowFutureGameVersions` for later 19.0.x hotfix builds. This is a version-check bypass, not a guarantee against future protocol changes.

## Build

1. Open [Experimental AI Bots Server (Among Us 19.0+)](https://github.com/Tvmanisback/Polus/actions/workflows/experimental-ai-bots.yml).
2. Choose **Run workflow** on branch `experimental/ai-bots`.
3. The workflow overlays the bot subsystem onto official Impostor 1.10.9, runs the upstream simulation tests, builds the patched server, and publishes a Linux artifact.
4. Download `Polus-AI-Bots-19.0plus-linux-x64` from the completed workflow run.

## Use

In a normal lobby hosted on the experimental server, type:

```text
!bots 5
```

Other commands: `!bots status` and `!bots off`. The built-in heuristic brain works without an OpenRouter API key.

## Live server protection

This is a **server executable**, not `plugins/Polus.ServerPlugin.dll`. Real simulated players require internal Impostor server integration; a normal plugin cannot provide this with the current plugin API.

The workflow does not deploy anything. It does not change the existing Altare server, root `plugins/` folder, `start.sh`, Caddy configuration, or live allocation. Testing requires a separate host and UDP allocation. Do not overwrite the live official installation.

The first real-client test must use the current Among Us 19.0.1 iPad client. The upstream bot author states their own real-client integration was not tested, so passing compilation and simulation tests is not enough to claim the iPad test succeeded.

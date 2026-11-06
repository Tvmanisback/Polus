# Polus AI Bots — Among Us 19.0+ port

This experiment ports the AI bot subsystem onto the **official Impostor 1.10.9 source**. It does not build the older Impostor fork as the server base.

The official 1.10.9 compatibility manager includes Among Us 19.0 PC and mobile entries. The included configuration also enables `AllowFutureGameVersions` for later 19.0.x hotfix builds. The vendored bot source is pinned to the GPL-3.0 revision recorded in `ios/ai-bots/vendor-*.json`; its license is included at `ios/ai-bots/LICENSE`.

## Build

1. Open [Experimental AI Bots Server (Among Us 19.0+)](https://github.com/Tvmanisback/Polus/actions/workflows/experimental-ai-bots.yml).
2. Choose **Run workflow** on branch `experimental/ai-bots`.
3. The workflow checks out official Impostor 1.10.9, applies the bot integration and protocol-compatibility patches, then builds and publishes a Linux server artifact.
4. Download `Polus-AI-Bots-19.0plus-linux-x64` from the completed workflow run.

The workflow builds from vendored source in this repository; it does not need access to the upstream bot author's private repository.

## Use

In a normal lobby hosted on the experimental server, type:

```text
!bots 5
```

Other commands: `!bots status` and `!bots off`. The built-in heuristic brain works without an OpenRouter API key.

## Compatibility and limitations

- The server base is official Impostor 1.10.9, which contains the 19.0 PC and mobile compatibility entries.
- The port also preserves unknown role-option payloads and adds the host meeting-close signals used by the bots.
- `AllowFutureGameVersions` is enabled for 19.0.x hotfix versions; it cannot compensate for a protocol change.
- The upstream bot author says the bot project has not been tested with a real game client. The first real-client test must use the current 19.0.1 iPad client.
- The bots have heuristic brains by default, walk maps, do tasks, report, and participate in meetings. Some advanced mechanics remain unsupported upstream.

## Live server protection

This is a **server executable**, not `plugins/Polus.ServerPlugin.dll`. Real simulated players require internal Impostor server integration; the current plugin API cannot provide actual moving/playing fake clients by itself.

The workflow does not deploy anything. It does not change the existing Altare server, root `plugins/` folder, `start.sh`, Caddy configuration, or live allocation. Testing requires a separate host and UDP allocation. Do not overwrite the live official installation.

A successful compile is not proof that the iPad client joins or that bots complete a real match. Those still need to be tested on a separate host.

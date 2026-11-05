# Polus iOS

Polus iOS is a separate, vanilla-client-compatible server project. It does **not** install the PC/Android role plugin and does not add custom roles to iOS. Its gameplay is based on server-controlled gamemodes.

## Current status

**Experimental — server not yet deployable.**

The first target is a custom region named **Polus iOS**. The region entry will point to the Polus matchmaking/game service once the service is implemented and its public endpoint has been verified. Do not install the template region file yet: it intentionally contains a placeholder hostname.

## Design

- Keep the existing Windows and Android role systems unchanged.
- iOS clients connect through a custom region entry.
- The server owns mode state, timers, player assignments, and win conditions.
- Use only game messages and fields that an unmodified Among Us client already understands. No custom iOS buttons or client-side UI are assumed.
- Start with Hot Potato as the protocol proof-of-concept, then consider Infection and Hide & Seek after vanilla-client testing.

## Folders

- `config/regionInfo.template.json` — documented region-file shape; placeholder only.
- `modes/README.md` — mode requirements and compatibility boundaries.
- `server/README.md` — server and hosting requirements.

## Browser terminal for development

The repository includes a GitHub Codespaces environment. Open the Polus repository, choose **Code → Codespaces → Create codespace on main**, and wait for the browser editor and terminal to load.

The environment provides .NET 6/8/9 and Java 17. It is intended for compiling, running tests, and launching a development server while the Codespace is active. See [the Codespaces setup notes](../.devcontainer/README.md).

Codespaces is not permanent hosting: it can stop when idle and the free personal-account quota is limited. Do not enable paid overages just to keep a test server running.

## Free hosting target

The preferred target is a free service that can keep the required HTTPS/WebSocket endpoint available. GitHub Pages can host static setup files, but it cannot run the live game server. GitHub Actions is build automation, not a 24/7 server.

Cloudflare Workers has a free tier and supports WebSocket upgrades, but its free tier is not a drop-in host for a complete Among Us server. The Polus server still needs a compatible protocol implementation and persistent room state; this must be proven before a region URL is published.

See the [Cloudflare Workers limits](https://developers.cloudflare.com/workers/platform/limits/) and [pricing](https://developers.cloudflare.com/workers/platform/pricing/).

## Release requirements

Before iOS is marked supported:

1. Confirm the current Among Us iOS build accepts the region entry.
2. Verify matchmaking, lobby creation, joining, and round start with unmodified clients.
3. Verify server-controlled mode state survives reconnects and host departure.
4. Test round end, meeting behavior, disconnects, and server restarts.
5. Publish a real region file only after the endpoint is live and tested.

# Polus iOS

Polus iOS is a separate, vanilla-client-compatible server project. It does **not** install the PC/Android role plugin and does not add custom roles to iOS. Its gameplay is intended to be based on server-controlled gamemodes.

## Current status

**Experimental — local server runtime setup is in place; iOS device testing and Polus gamemodes are not yet verified.**

The server uses the upstream Impostor C# implementation for Among Us networking. The pinned v1.10.9 release states support through Among Us 19.0. The Polus region file must point to a real HTTPS matchmaking endpoint, while the game server's UDP address is configured separately.

## Design

- Keep the existing Windows and Android role systems unchanged.
- iOS clients connect through a custom region entry.
- The server owns mode state, timers, player assignments, and win conditions.
- Use only game messages and fields that an unmodified Among Us client already understands. No custom iOS buttons or client-side UI are assumed.
- Start with Hot Potato as the protocol proof-of-concept, then consider Infection and Hide & Seek after vanilla-client testing.

## Folders

- config/regionInfo.template.json — region-file template; replace its placeholder host before use.
- modes/README.md — mode requirements and compatibility boundaries.
- server/README.md — server setup, local iOS testing, and network requirements.

## Free local testing

Follow [the iOS server guide](server/README.md). It downloads the official Impostor v1.10.9 server release, verifies the archive, starts the UDP game service, exposes the matchmaking HTTP endpoint through a temporary HTTPS tunnel, and generates a region file.

The iOS device must be on the same Wi-Fi as the computer running the server. The HTTPS tunnel does not carry UDP gameplay packets.

## Browser terminal

The repository includes a GitHub Codespaces environment. Open the Polus repository, choose **Code → Codespaces → Create codespace on main**, and wait for the browser editor and terminal to load.

Codespaces is useful for editing and compiling. It forwards TCP ports only, while Among Us gameplay requires UDP, so a Codespace is not a playable iOS server host. See [the Codespaces setup notes](../.devcontainer/README.md).

## Hosting

GitHub Pages can host setup files but cannot run the live game server. GitHub Actions is build automation, not persistent hosting. Do not publish a region URL until both the HTTPS matchmaking endpoint and UDP gameplay endpoint are reachable and have been tested with unmodified iOS clients.

## Release requirements

Before iOS is marked supported:

1. Confirm the current Among Us iOS build accepts the region entry.
2. Verify matchmaking, lobby creation, joining, and round start with unmodified clients.
3. Verify server-controlled mode state survives reconnects and host departure.
4. Test round end, meeting behavior, disconnects, and server restarts.
5. Publish a real region file only after the endpoint is live and tested.

# Polus iOS

Polus iOS connects unmodified Among Us clients through a custom HTTPS matchmaking region and the Impostor UDP game server. It does not install the PC/Android role plugin or add custom client buttons.

## Current status

**Experimental.** The server plugin now implements lobby chat commands for selecting three server-side modes. It has not yet been verified against an actual iPhone/iPad game session.

## Server and plugin

The upstream Impostor v1.10.9 runtime handles Among Us matchmaking and game protocol traffic. Polus adds a server plugin built against Impostor.Api 1.10.9.

Start the server with:

    bash ios/server/run-local.sh

This downloads the pinned server runtime, builds Polus.ServerPlugin, installs the plugin DLL, writes config.json, and launches the server.

## Chat commands

The host selects a mode in the Among Us chat:

- /hotpotato — choose Hot Potato.
- /infection — choose Infection.
- /hideandseek — choose Hide & Seek.
- /mode status — show the selected mode.
- /mode off — disable Polus mode and restore the lobby settings.
- /help — show commands.

During a round:
- Hot Potato chooses a random carrier. The carrier can use /pass player when the vanilla chat is available.
- Infection chooses a random patient zero. Infected players can use /infect player when chat is available.
- Hide & Seek marks the impostor as seeker and applies the hide-and-seek lobby settings; normal task/elimination win conditions remain in use.

These commands require the normal Among Us chat to be available. They do not create custom HUD buttons or client-side UI. The first two modes use chat-based passing/infection, not automatic touch detection.

## Region file

The region generator creates a file named exactly **region.json**:

    bash ios/server/make-region.sh https://YOUR-PUBLIC-HTTPS-MATCHMAKING-HOST

By default it writes to ios/config/region.json. The HTTPS URL must point to the Impostor HTTP matchmaking service. The game server's UDP address is set separately in the server config.

The website also has a region.json generator in its iOS download card. It can share the file through the iOS share sheet when supported. A website cannot write directly into Among Us's private app storage, and there is no verified Among Us iOS deep-link contract here that can silently import the file and launch the game. If Among Us appears as a share target, select it; otherwise use the region import method supported by your device.

## Codespaces testing

Open the repository in Codespaces, then run:

    bash ios/server/test-codespace.sh

This smoke test builds the plugin, starts Impostor, checks that the plugin loaded, and checks that the TCP matchmaking listener responds.

**Codespaces is not a full Among Us gameplay test host.** Its forwarded port can expose the HTTP matchmaking service over TCP, but Among Us game traffic uses UDP. A full iPhone test needs a host where the UDP game port is reachable from the iPhone. For a same-Wi-Fi test, use a computer on the same network as the iPhone and follow [the server guide](server/README.md).

## Compatibility checklist

Before marking iOS supported, verify region import, matchmaking, lobby creation, round start, mode commands, reconnects, round end, and disconnect cleanup on the current iOS release.

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
- /copsandrobbers — choose Cops & Robbers.
- /randomchaos — randomize lobby settings for the round.
- /classic — restore standard vanilla settings.
- /mode status — show the selected mode.
- /mode off — disable Polus mode and restore the lobby settings.
- /help — show commands.

During a round:
- Hot Potato chooses a random carrier. The carrier can use /pass player when the vanilla chat is available.
- Infection chooses a random patient zero. Infected players can use /infect player when chat is available.
- Hide & Seek marks the impostor as seeker and applies the hide-and-seek lobby settings; normal task/elimination win conditions remain in use.
- Cops & Robbers marks the impostor as cop. The cop can use /arrest player; arrested robbers can use /escape. Vanilla clients cannot be forced into a movement lock, so arrest is a visible role marker.
- Random Chaos rolls a new combination of impostor count, kill cooldown, emergency meetings, vision, and task counts each round.
- Classic restores the original lobby settings and applies no Polus mode modifiers.

These commands require the normal Among Us chat to be available. They do not create custom HUD buttons or client-side UI. Hot Potato and Infection use chat-based passing/infection, not automatic touch detection. Cops & Robbers uses chat-based arrest/escape markers; vanilla clients do not expose server-side movement locking.

## Region file

## iOS website launcher

The main website's iOS card uses Among Us's supported mobile deep link:

    amongus://init?serverip=YOUR_PUBLIC_IPV4&serverport=22023&servername=Polus_iOS&usedtls=false

Enter the server's public IPv4 address and port on the main Polus website, then tap **Open Among Us**. iOS opens Among Us and adds the Polus iOS custom region. The deep link requires a numeric IP address (not a DNS hostname) and custom servers must use `usedtls=false`.

For a same-Wi-Fi test, enter the server computer's LAN IPv4 address. For internet play, the server must have a reachable public IPv4 address and both TCP matchmaking and UDP game traffic must be forwarded on the configured port. The website cannot make a private or temporary CI runner publicly reachable.

The manual `make-region.sh` script remains available for alternate region-file testing; it writes a file named exactly `region.json` to `ios/config/region.json` by default.

## Codespaces testing

Open the repository in Codespaces, then run:

    bash ios/server/test-codespace.sh

This smoke test builds the plugin, starts Impostor, checks that the plugin loaded, and checks that the TCP matchmaking listener responds.

**Codespaces is not a full Among Us gameplay test host.** Its forwarded port can expose the HTTP matchmaking service over TCP, but Among Us game traffic uses UDP. A full iPhone test needs a host where the UDP game port is reachable from the iPhone. For a same-Wi-Fi test, use a computer on the same network as the iPhone and follow [the server guide](server/README.md).

## Compatibility checklist

Before marking iOS supported, verify region import, matchmaking, lobby creation, round start, mode commands, reconnects, round end, and disconnect cleanup on the current iOS release.

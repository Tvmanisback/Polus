# Polus iOS server

Polus uses the upstream [Impostor](https://github.com/Impostor/Impostor) runtime for the Among Us networking protocol. The Polus plugin adds the iOS mode chat commands.

**Pinned runtime:** Impostor v1.10.9. Its release states compatibility through Among Us 19.0; confirm the exact installed iOS build during device testing.

## Run on a local computer

The computer running the server and the iPhone/iPad must be on the same Wi-Fi network. Install .NET SDK 8, curl, Python 3, and cloudflared.

From the Polus repository root:

    bash ios/server/run-local.sh

The script downloads and checksum-verifies the upstream server, builds ios/Polus.ServerPlugin, copies Polus.ServerPlugin.dll into the runtime plugin directory, writes the server config, and starts Impostor.

If automatic LAN-IP detection is wrong, set POLUS_UDP_HOST to the computer's Wi-Fi IPv4 address:

    POLUS_UDP_HOST=192.168.1.25 bash ios/server/run-local.sh

The server binds both its HTTP matchmaking listener and UDP game listener to all network interfaces on port 22023. Allow both TCP 22023 (matchmaking) and UDP 22023 (game traffic) through the computer firewall.

On the iPhone/iPad, open the main Polus website, enter the server computer's LAN IPv4 address, and tap **Open Among Us**. The website uses the game's `amongus://init` deep link to add the custom region. The deep link requires an IP address rather than a DNS hostname and sets `usedtls=false`.

The optional `run-https-tunnel.sh` and `make-region.sh` scripts remain for separate HTTPS/region-file experiments; a TCP-only tunnel does not carry UDP gameplay traffic and is not a substitute for forwarding the UDP game port.

## Codespaces smoke test

In the Polus Codespace, run:

    bash ios/server/test-codespace.sh

The test builds the Polus plugin, starts the Impostor server, verifies the plugin's startup log, and checks that the HTTP listener responds on TCP port 22023. Set the Codespaces forwarded port 22023 visibility to Public only if you need to access that HTTP test endpoint from another device.

This is a server/plugin smoke test, **not** a playable iOS test. Codespaces port forwarding does not forward the UDP game traffic required by Among Us. Do not advertise a Codespaces URL as a playable Polus region.

## Chat commands

The host selects the mode from normal Among Us chat:

- /hotpotato
- /infection
- /hideandseek
- /mode status
- /mode off
- /help

Round commands:
- /pass player — current Hot Potato carrier passes the marker to a player.
- /infect player — an infected player adds a living player to the infected team.

Commands only work when the vanilla client makes chat available. Hot Potato and Infection are command-driven in this first version; no touch-detection mechanic or custom client UI is claimed.

## Networking facts

- Among Us matchmaking uses Impostor's HTTP service.
- Recent Among Us versions require HTTPS in front of that HTTP service.
- Gameplay traffic uses a separate UDP listener on port 22023.
- The HTTPS tunnel in run-https-tunnel.sh is for matchmaking only.
- A Codespaces forwarded TCP port cannot substitute for the UDP gameplay listener.

## Files

- config.template.json — Impostor runtime configuration.
- download-impostor.sh — pinned upstream runtime download with SHA-256 verification.
- run-local.sh — builds the plugin and launches the server.
- run-https-tunnel.sh — temporary HTTPS tunnel for matchmaking.
- make-region.sh — generates ios/config/region.json.
- test-codespace.sh — Codespaces server/plugin smoke test.
- ../Polus.ServerPlugin — Polus mode command plugin.

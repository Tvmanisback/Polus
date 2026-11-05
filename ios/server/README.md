# Polus iOS server

Polus uses the upstream [Impostor](https://github.com/Impostor/Impostor) server runtime for the actual Among Us protocol. Polus does not reimplement the game's networking layer.

**Experimental:** the server runtime can now be downloaded and launched for local testing. The Polus gamemode plugin and public hosting are not finished yet.

## Compatibility

The pinned upstream runtime is **Impostor v1.10.9**, which states support for Among Us 2024.6.18 through 19.0. Confirm the exact iOS build against the server before treating it as supported.

See [NOTICE.md](NOTICE.md) for upstream licensing and download details.

## Local iOS test (same Wi-Fi)

This is the free testing route. The computer running the server and the iPhone/iPad must be on the same Wi-Fi network. The computer's LAN IPv4 address is used for UDP game traffic. A temporary HTTPS tunnel exposes only the matchmaking HTTP endpoint.

1. Use a computer on the same Wi-Fi as the iOS device. Install the .NET 8 runtime, curl, python3, and cloudflared.
2. From the Polus repository root, run:

       bash ios/server/run-local.sh

   The script downloads the pinned official Impostor release, checks its SHA-256 digest, writes a local config, and starts the server. If automatic LAN-IP detection is wrong, set POLUS_UDP_HOST to the computer's Wi-Fi IPv4 address before running it.
3. In a second terminal, run:

       bash ios/server/run-https-tunnel.sh

   Copy the temporary https://...trycloudflare.com address printed by cloudflared.
4. In a third terminal, generate the region file:

       bash ios/server/make-region.sh https://YOUR-NAME.trycloudflare.com

5. Import that generated regionInfo.json into Among Us on the iOS device using the iOS region-import flow. Select **Polus iOS** in the region selector.
6. Create a private lobby and join it from a second device on the same Wi-Fi to test the full game connection.

Keep both the server and HTTPS tunnel terminals running. A quick tunnel URL changes when restarted, so regenerate the region file after restarting the tunnel.

### Important network limits

- The HTTPS tunnel handles matchmaking only. It does not tunnel the game's UDP gameplay packets.
- This local test only works when the iOS device can reach the server computer's LAN IP over the same network.
- GitHub Codespaces forwards TCP ports only. Its forwarded port cannot carry the UDP gameplay traffic, so a Codespace can compile the project but cannot directly host a playable iOS session.
- This is not 24/7 hosting. Closing the server computer or stopping either process ends the test server.
- To let players outside the local network join, the UDP game port must be reachable from the internet. Do not publish a region file until that has been tested.

## Server files

- config.template.json — baseline configuration for the upstream server.
- download-impostor.sh — downloads and verifies the pinned upstream release.
- run-local.sh — configures the LAN UDP address and starts the server.
- run-https-tunnel.sh — exposes the HTTP matchmaking endpoint over temporary HTTPS.
- make-region.sh — generates a region file from the HTTPS matchmaking URL.

## Gamemodes

The upstream server provides the compatible game networking and lobby lifecycle. Polus-specific Hot Potato, Infection, and Hide & Seek rules are still a separate implementation task; the server being able to host a vanilla lobby does not mean those modes are implemented.

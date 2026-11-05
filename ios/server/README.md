# Polus iOS server

This folder is reserved for the server implementation. No live server is included yet.

## Required capabilities

- Current Among Us matchmaking and game protocol compatibility.
- HTTPS/WebSocket transport compatible with the iOS client's custom-region implementation.
- Authoritative lobby and round state.
- Player join/leave handling and safe cleanup after disconnects.
- Server-side mode state, timers, assignment, and win checks.
- Health/status endpoint for monitoring.

## Hosting constraints

A static website is not a game server. GitHub Pages can distribute a region file and setup instructions, but cannot accept live game sessions. GitHub Actions can compile and test code, but workflow runners are temporary and must not be treated as a persistent public server.

A free Cloudflare Worker can accept HTTP requests and WebSocket upgrades within its published limits. That does not automatically make an existing Among Us server implementation compatible with the Workers runtime. Before choosing it as the production host, the protocol adapter and room-state implementation must be tested on the free plan.

No fake endpoint or placeholder address should be shipped as a working region.

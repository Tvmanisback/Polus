# Polus iOS gamemodes

These modes are designed for unmodified iOS clients. The server must communicate using normal Among Us game state and messages; it cannot assume the client has Polus code installed.

## First proof-of-concept: Hot Potato

- Select one player as the starting carrier.
- Track the carrier and round timer on the server.
- Use only client-visible, protocol-supported state to communicate the current carrier and remaining time.
- Transfer the carrier through a valid in-game interaction.
- End the round using a normal supported game-end flow.
- Handle disconnects and the carrier leaving the lobby.

**Important:** changing player names or sending chat text may be useful for status display, but those behaviors alone do not implement tagging, game rules, or a reliable win condition. They must be tested against the current iOS build.

## Later candidates

### Infection
The server tracks infected and uninfected players, applies valid game-state transitions, and checks the infection win condition.

### Hide & Seek
The server tracks seeker selection, round timing, and the supported game-end condition.

## Compatibility rule

Do not promise custom buttons, custom HUD elements, custom roles, or client-only effects to vanilla iOS players. If a mode requires a client patch, it is outside this iOS project.

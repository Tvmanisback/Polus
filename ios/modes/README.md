# Polus iOS gamemodes

These modes are server-side and use only chat and game state available to a vanilla Among Us client. They do not add custom buttons or a custom HUD.

## Commands

Host-only lobby selection:

- /hotpotato — select Hot Potato.
- /infection — select Infection.
- /hideandseek — select Hide & Seek.
- /mode status — show selected mode.
- /mode off — disable Polus mode and restore captured lobby settings.
- /help — list commands.

Round commands:

- /pass player — current Hot Potato carrier passes the marker to a living player.
- /infect player — an infected player adds a living player to the infected team.

## Mode behavior

### Hot Potato

A random player receives the potato marker when the round starts. The current carrier can pass it with /pass player when the normal Among Us chat is available. This first implementation is chat-driven; it does not detect physical touches or automatically eliminate a carrier on a timer.

### Infection

A random player starts as patient zero. Infected players can use /infect player while chat is available. The server tracks infected players and announces when every living player has been infected. This first implementation is command-driven, not touch-based.

### Hide & Seek

The server marks the impostor as seeker and applies a hide-and-seek-oriented lobby preset: one impostor, no emergency meetings, reduced crew vision, increased seeker vision, and task settings. Normal vanilla task/elimination win conditions are used.

## Compatibility boundary

The modes are not a replacement for a full client-side mod. Chat commands only work while the vanilla client permits chat. No custom buttons, automatic proximity tagging, role UI, or special revive mechanics are claimed.

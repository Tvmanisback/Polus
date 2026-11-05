# Polus iOS gamemodes

Polus iOS supports six server-managed modes using vanilla Among Us clients. The modes use standard lobby settings, player names, and normal chat commands; they do not add custom client buttons or a custom HUD.

## Host-only selection commands

- `/classic` — restore the lobby's original settings and play standard Among Us.
- `/hotpotato` — start Hot Potato.
- `/infection` — start Infection.
- `/hideandseek` — start Hide & Seek.
- `/copsandrobbers` — start Cops & Robbers.
- `/randomchaos` — roll randomized lobby settings for the round.
- `/mode status` — show the selected mode.
- `/mode off` — disable Polus mode and restore captured lobby settings.
- `/help` — list commands.

The same modes can be selected with `/mode classic`, `/mode hotpotato`, `/mode infection`, `/mode hideandseek`, `/mode copsandrobbers`, or `/mode randomchaos`.

## Round commands

- `/pass player` — current Hot Potato carrier passes the marker to a living player.
- `/infect player` — an infected player adds a living player to the infected team.
- `/arrest player` — the Cops & Robbers cop marks a robber as arrested.
- `/escape` — an arrested robber clears their arrest marker.

## Mode behavior

### Classic

Restores the original lobby settings captured when Polus mode selection began. No Polus role markers or mode-specific settings are applied.

### Hot Potato

A random player receives the potato marker when the round starts. The current carrier can pass it with `/pass player` when normal Among Us chat is available. This is chat-driven; it does not detect physical touches or automatically eliminate a carrier on a timer.

### Infection

A random player starts as patient zero. Infected players can use `/infect player` while chat is available. The server tracks infected players and announces when every living player has been infected. This is command-driven, not touch-based.

### Hide & Seek

The server marks the impostor as seeker and applies a hide-and-seek lobby preset: one impostor, no emergency meetings, reduced crew vision, increased seeker vision, and task settings. Normal vanilla task/elimination win conditions are used.

### Cops & Robbers

The impostor is marked as the cop; the other players are robbers. The cop can use `/arrest player` to mark a robber as arrested, and an arrested robber can use `/escape` to clear that marker. The preset disables emergency meetings and adjusts tasks. Vanilla clients do not provide a server-enforced movement lock, so arrest is a role/status marker rather than physically restraining a player.

### Random Chaos

At the start of each round, the server rolls a random combination of impostor count, kill cooldown, emergency meetings and cooldown, crew/impostor vision, and task counts. The settings are synchronized to the vanilla clients.

## Compatibility boundary

Chat commands only work while the vanilla client permits chat. No custom buttons, automatic proximity tagging, role UI, special revive mechanics, or server-enforced movement locking are claimed. These are server-side vanilla-client modes, not a replacement for a full client-side mod.

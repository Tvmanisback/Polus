using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Managers;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;

namespace Polus.ServerPlugin;

[ImpostorPlugin("tvmanisback.polus.ios")]
public sealed class PolusServerPlugin : PluginBase
{
    private readonly IEventManager _events;
    private readonly ILogger<PolusServerPlugin> _logger;
    private IDisposable? _listener;

    public PolusServerPlugin(IEventManager events, ILogger<PolusServerPlugin> logger)
    {
        _events = events;
        _logger = logger;
    }

    public override ValueTask EnableAsync()
    {
        _listener = _events.RegisterListener(new ModeCommandListener(_logger));
        _logger.LogInformation("Polus iOS server plugin enabled. Chat commands: /help");
        return default;
    }

    public override ValueTask DisableAsync()
    {
        _listener?.Dispose();
        _listener = null;
        _logger.LogInformation("Polus iOS server plugin disabled.");
        return default;
    }
}

internal enum PolusMode
{
    None,
    HotPotato,
    Infection,
    HideAndSeek,
    CopsAndRobbers,
    RandomChaos,
    Classic
}

internal sealed class ModeSession
{
    public PolusMode Mode { get; set; }
    public byte? CarrierId { get; set; }
    public byte? SeekerId { get; set; }
    public byte? CopId { get; set; }
    public ConcurrentDictionary<byte, string> OriginalNames { get; } = new();
    public ConcurrentDictionary<byte, byte> InfectedIds { get; } = new();
    public ConcurrentDictionary<byte, byte> ArrestedIds { get; } = new();
    public GameSettingsSnapshot? OriginalSettings { get; set; }
}

internal sealed record GameSettingsSnapshot(
    int NumImpostors,
    int NumEmergencyMeetings,
    int EmergencyCooldown,
    float KillCooldown,
    float CrewLightMod,
    float ImpostorLightMod,
    int NumCommonTasks,
    int NumLongTasks,
    int NumShortTasks)
{
    public static GameSettingsSnapshot Capture(NormalGameOptions options) => new(
        options.NumImpostors,
        options.NumEmergencyMeetings,
        options.EmergencyCooldown,
        options.KillCooldown,
        options.CrewLightMod,
        options.ImpostorLightMod,
        options.NumCommonTasks,
        options.NumLongTasks,
        options.NumShortTasks);

    public void Restore(NormalGameOptions options)
    {
        options.NumImpostors = NumImpostors;
        options.NumEmergencyMeetings = NumEmergencyMeetings;
        options.EmergencyCooldown = EmergencyCooldown;
        options.KillCooldown = KillCooldown;
        options.CrewLightMod = CrewLightMod;
        options.ImpostorLightMod = ImpostorLightMod;
        options.NumCommonTasks = NumCommonTasks;
        options.NumLongTasks = NumLongTasks;
        options.NumShortTasks = NumShortTasks;
    }
}

internal sealed class ModeCommandListener : IEventListener
{
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IGame, ModeSession> _sessions = new();

    public ModeCommandListener(ILogger logger) => _logger = logger;

    [EventListener]
    public async ValueTask OnPlayerChat(IPlayerChatEvent e)
    {
        var message = e.Message.Trim();
        if (!message.StartsWith("/", StringComparison.Ordinal))
        {
            return;
        }

        e.IsCancelled = true;
        var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var argument = parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : string.Empty;
        var session = _sessions.GetOrAdd(e.Game, _ => new ModeSession());

        if (command is "/help" or "/polus")
        {
            await Reply(e.PlayerControl, "Modes: /classic, /hotpotato, /infection, /hideandseek, /copsandrobbers, /randomchaos. Host selects. Round commands: /pass <player>, /infect <player>, /arrest <player>, /escape. Also /mode status and /mode off.");
            return;
        }

        if (command is "/classic" or "/hotpotato" or "/infection" or "/hideandseek" or "/copsandrobbers" or "/randomchaos" || command == "/mode")
        {
            if (!e.ClientPlayer.IsHost)
            {
                await Reply(e.PlayerControl, "Only the lobby host can select or stop a Polus mode.");
                return;
            }

            if (command == "/mode" && argument.Equals("status", StringComparison.OrdinalIgnoreCase))
            {
                await Reply(e.PlayerControl, $"Current Polus mode: {DisplayMode(session.Mode)}. Mode changes apply when the next round starts.");
                return;
            }

            if (command == "/mode" && argument.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                await RestoreNames(e.Game, session);
                RestoreSettings(e.Game, session);
                session.Mode = PolusMode.None;
                session.CarrierId = null;
                session.SeekerId = null;
                session.CopId = null;
                session.InfectedIds.Clear();
                session.ArrestedIds.Clear();
                await e.Game.SyncSettingsAsync();
                await Broadcast(e.Game, "Polus mode disabled. Original lobby settings restored.");
                return;
            }

            var selected = command switch
            {
                "/classic" => PolusMode.Classic,
                "/hotpotato" => PolusMode.HotPotato,
                "/infection" => PolusMode.Infection,
                "/hideandseek" => PolusMode.HideAndSeek,
                "/copsandrobbers" => PolusMode.CopsAndRobbers,
                "/randomchaos" => PolusMode.RandomChaos,
                "/mode" when argument.Equals("classic", StringComparison.OrdinalIgnoreCase) => PolusMode.Classic,
                "/mode" when argument.Equals("hotpotato", StringComparison.OrdinalIgnoreCase) => PolusMode.HotPotato,
                "/mode" when argument.Equals("infection", StringComparison.OrdinalIgnoreCase) => PolusMode.Infection,
                "/mode" when argument.Equals("hideandseek", StringComparison.OrdinalIgnoreCase) => PolusMode.HideAndSeek,
                "/mode" when argument.Equals("copsandrobbers", StringComparison.OrdinalIgnoreCase) => PolusMode.CopsAndRobbers,
                "/mode" when argument.Equals("randomchaos", StringComparison.OrdinalIgnoreCase) => PolusMode.RandomChaos,
                _ => PolusMode.None
            };

            if (selected == PolusMode.None)
            {
                await Reply(e.PlayerControl, "Use /mode status, /mode off, or /mode classic|hotpotato|infection|hideandseek|copsandrobbers|randomchaos.");
                return;
            }

            if (session.OriginalSettings is null && e.Game.Options is NormalGameOptions original)
            {
                session.OriginalSettings = GameSettingsSnapshot.Capture(original);
            }

            await RestoreNames(e.Game, session);
            session.Mode = selected;
            session.CarrierId = null;
            session.SeekerId = null;
            session.CopId = null;
            session.InfectedIds.Clear();
            session.ArrestedIds.Clear();
            ApplyLobbyPreset(e.Game, selected, session);
            await e.Game.SyncSettingsAsync();
            await Broadcast(e.Game, selected == PolusMode.Classic
                ? "Classic selected. Vanilla lobby settings restored; no Polus modifiers will be applied."
                : $"Polus mode selected: {DisplayMode(selected)}. Start the round to activate it. Use /mode status to check it.");
            return;
        }

        if (command == "/pass")
        {
            if (session.Mode != PolusMode.HotPotato || session.CarrierId != e.PlayerControl.PlayerId)
            {
                await Reply(e.PlayerControl, "Only the current Hot Potato carrier can use /pass <player>.");
                return;
            }

            var target = FindPlayer(e.Game, argument, session);
            if (target?.PlayerInfo is null || target.PlayerInfo.IsDead || target.PlayerId == e.PlayerControl.PlayerId)
            {
                await Reply(e.PlayerControl, "Player not found, already out, or invalid. Use /pass <part of their name>.");
                return;
            }

            session.CarrierId = target.PlayerId;
            await ApplyNameMarkers(e.Game, session);
            await Broadcast(e.Game, $"🥔 {BaseName(target, session)} has the potato. Commands work when Among Us chat is available.");
            return;
        }

        if (command == "/arrest")
        {
            if (session.Mode != PolusMode.CopsAndRobbers || session.CopId != e.PlayerControl.PlayerId)
            {
                await Reply(e.PlayerControl, "Only the cop can use /arrest <player>.");
                return;
            }

            var target = FindPlayer(e.Game, argument, session);
            if (target?.PlayerInfo is null || target.PlayerInfo.IsDead || target.PlayerId == session.CopId || session.ArrestedIds.ContainsKey(target.PlayerId))
            {
                await Reply(e.PlayerControl, "Player not found, already arrested, or invalid.");
                return;
            }

            session.ArrestedIds.TryAdd(target.PlayerId, target.PlayerId);
            await ApplyNameMarkers(e.Game, session);
            await Broadcast(e.Game, $"🚓 {BaseName(target, session)} has been marked arrested. This is a chat/role marker; vanilla clients do not support a server-enforced movement lock.");
            return;
        }

        if (command == "/escape")
        {
            if (session.Mode != PolusMode.CopsAndRobbers || session.CopId == e.PlayerControl.PlayerId ||
                !session.ArrestedIds.TryRemove(e.PlayerControl.PlayerId, out _))
            {
                await Reply(e.PlayerControl, "Only an arrested robber can use /escape in Cops & Robbers.");
                return;
            }

            await ApplyNameMarkers(e.Game, session);
            await Broadcast(e.Game, $"🏃 {BaseName(e.PlayerControl, session)} escaped the arrest marker.");
            return;
        }

        if (command == "/infect")
        {
            if (session.Mode != PolusMode.Infection || !session.InfectedIds.ContainsKey(e.PlayerControl.PlayerId))
            {
                await Reply(e.PlayerControl, "Only an infected player can use /infect <player>.");
                return;
            }

            var target = FindPlayer(e.Game, argument, session);
            if (target?.PlayerInfo is null || target.PlayerInfo.IsDead || session.InfectedIds.ContainsKey(target.PlayerId))
            {
                await Reply(e.PlayerControl, "Player not found, already infected, or not alive.");
                return;
            }

            session.InfectedIds.TryAdd(target.PlayerId, target.PlayerId);
            await ApplyNameMarkers(e.Game, session);
            await Broadcast(e.Game, $"☣ {BaseName(target, session)} has joined the infected team.");
            var living = e.Game.Players.Select(p => p.Character).Where(p => p?.PlayerInfo is { IsDead: false }).ToArray();
            if (living.Length > 0 && living.All(p => p is not null && session.InfectedIds.ContainsKey(p.PlayerId)))
            {
                await Broadcast(e.Game, "☣ Infection objective complete: every living player is infected.");
            }

            return;
        }

        await Reply(e.PlayerControl, "Unknown Polus command. Use /help.");
    }

    [EventListener]
    public async ValueTask OnGameStarted(IGameStartedEvent e)
    {
        if (!_sessions.TryGetValue(e.Game, out var session) || session.Mode == PolusMode.None)
        {
            return;
        }

        var players = e.Game.Players
            .Select(p => p.Character)
            .Where(p => p?.PlayerInfo is { IsDead: false })
            .Cast<IInnerPlayerControl>()
            .ToArray();

        if (players.Length == 0)
        {
            return;
        }

        foreach (var player in players)
        {
            session.OriginalNames.TryAdd(player.PlayerId, player.PlayerInfo!.PlayerName);
        }

        switch (session.Mode)
        {
            case PolusMode.HotPotato:
                session.CarrierId = players[Random.Shared.Next(players.Length)].PlayerId;
                await ApplyNameMarkers(e.Game, session);
                await Broadcast(e.Game, $"🥔 Hot Potato started. {GetName(players, session.CarrierId.Value, session)} has the potato. Pass it with /pass <player> when chat is available.");
                break;

            case PolusMode.Infection:
                var patientZero = players[Random.Shared.Next(players.Length)];
                session.InfectedIds.TryAdd(patientZero.PlayerId, patientZero.PlayerId);
                await ApplyNameMarkers(e.Game, session);
                await Broadcast(e.Game, $"☣ Infection started. {BaseName(patientZero, session)} is patient zero. Infected players can use /infect <player> when chat is available.");
                break;

            case PolusMode.HideAndSeek:
                var seeker = players.FirstOrDefault(p => p.PlayerInfo!.IsImpostor) ?? players[Random.Shared.Next(players.Length)];
                session.SeekerId = seeker.PlayerId;
                await ApplyNameMarkers(e.Game, session);
                await Broadcast(e.Game, $"🔎 Hide & Seek started. {BaseName(seeker, session)} is the seeker. Complete tasks to win.");
                break;

            case PolusMode.CopsAndRobbers:
                var cop = players.FirstOrDefault(p => p.PlayerInfo!.IsImpostor) ?? players[Random.Shared.Next(players.Length)];
                session.CopId = cop.PlayerId;
                await ApplyNameMarkers(e.Game, session);
                await Broadcast(e.Game, $"🚓 Cops & Robbers started. {BaseName(cop, session)} is the cop. The cop uses /arrest <player>; arrested robbers can use /escape. Arrests are chat markers, not movement locks.");
                break;

            case PolusMode.RandomChaos:
                await Broadcast(e.Game, "🎲 Random Chaos started. The lobby settings were randomized for this round.");
                break;

            case PolusMode.Classic:
                await Broadcast(e.Game, "🟦 Classic round started. Standard Among Us rules.");
                break;
        }
    }

    [EventListener]
    public async ValueTask OnGameEnded(IGameEndedEvent e)
    {
        if (_sessions.TryGetValue(e.Game, out var session))
        {
            await RestoreNames(e.Game, session);
            session.CarrierId = null;
            session.SeekerId = null;
            session.CopId = null;
            session.InfectedIds.Clear();
            session.ArrestedIds.Clear();

            // Prepare a fresh Random Chaos roll while the lobby is available,
            // so the next round receives synchronized settings before it starts.
            if (session.Mode == PolusMode.RandomChaos)
            {
                ApplyLobbyPreset(e.Game, PolusMode.RandomChaos, session);
                await e.Game.SyncSettingsAsync();
                await Broadcast(e.Game, "🎲 Random Chaos has rolled new settings for the next round.");
            }
        }
    }

    private static void ApplyLobbyPreset(IGame game, PolusMode mode, ModeSession session)
    {
        if (game.Options is not NormalGameOptions options)
        {
            return;
        }

        if (mode == PolusMode.Classic)
        {
            session.OriginalSettings?.Restore(options);
            session.OriginalSettings = null;
            return;
        }

        // Switching between modes always starts from the captured vanilla settings,
        // so settings from Random Chaos cannot leak into the next selected preset.
        session.OriginalSettings?.Restore(options);
        options.NumImpostors = 1;
        switch (mode)
        {
            case PolusMode.HotPotato:
                options.KillCooldown = 10f;
                options.NumEmergencyMeetings = 1;
                options.EmergencyCooldown = 15;
                break;
            case PolusMode.Infection:
                options.KillCooldown = 10f;
                options.NumEmergencyMeetings = 1;
                options.EmergencyCooldown = 15;
                break;
            case PolusMode.HideAndSeek:
                options.KillCooldown = 10f;
                options.NumEmergencyMeetings = 0;
                options.EmergencyCooldown = 0;
                options.CrewLightMod = 0.25f;
                options.ImpostorLightMod = 2.5f;
                options.NumCommonTasks = 0;
                options.NumLongTasks = 0;
                options.NumShortTasks = 5;
                break;
            case PolusMode.CopsAndRobbers:
                options.KillCooldown = 25f;
                options.NumEmergencyMeetings = 0;
                options.EmergencyCooldown = 0;
                options.NumCommonTasks = 0;
                options.NumLongTasks = 0;
                options.NumShortTasks = 4;
                break;
            case PolusMode.RandomChaos:
                options.NumImpostors = Random.Shared.Next(1, Math.Min(3, Math.Max(1, game.Players.Count())) + 1);
                options.KillCooldown = Random.Shared.Next(10, 46);
                options.NumEmergencyMeetings = Random.Shared.Next(0, 3);
                options.EmergencyCooldown = Random.Shared.Next(0, 31);
                options.CrewLightMod = (float)(Random.Shared.NextDouble() * 1.75 + 0.25);
                options.ImpostorLightMod = (float)(Random.Shared.NextDouble() * 2.5 + 0.5);
                options.NumCommonTasks = Random.Shared.Next(0, 3);
                options.NumLongTasks = Random.Shared.Next(0, 4);
                options.NumShortTasks = Random.Shared.Next(1, 7);
                break;
        }
    }

    private static void RestoreSettings(IGame game, ModeSession session)
    {
        if (game.Options is NormalGameOptions options && session.OriginalSettings is not null)
        {
            session.OriginalSettings.Restore(options);
        }

        session.OriginalSettings = null;
    }

    private static async ValueTask ApplyNameMarkers(IGame game, ModeSession session)
    {
        foreach (var client in game.Players)
        {
            var player = client.Character;
            if (player?.PlayerInfo is null)
            {
                continue;
            }

            var original = session.OriginalNames.GetOrAdd(player.PlayerId, player.PlayerInfo.PlayerName);
            var label = session.Mode switch
            {
                PolusMode.HotPotato when session.CarrierId == player.PlayerId => "🥔 " + original,
                PolusMode.Infection when session.InfectedIds.ContainsKey(player.PlayerId) => "☣ " + original,
                PolusMode.HideAndSeek when session.SeekerId == player.PlayerId => "🔎 " + original,
                PolusMode.CopsAndRobbers when session.CopId == player.PlayerId => "🚓 " + original,
                PolusMode.CopsAndRobbers when session.ArrestedIds.ContainsKey(player.PlayerId) => "🚔 " + original,
                _ => original
            };

            await player.SetNameAsync(label);
        }
    }

    private static async ValueTask RestoreNames(IGame game, ModeSession session)
    {
        foreach (var client in game.Players)
        {
            var player = client.Character;
            if (player?.PlayerInfo is null)
            {
                continue;
            }

            if (session.OriginalNames.TryGetValue(player.PlayerId, out var original))
            {
                await player.SetNameAsync(original);
            }
        }

        session.OriginalNames.Clear();
    }

    private static IInnerPlayerControl? FindPlayer(IGame game, string query, ModeSession session)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        return game.Players
            .Select(p => p.Character)
            .Where(p => p?.PlayerInfo is not null)
            .Cast<IInnerPlayerControl>()
            .FirstOrDefault(p => BaseName(p, session).Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static string BaseName(IInnerPlayerControl player, ModeSession session) =>
        session.OriginalNames.TryGetValue(player.PlayerId, out var original)
            ? original
            : player.PlayerInfo?.PlayerName ?? "Player";

    private static string GetName(IInnerPlayerControl[] players, byte playerId, ModeSession session)
    {
        var player = players.FirstOrDefault(p => p.PlayerId == playerId);
        return player is null ? "A player" : BaseName(player, session);
    }

    private static string DisplayMode(PolusMode mode) => mode switch
    {
        PolusMode.HotPotato => "Hot Potato",
        PolusMode.Infection => "Infection",
        PolusMode.HideAndSeek => "Hide & Seek",
        PolusMode.CopsAndRobbers => "Cops & Robbers",
        PolusMode.RandomChaos => "Random Chaos",
        PolusMode.Classic => "Classic",
        _ => "Off"
    };

    private static ValueTask Reply(IInnerPlayerControl player, string message) =>
        player.SendChatToPlayerAsync("[Polus] " + message);

    private static async ValueTask Broadcast(IGame game, string message)
    {
        foreach (var client in game.Players)
        {
            if (client.Character is { } player)
            {
                await player.SendChatAsync("[Polus] " + message);
            }
        }
    }
}

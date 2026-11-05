using Polus.RoleCore;
using UnityEngine;

namespace Polus.AndroidMod;

/// <summary>
/// Standalone Town-of-Host-style runtime. It keeps role state on the host and
/// uses ordinary Among Us gameplay hooks; it has no MiraAPI/Reactor dependency.
/// </summary>
internal static class PolusHostRoles
{
    internal sealed record HostRole(string Id, string Name, RoleTeam Team, string Description,
        int CooldownSeconds, string AbilityLabel);
    private static readonly Dictionary<string, HostRole> Roles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, object?> Options = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> Keybinds = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<(byte Player, string Role, int Meeting)> MeetingUses = new();
    private static readonly List<(byte Voter, byte? Target)> ObservedVotes = new();
    private static readonly Dictionary<byte, DateTimeOffset> LastAbilityUse = new();
    private static int _meetingNumber;
    private static bool _matchEnded;

    public static void RegisterRoles(IEnumerable<RoleDefinition> definitions)
    {
        Roles.Clear();
        foreach (var definition in definitions)
        {
            Roles[definition.Id] = new HostRole(
                definition.Id, definition.Name, definition.Team, definition.Description,
                Math.Clamp(definition.CooldownSeconds, 0, 600),
                definition.Name + " Ability");

            SetDefault("role." + definition.Id + ".enabled", definition.EnabledByDefault);
            SetDefault("role." + definition.Id + ".spawnWeight", Math.Clamp(definition.SpawnChance, 0, 100));
        }

        Keybinds["polus.menu"] = "F8";
    }

    private static void SetDefault<T>(string id, T value)
    {
        if (!Options.ContainsKey(id)) Options[id] = value;
    }

    public static T GetOption<T>(string id, T fallback)
    {
        return Options.TryGetValue(id, out var value) && value is T typed ? typed : fallback;
    }

    public static void SetOption<T>(string id, T value)
    {
        if (Options.ContainsKey(id)) Options[id] = value;
    }

    public static bool TryGetKeybind(string id, out string key) =>
        Keybinds.TryGetValue(id, out key!);

    public static string? GetActionButton(string roleId) =>
        Roles.TryGetValue(roleId, out var role) ? role.AbilityLabel : null;

    public static bool HasMeetingButton(string roleId) =>
        roleId is "auditor" or "mediator" or "beaconist" or "calibrator" or "signalwright" or "broker";

    public static bool TryUseAction(string roleId, byte playerId, bool isHost, out string result)
    {
        result = "Role ability is unavailable.";
        if (!isHost)
        {
            result = "Only the host can execute Polus role actions.";
            return false;
        }
        if (_matchEnded || !Roles.ContainsKey(roleId))
        {
            result = "This role is not active in the current match.";
            return false;
        }

        var role = Roles[roleId];
        if (LastAbilityUse.TryGetValue(playerId, out var last)
            && (DateTimeOffset.UtcNow - last).TotalSeconds < role.CooldownSeconds)
        {
            result = "Ability is cooling down.";
            return false;
        }

        var player = PolusAndroidMod.GetPlayerForApi(playerId);
        var transform = player == null ? null : PolusAndroidMod.GetMemberForApi(player, "transform");
        var position = transform == null ? Vector3.zero :
            (Vector3)(PolusAndroidMod.GetMemberForApi(transform, "position") ?? Vector3.zero);

        result = AndroidRoleMechanics.Activate(roleId, playerId, position);
        LastAbilityUse[playerId] = DateTimeOffset.UtcNow;
        return true;
    }

    public static bool TryUseMeetingAbility(string roleId, byte playerId, bool isHost, out string result)
    {
        result = "Meeting ability is unavailable.";
        if (!isHost)
        {
            result = "Only the host can execute Polus meeting actions.";
            return false;
        }
        if (!HasMeetingButton(roleId) || !MeetingUses.Add((playerId, roleId, _meetingNumber)))
        {
            result = "This meeting ability is unavailable or was already used.";
            return false;
        }

        result = AndroidRoleMechanics.UseMeetingAbility(roleId, playerId);
        return true;
    }

    public static void BeginMeeting()
    {
        _meetingNumber++;
    }

    public static void EndMeeting()
    {
        // MeetingUses is keyed by meeting number, so earlier meetings remain harmless.
    }

    public static void RecordObservedVote(byte voter, byte? target, bool isHost)
    {
        if (isHost) ObservedVotes.Add((voter, target));
    }

    public static void OnGameEnded() => _matchEnded = true;

    public static void ResetMatch()
    {
        MeetingUses.Clear();
        ObservedVotes.Clear();
        LastAbilityUse.Clear();
        _meetingNumber = 0;
        _matchEnded = false;
    }
}

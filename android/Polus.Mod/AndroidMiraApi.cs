namespace Polus.AndroidMod;

/// <summary>
/// Android-native compatibility surface for the MiraAPI features Polus consumes.
/// It deliberately does not load BepInEx/Reactor/MiraAPI binaries; those target the
/// Windows IL2CPP plugin stack and cannot be loaded by LemonLoader.
/// </summary>
internal static class AndroidMiraApi
{
    internal sealed record CustomRole(string Id, string Name, string Team, string Description);
    internal sealed record ActionButton(string RoleId, string Label, int CooldownSeconds, int MaxUses);
    internal sealed record MeetingButton(string RoleId, string Label, int MaxUses);

    private static readonly Dictionary<string, CustomRole> Roles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ActionButton> Actions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, MeetingButton> MeetingActions = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<CustomRole> RegisteredRoles => Roles.Values;
    public static IReadOnlyCollection<ActionButton> RegisteredActionButtons => Actions.Values;
    public static IReadOnlyCollection<MeetingButton> RegisteredMeetingButtons => MeetingActions.Values;

    public static void RegisterRoles(IEnumerable<Polus.RoleCore.RoleDefinition> definitions)
    {
        Roles.Clear();
        Actions.Clear();
        MeetingActions.Clear();

        foreach (var role in definitions)
        {
            Roles.Add(role.Id, new CustomRole(role.Id, role.Name, role.Team.ToString(), role.Description));
            Actions.Add(role.Id, new ActionButton(role.Id, role.Name + " Ability",
                Math.Clamp(role.CooldownSeconds, 0, 600), 1));

            if (role.Id is "auditor" or "mediator" or "beaconist" or "calibrator" or "signalwright" or "broker")
                MeetingActions.Add(role.Id, new MeetingButton(role.Id, role.Name + " Meeting Ability", 1));
        }
    }

    public static bool IsRegistered(string roleId) => Roles.ContainsKey(roleId);
    public static ActionButton? GetActionButton(string roleId) =>
        Actions.TryGetValue(roleId, out var button) ? button : null;
    public static MeetingButton? GetMeetingButton(string roleId) =>
        MeetingActions.TryGetValue(roleId, out var button) ? button : null;
}

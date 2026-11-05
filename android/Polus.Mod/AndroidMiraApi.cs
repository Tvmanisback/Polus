using Polus.MiraApi.Android;
using Polus.RoleCore;

namespace Polus.AndroidMod;

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

    public static void RegisterRoles(IEnumerable<RoleDefinition> definitions)
    {
        Roles.Clear();
        Actions.Clear();
        MeetingActions.Clear();
        var api = MiraApi.Instance;
        api.Roles.Clear();

        foreach (var role in definitions)
        {
            var roleId = role.Id;
            Roles[roleId] = new CustomRole(roleId, role.Name, role.Team.ToString(), role.Description);
            Actions[roleId] = new ActionButton(roleId, role.Name + " Ability",
                Math.Clamp(role.CooldownSeconds, 0, 600), int.MaxValue);
            api.Options.Register(new MiraOption<bool>("role." + roleId + ".enabled", "Enable " + role.Name, role.EnabledByDefault));
            api.Options.Register(new MiraOption<int>("role." + roleId + ".spawnWeight", role.Name + " spawn weight", role.SpawnChance, 0, 100));
            api.Roles.Register(new MiraRole(roleId, role.Name, role.Team.ToString(),
                role.Description, HostOnly: true, CooldownSeconds: Math.Clamp(role.CooldownSeconds, 0, 600),
                MaxUses: int.MaxValue));
            api.Buttons.Register(new MiraButton(roleId, roleId, role.Name + " Ability",
                Math.Clamp(role.CooldownSeconds, 0, 600), int.MaxValue,
                context =>
                {
                    var player = PolusAndroidMod.GetPlayerForApi(context.PlayerId);
                    var transform = player == null ? null : PolusAndroidMod.GetMemberForApi(player, "transform");
                    var position = transform == null ? UnityEngine.Vector3.zero :
                        (UnityEngine.Vector3)(PolusAndroidMod.GetMemberForApi(transform, "position") ?? UnityEngine.Vector3.zero);
                    return AndroidRoleMechanics.Activate(roleId, context.PlayerId, position);
                }));

            if (roleId is "auditor" or "mediator" or "beaconist" or "calibrator" or "signalwright" or "broker")
            {
                MeetingActions[roleId] = new MeetingButton(roleId, role.Name + " Meeting Ability", 1);
                api.MeetingButtons.Register(new MiraMeetingButton(roleId, roleId,
                    role.Name + " Meeting Ability", 1,
                    context => AndroidRoleMechanics.UseMeetingAbility(roleId, context.PlayerId)));
            }
        }
    }

    public static bool IsRegistered(string roleId) => Roles.ContainsKey(roleId);
    public static ActionButton? GetActionButton(string roleId) =>
        Actions.TryGetValue(roleId, out var button) ? button : null;
    public static MeetingButton? GetMeetingButton(string roleId) =>
        MeetingActions.TryGetValue(roleId, out var button) ? button : null;

    public static bool TryUseAction(string roleId, byte playerId, bool isHost, out string result) =>
        MiraApi.Instance.Buttons.TryUse(roleId,
            new MiraActionContext(playerId, isHost, DateTimeOffset.UtcNow), out result);

    public static bool TryUseMeeting(string roleId, byte playerId, bool isHost, int meetingNumber, out string result) =>
        MiraApi.Instance.MeetingButtons.TryUse(roleId,
            new MiraMeetingContext(playerId, isHost, meetingNumber), out result);
}

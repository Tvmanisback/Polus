using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.GameEnd;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Polus.Plugin.Roles;
using Polus.RoleCore;

namespace Polus.Plugin;

public static class PolusRoleEvents
{
    private static readonly ConcurrentDictionary<byte, int> CompletedTasks = new();
    private static readonly ConcurrentDictionary<byte, byte> NeutralWinsTriggered = new();
    private static readonly HostRoleEngine SharedRoleEngine = new();
    private static readonly RoleEffectEngine SharedEffects = new(true);

    [RegisterEvent]
    public static void OnIntroBegin(IntroBeginEvent ev)
    {
        CompletedTasks.Clear();
        NeutralWinsTriggered.Clear();
        PolusRoleMechanics.Reset();
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        var assignments = new List<RoleAssignment>();
        foreach (var player in PlayerControl.AllPlayerControls.ToArray())
        {
            if (player?.Data == null) continue;
            PolusRoleMechanics.InitializePlayer(player);
            var role = player.Data.Role;
            if (role is not ICustomRole custom) continue;
            var roleName = role.GetType().Name.EndsWith("Role", StringComparison.Ordinal)
                ? role.GetType().Name[..^4]
                : role.GetType().Name;
            var roleId = Regex.Replace(roleName, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
            var team = role is PolusImpostorRole ? RoleTeam.Impostor
                : role is PolusNeutralRole ? RoleTeam.Neutral : RoleTeam.Crewmate;
            assignments.Add(new RoleAssignment(player.PlayerId,
                new RoleDefinition(roleId, custom.RoleName, team, custom.RoleName,
                    EnabledByDefault: true, SpawnChance: 100, CooldownSeconds: 25,
                    Mechanic: "Shared Polus role engine", WinCondition: team == RoleTeam.Neutral ? "Complete role objective" : "")));
        }
        SharedRoleEngine.EndMatch();
        SharedEffects.EndMatch();
        if (assignments.Count > 0)
        {
            SharedRoleEngine.StartMatch(assignments);
            SharedEffects.BeginMatch(assignments);
        }
    }

    [RegisterEvent]
    public static void OnTaskCompleted(CompleteTaskEvent ev)
    {
        if (ev.Player == null || ev.Player.Data == null) return;
        CompletedTasks.AddOrUpdate(ev.Player.PlayerId, 1, (_, current) => current + 1);
        PolusRoleMechanics.RecordTask(ev.Player, ev.Task.TaskType.ToString());
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) { CheckNeutralWin(ev.Player); return; }
        try
        {
            SharedRoleEngine.RecordTask(ev.Player.PlayerId, ev.Task.TaskType.ToString(), ev.Player.transform.position.x, ev.Player.transform.position.y);
            SharedEffects.RecordTask(ev.Player.PlayerId, ev.Task.TaskType.ToString(), ev.Player.transform.position.x, ev.Player.transform.position.y);
        }
        catch { }
        CheckNeutralWin(ev.Player);
    }

    [RegisterEvent]
    public static void OnMeetingStarted(StartMeetingEvent ev)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
        SharedRoleEngine.BeginMeeting();
        SharedEffects.BeginMeeting();
        foreach (var player in PlayerControl.AllPlayerControls.ToArray())
        {
            if (player == null || player.Data == null) continue;
            var tasksSinceMeeting = GetCompletedTaskCount(player.PlayerId);
            if (PolusRoleMechanics.ShouldGrantMeetingVote(player, tasksSinceMeeting))
                player.GetVoteData().IncreaseRemainingVotes(1);
            PolusRoleMechanics.RecordMeeting(player);
            if (player.Data.Role is MediatorRole) CompletedTasks[player.PlayerId] = 0;
        }
    }

    // Called only by the local role's action button. Each assigned role gets one
    // meaningful, match-scoped action: a bonus meeting vote, or neutral objective progress.
    public static bool ActivateModdedRoleAbility(PlayerControl player) =>
        ActivateRoleAbility(player, vanillaButton: false);

    public static bool ActivateVanillaRoleAbility(PlayerControl player) =>
        ActivateRoleAbility(player, vanillaButton: true);

    private static bool ActivateRoleAbility(PlayerControl player, bool vanillaButton)
    {
        if (player == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;
        var decision = SharedEffects.TryUseAbility(player.PlayerId, DateTimeOffset.UtcNow, 25, out _);
        if (!decision.Accepted) return false;
        var activated = vanillaButton
            ? PolusRoleMechanics.ActivateVanillaRoleAbility(player)
            : PolusRoleMechanics.ActivateModdedRoleAbility(player);
        if (activated && player != null) CheckNeutralWin(player);
        return activated;
    }

    // Compatibility alias for the existing MiraAPI button.
    public static bool ActivateRoleAbility(PlayerControl player) =>
        ActivateModdedRoleAbility(player);


    private static void CheckNeutralWin(PlayerControl player)
    {
        if (player.AmOwner && player.Data?.Role is PolusNeutralRole neutral &&
            PolusRoleMechanics.GetProgress(player.PlayerId) >= neutral.ObjectiveTaskGoal &&
            NeutralWinsTriggered.TryAdd(player.PlayerId, 0))
            CustomGameOver.Trigger<PolusNeutralVictory>([player.Data]);
    }

    public static int GetCompletedTaskCount(byte playerId) =>
        CompletedTasks.TryGetValue(playerId, out var count) ? count : 0;

    public static int GetRoleObjectiveProgress(byte playerId) => PolusRoleMechanics.GetProgress(playerId);

    public static int GetSharedRoleProgress(byte playerId) => SharedRoleEngine.GetProgress(playerId);
}

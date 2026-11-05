using System.Collections.Concurrent;
using System.Linq;
using MiraAPI.Events;
using MiraAPI.Events.Vanilla.Meeting;
using MiraAPI.Events.Vanilla.Gameplay;
using MiraAPI.Events.Vanilla.Player;
using MiraAPI.GameEnd;
using MiraAPI.Roles;
using MiraAPI.Utilities;
using Polus.Plugin.Roles;

namespace Polus.Plugin;

public static class PolusRoleEvents
{
    private static readonly ConcurrentDictionary<byte, int> CompletedTasks = new();
    private static readonly ConcurrentDictionary<byte, byte> NeutralWinsTriggered = new();

    [RegisterEvent]
    public static void OnIntroBegin(IntroBeginEvent ev)
    {
        CompletedTasks.Clear();
        NeutralWinsTriggered.Clear();
        PolusRoleMechanics.Reset();
        foreach (var player in PlayerControl.AllPlayerControls.ToArray())
            PolusRoleMechanics.InitializePlayer(player);
    }

    [RegisterEvent]
    public static void OnTaskCompleted(CompleteTaskEvent ev)
    {
        if (ev.Player == null || ev.Player.Data == null) return;
        CompletedTasks.AddOrUpdate(ev.Player.PlayerId, 1, (_, current) => current + 1);
        PolusRoleMechanics.RecordTask(ev.Player, ev.Task.TaskType.ToString());
        CheckNeutralWin(ev.Player);
    }

    [RegisterEvent]
    public static void OnMeetingStarted(StartMeetingEvent ev)
    {
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
    public static bool ActivateRoleAbility(PlayerControl player)
    {
        var activated = PolusRoleMechanics.Activate(player);
        if (activated && player != null) CheckNeutralWin(player);
        return activated;
    }


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
}

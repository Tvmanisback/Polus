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
    private static readonly ConcurrentDictionary<byte, byte> BonusVotePending = new();

    [RegisterEvent]
    public static void OnIntroBegin(IntroBeginEvent ev)
    {
        CompletedTasks.Clear();
        NeutralWinsTriggered.Clear();
        BonusVotePending.Clear();
        PolusRoleMechanics.Reset();
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
            PolusRoleMechanics.RecordMeeting(player);
            if (BonusVotePending.TryRemove(player.PlayerId, out _))
                player.GetVoteData().IncreaseRemainingVotes(1);
        }
    }

    // Called only by the local role's action button. Each assigned role gets one
    // meaningful, match-scoped action: a bonus meeting vote, or neutral objective progress.
    public static bool ActivateRoleAbility(PlayerControl player) => PolusRoleMechanics.Activate(player);

    public static void QueueBonusVote(byte playerId) => BonusVotePending[playerId] = 0;

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

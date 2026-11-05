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
    private static readonly ConcurrentDictionary<byte, byte> AbilityUsed = new();
    private static readonly ConcurrentDictionary<byte, byte> BonusVotePending = new();

    [RegisterEvent]
    public static void OnIntroBegin(IntroBeginEvent ev)
    {
        CompletedTasks.Clear();
        NeutralWinsTriggered.Clear();
        AbilityUsed.Clear();
        BonusVotePending.Clear();
    }

    [RegisterEvent]
    public static void OnTaskCompleted(CompleteTaskEvent ev)
    {
        if (ev.Player == null || ev.Player.Data == null) return;
        var count = CompletedTasks.AddOrUpdate(ev.Player.PlayerId, 1, (_, current) => current + 1);
        CheckNeutralWin(ev.Player);
    }

    [RegisterEvent]
    public static void OnMeetingStarted(StartMeetingEvent ev)
    {
        foreach (var player in PlayerControl.AllPlayerControls.ToArray())
        {
            if (player == null || player.Data == null) continue;
            if (player.Data.Role is AuditorRole)
                player.GetVoteData().IncreaseRemainingVotes(1);
            else if (player.Data.Role is MediatorRole &&
                     CompletedTasks.TryGetValue(player.PlayerId, out var completed) &&
                     completed >= 3)
            {
                player.GetVoteData().IncreaseRemainingVotes(1);
                CompletedTasks[player.PlayerId] = 0;
            }

            if (BonusVotePending.TryRemove(player.PlayerId, out _))
                player.GetVoteData().IncreaseRemainingVotes(1);
        }
    }

    // Called only by the local role's action button. Each assigned role gets one
    // meaningful, match-scoped action: a bonus meeting vote, or neutral objective progress.
    public static bool ActivateRoleAbility(PlayerControl player)
    {
        if (player == null || player.Data == null || !player.AmOwner) return false;
        var role = player.Data.Role;
        if (role is not ICustomRole || !AbilityUsed.TryAdd(player.PlayerId, 0)) return false;

        if (role is PolusNeutralRole neutral)
        {
            var count = CompletedTasks.AddOrUpdate(player.PlayerId, 1, (_, current) => current + 1);
            if (count >= neutral.ObjectiveTaskGoal &&
                NeutralWinsTriggered.TryAdd(player.PlayerId, 0))
                CustomGameOver.Trigger<PolusNeutralVictory>([player.Data]);
        }
        else
        {
            BonusVotePending[player.PlayerId] = 0;
        }

        return true;
    }

    private static void CheckNeutralWin(PlayerControl player)
    {
        if (player.AmOwner && player.Data?.Role is PolusNeutralRole neutral &&
            GetCompletedTaskCount(player.PlayerId) >= neutral.ObjectiveTaskGoal &&
            NeutralWinsTriggered.TryAdd(player.PlayerId, 0))
            CustomGameOver.Trigger<PolusNeutralVictory>([player.Data]);
    }

    public static int GetCompletedTaskCount(byte playerId) =>
        CompletedTasks.TryGetValue(playerId, out var count) ? count : 0;
}

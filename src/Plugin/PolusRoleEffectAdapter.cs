using Polus.RoleCore;
using UnityEngine;
using MiraAPI.Utilities;
using Object = UnityEngine.Object;

namespace Polus.Plugin;

/// <summary>
/// Executes semantic role commands through the game's normal local game objects and
/// supported networked transforms. All calls originate on the host.
/// </summary>
internal static class PolusRoleEffectAdapter
{
    public static bool Execute(PlayerControl actor, RoleEffectCommand command)
    {
        if (actor?.Data == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return false;

        var succeeded = command.Kind switch
        {
            "emergency_meeting" => OpenEmergencyMeeting(actor),
            "snap_to_nearest_player" => SnapToNearest(actor),
            "swap_with_nearest_player" => SwapWithNearest(actor),
            "return_to_last_sector" => ReturnToCheckpoint(actor, command),
            "temporary_self_visual" => ApplyTemporaryVisual(actor, dark: true),
            "temporary_self_palette" => ApplyTemporaryVisual(actor, dark: false),
            "mark_nearest_player" => MarkNearest(actor),
            "meeting_bonus_vote" => GrantBonusVote(actor, command.Value),
            _ => true
        };

        if (succeeded && command.Kind is not "none")
            Helpers.CreateAndShowNotification(command.Message, new Color32(105, 190, 255, 255),
                new Vector3(0f, 2f, -50f));
        return succeeded;
    }

    private static PlayerControl? Nearest(PlayerControl actor)
    {
        var target = actor.GetClosestPlayer(true, 2.5f);
        return target != null && target != actor && target.Data != null && !target.Data.IsDead
            ? target : null;
    }

    private static bool SnapToNearest(PlayerControl actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        actor.NetTransform.RpcSnapTo(target.transform.position);
        return true;
    }

    private static bool SwapWithNearest(PlayerControl actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        var actorPosition = actor.transform.position;
        var targetPosition = target.transform.position;
        actor.NetTransform.RpcSnapTo(targetPosition);
        target.NetTransform.RpcSnapTo(actorPosition);
        return true;
    }

    private static bool ReturnToCheckpoint(PlayerControl actor, RoleEffectCommand command)
    {
        actor.NetTransform.RpcSnapTo(new Vector2(command.X, command.Y));
        return true;
    }

    private static bool OpenEmergencyMeeting(PlayerControl actor)
    {
        if (ShipStatus.Instance == null || ShipStatus.Instance.EmergencyButton == null) return false;
        actor.NetTransform.Halt();
        var emergency = ShipStatus.Instance.EmergencyButton;
        var minigame = Object.Instantiate(emergency.MinigamePrefab, Camera.main!.transform, false);
        minigame.transform.localPosition = new Vector3(0f, 0f, -50f);
        minigame.Begin(null);
        return true;
    }

    private static bool ApplyTemporaryVisual(PlayerControl actor, bool dark)
    {
        foreach (var sprite in actor.cosmetics.transform.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.color = dark ? new Color(0.35f, 0.35f, 0.35f, 1f)
                : new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
        return true;
    }

    private static bool MarkNearest(PlayerControl actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        target.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.magenta));
        return true;
    }
    private static bool GrantBonusVote(PlayerControl actor, int count)
    {
        actor.GetVoteData().IncreaseRemainingVotes(Mathf.Clamp(count, 1, 3));
        return true;
    }

}

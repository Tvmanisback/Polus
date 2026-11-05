using System.Collections;
using System.Reflection;
using Polus.RoleCore;
using UnityEngine;

namespace Polus.AndroidMod;

/// <summary>Reflection-only bridge to vanilla Among Us actions on the Android host.</summary>
internal static class AndroidRoleEffectAdapter
{
    public static bool Execute(byte playerId, RoleEffectCommand command, out string result)
    {
        result = command.Message;
        if (!PolusAndroidMod.IsHostForApi()) { result = "Only the host can execute role effects."; return false; }
        var actor = PolusAndroidMod.GetPlayerForApi(playerId);
        if (actor == null) { result = "The assigned player is no longer in the match."; return false; }

        try
        {
            var ok = command.Kind switch
            {
                "emergency_meeting" => EmergencyMeeting(actor),
                "snap_to_nearest_player" => SnapToNearest(actor),
                "swap_with_nearest_player" => SwapWithNearest(actor),
                "return_to_last_sector" => Snap(actor, new Vector2(command.X, command.Y)),
                "temporary_self_visual" => Tint(actor, true),
                "temporary_self_palette" => Tint(actor, false),
                "mark_nearest_player" => MarkNearest(actor),
                "meeting_bonus_vote" => GrantBonusVote(actor),
                _ => true
            };
            if (!ok) result = command.Message + " (this game version did not expose the required action)";
            return ok;
        }
        catch (Exception ex)
        {
            result = "Role effect could not be applied: " + ex.Message;
            return false;
        }
    }

    private static bool EmergencyMeeting(object actor)
    {
        var method = actor.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "CmdReportDeadBody" && m.GetParameters().Length == 1);
        if (method == null) return false;
        method.Invoke(actor, new object?[] { null });
        return true;
    }

    private static object? Nearest(object actor)
    {
        var actorTransform = Member(actor, "transform");
        var actorPosition = actorTransform == null ? Vector3.zero : (Vector3)(Member(actorTransform, "position") ?? Vector3.zero);
        var type = AccessToolsType("PlayerControl");
        var all = type == null ? null : StaticMember(type, "AllPlayerControls");
        if (all is not IEnumerable enumerable) return null;
        object? best = null;
        var bestDistance = 2.5f;
        foreach (var candidate in enumerable)
        {
            if (candidate == null || ReferenceEquals(candidate, actor)) continue;
            var data = Member(candidate, "Data");
            if (data == null || TryBool(data, "IsDead")) continue;
            var transform = Member(candidate, "transform");
            if (transform == null) continue;
            var position = (Vector3)(Member(transform, "position") ?? Vector3.zero);
            var distance = Vector2.Distance(actorPosition, position);
            if (distance < bestDistance) { bestDistance = distance; best = candidate; }
        }
        return best;
    }

    private static bool SnapToNearest(object actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        var transform = Member(target, "transform");
        var position = transform == null ? Vector3.zero : (Vector3)(Member(transform, "position") ?? Vector3.zero);
        return Snap(actor, position);
    }

    private static bool SwapWithNearest(object actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        var actorTransform = Member(actor, "transform");
        var targetTransform = Member(target, "transform");
        if (actorTransform == null || targetTransform == null) return false;
        var actorPosition = (Vector3)(Member(actorTransform, "position") ?? Vector3.zero);
        var targetPosition = (Vector3)(Member(targetTransform, "position") ?? Vector3.zero);
        return Snap(actor, targetPosition) && Snap(target, actorPosition);
    }

    private static bool Snap(object player, Vector2 position)
    {
        var netTransform = Member(player, "NetTransform");
        if (netTransform == null) return false;
        var method = netTransform.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "RpcSnapTo" && m.GetParameters().Length == 1);
        if (method == null) return false;
        method.Invoke(netTransform, new object[] { position });
        return true;
    }

    private static bool Tint(object player, bool dark)
    {
        var cosmetics = Member(player, "cosmetics");
        if (cosmetics is not Component component) return false;
        foreach (var sprite in component.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.color = dark ? new Color(0.35f, 0.35f, 0.35f, 1f)
                : new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
        return true;
    }

    private static bool MarkNearest(object actor)
    {
        var target = Nearest(actor);
        if (target == null) return false;
        var cosmetics = Member(target, "cosmetics");
        if (cosmetics == null) return false;
        var method = cosmetics.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => m.Name == "SetOutline" && m.GetParameters().Length == 2);
        if (method == null) return false;
        method.Invoke(cosmetics, new object?[] { true, null });
        return true;
    }

    private static bool GrantBonusVote(object player)
    {
        var voteData = InvokeNoArg(player, "GetVoteData");
        if (voteData == null) return false;
        var method = voteData.GetType().GetMethod("IncreaseRemainingVotes", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null) return false;
        method.Invoke(voteData, new object[] { 1 });
        return true;
    }

    private static object? InvokeNoArg(object target, string name) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null)?.Invoke(target, null);

    private static Type? AccessToolsType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name, false)).FirstOrDefault(t => t != null);

    private static object? StaticMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        return type.GetProperty(name, flags)?.GetValue(null) ?? type.GetField(name, flags)?.GetValue(null);
    }

    private static object? Member(object target, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return target.GetType().GetProperty(name, flags)?.GetValue(target) ?? target.GetType().GetField(name, flags)?.GetValue(target);
    }

    private static bool TryBool(object target, string name)
    {
        try { return Convert.ToBoolean(Member(target, name)); } catch { return false; }
    }
}

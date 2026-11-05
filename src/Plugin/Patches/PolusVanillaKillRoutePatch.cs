using HarmonyLib;
using InnerNet;
using Polus.Plugin;

namespace Polus.Plugin.Patches;

/// <summary>
/// Vanilla clients cannot load Polus buttons. Their normal kill request is therefore
/// treated as the role-ability input on the host, instead of allowing a murder.
/// The host executes the role command and all clients receive only normal game effects.
/// </summary>
[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
internal static class PolusVanillaKillRoutePatch
{
    private static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return true;
        if (__instance?.Data == null || __instance.Data.IsDead ||
            !PolusRoleEvents.HasSharedRole(__instance.PlayerId))
            return true;

        PolusRoleEvents.ActivateVanillaRoleAbility(__instance);
        return false;
    }
}

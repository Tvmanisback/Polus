using HarmonyLib;
using Polus.Plugin.Buttons;
using Polus.Plugin.Roles;
using UnityEngine;

namespace Polus.Plugin.Patches;

// Uses the game's existing Kill button as a role-ability button for the
// vanilla-button implementation. The original kill action is suppressed
// only while the local player has a Polus role.
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class PolusVanillaRoleButtonUpdatePatch
{
    private static float _cooldown;

    private static bool HasPolusRole(RoleBehaviour? role) =>
        role is PolusCrewmateRole or PolusImpostorRole or PolusNeutralRole &&
        role is not QuartermasterRole;

    private static void Postfix(HudManager __instance)
    {
        var player = PlayerControl.LocalPlayer;
        var button = __instance?.KillButton;
        if (player?.Data == null || button == null || !HasPolusRole(player.Data.Role))
            return;

        if (MeetingHud.Instance != null || player.Data.IsDead ||
            AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
        {
            button.gameObject.SetActive(false);
            return;
        }

        _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);
        button.gameObject.SetActive(true);
        button.enabled = true;
        button.buttonLabelText.text = PolusRoleAbilityButton.GetAbilityLabel(player.Data.Role);
        button.SetTarget(null);
        button.SetCoolDown(_cooldown, 25f);
    }

    internal static bool CanActivate => _cooldown <= 0f;
    internal static void StartCooldown() => _cooldown = 25f;
}

[HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
internal static class PolusVanillaRoleButtonClickPatch
{
    private static bool Prefix(KillButton __instance)
    {
        var player = PlayerControl.LocalPlayer;
        if (player?.Data == null ||
            player.Data.Role is not (PolusCrewmateRole or PolusImpostorRole or PolusNeutralRole) ||
            player.Data.Role is QuartermasterRole)
            return true;

        if (MeetingHud.Instance != null || player.Data.IsDead ||
            AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            return false;

        if (__instance != null && __instance.isActiveAndEnabled &&
            PolusVanillaRoleButtonUpdatePatch.CanActivate &&
            PolusRoleEvents.ActivateVanillaRoleAbility(player))
            PolusVanillaRoleButtonUpdatePatch.StartCooldown();

        return false;
    }
}

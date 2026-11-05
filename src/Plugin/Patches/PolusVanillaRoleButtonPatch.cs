using HarmonyLib;
using Polus.Plugin.Buttons;
using Polus.Plugin.Roles;
using UnityEngine;

namespace Polus.Plugin.Patches;

// Creates a second KillButton instance for the role ability. The game's original
// KillButton remains in place and continues to handle normal impostor kills.
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class PolusVanillaRoleButtonUpdatePatch
{
    private static KillButton? _abilityButton;
    private static float _cooldown;

    internal static KillButton? AbilityButton => _abilityButton;
    internal static bool CanActivate => _cooldown <= 0f;
    internal static void StartCooldown() => _cooldown = 25f;

    private static void Postfix(HudManager __instance)
    {
        var original = __instance?.KillButton;
        if (original == null) return;

        if (_abilityButton == null)
        {
            _abilityButton = Object.Instantiate(original, original.transform.parent);
            _abilityButton.name = "PolusRoleAbilityButton";
            _abilityButton.transform.localPosition =
                original.transform.localPosition + new Vector3(-0.85f, 0f, 0f);
        }

        var player = PlayerControl.LocalPlayer;
        var button = _abilityButton;
        if (player?.Data == null || button == null) return;

        var inGame = MeetingHud.Instance == null &&
                     !player.Data.IsDead &&
                     AmongUsClient.Instance != null &&
                     AmongUsClient.Instance.GameState == InnerNetClient.GameStates.Started;

        if (!inGame)
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
}

[HarmonyPatch(typeof(KillButton), nameof(KillButton.DoClick))]
internal static class PolusVanillaRoleButtonClickPatch
{
    private static bool Prefix(KillButton __instance)
    {
        // Only consume clicks on our duplicated ability button. The original
        // KillButton always falls through to the game's normal kill handling.
        if (__instance == null ||
            __instance != PolusVanillaRoleButtonUpdatePatch.AbilityButton)
            return true;

        var player = PlayerControl.LocalPlayer;
        if (player?.Data == null ||
            MeetingHud.Instance != null ||
            player.Data.IsDead ||
            AmongUsClient.Instance == null ||
            AmongUsClient.Instance.GameState != InnerNetClient.GameStates.Started)
            return false;

        if (PolusVanillaRoleButtonUpdatePatch.CanActivate &&
            PolusRoleEvents.ActivateVanillaRoleAbility(player))
            PolusVanillaRoleButtonUpdatePatch.StartCooldown();

        return false;
    }
}

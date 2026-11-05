using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Utilities.Assets;
using Polus.Plugin.Roles;
using UnityEngine;

namespace Polus.Plugin.Buttons;

/// <summary>
/// Every Polus role receives one match-scoped action. Crew and Impostor roles
/// bank a bonus vote for the next meeting; Neutral roles advance their objective.
/// </summary>
public sealed class PolusRoleAbilityButton : CustomActionButton
{
    public override string Name
    {
        get
        {
            var role = PlayerControl.LocalPlayer?.Data?.Role;
            return role is PolusNeutralRole ? "Objective Focus"
                : role is PolusImpostorRole ? "Deception"
                : "Field Report";
        }
    }

    public override float Cooldown => 30f;
    public override int MaxUses => 1;
    public override LoadableAsset<Sprite> Sprite => MiraAssets.CrewmateFile;
    public override MiraKeybind? Keybind => MiraGlobalKeybinds.SecondaryAbility;

    public override bool Enabled(RoleBehaviour? role) =>
        (role is PolusCrewmateRole || role is PolusImpostorRole || role is PolusNeutralRole) &&
        PlayerControl.LocalPlayer != null;

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player != null)
            PolusRoleEvents.ActivateRoleAbility(player);
    }
}

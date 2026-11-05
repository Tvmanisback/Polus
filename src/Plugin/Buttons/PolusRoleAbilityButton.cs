using MiraAPI.Hud;
using MiraAPI.Keybinds;
using MiraAPI.Utilities.Assets;
using Polus.Plugin.Roles;
using UnityEngine;

namespace Polus.Plugin.Buttons;

public sealed class PolusRoleAbilityButton : CustomActionButton
{
    public override string Name
    {
        get
        {
            var role = PlayerControl.LocalPlayer?.Data?.Role;
            return role switch
            {
                CartographerRole => "Map Survey",
                AuditorRole => "Audit",
                RelayRole => "Decode Signal",
                QuartermasterRole => "Supply Call",
                SurveyorRole => "Survey",
                CourierRole => "Delivery Log",
                ArchivistRole => "Open Archive",
                BeaconistRole => "Beacon Pulse",
                MediatorRole => "Mediate",
                CalibratorRole => "Calibrate",
                SignalwrightRole => "Link Signal",
                TimekeeperRole => "Time Check",
                CounterfeiterRole => "Forge Report",
                EchoistRole => "Echo Cue",
                BlackoutArtistRole => "Blackout",
                BrokerRole => "Trade",
                DecoyistRole => "Decoy Shift",
                LedgeristRole => "Delay Ledger",
                SwitchboarderRole => "Switch",
                WeathermakerRole => "Weather Shift",
                MisdirectorRole => "Misdirect",
                ScramblerRole => "Scramble",
                ParasiticSignalRole => "Attach Signal",
                JigsawRole => "Pattern Check",
                PilgrimRole => "Route Check",
                CuratorRole => "Catalogue",
                WayfinderRole => "Compass",
                CollectorRole => "Collection",
                MetronomeRole => "Rhythm Check",
                CartoucheRole => "Symbol Check",
                LighthouseRole => "Signal Check",
                WeathervaneRole => "Forecast",
                PuzzlewrightRole => "Logic Check",
                TimeCapsuleRole => "Compare Snapshot",
                NomadRole => "Itinerary",
                _ => "Polus Ability"
            };
        }
    }

    public override float Cooldown => 25f;
    public override int MaxUses => 0;
    public override LoadableAsset<Sprite> Sprite => MiraAssets.CrewmateFile;
    public override MiraKeybind? Keybind => MiraGlobalKeybinds.SecondaryAbility;

    public override bool Enabled(RoleBehaviour? role) =>
        (role is PolusCrewmateRole || role is PolusImpostorRole || role is PolusNeutralRole) &&
        PlayerControl.LocalPlayer != null;

    protected override void OnClick()
    {
        var player = PlayerControl.LocalPlayer;
        if (player != null) PolusRoleEvents.ActivateRoleAbility(player);
    }
}

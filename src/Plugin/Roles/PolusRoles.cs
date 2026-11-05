using MiraAPI.Roles;
using UnityEngine;

namespace Polus.Plugin.Roles;

public abstract class PolusCrewmateRole : CrewmateRole, ICustomRole
{
    public abstract string IdPart { get; }
    public string IdPrefix => "Polus.Role.Crewmate";
    public abstract string RoleName { get; }
    public abstract string RoleDescription { get; }
    public string RoleMedDescription => RoleDescription;
    public string RoleLongDescription => RoleDescription + $"\nRole progress: {(Player ? PolusRoleEvents.GetRoleObjectiveProgress(Player.PlayerId) : 0)}";
    public Color RoleColor => new Color32(105, 190, 255, 255);
    public ModdedRoleTeams Team => ModdedRoleTeams.Crewmate;
    public CustomRoleConfiguration Configuration => new(this) { DefaultRoleCount = 1, DefaultChance = 35, MaxRoleCount = 1 };
    public bool CanLocalPlayerSeeRole(PlayerControl player) => true;
}

public abstract class PolusImpostorRole : ImpostorRole, ICustomRole
{
    public abstract string IdPart { get; }
    public string IdPrefix => "Polus.Role.Impostor";
    public abstract string RoleName { get; }
    public abstract string RoleDescription { get; }
    public string RoleMedDescription => RoleDescription;
    public string RoleLongDescription => RoleDescription + $"\nAbility progress: {(Player ? PolusRoleEvents.GetRoleObjectiveProgress(Player.PlayerId) : 0)}";
    public Color RoleColor => new Color32(235, 80, 80, 255);
    public ModdedRoleTeams Team => ModdedRoleTeams.Impostor;
    public CustomRoleConfiguration Configuration => new(this) { DefaultRoleCount = 1, DefaultChance = 35, MaxRoleCount = 1 };
    public bool CanLocalPlayerSeeRole(PlayerControl player) => true;
}

public abstract class PolusNeutralRole : CrewmateRole, ICustomRole
{
    public abstract string IdPart { get; }
    public string IdPrefix => "Polus.Role.Neutral";
    public abstract string RoleName { get; }
    public abstract string RoleDescription { get; }
    public abstract int ObjectiveTaskGoal { get; }
    public string RoleMedDescription => RoleDescription;
    public string RoleLongDescription => RoleDescription + $"\nObjective progress: {(Player ? PolusRoleEvents.GetRoleObjectiveProgress(Player.PlayerId) : 0)}/{ObjectiveTaskGoal}";
    public Color RoleColor => new Color32(190, 145, 255, 255);
    public ModdedRoleTeams Team => ModdedRoleTeams.Custom;
    public CustomRoleConfiguration Configuration => new(this)
    {
        DefaultRoleCount = 1,
        DefaultChance = 25,
        MaxRoleCount = 1,
        CanGetKilled = true,
        UseVanillaKillButton = false,
        CanUseVent = false,
        CanUseSabotage = false,
        TasksCountForProgress = false
    };
    public bool CanLocalPlayerSeeRole(PlayerControl player) => true;
    public override bool DidWin(GameOverReason gameOverReason) => gameOverReason == MiraAPI.GameEnd.CustomGameOver.GameOverReason<PolusNeutralVictory>();
}

public sealed class CartographerRole : PolusCrewmateRole
{
    public override string IdPart => "cartographer";
    public override string RoleName => "Cartographer";
    public override string RoleDescription => "Records each new map sector reached while completing tasks; its route journal shows how much ground it has covered.";
}

public sealed class AuditorRole : PolusCrewmateRole
{
    public override string IdPart => "auditor";
    public override string RoleName => "Auditor";
    public override string RoleDescription => "Can audit the number of tasks completed since the previous checkpoint and earn a bonus vote when progress is found.";
}

public sealed class RelayRole : PolusCrewmateRole
{
    public override string IdPart => "relay";
    public override string RoleName => "Relay";
    public override string RoleDescription => "Decodes one signal for every two completed tasks.";
}

public sealed class QuartermasterRole : PolusCrewmateRole
{
    public override string IdPart => "quartermaster";
    public override string RoleName => "Quartermaster";
    public override string RoleDescription => "Unlocks a one-use emergency meeting call after completing four tasks.";
}

public sealed class SurveyorRole : PolusCrewmateRole
{
    public override string IdPart => "surveyor";
    public override string RoleName => "Surveyor";
    public override string RoleDescription => "Builds a survey by recording distinct task types and map sectors.";
}

public sealed class CourierRole : PolusCrewmateRole
{
    public override string IdPart => "courier";
    public override string RoleName => "Courier";
    public override string RoleDescription => "Every completed task is recorded as a delivery in its personal ledger.";
}

public sealed class ArchivistRole : PolusCrewmateRole
{
    public override string IdPart => "archivist";
    public override string RoleName => "Archivist";
    public override string RoleDescription => "Maintains a rolling event ledger containing task completions and meetings.";
}

public sealed class BeaconistRole : PolusCrewmateRole
{
    public override string IdPart => "beaconist";
    public override string RoleName => "Beaconist";
    public override string RoleDescription => "Charges a beacon for every two completed tasks and can claim a bonus vote when charged.";
}

public sealed class MediatorRole : PolusCrewmateRole
{
    public override string IdPart => "mediator";
    public override string RoleName => "Mediator";
    public override string RoleDescription => "Earns one bonus vote at the first meeting after completing three tasks.";
}

public sealed class CalibratorRole : PolusCrewmateRole
{
    public override string IdPart => "calibrator";
    public override string RoleName => "Calibrator";
    public override string RoleDescription => "Earns a bonus vote at a meeting after completing the same task type twice in a row.";
}

public sealed class SignalwrightRole : PolusCrewmateRole
{
    public override string IdPart => "signalwright";
    public override string RoleName => "Signalwright";
    public override string RoleDescription => "Earns a bonus vote at a meeting after completing three different task types consecutively.";
}

public sealed class TimekeeperRole : PolusCrewmateRole
{
    public override string IdPart => "timekeeper";
    public override string RoleName => "Timekeeper";
    public override string RoleDescription => "Stores task-count checkpoints and compares progress between snapshots.";
}

public sealed class CounterfeiterRole : PolusImpostorRole
{
    public override string IdPart => "counterfeiter";
    public override string RoleName => "Counterfeiter";
    public override string RoleDescription => "Can prepare one decoy task-board entry and track how many forged reports remain available.";
}

public sealed class EchoistRole : PolusImpostorRole
{
    public override string IdPart => "echoist";
    public override string RoleName => "Echoist";
    public override string RoleDescription => "Emits a short signal cue and records each echo so reported events can be compared.";
}

public sealed class BlackoutArtistRole : PolusImpostorRole
{
    public override string IdPart => "blackout_artist";
    public override string RoleName => "Blackout Artist";
    public override string RoleDescription => "Applies an eight-second dark visual filter to its character display, then restores its normal appearance.";
}

public sealed class BrokerRole : PolusImpostorRole
{
    public override string IdPart => "broker";
    public override string RoleName => "Broker";
    public override string RoleDescription => "Receives one additional vote at every meeting and records each clue-trade activation.";
}

public sealed class DecoyistRole : PolusImpostorRole
{
    public override string IdPart => "decoyist";
    public override string RoleName => "Decoyist";
    public override string RoleDescription => "Can reposition to a nearby player to create a decoy movement pattern.";
}

public sealed class LedgeristRole : PolusImpostorRole
{
    public override string IdPart => "ledgerist";
    public override string RoleName => "Ledgerist";
    public override string RoleDescription => "Stores a separate task-ledger checkpoint and tracks each delayed refresh window.";
}

public sealed class SwitchboarderRole : PolusImpostorRole
{
    public override string IdPart => "switchboarder";
    public override string RoleName => "Switchboarder";
    public override string RoleDescription => "Swaps positions with the nearest player using a networked position exchange.";
}

public sealed class WeathermakerRole : PolusImpostorRole
{
    public override string IdPart => "weathermaker";
    public override string RoleName => "Weathermaker";
    public override string RoleDescription => "Changes its character's visual palette for eight seconds to signal a new ambient condition.";
}

public sealed class MisdirectorRole : PolusImpostorRole
{
    public override string IdPart => "misdirector";
    public override string RoleName => "Misdirector";
    public override string RoleDescription => "Can return to its previous meeting checkpoint to confuse movement tracking.";
}

public sealed class ScramblerRole : PolusImpostorRole
{
    public override string IdPart => "scrambler";
    public override string RoleName => "Scrambler";
    public override string RoleDescription => "Scrambles its recorded status sequence and tracks each scramble activation.";
}

public sealed class ParasiticSignalRole : PolusImpostorRole
{
    public override string IdPart => "parasitic_signal";
    public override string RoleName => "Parasitic Signal";
    public override string RoleDescription => "Attaches a visible signal outline to a nearby player and records the tagged target.";
}

public sealed class JigsawRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 4;
    public override string IdPart => "jigsaw";
    public override string RoleName => "Jigsaw";
    public override string RoleDescription => "Collects a pattern fragment for each distinct task type completed.";
}

public sealed class PilgrimRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 5;
    public override string IdPart => "pilgrim";
    public override string RoleName => "Pilgrim";
    public override string RoleDescription => "Builds a route by completing different task types without repeating the previous one.";
}

public sealed class CuratorRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 4;
    public override string IdPart => "curator";
    public override string RoleName => "Curator";
    public override string RoleDescription => "Catalogues each unique combination of task type and map sector.";
}

public sealed class WayfinderRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 6;
    public override string IdPart => "wayfinder";
    public override string RoleName => "Wayfinder";
    public override string RoleDescription => "Builds a route by moving between map sectors; each sector transition is a route stop.";
}

public sealed class CollectorRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 5;
    public override string IdPart => "collector";
    public override string RoleName => "Collector";
    public override string RoleDescription => "Collects one artifact for each new map sector visited while completing tasks.";
}

public sealed class MetronomeRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 6;
    public override string IdPart => "metronome";
    public override string RoleName => "Metronome";
    public override string RoleDescription => "Completes tasks in a timing window to maintain a rhythm sequence.";
}

public sealed class CartoucheRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 4;
    public override string IdPart => "cartouche";
    public override string RoleName => "Cartouche";
    public override string RoleDescription => "Collects distinct pairs of task types as matching symbols.";
}

public sealed class LighthouseRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 7;
    public override string IdPart => "lighthouse";
    public override string RoleName => "Lighthouse";
    public override string RoleDescription => "Restores one beacon segment for every pair of completed tasks.";
}

public sealed class WeathervaneRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 5;
    public override string IdPart => "weathervane";
    public override string RoleName => "Weathervane";
    public override string RoleDescription => "Builds prediction progress by maintaining a sequence of alternating task types.";
}

public sealed class PuzzlewrightRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 8;
    public override string IdPart => "puzzlewright";
    public override string RoleName => "Puzzlewright";
    public override string RoleDescription => "Solves its logic sequence by avoiding consecutive repeats of the same task type.";
}

public sealed class TimeCapsuleRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 6;
    public override string IdPart => "timecapsule";
    public override string RoleName => "Time Capsule";
    public override string RoleDescription => "Compares task-count snapshots to measure progress between checkpoints.";
}

public sealed class NomadRole : PolusNeutralRole
{
    public override int ObjectiveTaskGoal => 7;
    public override string IdPart => "nomad";
    public override string RoleName => "Nomad";
    public override string RoleDescription => "Builds an itinerary score by returning to previously visited sectors after leaving them.";
}


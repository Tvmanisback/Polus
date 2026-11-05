using System.Collections.Concurrent;
using UnityEngine;
using MiraAPI.Utilities;
using MiraAPI.Roles;
using Polus.Plugin.Roles;

namespace Polus.Plugin;

public static class PolusRoleMechanics
{
    private sealed class State
    {
        public int Tasks, Progress, Snapshot, Streak, Chain;
        public float LastTaskAt;
        public string LastType = "";
        public Vector3 LastPosition;
        public bool HasLastPosition;
        public readonly HashSet<string> Types = new();
        public readonly HashSet<string> Sectors = new();
        public readonly HashSet<string> TypeSectorPairs = new();
        public readonly HashSet<string> TaskPairs = new();
        public string LastSector = "";
        public int RouteStops;
        public readonly List<string> Ledger = new();
    }

    private static readonly ConcurrentDictionary<byte, State> States = new();
    private static PlayerControl? LastTaggedPlayer;
    public static void Reset()
    {
        EndTimedEffects();
        States.Clear();
    }
    private static State Get(byte id) => States.GetOrAdd(id, _ => new State());
    public static int GetProgress(byte id) => States.TryGetValue(id, out var s) ? s.Progress : 0;
    public static int GetTasks(byte id) => States.TryGetValue(id, out var s) ? s.Tasks : 0;
    private static string Sector(Vector3 p) => $"{Mathf.FloorToInt(p.x / 4f)}:{Mathf.FloorToInt(p.y / 4f)}";

    public static void RecordTask(PlayerControl player, string type)
    {
        if (player?.Data == null || string.IsNullOrWhiteSpace(type)) return;
        var s = Get(player.PlayerId);
        var gap = Time.time - s.LastTaskAt;
        s.Tasks++;
        s.LastPosition = player.transform.position;
        s.HasLastPosition = true;
        s.Types.Add(type);
        var sector = Sector(player.transform.position);
        s.Sectors.Add(sector);
        s.TypeSectorPairs.Add(type + "@" + sector);
        if (s.LastType.Length > 0 && s.LastType != type)
            s.TaskPairs.Add(string.CompareOrdinal(s.LastType, type) < 0 ? s.LastType + "|" + type : type + "|" + s.LastType);
        if (s.LastSector != sector) s.RouteStops++;
        s.LastSector = sector;
        s.Ledger.Add(type);
        if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
        s.Streak = s.LastType == type ? s.Streak + 1 : 1;
        s.Chain = s.LastType == type ? 0 : s.Chain + 1;

        switch (player.Data.Role)
        {
            case CartographerRole: s.Progress = s.Sectors.Count; break;
            case RelayRole: s.Progress = s.Tasks / 2; break;
            case SurveyorRole: s.Progress = s.Types.Count; break;
            case CourierRole: s.Progress = s.Tasks; break;
            case ArchivistRole: s.Progress = s.Ledger.Count; break;
            case BeaconistRole: s.Progress = s.Tasks / 2; break;
            case MediatorRole: s.Progress = s.Tasks / 3; break;
            case CalibratorRole: s.Progress = s.Streak; break;
            case SignalwrightRole: s.Progress = s.Chain; break;
            case TimekeeperRole: s.Progress = s.Tasks - s.Snapshot; break;
            case JigsawRole: s.Progress = s.Types.Count; break;
            case CollectorRole: s.Progress = s.Sectors.Count; break;
            case PilgrimRole: s.Progress = s.LastType != type ? s.Progress + 1 : 0; break;
            case CuratorRole: s.Progress = s.TypeSectorPairs.Count; break;
            case WayfinderRole: s.Progress = s.RouteStops; break;
            case NomadRole: s.Progress = s.Sectors.Count; break;
            case MetronomeRole: s.Progress = gap >= 3f && gap <= 15f ? s.Progress + 1 : 1; break;
            case CartoucheRole: s.Progress = s.TaskPairs.Count; break;
            case WeathervaneRole: s.Progress = s.Chain / 2; break;
            case LighthouseRole: if (s.Tasks % 2 == 0) s.Progress++; break;
            case PuzzlewrightRole: s.Progress = s.Streak == 1 ? s.Progress + 1 : 0; break;
            case TimeCapsuleRole: s.Progress = s.Tasks - s.Snapshot; break;
        }

        s.LastType = type;
        s.LastTaskAt = Time.time;
    }

    public static int TakeSnapshot(byte id)
    {
        var s = Get(id);
        var difference = s.Tasks - s.Snapshot;
        s.Snapshot = s.Tasks;
        s.Progress = difference;
        return difference;
    }

    public static bool Activate(PlayerControl player)
    {
        if (player?.Data == null || !player.AmOwner || player.Data.Role is not ICustomRole custom) return false;
        var s = Get(player.PlayerId);
        string message;

        switch (player.Data.Role)
        {
            case CartographerRole:
                s.Sectors.Add(Sector(player.transform.position));
                s.Progress = s.Sectors.Count;
                message = $"Route journal updated: {s.Progress} sectors mapped.";
                break;
            case AuditorRole:
                var audited = TakeSnapshot(player.PlayerId);
                if (audited > 0) PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Audit checkpoint: {audited} task completions since the previous check.";
                break;
            case RelayRole:
                message = $"Decoded {s.Tasks / 2} task signals. Complete two tasks per signal.";
                break;
            case QuartermasterRole:
                message = "Complete four tasks to unlock the emergency meeting call.";
                break;
            case SurveyorRole:
                s.Sectors.Add(Sector(player.transform.position));
                s.Progress = s.Sectors.Count;
                message = $"Survey updated: {s.Progress} unique sectors visited.";
                break;
            case CourierRole:
                message = $"Delivery ledger: {s.Tasks} tasks delivered.";
                break;
            case ArchivistRole:
                message = $"Archive contains {s.Ledger.Count} recorded events.";
                break;
            case BeaconistRole:
                if (s.Tasks >= 2) PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Beacon has {s.Tasks / 2} charged signals.";
                break;
            case MediatorRole:
                if (s.Tasks >= 3) PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Mediation progress: {s.Tasks}/3 tasks.";
                break;
            case CalibratorRole:
                if (s.Streak >= 2) PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Calibration streak: {s.Streak} matching task types.";
                break;
            case SignalwrightRole:
                if (s.Chain >= 3) PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Signal chain: {s.Chain} consecutive different task types.";
                break;
            case TimekeeperRole:
            case TimeCapsuleRole:
                var snapshot = TakeSnapshot(player.PlayerId);
                message = $"Checkpoint comparison: {snapshot} tasks completed since the last snapshot.";
                break;
            case CounterfeiterRole:
                s.Progress++;
                s.Ledger.Add("Forged task-board entry");
                message = $"Decoy board entry planted in your private ledger. Entries: {s.Progress}.";
                break;
            case EchoistRole:
                s.Progress++;
                s.Ledger.Add($"Echo cue #{s.Progress}");
                message = $"Echo cue emitted and logged. Cues: {s.Progress}.";
                break;
            case BlackoutArtistRole:
                s.Progress++;
                foreach (var sprite in player.cosmetics.transform.GetComponentsInChildren<SpriteRenderer>())
                    sprite.color = new Color(0.35f, 0.35f, 0.35f, 1f);
                message = $"Display blackout applied. Patterns: {s.Progress}.";
                break;
            case BrokerRole:
                s.Progress++;
                PolusRoleEvents.QueueBonusVote(player.PlayerId);
                message = $"Trade completed. Exchanges: {s.Progress}.";
                break;
            case DecoyistRole:
                var decoy = player.GetClosestPlayer(true, 2.5f);
                if (decoy != null) player.NetTransform.RpcSnapTo(decoy.transform.position);
                s.Progress++;
                message = decoy == null ? "No nearby decoy destination found." : "Decoy reposition completed.";
                break;
            case LedgeristRole:
                var delayedTasks = TakeSnapshot(player.PlayerId);
                s.Progress++;
                message = $"Ledger refresh checkpoint stored. {delayedTasks} task entries since the last checkpoint; windows: {s.Progress}.";
                break;
            case SwitchboarderRole:
                var other = player.GetClosestPlayer(true, 2.5f);
                if (other != null)
                {
                    var old = player.transform.position;
                    player.NetTransform.RpcSnapTo(other.transform.position);
                    other.NetTransform.RpcSnapTo(old);
                }
                s.Progress++;
                message = other == null ? "No nearby player to switch with." : "Positions switched.";
                break;
            case WeathermakerRole:
                s.Progress++;
                player.cosmetics.currentBodySprite.BodySprite.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f);
                message = $"Ambient palette changed. Changes: {s.Progress}.";
                break;
            case MisdirectorRole:
                s.Progress++;
                if (s.HasLastPosition) player.NetTransform.RpcSnapTo(s.LastPosition);
                message = s.HasLastPosition ? "Misdirection shift returned you to your last recorded task location." : "No recorded location is available yet.";
                break;
            case ScramblerRole:
                s.Progress++;
                s.Ledger.Reverse();
                message = $"Your event ledger was scrambled. Scrambles: {s.Progress}; entries reordered: {s.Ledger.Count}.";
                break;
            case ParasiticSignalRole:
                var tagged = player.GetClosestPlayer(true, 2.5f);
                if (tagged != null)
                {
                    tagged.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.magenta));
                    LastTaggedPlayer = tagged;
                }
                s.Progress++;
                message = tagged == null ? "Move closer to a player to attach a signal." : $"Signal attached to {tagged.Data.PlayerName}.";
                break;
            case JigsawRole:
            case PilgrimRole:
            case CuratorRole:
            case WayfinderRole:
            case CollectorRole:
            case MetronomeRole:
            case CartoucheRole:
            case LighthouseRole:
            case WeathervaneRole:
            case PuzzlewrightRole:
            case NomadRole:
                message = $"{custom.RoleName} objective: {s.Progress}/{((PolusNeutralRole)player.Data.Role).ObjectiveTaskGoal}. Complete your role-specific task pattern.";
                break;
            default:
                return false;
        }

        Helpers.CreateAndShowNotification(message, custom.RoleColor, new Vector3(0f, 2f, -50f));
        return true;
    }

    public static void EndTimedEffects()
    {
        if (LastTaggedPlayer != null)
            LastTaggedPlayer.cosmetics.SetOutline(false, new Il2CppSystem.Nullable<Color>(Color.magenta));
        LastTaggedPlayer = null;
    }

    public static void RecordMeeting(PlayerControl player)
    {
        if (player?.Data == null) return;
        var s = Get(player.PlayerId);
        s.Ledger.Add("Meeting");
        if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
        if (player.Data.Role is ArchivistRole) s.Progress = s.Ledger.Count;
    }
}

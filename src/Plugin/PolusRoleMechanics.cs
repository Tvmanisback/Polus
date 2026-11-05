using System.Collections.Concurrent;
using UnityEngine;
using Polus.Plugin.Roles;

namespace Polus.Plugin;

public static class PolusRoleMechanics
{
    private sealed class State
    {
        public int Tasks, Progress, Snapshot, Streak, Chain;
        public float LastTaskAt;
        public string LastType = "";
        public readonly HashSet<string> Types = new();
        public readonly HashSet<string> Sectors = new();
        public readonly List<string> Ledger = new();
    }

    private static readonly ConcurrentDictionary<byte, State> States = new();
    public static void Reset() => States.Clear();
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
        s.Types.Add(type);
        s.Sectors.Add(Sector(player.transform.position));
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
            case JigsawRole:
            case CollectorRole: s.Progress = s.Types.Count; break;
            case PilgrimRole: s.Progress = s.LastType != type ? s.Progress + 1 : 0; break;
            case CuratorRole: s.Progress = s.Types.Count + Math.Max(0, s.Sectors.Count - 1); break;
            case WayfinderRole:
            case NomadRole: s.Progress = s.Sectors.Count; break;
            case MetronomeRole: s.Progress = gap >= 3f && gap <= 15f ? s.Progress + 1 : 1; break;
            case CartoucheRole:
            case WeathervaneRole: s.Progress = s.Chain; break;
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

    public static void RecordMeeting(PlayerControl player)
    {
        if (player?.Data == null) return;
        var s = Get(player.PlayerId);
        s.Ledger.Add("Meeting");
        if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
        if (player.Data.Role is ArchivistRole) s.Progress = s.Ledger.Count;
    }
}

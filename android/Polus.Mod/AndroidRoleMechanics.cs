using System.Collections.Concurrent;
using UnityEngine;

namespace Polus.AndroidMod;

internal static class AndroidRoleMechanics
{
    private sealed class PlayerState
    {
        public int Tasks;
        public int Snapshot;
        public int Progress;
        public int Streak;
        public string LastTask = "";
        public readonly HashSet<string> TaskTypes = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sectors = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Ledger = new();
        public Vector3 LastMeetingPosition;
        public bool HasMeetingPosition;
        public bool MeetingAbilityUsed;
    }

    private static readonly ConcurrentDictionary<byte, PlayerState> States = new();
    private static PlayerState State(byte id) => States.GetOrAdd(id, _ => new PlayerState());

    public static void Reset()
    {
        States.Clear();
    }

    public static void RecordTask(byte playerId, Vector3 position, string taskType)
    {
        if (string.IsNullOrWhiteSpace(taskType)) taskType = "task";
        var s = State(playerId);
        s.Tasks++;
        s.TaskTypes.Add(taskType);
        s.Sectors.Add(Sector(position));
        s.Streak = string.Equals(s.LastTask, taskType, StringComparison.OrdinalIgnoreCase) ? s.Streak + 1 : 1;
        s.LastTask = taskType;
        s.Ledger.Add(taskType);
        if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
    }

    public static string GetProgress(byte id, string roleId)
    {
        var s = State(id);
        return roleId switch
        {
            "cartographer" or "collector" => $"{s.Sectors.Count} sectors",
            "auditor" or "timekeeper" or "timecapsule" => $"{s.Tasks - s.Snapshot} tasks since checkpoint",
            "relay" or "beaconist" or "lighthouse" => $"{s.Tasks / 2} signals",
            "surveyor" or "jigsaw" => $"{s.TaskTypes.Count} task types",
            "courier" => $"{s.Tasks} deliveries",
            "archivist" => $"{s.Ledger.Count} log entries",
            "mediator" => $"{s.Tasks}/3 tasks",
            "calibrator" => $"{s.Streak} task streak",
            "signalwright" => $"{(s.Streak == 1 ? 1 : 0)} signal chain",
            "curator" => $"{s.TaskTypes.Count * s.Sectors.Count} catalog pairs (estimate)",
            "pilgrim" or "puzzlewright" => $"{s.Progress} pattern steps",
            _ => $"{s.Progress} activations"
        };
    }

    public static string Activate(string roleId, byte playerId, Vector3 position)
    {
        var s = State(playerId);
        switch (roleId)
        {
            case "cartographer":
            case "surveyor":
            case "collector":
                s.Sectors.Add(Sector(position));
                s.Progress = s.Sectors.Count;
                return $"Survey recorded: {s.Progress} distinct map sectors.";
            case "auditor":
                var audited = s.Tasks - s.Snapshot;
                s.Snapshot = s.Tasks;
                return $"Audit complete: {audited} tasks since the previous checkpoint.";
            case "relay":
                return $"Decoded signals: {s.Tasks / 2}. Complete two tasks per signal.";
            case "quartermaster":
                return s.Tasks >= 4
                    ? "Meeting call unlocked. Use the Emergency button."
                    : $"Meeting call unlocks after four tasks ({s.Tasks}/4).";
            case "courier":
                return $"Delivery ledger: {s.Tasks} completed tasks.";
            case "archivist":
                return $"Archive: {s.Ledger.Count} recent task events.";
            case "beaconist":
                return $"Beacon charge: {s.Tasks / 2} signals.";
            case "mediator":
                return $"Meeting preparation: {s.Tasks}/3 completed tasks.";
            case "calibrator":
                return $"Calibration streak: {s.Streak}.";
            case "signalwright":
                return $"Signal streak: {s.Streak}.";
            case "timekeeper":
            case "timecapsule":
            case "ledgerist":
                var delta = s.Tasks - s.Snapshot;
                s.Snapshot = s.Tasks;
                s.Progress++;
                return $"Checkpoint stored: {delta} tasks since the last snapshot.";
            case "counterfeiter":
                s.Progress++;
                s.Ledger.Add("Decoy task-board note");
                return $"Decoy entry added to your private ledger ({s.Progress}).";
            case "echoist":
                s.Progress++;
                s.Ledger.Add($"Echo cue {s.Progress}");
                return $"Echo cue recorded ({s.Progress}).";
            case "blackout_artist":
                s.Progress++;
                return "Blackout marker activated for your Polus status panel.";
            case "broker":
                s.Progress++;
                return $"Trade log updated ({s.Progress}).";
            case "decoyist":
                return "Decoy reposition is unavailable until the Android network adapter is connected.";
            case "switchboarder":
                return "Position exchange is unavailable until the Android network adapter is connected.";
            case "weathermaker":
                s.Progress++;
                return $"Weather signal changed ({s.Progress}).";
            case "misdirector":
                return s.HasMeetingPosition
                    ? "Meeting checkpoint found. Position restore requires the network adapter."
                    : "No meeting checkpoint has been recorded yet.";
            case "scrambler":
                s.Ledger.Reverse();
                s.Progress++;
                return $"Your private event ledger was scrambled ({s.Progress}).";
            case "parasitic_signal":
                return "Signal target marking requires the Android player-outline adapter.";
            case "jigsaw":
            case "curator":
            case "wayfinder":
            case "metronome":
            case "cartouche":
            case "weathervane":
            case "puzzlewright":
            case "nomad":
                s.Progress++;
                return $"{roleId}: objective progress {s.Progress}. Complete distinct task patterns to advance.";
            default:
                return "No Android ability is registered for this role yet.";
        }
    }

    public static void OnMeeting(byte playerId, Vector3 position, string roleId)
    {
        var s = State(playerId);
        s.LastMeetingPosition = position;
        s.HasMeetingPosition = true;
        s.MeetingAbilityUsed = false;
        s.Ledger.Add("Meeting");
        if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
        if (roleId is "auditor" or "beaconist" or "timecapsule") s.Snapshot = s.Tasks;
    }

    public static bool HasMeetingAbility(string roleId, byte playerId)
    {
        var s = State(playerId);
        return roleId is "auditor" or "mediator" or "beaconist" or "calibrator" or "signalwright" or "broker"
            && !s.MeetingAbilityUsed;
    }

    public static string UseMeetingAbility(string roleId, byte playerId)
    {
        var s = State(playerId);
        if (!HasMeetingAbility(roleId, playerId))
            return "No meeting ability is available.";
        s.MeetingAbilityUsed = true;
        return roleId switch
        {
            "auditor" => $"Audit report: {s.Tasks} total tasks completed.",
            "mediator" => $"Mediation report: {s.Tasks} tasks completed.",
            "beaconist" => $"Beacon report: {s.Tasks / 2} signals charged.",
            "calibrator" => $"Calibration report: current streak {s.Streak}.",
            "signalwright" => $"Signal report: current streak {s.Streak}.",
            "broker" => $"Trade report: {s.Progress} trades recorded.",
            _ => "Meeting report recorded."
        };
    }

    private static string Sector(Vector3 position) =>
        $"{Mathf.FloorToInt(position.x / 4f)}:{Mathf.FloorToInt(position.y / 4f)}";
}

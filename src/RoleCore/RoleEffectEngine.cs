namespace Polus.RoleCore;

/// <summary>
/// Shared, host-authoritative rules for Polus roles. This layer decides what a role
/// action means; PC and Android adapters are responsible for invoking supported
/// Among Us game actions/RPCs. It deliberately does not fabricate protocol packets.
/// </summary>
public sealed class RoleEffectEngine
{
    private sealed class State
    {
        public string RoleId = "";
        public int Tasks;
        public int Progress;
        public int Snapshot;
        public int Streak;
        public int Chain;
        public string LastTask = "";
        public string LastSector = "";
        public string LastPosition = "";
        public float LastX;
        public float LastY;
        public readonly HashSet<string> TaskTypes = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sectors = new(StringComparer.Ordinal);
        public readonly HashSet<string> TypeSectorPairs = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> TaskPairs = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> Ledger = new();
        public bool MeetingActionUsed;
    }

    private readonly object gate = new();
    private readonly Dictionary<byte, State> states = new();
    private readonly Dictionary<byte, DateTimeOffset> cooldowns = new();
    private readonly bool isHost;
    private int meetingNumber;

    public RoleEffectEngine(bool isHost) => this.isHost = isHost;
    public int MeetingNumber { get { lock (gate) return meetingNumber; } }

    public void BeginMatch(IEnumerable<RoleAssignment> assignments)
    {
        EnsureHost();
        ArgumentNullException.ThrowIfNull(assignments);
        lock (gate)
        {
            states.Clear();
            cooldowns.Clear();
            meetingNumber = 0;
            foreach (var assignment in assignments)
            {
                if (states.ContainsKey(assignment.PlayerId))
                    throw new InvalidOperationException("A player cannot receive multiple primary roles.");
                states.Add(assignment.PlayerId, new State { RoleId = Normalize(assignment.Role.Id) });
            }
        }
    }

    public RoleActionResult RecordTask(byte playerId, string taskType, float x, float y)
    {
        EnsureHost();
        lock (gate)
        {
            if (!states.TryGetValue(playerId, out var s))
                return Reject("unassigned", "Player has no Polus role.");
            taskType = string.IsNullOrWhiteSpace(taskType) ? "task" : taskType.Trim();
            var sector = $"{MathF.Floor(x / 4f)}:{MathF.Floor(y / 4f)}";
            var differentTask = s.LastTask.Length == 0 || !s.LastTask.Equals(taskType, StringComparison.OrdinalIgnoreCase);
            s.Tasks++;
            s.TaskTypes.Add(taskType);
            s.Sectors.Add(sector);
            s.TypeSectorPairs.Add(taskType + "@" + sector);
            if (s.LastTask.Length > 0 && !s.LastTask.Equals(taskType, StringComparison.OrdinalIgnoreCase))
            {
                var pair = string.Compare(s.LastTask, taskType, StringComparison.OrdinalIgnoreCase) < 0
                    ? s.LastTask + "|" + taskType : taskType + "|" + s.LastTask;
                s.TaskPairs.Add(pair);
                s.Chain++;
            }
            else s.Chain = 0;
            s.Streak = s.LastTask.Equals(taskType, StringComparison.OrdinalIgnoreCase) ? s.Streak + 1 : 1;
            if (s.LastSector.Length > 0 && !s.LastSector.Equals(sector, StringComparison.Ordinal))
                s.Progress++;
            s.LastSector = sector;
            s.LastTask = taskType;
            s.LastPosition = sector;
            s.LastX = x;
            s.LastY = y;
            s.Ledger.Add(taskType);
            if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);

            s.Progress = s.RoleId switch
            {
                "cartographer" or "collector" => s.Sectors.Count,
                "surveyor" or "jigsaw" => s.TaskTypes.Count,
                "relay" or "beaconist" or "lighthouse" => s.Tasks / 2,
                "courier" => s.Tasks,
                "archivist" => s.Ledger.Count,
                "mediator" => s.Tasks / 3,
                "calibrator" => s.Streak,
                "signalwright" => s.Chain,
                "timekeeper" or "timecapsule" => s.Tasks - s.Snapshot,
                "curator" => s.TypeSectorPairs.Count,
                "pilgrim" => differentTask ? s.Progress + 1 : 0,
                "wayfinder" => s.Sectors.Count,
                "metronome" => s.Tasks % 3 == 0 && s.Chain >= 2 ? s.Progress + 1 : s.Progress,
                "cartouche" => s.TaskPairs.Count,
                "weathervane" => s.Chain / 2,
                "puzzlewright" => differentTask ? s.Progress + 1 : 0,
                "nomad" => Math.Max(0, s.Tasks - s.Sectors.Count),
                _ => s.Progress
            };
            return new RoleActionResult(true, "task_recorded", $"Recorded {s.Tasks} tasks; role progress is {s.Progress}.");
        }
    }

    public RoleActionResult TryUseAbility(byte playerId, DateTimeOffset now, int cooldownSeconds, out RoleEffectCommand command)
    {
        EnsureHost();
        lock (gate)
        {
            command = RoleEffectCommand.None;
            if (!states.TryGetValue(playerId, out var s)) return Reject("unassigned", "Player has no Polus role.");
            if (cooldowns.TryGetValue(playerId, out var until) && now < until)
                return Reject("cooldown", "Role ability is cooling down.");

            command = BuildAbility(s);
            if (command.Kind == "none") return Reject("unimplemented", "No action is registered for this role.");
            cooldowns[playerId] = now.AddSeconds(Math.Clamp(cooldownSeconds, 0, 600));
            return new RoleActionResult(true, "accepted", command.Message);
        }
    }

    public RoleActionResult TryUseMeetingAbility(byte playerId, out RoleEffectCommand command)
    {
        EnsureHost();
        lock (gate)
        {
            command = RoleEffectCommand.None;
            if (!states.TryGetValue(playerId, out var s)) return Reject("unassigned", "Player has no Polus role.");
            if (s.MeetingActionUsed) return Reject("already_used", "Meeting ability already used this meeting.");
            command = s.RoleId switch
            {
                "auditor" => Cmd("meeting_audit", $"Audit: {s.Tasks - s.Snapshot} tasks since checkpoint.", s.Tasks - s.Snapshot),
                "mediator" when s.Tasks >= 3 => Cmd("meeting_bonus_vote", "Mediator earned a bonus vote.", 1),
                "beaconist" when s.Tasks >= 2 => Cmd("meeting_bonus_vote", "Beaconist earned a bonus vote.", 1),
                "calibrator" when s.Streak >= 2 => Cmd("meeting_bonus_vote", "Calibrator earned a bonus vote.", 1),
                "signalwright" when s.Chain >= 2 => Cmd("meeting_bonus_vote", "Signalwright earned a bonus vote.", 1),
                "broker" => Cmd("meeting_bonus_vote", "Broker earned a bonus vote.", 1),
                _ => RoleEffectCommand.None
            };
            if (command.Kind == "none") return Reject("not_ready", "This role has no meeting action ready.");
            s.MeetingActionUsed = true;
            return new RoleActionResult(true, "accepted", command.Message);
        }
    }

    public void BeginMeeting()
    {
        EnsureHost();
        lock (gate)
        {
            meetingNumber++;
            foreach (var s in states.Values)
            {
                s.MeetingActionUsed = false;
                s.Ledger.Add("Meeting");
                if (s.Ledger.Count > 40) s.Ledger.RemoveAt(0);
                if (s.RoleId is "auditor" or "beaconist" or "timecapsule") s.Snapshot = s.Tasks;
            }
        }
    }

    public int GetProgress(byte playerId)
    {
        lock (gate) return states.TryGetValue(playerId, out var s) ? s.Progress : 0;
    }

    public string? GetRoleId(byte playerId)
    {
        lock (gate) return states.TryGetValue(playerId, out var s) ? s.RoleId : null;
    }

    public void EndMatch()
    {
        EnsureHost();
        lock (gate) { states.Clear(); cooldowns.Clear(); meetingNumber = 0; }
    }

    private static RoleEffectCommand BuildAbility(State s)
    {
        switch (s.RoleId)
        {
            case "cartographer": case "surveyor": case "collector":
                return Cmd("record_map_sector", $"Map updated: {s.Sectors.Count} unique sectors.", s.Sectors.Count);
            case "auditor":
                var audited = s.Tasks - s.Snapshot; s.Snapshot = s.Tasks;
                return Cmd("audit_checkpoint", $"Audit checkpoint: {audited} tasks.", audited);
            case "relay": return Cmd("decode_signal", $"Decoded signals: {s.Tasks / 2}.", s.Tasks / 2);
            case "quartermaster": return s.Tasks >= 4 ? Cmd("emergency_meeting", "Quartermaster emergency call.", 1) : RoleEffectCommand.None;
            case "courier": return Cmd("delivery_ledger", $"Deliveries: {s.Tasks}.", s.Tasks);
            case "archivist": return Cmd("event_ledger", $"Archive entries: {s.Ledger.Count}.", s.Ledger.Count);
            case "beaconist": return Cmd("beacon_status", $"Beacon charge: {s.Tasks / 2}.", s.Tasks / 2);
            case "mediator": return Cmd("mediation_status", $"Mediation progress: {s.Tasks}/3 tasks.", s.Tasks);
            case "calibrator": return Cmd("calibration_status", $"Matching task streak: {s.Streak}.", s.Streak);
            case "signalwright": return Cmd("signal_chain_status", $"Different-task chain: {s.Chain}.", s.Chain);
            case "timekeeper": case "timecapsule": case "ledgerist":
                var delta = s.Tasks - s.Snapshot; s.Snapshot = s.Tasks;
                return Cmd("store_checkpoint", $"Checkpoint stored: {delta} tasks since snapshot.", delta);
            case "counterfeiter": s.Progress++; s.Ledger.Add("Forged board marker"); return Cmd("private_decoy_entry", "Private decoy entry recorded.", s.Progress);
            case "echoist": s.Progress++; return Cmd("emit_signal_cue", "Signal cue emitted.", s.Progress);
            case "blackout_artist": s.Progress++; return Cmd("temporary_self_visual", "Temporary self-only visual effect requested.", 8);
            case "broker": s.Progress++; return Cmd("record_trade", "Trade recorded.", s.Progress);
            case "decoyist": s.Progress++; return Cmd("snap_to_nearest_player", "Decoy reposition activated.", s.Progress);
            case "switchboarder": s.Progress++; return Cmd("swap_with_nearest_player", "Position exchange activated.", s.Progress);
            case "weathermaker": s.Progress++; return Cmd("temporary_self_palette", "Temporary palette change requested.", 8);
            case "misdirector": return string.IsNullOrEmpty(s.LastPosition) ? RoleEffectCommand.None : new RoleEffectCommand("return_to_last_sector", "Returned to the last task checkpoint.", 1, s.LastX, s.LastY);
            case "scrambler": s.Ledger.Reverse(); s.Progress++; return Cmd("scramble_private_ledger", "Private event ledger reordered.", s.Progress);
            case "parasitic_signal": s.Progress++; return Cmd("mark_nearest_player", "Signal marker attached.", 8);
            case "jigsaw": case "pilgrim": case "curator": case "wayfinder": case "metronome":
            case "cartouche": case "lighthouse": case "weathervane": case "puzzlewright": case "nomad":
                return Cmd("show_objective_progress", $"{s.RoleId}: objective progress {s.Progress}.", s.Progress);
            default: return RoleEffectCommand.None;
        }
    }

    private static RoleEffectCommand Cmd(string kind, string message, int value) => new(kind, message, value);
    private static string Normalize(string id)
    {
        var normalized = id.Trim().Replace('-', '_').ToLowerInvariant();
        return normalized == "time_capsule" ? "timecapsule" : normalized;
    }
    private void EnsureHost() { if (!isHost) throw new InvalidOperationException("Only the host may mutate role effects."); }
    private static RoleActionResult Reject(string code, string message) => new(false, code, message);
}

/// <summary>Semantic effect request for a platform adapter; never a raw network packet.</summary>
public sealed record RoleEffectCommand(string Kind, string Message, int Value, float X = 0f, float Y = 0f)
{
    public static RoleEffectCommand None { get; } = new("none", "", 0);
}

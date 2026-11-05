namespace Polus.RoleCore;

/// <summary>
/// Platform-neutral host role state shared by the PC and Android game adapters.
/// Adapters must validate game events and perform the corresponding Among Us RPCs.
/// </summary>
public sealed class HostRoleEngine
{
    private sealed class PlayerState
    {
        public string RoleId = "";
        public int Tasks;
        public int Progress;
        public int Snapshot;
        public int Streak;
        public string LastTask = "";
        public DateTimeOffset CooldownUntil;
        public readonly HashSet<string> TaskTypes = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Sectors = new(StringComparer.Ordinal);
        public readonly List<string> EventLog = new();
        public bool MeetingAbilityUsed;
    }

    private readonly object _gate = new();
    private readonly Dictionary<byte, PlayerState> _players = new();
    private readonly List<(byte Voter, byte? Target)> _votes = new();
    private int _meeting;
    private bool _running;

    public bool IsRunning { get { lock (_gate) return _running; } }
    public int MeetingNumber { get { lock (_gate) return _meeting; } }

    public void StartMatch(IEnumerable<RoleAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        lock (_gate)
        {
            _players.Clear();
            _votes.Clear();
            _meeting = 0;
            foreach (var assignment in assignments)
            {
                if (_players.ContainsKey(assignment.PlayerId))
                    throw new InvalidOperationException("A player cannot receive multiple primary roles.");
                _players.Add(assignment.PlayerId, new PlayerState { RoleId = assignment.Role.Id });
            }
            _running = true;
        }
    }

    public bool TryGetRole(byte playerId, out string roleId)
    {
        lock (_gate)
        {
            if (_running && _players.TryGetValue(playerId, out var state))
            {
                roleId = state.RoleId;
                return true;
            }
            roleId = "";
            return false;
        }
    }

    public ObjectiveProgress RecordTask(byte playerId, string taskType, float x, float y)
    {
        if (string.IsNullOrWhiteSpace(taskType)) taskType = "task";
        lock (_gate)
        {
            var state = GetRunningPlayer(playerId);
            state.Tasks++;
            state.TaskTypes.Add(taskType);
            state.Sectors.Add($"{MathF.Floor(x / 4f)}:{MathF.Floor(y / 4f)}");
            state.Streak = string.Equals(state.LastTask, taskType, StringComparison.OrdinalIgnoreCase)
                ? state.Streak + 1 : 1;
            state.LastTask = taskType;
            state.Progress = state.RoleId switch
            {
                "cartographer" or "collector" => state.Sectors.Count,
                "surveyor" or "jigsaw" => state.TaskTypes.Count,
                "relay" or "beaconist" or "lighthouse" => state.Tasks / 2,
                "courier" => state.Tasks,
                "archivist" => state.EventLog.Count,
                "mediator" => state.Tasks / 3,
                "calibrator" => state.Streak,
                "signalwright" => state.Streak,
                "timekeeper" or "timecapsule" => state.Tasks - state.Snapshot,
                _ => state.Progress
            };
            state.EventLog.Add(taskType);
            if (state.EventLog.Count > 40) state.EventLog.RemoveAt(0);
            return new ObjectiveProgress(playerId, state.RoleId, "tasks", state.Tasks, 0, false);
        }
    }

    public RoleActionResult TryUseAbility(byte playerId, DateTimeOffset now, int cooldownSeconds)
    {
        lock (_gate)
        {
            if (!_running || !_players.TryGetValue(playerId, out var state))
                return new RoleActionResult(false, "no_role", "No active Polus role is assigned.");
            if (now < state.CooldownUntil)
                return new RoleActionResult(false, "cooldown", "The role ability is cooling down.");
            state.CooldownUntil = now.AddSeconds(Math.Clamp(cooldownSeconds, 0, 600));
            return new RoleActionResult(true, "accepted", "Ability accepted by the host role engine.");
        }
    }

    public void BeginMeeting()
    {
        lock (_gate)
        {
            if (!_running) return;
            _meeting++;
            foreach (var state in _players.Values)
            {
                state.MeetingAbilityUsed = false;
                state.EventLog.Add("Meeting");
                if (state.EventLog.Count > 40) state.EventLog.RemoveAt(0);
                if (state.RoleId is "auditor" or "beaconist" or "timecapsule")
                    state.Snapshot = state.Tasks;
            }
        }
    }

    public bool TryUseMeetingAbility(byte playerId)
    {
        lock (_gate)
        {
            if (!_running || !_players.TryGetValue(playerId, out var state) || state.MeetingAbilityUsed)
                return false;
            state.MeetingAbilityUsed = true;
            return true;
        }
    }

    public void RecordVote(byte voter, byte? target)
    {
        lock (_gate)
        {
            if (_running) _votes.Add((voter, target));
        }
    }

    public int GetProgress(byte playerId)
    {
        lock (_gate) return _players.TryGetValue(playerId, out var state) ? state.Progress : 0;
    }

    public void EndMatch()
    {
        lock (_gate)
        {
            _running = false;
            _players.Clear();
            _votes.Clear();
            _meeting = 0;
        }
    }

    private PlayerState GetRunningPlayer(byte playerId)
    {
        if (!_running || !_players.TryGetValue(playerId, out var state))
            throw new InvalidOperationException("Player has no active host role.");
        return state;
    }
}

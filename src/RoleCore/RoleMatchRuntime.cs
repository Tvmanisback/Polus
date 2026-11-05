using System.Security.Cryptography;

namespace Polus.RoleCore;

public sealed record PlayerSlot(byte PlayerId, bool IsAlive = true);
public sealed record RoleAssignment(byte PlayerId, RoleDefinition Role);
public sealed record RoleActionResult(bool Accepted, string Code, string Message);
public sealed record ObjectiveProgress(byte PlayerId, string RoleId, string ObjectiveId, int Current, int Required, bool Completed);

/// <summary>
/// Host-owned match state. The game adapter must validate every event before forwarding it here.
/// This class does not itself hook Among Us or transmit network messages.
/// </summary>
public sealed class RoleMatchRuntime
{
    private readonly object gate = new();
    private readonly Dictionary<byte, RoleDefinition> assignments = new();
    private readonly Dictionary<byte, DateTimeOffset> cooldownEnds = new();
    private readonly Dictionary<(byte PlayerId, string ObjectiveId), int> objectiveProgress = new();
    private readonly HashSet<(byte PlayerId, string ObjectiveId)> completedObjectives = new();
    private readonly Dictionary<byte, DateTimeOffset> lastActionAt = new();

    public bool IsHost { get; }
    public bool IsRunning { get; private set; }
    public IReadOnlyDictionary<byte, RoleDefinition> Assignments
    {
        get { lock (gate) return new Dictionary<byte, RoleDefinition>(assignments); }
    }

    public RoleMatchRuntime(bool isHost) => IsHost = isHost;

    public IReadOnlyList<RoleAssignment> StartMatch(
        IReadOnlyList<PlayerSlot> players,
        IReadOnlyList<RoleDefinition> catalog,
        HostSettings settings,
        int impostorCount,
        int neutralCount,
        int? seed = null)
    {
        EnsureHost();
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(catalog);
        var validated = HostSettingsValidator.Validate(settings);

        lock (gate)
        {
            if (IsRunning) throw new InvalidOperationException("A match is already running.");
            if (players.Count < 4) throw new ArgumentException("At least four players are required.");
            if (players.Select(p => p.PlayerId).Distinct().Count() != players.Count)
                throw new ArgumentException("Player IDs must be unique.");
            if (players.Any(p => !p.IsAlive))
                throw new InvalidOperationException("Dead players cannot be included in initial role assignment.");
            if (impostorCount < 1 || neutralCount < 0 || impostorCount + neutralCount > players.Count)
                throw new ArgumentOutOfRangeException(nameof(impostorCount));
            if (impostorCount + neutralCount > validated.MaxRolesPerGame)
                throw new InvalidOperationException("Requested team roles exceed MaxRolesPerGame.");

            var random = seed.HasValue ? new Random(seed.Value) : new Random(RandomNumberGenerator.GetInt32(int.MaxValue));
            var enabled = catalog
                .Where(r => validated.EnabledRoles.TryGetValue(r.Id, out var on) && on)
                .Where(r => r.SpawnChance > 0)
                .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var impostors = enabled.Where(r => r.Team == RoleTeam.Impostor).ToList();
            var neutrals = validated.AllowNeutralRoles
                ? enabled.Where(r => r.Team == RoleTeam.Neutral).ToList()
                : new List<RoleDefinition>();
            var crewmates = enabled.Where(r => r.Team == RoleTeam.Crewmate).ToList();

            var selected = new List<(PlayerSlot Player, RoleDefinition Role)>();
            AddWeightedTeam(players, impostorCount, impostors, selected, random);
            AddWeightedTeam(players, neutralCount, neutrals, selected, random);
            var crewSlots = crewmates.Count == 0 ? 0 : Math.Min(
                Math.Max(0, validated.MaxRolesPerGame - selected.Count),
                players.Count - selected.Count);
            AddWeightedTeam(players, crewSlots, crewmates, selected, random);

            assignments.Clear();
            cooldownEnds.Clear();
            objectiveProgress.Clear();
            completedObjectives.Clear();
            lastActionAt.Clear();
            foreach (var item in selected)
                assignments.Add(item.Player.PlayerId, item.Role);

            IsRunning = true;
            return selected.Select(x => new RoleAssignment(x.Player.PlayerId, x.Role)).ToArray();
        }
    }

    private static void AddWeightedTeam(
        IReadOnlyList<PlayerSlot> players,
        int count,
        List<RoleDefinition> pool,
        List<(PlayerSlot Player, RoleDefinition Role)> result,
        Random random)
    {
        if (count == 0) return;
        if (pool.Count < count)
            throw new InvalidOperationException($"Not enough enabled roles in the {pool.FirstOrDefault()?.Team.ToString() ?? "requested"} pool.");

        var availablePlayers = players.Where(p => result.All(x => x.Player.PlayerId != p.PlayerId)).ToList();
        if (availablePlayers.Count < count) throw new InvalidOperationException("Not enough unassigned players.");

        var remaining = pool.ToList();
        for (var i = 0; i < count; i++)
        {
            var totalWeight = remaining.Sum(r => Math.Max(1, r.SpawnChance));
            var roll = random.Next(totalWeight);
            var selectedIndex = 0;
            for (; selectedIndex < remaining.Count - 1; selectedIndex++)
            {
                roll -= Math.Max(1, remaining[selectedIndex].SpawnChance);
                if (roll < 0) break;
            }

            var role = remaining[selectedIndex];
            remaining.RemoveAt(selectedIndex);
            result.Add((availablePlayers[i], role));
        }
    }

    public RoleActionResult TryUseAbility(byte playerId, DateTimeOffset now)
    {
        EnsureHost();
        lock (gate)
        {
            if (!IsRunning) return Reject("match_not_running", "The match is not running.");
            if (!assignments.TryGetValue(playerId, out var role))
                return Reject("no_role", "This player has no Polus role.");
            if (lastActionAt.TryGetValue(playerId, out var previous) && now < previous)
                return Reject("invalid_time", "Action timestamps must be monotonic.");
            if (cooldownEnds.TryGetValue(playerId, out var until) && now < until)
                return Reject("cooldown", $"Ability is ready at {until:O}.");

            cooldownEnds[playerId] = now.AddSeconds(Math.Clamp(role.CooldownSeconds, 0, 600));
            lastActionAt[playerId] = now;
            return new RoleActionResult(true, "accepted", "Ability request accepted; the game adapter must now execute the role-specific effect.");
        }
    }

    /// <summary>Credits a named objective only after the host adapter validates its game event.</summary>
    public ObjectiveProgress RecordObjective(byte playerId, string objectiveId, int amount, int required)
    {
        EnsureHost();
        if (string.IsNullOrWhiteSpace(objectiveId)) throw new ArgumentException("Objective ID cannot be empty.", nameof(objectiveId));
        if (amount <= 0 || required <= 0) throw new ArgumentOutOfRangeException(nameof(amount));

        lock (gate)
        {
            if (!IsRunning) throw new InvalidOperationException("No active match.");
            if (!assignments.TryGetValue(playerId, out var role))
                throw new InvalidOperationException("Player has no assigned role.");

            var key = (playerId, objectiveId);
            var current = objectiveProgress.GetValueOrDefault(key);
            if (completedObjectives.Contains(key))
                return new ObjectiveProgress(playerId, role.Id, objectiveId, current, required, true);

            current = Math.Min(required, checked(current + amount));
            objectiveProgress[key] = current;
            var done = current >= required;
            if (done) completedObjectives.Add(key);
            return new ObjectiveProgress(playerId, role.Id, objectiveId, current, required, done);
        }
    }

    public bool IsObjectiveComplete(byte playerId, string objectiveId)
    {
        lock (gate) return completedObjectives.Contains((playerId, objectiveId));
    }

    public void EndMatch()
    {
        EnsureHost();
        lock (gate)
        {
            IsRunning = false;
            assignments.Clear();
            cooldownEnds.Clear();
            objectiveProgress.Clear();
            completedObjectives.Clear();
            lastActionAt.Clear();
        }
    }

    private void EnsureHost()
    {
        if (!IsHost) throw new InvalidOperationException("Only the authoritative host may mutate match role state.");
    }

    private static RoleActionResult Reject(string code, string message) => new(false, code, message);
}

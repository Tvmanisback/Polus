using System.Collections.Concurrent;

namespace Polus.MiraApi.Android;

/// <summary>
/// Android/LemonLoader implementation of the MiraAPI-facing runtime surface used by Polus.
/// It is host-authoritative and deliberately has no Reactor client RPC transport.
/// </summary>
public sealed class MiraApi
{
    public static MiraApi Instance { get; } = new();
    private MiraApi() { }

    public MiraRoleRegistry Roles { get; } = new();
    public MiraButtonRegistry Buttons { get; } = new();
    public MiraMeetingRegistry MeetingButtons { get; } = new();
    public MiraOptionRegistry Options { get; } = new();
    public MiraEventBus Events { get; } = new();
    public MiraColorRegistry Colors { get; } = new();
    public MiraAssetRegistry Assets { get; } = new();
    public MiraKeybindRegistry Keybinds { get; } = new();
    public MiraLocalSettings LocalSettings { get; } = new();
    public MiraVotingLedger Voting { get; } = new();
    public MiraModifierRegistry Modifiers { get; } = new();
    public MiraGameEndConditionRegistry GameEndConditions { get; } = new();
    public MiraCompatibilityRegistry Compatibility { get; } = new();
    private readonly HashSet<IMiraPlugin> _plugins = new();
    private readonly object _pluginGate = new();

    public IReadOnlyCollection<IMiraPlugin> Plugins
    {
        get { lock (_pluginGate) return _plugins.ToArray(); }
    }

    public void RegisterPlugin(IMiraPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        lock (_pluginGate)
        {
            if (!_plugins.Add(plugin)) return;
        }
        try { plugin.Initialize(this); }
        catch
        {
            lock (_pluginGate) _plugins.Remove(plugin);
            throw;
        }
    }

    public void ResetMatch()
    {
        Buttons.ResetMatch();
        MeetingButtons.ResetMatch();
        Voting.Reset();
        Events.Publish(new MiraMatchResetEvent());
    }
}

public interface IMiraPlugin
{
    void Initialize(MiraApi api);
}

public sealed record MiraModifier(string Id, string Name, string Description);
public sealed record MiraGameEndCondition(string Id, string Name, Func<string?> Evaluate);
public sealed record MiraCompatibilityRule(string Id, Func<bool> IsCompatible, string Warning);

public sealed class MiraModifierRegistry
{
    private readonly Dictionary<string, MiraModifier> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<MiraModifier> All => _items.Values;
    public void Register(MiraModifier modifier) => _items[modifier.Id] = modifier;
    public bool TryGet(string id, out MiraModifier modifier) => _items.TryGetValue(id, out modifier!);
}

public sealed class MiraGameEndConditionRegistry
{
    private readonly Dictionary<string, MiraGameEndCondition> _items = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<MiraGameEndCondition> All => _items.Values;
    public void Register(MiraGameEndCondition condition) => _items[condition.Id] = condition;
    public bool TryEvaluate(out string reason)
    {
        foreach (var condition in _items.Values)
        {
            var result = condition.Evaluate();
            if (!string.IsNullOrWhiteSpace(result)) { reason = result; return true; }
        }
        reason = string.Empty;
        return false;
    }
}

public sealed class MiraCompatibilityRegistry
{
    private readonly Dictionary<string, MiraCompatibilityRule> _rules = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<MiraCompatibilityRule> All => _rules.Values;
    public void Register(MiraCompatibilityRule rule) => _rules[rule.Id] = rule;
    public IReadOnlyList<string> GetWarnings() => _rules.Values
        .Where(rule => !rule.IsCompatible())
        .Select(rule => rule.Warning)
        .ToArray();
}

public sealed record MiraRole(string Id, string Name, string Team, string Description,
    bool HostOnly = true, int CooldownSeconds = 30, int MaxUses = 1);

public sealed record MiraButton(string Id, string RoleId, string Label, int CooldownSeconds,
    int MaxUses, Func<MiraActionContext, string> OnClick);

public sealed record MiraMeetingButton(string Id, string RoleId, string Label, int MaxUses,
    Func<MiraMeetingContext, string> OnClick);

public sealed record MiraActionContext(byte PlayerId, bool IsHost, DateTimeOffset Now);
public sealed record MiraMeetingContext(byte PlayerId, bool IsHost, int MeetingNumber);
public sealed record MiraOption<T>(string Id, string Name, T DefaultValue, T? Minimum = default,
    T? Maximum = default);

public abstract record MiraEvent;
public sealed record MiraMatchResetEvent : MiraEvent;
public sealed record MiraRoleAssignedEvent(byte PlayerId, MiraRole Role) : MiraEvent;
public sealed record MiraAbilityUsedEvent(byte PlayerId, string ButtonId, string Result) : MiraEvent;
public sealed record MiraMeetingStartedEvent(int MeetingNumber) : MiraEvent;
public sealed record MiraMeetingEndedEvent(int MeetingNumber) : MiraEvent;
public sealed record MiraTaskCompletedEvent(byte PlayerId, string TaskId) : MiraEvent;
public sealed record MiraVoteRecordedEvent(byte VoterId, byte? TargetId, bool IsHost) : MiraEvent;
public sealed record MiraGameEndedEvent(string Reason) : MiraEvent;

public sealed class MiraEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _gate = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : MiraEvent
    {
        lock (_gate)
        {
            var list = _handlers.GetOrAdd(typeof(T), _ => new List<Delegate>());
            list.Add(handler);
        }
        return new Subscription(() =>
        {
            lock (_gate)
                if (_handlers.TryGetValue(typeof(T), out var list)) list.Remove(handler);
        });
    }

    public void Publish<T>(T value) where T : MiraEvent
    {
        Delegate[] handlers;
        lock (_gate)
            handlers = _handlers.TryGetValue(typeof(T), out var list) ? list.ToArray() : Array.Empty<Delegate>();
        foreach (var handler in handlers)
            try { ((Action<T>)handler)(value); } catch { /* A plugin event must not crash the game loop. */ }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

public sealed class MiraRoleRegistry
{
    private readonly Dictionary<string, MiraRole> _roles = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<MiraRole> All => _roles.Values;
    public void Register(MiraRole role) => _roles[role.Id] = role;
    public bool TryGet(string id, out MiraRole role) => _roles.TryGetValue(id, out role!);
    public void Clear() => _roles.Clear();
}

public sealed class MiraButtonRegistry
{
    private readonly Dictionary<string, MiraButton> _buttons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(byte Player, string Button), (DateTimeOffset LastUse, int Uses)> _uses = new();
    public IReadOnlyCollection<MiraButton> All => _buttons.Values;
    public void Register(MiraButton button) => _buttons[button.Id] = button;
    public bool TryUseForRole(string roleId, MiraActionContext context, out string result)
    {
        var button = _buttons.Values.FirstOrDefault(item => item.RoleId.Equals(roleId, StringComparison.OrdinalIgnoreCase));
        if (button == null) { result = "No ability button is registered for this role."; return false; }
        return TryUse(button.Id, context, out result);
    }

    public bool TryUse(string id, MiraActionContext context, out string result)
    {
        result = "Button is not registered.";
        if (!context.IsHost) { result = "Only the host can use Polus abilities."; return false; }
        if (!_buttons.TryGetValue(id, out var button)) return false;
        var key = (context.PlayerId, id);
        var state = _uses.GetValueOrDefault(key);
        if (state.Uses >= button.MaxUses) { result = "No uses remaining."; return false; }
        if (state.Uses > 0 && (context.Now - state.LastUse).TotalSeconds < button.CooldownSeconds)
        { result = "Ability is cooling down."; return false; }
        result = button.OnClick(context);
        _uses[key] = (context.Now, state.Uses + 1);
        return true;
    }
    public void ResetMatch() => _uses.Clear();
}

public sealed class MiraMeetingRegistry
{
    private readonly Dictionary<string, MiraMeetingButton> _buttons = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(byte Player, string Button, int Meeting)> _used = new();
    public IReadOnlyCollection<MiraMeetingButton> All => _buttons.Values;
    public void Register(MiraMeetingButton button) => _buttons[button.Id] = button;
    public bool TryUseForRole(string roleId, MiraMeetingContext context, out string result)
    {
        var button = _buttons.Values.FirstOrDefault(item => item.RoleId.Equals(roleId, StringComparison.OrdinalIgnoreCase));
        if (button == null) { result = "No meeting button is registered for this role."; return false; }
        return TryUse(button.Id, context, out result);
    }

    public bool TryUse(string id, MiraMeetingContext context, out string result)
    {
        result = "Meeting button is not registered.";
        if (!context.IsHost) { result = "Only the host can use Polus meeting abilities."; return false; }
        if (!_buttons.TryGetValue(id, out var button)) return false;
        if (!_used.Add((context.PlayerId, id, context.MeetingNumber))) { result = "This meeting ability was already used."; return false; }
        result = button.OnClick(context);
        return true;
    }
    public void ResetMatch() => _used.Clear();
}

public sealed class MiraOptionRegistry
{
    private readonly Dictionary<string, object> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public void Register<T>(MiraOption<T> option)
    {
        _definitions[option.Id] = option;
        if (!_values.ContainsKey(option.Id)) _values[option.Id] = option.DefaultValue;
    }
    public T Get<T>(string id, T fallback = default!) =>
        _values.TryGetValue(id, out var value) && value is T typed ? typed : fallback;
    public void Set<T>(string id, T value)
    {
        if (_definitions.ContainsKey(id)) _values[id] = value;
    }
    public IReadOnlyDictionary<string, object?> Values => _values;
}

public sealed class MiraColorRegistry
{
    private readonly Dictionary<string, (byte R, byte G, byte B)> _colors = new(StringComparer.OrdinalIgnoreCase);
    public void Register(string id, byte r, byte g, byte b) => _colors[id] = (r, g, b);
    public bool TryGet(string id, out (byte R, byte G, byte B) color) => _colors.TryGetValue(id, out color);
}

public sealed class MiraAssetRegistry
{
    private readonly Dictionary<string, string> _assets = new(StringComparer.OrdinalIgnoreCase);
    public void Register(string id, string embeddedResourceName) => _assets[id] = embeddedResourceName;
    public bool TryGet(string id, out string resourceName) => _assets.TryGetValue(id, out resourceName!);
}

public sealed class MiraKeybindRegistry
{
    private readonly Dictionary<string, string> _keys = new(StringComparer.OrdinalIgnoreCase);
    public void Register(string id, string defaultKey) => _keys.TryAdd(id, defaultKey);
    public bool TryGet(string id, out string key) => _keys.TryGetValue(id, out key!);
}

public sealed class MiraLocalSettings
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    public string Get(string key, string fallback = "") => _values.TryGetValue(key, out var value) ? value : fallback;
    public void Set(string key, string value) => _values[key] = value;
}

public sealed class MiraVotingLedger
{
    private readonly List<(byte Voter, byte? Target)> _votes = new();
    public IReadOnlyList<(byte Voter, byte? Target)> Votes => _votes;
    public void RecordHostObservedVote(byte voter, byte? target, bool isHost)
    {
        if (!isHost) throw new InvalidOperationException("Voting ledger is host-only.");
        _votes.Add((voter, target));
    }
    public void Reset() => _votes.Clear();
}

using System.Collections;
using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using MelonLoader;
using Polus.RoleCore;
using UnityEngine;

[assembly: MelonInfo(typeof(Polus.AndroidMod.PolusAndroidMod), "Polus Android", "1.3.0", "Tvman")]
[assembly: MelonGame("Innersloth", "Among Us")]

namespace Polus.AndroidMod;

public sealed class PolusAndroidMod : MelonMod
{
    private const byte RoleSyncRpc = 250;
    private const byte RoleActionRpc = 249;
    private static readonly Dictionary<byte, RoleDefinition> AssignedRoles = new();
    private static readonly Dictionary<byte, int> AbilityUses = new();
    private static readonly object StateLock = new();
    private static IReadOnlyList<RoleDefinition> _catalog = Array.Empty<RoleDefinition>();
    private static RoleMatchRuntime? _hostRuntime;
    private static Harmony? _harmony;
    private static bool _choiceMade;
    private static bool _moddedMode;
    private static bool _panelOpen;
    private static bool _meetingOpen;
    private static string _status = "Choose a mode to continue.";
    private static Rect _panel = new(18, 70, 330, 420);
    private static string _modeFile = "";

    public override void OnInitializeMelon()
    {
        _modeFile = Path.Combine(MelonEnvironment.UserDataDirectory, "Polus.mode");
        _catalog = LoadCatalog();
        try
        {
            if (File.Exists(_modeFile))
            {
                var saved = File.ReadAllText(_modeFile).Trim();
                if (saved.Equals("modded", StringComparison.OrdinalIgnoreCase)) SelectMode(true);
                else if (saved.Equals("vanilla", StringComparison.OrdinalIgnoreCase)) SelectMode(false);
            }
        }
        catch (Exception ex) { LoggerInstance.Warning("Could not read saved mode: " + ex.Message); }

        LoggerInstance.Msg("Polus Android loaded. The in-game mode selector will appear before Polus features activate.");
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F8) && _choiceMade && _moddedMode)
            _panelOpen = !_panelOpen;
    }

    public override void OnGUI()
    {
        if (!_choiceMade)
        {
            DrawModeSelector();
            return;
        }

        if (!_moddedMode) return;

        if (GUI.Button(new Rect(Screen.width - 150, 12, 138, 42), "POLUS  •  MENU"))
            _panelOpen = !_panelOpen;

        if (_panelOpen)
            _panel = GUI.Window(0x504F4C, _panel, DrawPanel, "POLUS ANDROID");

        var localId = GetLocalPlayerId();
        if (localId.HasValue && AssignedRoles.TryGetValue(localId.Value, out var role))
        {
            var label = role.Name + "  •  ABILITY";
            if (GUI.Button(new Rect(Screen.width - 190, Screen.height - 92, 178, 68), label))
                RequestAbility(localId.Value, role);
        }

        if (_meetingOpen)
            GUI.Label(new Rect(12, Screen.height - 36, 320, 28), "Polus meeting hooks active");
    }

    private void DrawModeSelector()
    {
        var rect = new Rect((Screen.width - 360) / 2, (Screen.height - 240) / 2, 360, 240);
        GUI.Box(rect, "WELCOME TO POLUS");
        GUI.Label(new Rect(rect.x + 18, rect.y + 48, 324, 42), "Choose how Among Us should start.");
        if (GUI.Button(new Rect(rect.x + 24, rect.y + 102, 312, 48), "VANILLA  •  No Polus features"))
            SelectMode(false);
        if (GUI.Button(new Rect(rect.x + 24, rect.y + 160, 312, 48), "POLUS MODDED  •  Enable roles"))
            SelectMode(true);
    }

    private void DrawPanel(int id)
    {
        GUILayout.BeginVertical();
        GUILayout.Label(_status);
        GUILayout.Space(8);
        if (GUILayout.Button("Assign roles (host / private lobby)"))
            AssignRoles();
        if (GUILayout.Button("Call emergency meeting"))
            RequestMeeting();
        if (GUILayout.Button("Hide menu"))
            _panelOpen = false;
        GUILayout.Space(8);
        GUILayout.Label("Assigned roles");
        lock (StateLock)
        {
            foreach (var entry in AssignedRoles.OrderBy(x => x.Key))
                GUILayout.Label($"Player {entry.Key}: {entry.Value.Name}");
        }
        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0, 0, 10000, 28));
    }

    private void SelectMode(bool modded)
    {
        _choiceMade = true;
        _moddedMode = modded;
        _panelOpen = modded;
        _status = modded ? "Polus Modded selected. Use private lobbies with Polus on every client." : "Vanilla selected. Polus gameplay hooks are disabled.";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_modeFile)!);
            File.WriteAllText(_modeFile, modded ? "modded" : "vanilla");
        }
        catch (Exception ex) { LoggerInstance.Warning("Could not save mode: " + ex.Message); }

        if (modded) ActivateModdedHooks();
        else DeactivateModdedHooks();
    }

    private void ActivateModdedHooks()
    {
        if (_harmony != null) return;
        _harmony = new Harmony("org.polus.android");
        PatchByName("MeetingHud", "Start", postfix: AccessTools.Method(typeof(PolusAndroidMod), nameof(MeetingStarted)));
        PatchByName("MeetingHud", "OnDestroy", prefix: AccessTools.Method(typeof(PolusAndroidMod), nameof(MeetingEnding)));
        PatchByName("PlayerControl", "HandleRpc", prefix: AccessTools.Method(typeof(PolusAndroidMod), nameof(HandleRpcPrefix)));
        _status = "Modded mode active. Host-authoritative role sync is enabled for Polus clients.";
    }

    private void DeactivateModdedHooks()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        _hostRuntime = null;
        lock (StateLock) { AssignedRoles.Clear(); AbilityUses.Clear(); }
    }

    private void PatchByName(string typeName, string methodName, MethodInfo? prefix = null, MethodInfo? postfix = null)
    {
        try
        {
            var type = AccessTools.TypeByName(typeName);
            var target = type?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName);
            if (target == null) { LoggerInstance.Warning($"Could not find {typeName}.{methodName}; related hook is unavailable."); return; }
            _harmony!.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(prefix),
                postfix: postfix == null ? null : new HarmonyMethod(postfix));
        }
        catch (Exception ex) { LoggerInstance.Warning($"Could not patch {typeName}.{methodName}: {ex.Message}"); }
    }

    private void AssignRoles()
    {
        if (!IsHost())
        {
            _status = "Only the lobby host can assign roles.";
            return;
        }

        var players = GetPlayers();
        if (players.Count < 4)
        {
            _status = "Start a private game with at least four players first.";
            return;
        }

        try
        {
            var enabled = _catalog.ToDictionary(r => r.Id, _ => true, StringComparer.OrdinalIgnoreCase);
            var settings = HostSettingsValidator.Validate(new HostSettings(true, 3, false, enabled));
            _hostRuntime = new RoleMatchRuntime(true);
            var assignments = _hostRuntime.StartMatch(players.Select(p => new PlayerSlot(p)).ToArray(),
                _catalog, settings, impostorCount: 1, neutralCount: 0);
            lock (StateLock)
            {
                AssignedRoles.Clear();
                foreach (var item in assignments) AssignedRoles[item.PlayerId] = item.Role;
            }
            foreach (var item in assignments)
                SendRpc(RoleSyncRpc, $"ROLE|{item.PlayerId}|{item.Role.Id}");
            _status = $"Assigned {assignments.Count} custom roles. All players need Polus installed.";
        }
        catch (Exception ex) { _status = "Role assignment failed: " + ex.Message; }
    }

    private void RequestAbility(byte playerId, RoleDefinition role)
    {
        if (!IsHost())
        {
            _status = "Ability requests are host-authoritative in this build.";
            return;
        }
        if (_hostRuntime == null) { _status = "The host must assign roles first."; return; }
        var result = _hostRuntime.TryUseAbility(playerId, DateTimeOffset.UtcNow);
        _status = result.Accepted ? $"{role.Name}: ability request accepted." : result.Message;
        if (result.Accepted)
        {
            var uses = AbilityUses.GetValueOrDefault(playerId) + 1;
            AbilityUses[playerId] = uses;
            SendRpc(RoleActionRpc, $"ACTION|{playerId}|{role.Id}|{uses}");
        }
    }

    private void RequestMeeting()
    {
        if (!IsHost()) { _status = "Only the host can request a meeting."; return; }
        try
        {
            var local = GetLocalPlayer();
            if (local == null) { _status = "Player is not in a match."; return; }
            var method = local.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "CmdReportDeadBody" && m.GetParameters().Length == 1);
            if (method == null) { _status = "This game version does not expose the emergency-meeting call expected by Polus."; return; }
            method.Invoke(local, new object?[] { null });
            _status = "Emergency meeting requested.";
        }
        catch (Exception ex) { _status = "Meeting request failed: " + ex.Message; }
    }

    private static void HandleRpcPrefix(object __instance, byte callId, object reader)
    {
        if (!_moddedMode) return;
        try
        {
            if (callId != RoleSyncRpc && callId != RoleActionRpc) return;
            var read = reader.GetType().GetMethod("ReadString", Type.EmptyTypes);
            var payload = read?.Invoke(reader, null) as string;
            if (string.IsNullOrWhiteSpace(payload)) return;
            var parts = payload.Split('|');
            if (parts.Length < 3) return;

            if (parts[0] == "ROLE" && byte.TryParse(parts[1], out var playerId))
            {
                var role = _catalog.FirstOrDefault(r => r.Id.Equals(parts[2], StringComparison.OrdinalIgnoreCase));
                if (role == null) return;
                lock (StateLock) AssignedRoles[playerId] = role;
                _status = $"Role received: {role.Name}.";
            }
            else if (parts[0] == "ACTION")
            {
                _status = $"Player {parts[1]} used a Polus role ability.";
            }
        }
        catch { /* Unknown or malformed mod RPCs must never interrupt the game RPC handler. */ }
    }

    private static void MeetingStarted() => _meetingOpen = true;
    private static void MeetingEnding() => _meetingOpen = false;

    private static IReadOnlyList<RoleDefinition> LoadCatalog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("Roles.json", StringComparison.OrdinalIgnoreCase));
        if (resource == null) return Array.Empty<RoleDefinition>();
        using var stream = assembly.GetManifestResourceStream(resource);
        using var reader = new StreamReader(stream!);
        return RoleCatalogLoader.Load(reader.ReadToEnd());
    }

    private static List<byte> GetPlayers()
    {
        var result = new List<byte>();
        var type = AccessTools.TypeByName("PlayerControl");
        var collection = type == null ? null : GetStaticMember(type, "AllPlayerControls");
        if (collection is IEnumerable enumerable)
            foreach (var player in enumerable)
                if (player != null && TryGetByte(player, "PlayerId", out var id)) result.Add(id);
        return result.Distinct().ToList();
    }

    private static object? GetLocalPlayer()
    {
        var type = AccessTools.TypeByName("PlayerControl");
        return type == null ? null : GetStaticMember(type, "LocalPlayer");
    }

    private static byte? GetLocalPlayerId()
    {
        var player = GetLocalPlayer();
        return player != null && TryGetByte(player, "PlayerId", out var id) ? id : null;
    }

    private static bool IsHost()
    {
        var clientType = AccessTools.TypeByName("AmongUsClient");
        var client = clientType == null ? null : GetStaticMember(clientType, "Instance");
        return client != null && TryGetBool(client, "AmHost");
    }

    private static object? GetStaticMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        return type.GetProperty(name, flags)?.GetValue(null) ?? type.GetField(name, flags)?.GetValue(null);
    }

    private static bool TryGetByte(object target, string name, out byte value)
    {
        var raw = GetMember(target, name);
        try { value = Convert.ToByte(raw); return true; }
        catch { value = 0; return false; }
    }

    private static bool TryGetBool(object target, string name)
    {
        try { return Convert.ToBoolean(GetMember(target, name)); }
        catch { return false; }
    }

    private static object? GetMember(object target, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        return target.GetType().GetProperty(name, flags)?.GetValue(target) ?? target.GetType().GetField(name, flags)?.GetValue(target);
    }

    private static void SendRpc(byte rpcId, string payload)
    {
        try
        {
            var clientType = AccessTools.TypeByName("AmongUsClient");
            var client = clientType == null ? null : GetStaticMember(clientType, "Instance");
            var local = GetLocalPlayer();
            if (client == null || local == null) return;
            var netId = GetMember(local, "NetId");
            var start = client.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "StartRpcImmediately" && m.GetParameters().Length == 4);
            if (start == null) { _status = "This game build does not expose the expected RPC writer."; return; }
            var parameters = start.GetParameters();
            var sendEnum = parameters[2].ParameterType;
            var reliable = Enum.Parse(sendEnum, "Reliable", true);
            var writer = start.Invoke(client, new object?[] { netId, rpcId, reliable, -1 });
            if (writer == null) return;
            writer.GetType().GetMethod("Write", new[] { typeof(string) })?.Invoke(writer, new object[] { payload });
            var finish = client.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "FinishRpcImmediately" && m.GetParameters().Length == 1);
            finish?.Invoke(client, new[] { writer });
        }
        catch (Exception ex) { _status = "Network send failed: " + ex.Message; }
    }
}

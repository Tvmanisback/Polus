using Polus.MiraApi.Android;
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
    private static readonly Dictionary<byte, RoleDefinition> AssignedRoles = new();
    private static readonly Dictionary<byte, int> AbilityUses = new();
    private static readonly object StateLock = new();
    private static IReadOnlyList<RoleDefinition> _catalog = Array.Empty<RoleDefinition>();
    private static RoleMatchRuntime? _hostRuntime;
    private static HarmonyLib.Harmony? _harmony;
    private static bool _choiceMade;
    private static bool _moddedMode;
    private static bool _panelOpen;
    private static bool _meetingOpen;
    private static int _meetingNumber;
    private static string _status = "Choose a mode to continue.";
    private static Rect _panel = new(18, 70, 380, 520);
    private static Vector2 _optionsScroll;
    private static string _modeFile = "";

    public override void OnInitializeMelon()
    {
        _modeFile = Path.Combine(Application.persistentDataPath, "Polus.mode");
        _catalog = LoadCatalog();
        AndroidMiraApi.RegisterRoles(_catalog);
        _choiceMade = false;
        _moddedMode = false;
        LoggerInstance.Msg("Polus Android loaded. Choose Vanilla or Polus Modded on every launch.");
    }

    public override void OnUpdate()
    {
        if (MiraApi.Instance.Keybinds.TryGet("polus.menu", out var menuKey)
            && Enum.TryParse<KeyCode>(menuKey, true, out var key)
            && Input.GetKeyDown(key) && _choiceMade && _moddedMode)
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
            _panel = GUI.Window(0x504F4C, _panel, id => DrawPanel(id), "POLUS ANDROID");

        var localId = GetLocalPlayerId();
        if (localId.HasValue && AssignedRoles.TryGetValue(localId.Value, out var role))
        {
            var actionButton = AndroidMiraApi.GetActionButton(role.Id);
            var label = actionButton?.Label ?? (role.Name + "  •  ABILITY");
            if (GUI.Button(new Rect(Screen.width - 190, Screen.height - 92, 178, 68), label))
                RequestAbility(localId.Value, role);
        }

        if (_meetingOpen)
        {
            GUI.Label(new Rect(12, Screen.height - 36, 320, 28), "POLUS MEETING");
            var meetingPlayerId = GetLocalPlayerId();
            if (meetingPlayerId.HasValue && AssignedRoles.TryGetValue(meetingPlayerId.Value, out var meetingRole)
                && AndroidMiraApi.GetMeetingButton(meetingRole.Id) != null
                && AndroidRoleMechanics.HasMeetingAbility(meetingRole.Id, meetingPlayerId.Value))
            {
                if (GUI.Button(new Rect(12, Screen.height - 92, 178, 48), "MEETING ABILITY"))
                    _status = AndroidRoleMechanics.UseMeetingAbility(meetingRole.Id, meetingPlayerId.Value);
            }
        }
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
        GUILayout.Label("Role options");
        _optionsScroll = GUILayout.BeginScrollView(_optionsScroll, GUILayout.Height(190));
        foreach (var roleOption in _catalog)
        {
            var enabledKey = "role." + roleOption.Id + ".enabled";
            var enabled = MiraApi.Instance.Options.Get(enabledKey, roleOption.EnabledByDefault);
            var nextEnabled = GUILayout.Toggle(enabled, "Enable " + roleOption.Name);
            if (nextEnabled != enabled) MiraApi.Instance.Options.Set(enabledKey, nextEnabled);
            var weightKey = "role." + roleOption.Id + ".spawnWeight";
            var weight = MiraApi.Instance.Options.Get(weightKey, roleOption.SpawnChance);
            GUILayout.Label(roleOption.Name + " spawn weight: " + weight);
            var nextWeight = Mathf.RoundToInt(GUILayout.HorizontalSlider(weight, 0, 100));
            if (nextWeight != weight) MiraApi.Instance.Options.Set(weightKey, nextWeight);
        }
        GUILayout.EndScrollView();
        GUILayout.Space(8);
        if (GUILayout.Button("Assign roles (host / private lobby)"))
            AssignRoles();
        if (GUILayout.Button("Call emergency meeting"))
            RequestMeeting();
        var localPlayerId = GetLocalPlayerId();
        if (localPlayerId.HasValue && AssignedRoles.TryGetValue(localPlayerId.Value, out var localRole))
        {
            GUILayout.Label("Role: " + localRole.Name);
            GUILayout.Label("Progress: " + AndroidRoleMechanics.GetProgress(localPlayerId.Value, localRole.Id));
        }
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
        _harmony = new HarmonyLib.Harmony("org.polus.android");
        PatchByName("MeetingHud", "Start", postfix: AccessTools.Method(typeof(PolusAndroidMod), nameof(MeetingStarted)));
        PatchByName("MeetingHud", "OnDestroy", prefix: AccessTools.Method(typeof(PolusAndroidMod), nameof(MeetingEnding)));
        PatchByName("MeetingHud", "CastVote", postfix: AccessTools.Method(typeof(PolusAndroidMod), nameof(VoteCastPostfix)));
        PatchByName("EndGameManager", "Start", postfix: AccessTools.Method(typeof(PolusAndroidMod), nameof(GameEndedPostfix)));
        PatchByName("PlayerControl", "CompleteTask", postfix: AccessTools.Method(typeof(PolusAndroidMod), nameof(TaskCompletedPostfix)));
        _status = "Host-only mode active. Other players remain on unmodified clients.";
    }

    private void DeactivateModdedHooks()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        _hostRuntime = null;
        lock (StateLock) { AssignedRoles.Clear(); AbilityUses.Clear(); }
        AndroidRoleMechanics.Reset();
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
            var configuredCatalog = _catalog.Select(r => r with
            {
                SpawnChance = Math.Clamp(MiraApi.Instance.Options.Get("role." + r.Id + ".spawnWeight", r.SpawnChance), 0, 100)
            }).ToArray();
            var enabled = configuredCatalog.ToDictionary(r => r.Id,
                r => MiraApi.Instance.Options.Get("role." + r.Id + ".enabled", r.EnabledByDefault), StringComparer.OrdinalIgnoreCase);
            var settings = HostSettingsValidator.Validate(new HostSettings(true, 3, false, enabled));
            _hostRuntime = new RoleMatchRuntime(true);
            MiraApi.Instance.ResetMatch();
            AndroidRoleMechanics.Reset();
            _meetingNumber = 0;
            var assignments = _hostRuntime.StartMatch(players.Select(p => new PlayerSlot(p)).ToArray(),
                configuredCatalog, settings, impostorCount: 1, neutralCount: 0);
            lock (StateLock)
            {
                AssignedRoles.Clear();
                foreach (var item in assignments)
                {
                    AssignedRoles[item.PlayerId] = item.Role;
                    if (MiraApi.Instance.Roles.TryGet(item.Role.Id, out var miraRole))
                        MiraApi.Instance.Events.Publish(new MiraRoleAssignedEvent(item.PlayerId, miraRole));
                }
            }
            _status = $"Assigned {assignments.Count} host-tracked roles. Only the host runs Polus.";
        }
        catch (Exception ex) { _status = "Role assignment failed: " + ex.Message; }
    }

    private void RequestAbility(byte playerId, RoleDefinition role)
    {
        if (!IsHost()) { _status = "Polus role actions are host-only."; return; }
        AcceptAbilityRequest(playerId, role.Id);
    }

    private static void AcceptAbilityRequest(byte playerId, string roleId)
    {
        if (_hostRuntime == null || !AssignedRoles.TryGetValue(playerId, out var assigned)
            || !assigned.Id.Equals(roleId, StringComparison.OrdinalIgnoreCase))
        {
            _status = "Host rejected an invalid role ability request.";
            return;
        }
        var result = _hostRuntime.TryUseAbility(playerId, DateTimeOffset.UtcNow);
        if (!result.Accepted) { _status = result.Message; return; }
        if (!AndroidMiraApi.TryUseAction(assigned.Id, playerId, IsHost(), out var actionResult))
        {
            _status = actionResult;
            return;
        }
        var uses = AbilityUses.GetValueOrDefault(playerId) + 1;
        AbilityUses[playerId] = uses;
        _status = assigned.Name + ": " + actionResult;
        MiraApi.Instance.Events.Publish(new MiraAbilityUsedEvent(playerId, assigned.Id, actionResult));
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

    private static void MeetingStarted()
    {
        _meetingOpen = true;
        _meetingNumber++;
        MiraApi.Instance.Events.Publish(new MiraMeetingStartedEvent(_meetingNumber));
        var localId = GetLocalPlayerId();
        var local = GetLocalPlayer();
        if (localId.HasValue && local != null && AssignedRoles.TryGetValue(localId.Value, out var role))
        {
            var transform = GetMember(local, "transform");
            var position = transform == null ? Vector3.zero : (Vector3)(GetMember(transform, "position") ?? Vector3.zero);
            AndroidRoleMechanics.OnMeeting(localId.Value, position, role.Id);
        }
    }


    private static void GameEndedPostfix()
    {
        var reason = MiraApi.Instance.GameEndConditions.TryEvaluate(out var customReason)
            ? customReason : "Among Us game ended.";
        MiraApi.Instance.Events.Publish(new MiraGameEndedEvent(reason));
    }

    private static void VoteCastPostfix(object[] __args)
    {
        if (!_moddedMode || !IsHost() || __args == null || __args.Length < 2) return;
        try
        {
            var voter = Convert.ToByte(__args[0]);
            byte? target = __args[1] == null ? null : Convert.ToByte(__args[1]);
            MiraApi.Instance.Voting.RecordHostObservedVote(voter, target, isHost: true);
            MiraApi.Instance.Events.Publish(new MiraVoteRecordedEvent(voter, target, IsHost()));
        }
        catch (Exception ex) { MelonLogger.Warning("Vote observer hook failed: " + ex.Message); }
    }

    private static void MeetingEnding()
    {
        _meetingOpen = false;
        MiraApi.Instance.Events.Publish(new MiraMeetingEndedEvent(_meetingNumber));
    }

    private static void TaskCompletedPostfix(object __instance, object[] __args)
    {
        if (!_moddedMode || __instance == null) return;
        try
        {
            if (!TryGetByte(__instance, "PlayerId", out var playerId)) return;
            var taskType = __args?.FirstOrDefault()?.ToString() ?? "task";
            var transform = GetMember(__instance, "transform");
            var position = transform == null ? Vector3.zero : (Vector3)(GetMember(transform, "position") ?? Vector3.zero);
            AndroidRoleMechanics.RecordTask(playerId, position, taskType);
            MiraApi.Instance.Events.Publish(new MiraTaskCompletedEvent(playerId, taskType));
        }
        catch (Exception ex) { MelonLogger.Warning("Task progress hook failed: " + ex.Message); }
    }

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

    internal static object? GetPlayerForApi(byte playerId) => GetPlayerById(playerId);
    internal static object? GetMemberForApi(object target, string name) => GetMember(target, name);

    private static object? GetPlayerById(byte playerId)
    {
        var type = AccessTools.TypeByName("PlayerControl");
        var collection = type == null ? null : GetStaticMember(type, "AllPlayerControls");
        if (collection is IEnumerable enumerable)
        {
            foreach (var player in enumerable)
                if (player != null && TryGetByte(player, "PlayerId", out var id) && id == playerId)
                    return player;
        }
        return null;
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

}

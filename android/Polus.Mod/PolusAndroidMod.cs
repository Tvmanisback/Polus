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
    private static readonly HostRoleEngine SharedRoleEngine = new();
    private static readonly RoleEffectEngine SharedEffects = new(true);
    private static HarmonyLib.Harmony? _harmony;
    private static bool _choiceMade;
    private static bool _moddedMode;
    private static bool _panelOpen;
    private static bool _meetingOpen;
    private static int _meetingNumber;
    private static string _status = "Choose a mode to continue.";
    private static Rect _panel = new(18, 70, 380, 520);
    private static int _rolePage;
    private static readonly HashSet<byte> NeutralObjectiveNotices = new();
    private static bool NeutralVictoryTriggered;
    private static string _modeFile = "";

    public override void OnInitializeMelon()
    {
        _modeFile = Path.Combine(Application.persistentDataPath, "Polus.mode");
        _catalog = LoadCatalog();
        PolusHostRoles.RegisterRoles(_catalog);
        _choiceMade = false;
        _moddedMode = false;
        LoggerInstance.Msg("Polus Android loaded. Choose Vanilla or Polus Modded on every launch.");
    }

    public override void OnUpdate()
    {
        AndroidRoleEffectAdapter.Tick();
        CheckNeutralObjectives();
        if (PolusHostRoles.TryGetKeybind("polus.menu", out var menuKey)
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

        if (_panelOpen) DrawPanel(0);

        var localId = GetLocalPlayerId();
        if (localId.HasValue && AssignedRoles.TryGetValue(localId.Value, out var role))
        {
            var actionButton = PolusHostRoles.GetActionButton(role.Id);
            var label = actionButton?.Label ?? (role.Name + "  •  ABILITY");
            if (GUI.Button(new Rect(Screen.width - 190, Screen.height - 92, 178, 68), label))
                RequestAbility(localId.Value, role);
        }

        if (_meetingOpen)
        {
            GUI.Label(new Rect(12, Screen.height - 36, 320, 28), "POLUS MEETING");
            var meetingPlayerId = GetLocalPlayerId();
            if (meetingPlayerId.HasValue && AssignedRoles.TryGetValue(meetingPlayerId.Value, out var meetingRole)
                && SharedEffects.HasMeetingAbility(meetingPlayerId.Value))
            {
                if (GUI.Button(new Rect(12, Screen.height - 92, 178, 48), "MEETING ABILITY"))
                {
                    var sharedDecision = SharedEffects.TryUseMeetingAbility(meetingPlayerId.Value, out var meetingCommand);
                    if (!sharedDecision.Accepted) _status = sharedDecision.Message;
                    else if (AndroidRoleEffectAdapter.Execute(meetingPlayerId.Value, meetingCommand, out var adapterMessage))
                        _status = meetingCommand.Message + " " + adapterMessage;
                    else _status = adapterMessage;
                }
            }
        }
    }

    private static void CheckNeutralObjectives()
    {
        if (!_moddedMode || !IsHost() || _hostRuntime == null || !_hostRuntime.IsRunning) return;
        var requirements = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in _catalog.Where(r => r.Team == RoleTeam.Neutral))
        {
            var words = role.WinCondition.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1 && int.TryParse(words[1], out var required) && required > 0)
                requirements[role.Id] = required;
        }

        foreach (var completion in SharedEffects.GetCompletedNeutralObjectives(requirements))
        {
            if (!NeutralObjectiveNotices.Add(completion.PlayerId) || NeutralVictoryTriggered) continue;
            var roleName = _catalog.FirstOrDefault(r => r.Id.Equals(completion.RoleId, StringComparison.OrdinalIgnoreCase))?.Name
                ?? completion.RoleId;
            if (TryTriggerCrewVictory())
            {
                NeutralVictoryTriggered = true;
                _status = roleName + " won! Showing the standard Crewmate victory screen.";
            }
            else
            {
                _status = roleName + " completed the objective, but the standard Crewmate end-game call was unavailable.";
            }
        }
    }

    private static bool TryTriggerCrewVictory()
    {
        try
        {
            var shipType = AccessTools.TypeByName("ShipStatus");
            var ship = shipType == null ? null : GetStaticMember(shipType, "Instance");
            if (shipType == null || ship == null) return false;
            var method = shipType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "RpcEndGame" && m.GetParameters().Length == 2
                    && m.GetParameters()[0].ParameterType.IsEnum
                    && m.GetParameters()[1].ParameterType == typeof(bool));
            if (method == null) return false;
            var reason = Enum.Parse(method.GetParameters()[0].ParameterType, "HumansByVote", true);
            method.Invoke(ship, new object[] { reason, false });
            return true;
        }
        catch (Exception ex)
        {
            MelonLogger.Warning("Could not trigger standard Crewmate victory: " + ex.Message);
            return false;
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
        GUI.Box(_panel, "POLUS ANDROID");
        var left = _panel.x + 12f;
        var width = _panel.width - 24f;
        var y = _panel.y + 34f;
        GUI.Label(new Rect(left, y, width, 34), _status);
        y += 38f;
        GUI.Label(new Rect(left, y, width, 24), "Role options");
        y += 25f;

        const int pageSize = 4;
        var pageCount = Math.Max(1, (int)Math.Ceiling(_catalog.Count / (double)pageSize));
        _rolePage = Math.Clamp(_rolePage, 0, pageCount - 1);
        foreach (var roleOption in _catalog.Skip(_rolePage * pageSize).Take(pageSize))
        {
            var enabledKey = "role." + roleOption.Id + ".enabled";
            var enabled = PolusHostRoles.GetOption(enabledKey, roleOption.EnabledByDefault);
            var toggleText = (enabled ? "[ON] " : "[OFF] ") + roleOption.Name;
            if (GUI.Button(new Rect(left, y, width, 28), toggleText))
                PolusHostRoles.SetOption(enabledKey, !enabled);
            y += 28f;

            var weightKey = "role." + roleOption.Id + ".spawnWeight";
            var weight = PolusHostRoles.GetOption(weightKey, roleOption.SpawnChance);
            GUI.Label(new Rect(left, y, width - 90f, 24), roleOption.Team + "  •  Spawn weight: " + weight);
            if (GUI.Button(new Rect(left + width - 82f, y, 36f, 24), "-"))
                PolusHostRoles.SetOption(weightKey, Math.Max(0, weight - 5));
            if (GUI.Button(new Rect(left + width - 42f, y, 36f, 24), "+"))
                PolusHostRoles.SetOption(weightKey, Math.Min(100, weight + 5));
            y += 28f;
        }

        if (GUI.Button(new Rect(left, y, 78f, 28f), "Previous"))
            _rolePage = Math.Max(0, _rolePage - 1);
        GUI.Label(new Rect(left + 82f, y, width - 164f, 28f), "Page " + (_rolePage + 1) + " / " + pageCount);
        if (GUI.Button(new Rect(left + width - 78f, y, 78f, 28f), "Next"))
            _rolePage = Math.Min(pageCount - 1, _rolePage + 1);
        y += 34f;

        if (GUI.Button(new Rect(left, y, width, 30f), "Assign roles (host / private lobby)"))
            AssignRoles();
        y += 34f;
        if (GUI.Button(new Rect(left, y, width, 30f), "Call emergency meeting"))
            RequestMeeting();
        y += 34f;

        var localPlayerId = GetLocalPlayerId();
        if (localPlayerId.HasValue && AssignedRoles.TryGetValue(localPlayerId.Value, out var localRole))
        {
            GUI.Label(new Rect(left, y, width, 24f), "Role: " + localRole.Name + "  •  Progress: " + SharedEffects.GetProgress(localPlayerId.Value));
            y += 25f;
        }

        GUI.Label(new Rect(left, y, width, 24f), "Assigned roles");
        y += 24f;
        lock (StateLock)
        {
            foreach (var entry in AssignedRoles.OrderBy(x => x.Key).Take(2))
            {
                GUI.Label(new Rect(left, y, width, 22f), "Player " + entry.Key + ": " + entry.Value.Name);
                y += 22f;
            }
        }
        if (GUI.Button(new Rect(left, _panel.yMax - 38f, width, 28f), "Hide menu"))
            _panelOpen = false;
    }

    private void SelectMode(bool modded)
    {
        _choiceMade = true;
        _moddedMode = modded;
        _panelOpen = modded;
        _status = modded ? "Polus host-only mode selected. Vanilla clients can join without installing Polus." : "Vanilla selected. Polus gameplay hooks are disabled.";
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
        PatchByName("PlayerControl", "CheckMurder", prefix: AccessTools.Method(typeof(PolusAndroidMod), nameof(VanillaKillRequestPrefix)));
        _status = "Host-only mode active. Other players remain on unmodified clients.";
    }

    private void DeactivateModdedHooks()
    {
        _harmony?.UnpatchSelf();
        _harmony = null;
        _hostRuntime = null;
        if (IsHost()) { SharedRoleEngine.EndMatch(); SharedEffects.EndMatch(); }
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
            var configuredCatalog = _catalog.Select(r => r with
            {
                SpawnChance = Math.Clamp(PolusHostRoles.GetOption("role." + r.Id + ".spawnWeight", r.SpawnChance), 0, 100)
            }).ToArray();
            var enabled = configuredCatalog.ToDictionary(r => r.Id,
                r => PolusHostRoles.GetOption("role." + r.Id + ".enabled", r.EnabledByDefault), StringComparer.OrdinalIgnoreCase);
            var neutralCount = configuredCatalog.Any(r => r.Team == RoleTeam.Neutral && enabled[r.Id]) ? 1 : 0;
            var settings = HostSettingsValidator.Validate(new HostSettings(true, 3, true, enabled));
            _hostRuntime = new RoleMatchRuntime(true);
            PolusHostRoles.ResetMatch();
            SharedRoleEngine.EndMatch();
            SharedEffects.EndMatch();
            NeutralObjectiveNotices.Clear();
            NeutralVictoryTriggered = false;
            _meetingNumber = 0;
            var assignments = _hostRuntime.StartMatch(players.Select(p => new PlayerSlot(p)).ToArray(),
                configuredCatalog, settings, impostorCount: 1, neutralCount: neutralCount);
            SharedRoleEngine.StartMatch(assignments);
            SharedEffects.BeginMatch(assignments);
            lock (StateLock)
            {
                AssignedRoles.Clear();
                foreach (var item in assignments)
                {
                    AssignedRoles[item.PlayerId] = item.Role;
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
        var effectDecision = SharedEffects.TryUseAbility(playerId, DateTimeOffset.UtcNow, assigned.CooldownSeconds, out var effectCommand);
        if (!effectDecision.Accepted) { _status = effectDecision.Message; return; }
        if (!AndroidRoleEffectAdapter.Execute(playerId, effectCommand, out var actionResult))
        {
            _status = actionResult;
            return;
        }
        var uses = AbilityUses.GetValueOrDefault(playerId) + 1;
        AbilityUses[playerId] = uses;
        _status = assigned.Name + ": " + actionResult;
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
        if (!IsHost()) return;
        _meetingNumber++;
        PolusHostRoles.BeginMeeting();
        SharedRoleEngine.BeginMeeting();
        SharedEffects.BeginMeeting();
        var localId = GetLocalPlayerId();
        var local = GetLocalPlayer();
        if (localId.HasValue && local != null && AssignedRoles.TryGetValue(localId.Value, out var role))
        {
            var transform = GetMember(local, "transform");
            var position = transform == null ? Vector3.zero : (Vector3)(GetMember(transform, "position") ?? Vector3.zero);
        }
    }



    // Vanilla impostor clients already have a Kill button. On the host, consume
    // that ordinary request and execute the assigned Polus role action instead.
    private static bool VanillaKillRequestPrefix(object __instance, object[] __args)
    {
        if (!_moddedMode || !IsHost() || __instance == null) return true;
        if (!TryGetByte(__instance, "PlayerId", out var playerId)) return true;
        if (!AssignedRoles.TryGetValue(playerId, out var role)) return true;
        var data = GetMember(__instance, "Data");
        if (data == null || TryGetBool(data, "IsDead")) return false;
        AcceptAbilityRequest(playerId, role.Id);
        return false;
    }

    private static void GameEndedPostfix()
    {
        if (!IsHost()) return;
        PolusHostRoles.OnGameEnded();
        SharedRoleEngine.EndMatch();
        SharedEffects.EndMatch();
    }

    private static void VoteCastPostfix(object[] __args)
    {
        if (!_moddedMode || !IsHost() || __args == null || __args.Length < 2) return;
        try
        {
            var voter = Convert.ToByte(__args[0]);
            byte? target = __args[1] == null ? null : Convert.ToByte(__args[1]);
            PolusHostRoles.RecordObservedVote(voter, target, IsHost());
            SharedRoleEngine.RecordVote(voter, target);
        }
        catch (Exception ex) { MelonLogger.Warning("Vote observer hook failed: " + ex.Message); }
    }

    private static void MeetingEnding()
    {
        _meetingOpen = false;
        PolusHostRoles.EndMeeting();
    }

    private static void TaskCompletedPostfix(object __instance, object[] __args)
    {
        if (!_moddedMode || !IsHost() || __instance == null) return;
        try
        {
            if (!TryGetByte(__instance, "PlayerId", out var playerId)) return;
            var taskType = __args?.FirstOrDefault()?.ToString() ?? "task";
            var transform = GetMember(__instance, "transform");
            var position = transform == null ? Vector3.zero : (Vector3)(GetMember(transform, "position") ?? Vector3.zero);
            SharedRoleEngine.RecordTask(playerId, taskType, position.x, position.y);
            SharedEffects.RecordTask(playerId, taskType, position.x, position.y);
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

    internal static void SetStatusForApi(string message) => _status = message;
    internal static bool IsHostForApi() => IsHost();
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

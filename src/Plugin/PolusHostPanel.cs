using System;
using System.Linq;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using HarmonyLib;

namespace Polus.Plugin;

public sealed class PolusHostPanel : MonoBehaviour
{
    private static bool _registered;
    private bool _visible;
    private int _selected;
    private Rect _window = new Rect(80, 70, 390, 470);

    public PolusHostPanel(IntPtr ptr) : base(ptr) { }

    public static void Register()
    {
        if (_registered) return;
        ClassInjector.RegisterTypeInIl2Cpp<PolusHostPanel>();
        _registered = true;
    }

    private static bool IsHost =>
        AmongUsClient.Instance != null &&
        AmongUsClient.Instance.AmHost &&
        PlayerControl.LocalPlayer != null;

    public void Update()
    {
        if (!IsHost)
        {
            _visible = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.P))
            _visible = !_visible;
    }

    public void OnGUI()
    {
        // The host-only check is repeated at render time. Non-host clients never
        // draw this window, even if they somehow retain the component.
        if (!_visible || !IsHost) return;
        _window = GUI.Window(731904, _window, (GUI.WindowFunction)DrawWindow,
            "POLUS HOST PANEL  |  P to close");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("PLAYER MODERATION");
        var players = PlayerControl.AllPlayerControls.ToArray()
            .Where(p => p != null && p.Data != null && !p.Data.Disconnected && !p.AmOwner)
            .ToArray();

        if (players.Length == 0)
        {
            GUILayout.Label("No other connected players.");
        }
        else
        {
            _selected = Mathf.Clamp(_selected, 0, players.Length - 1);
            for (var i = 0; i < players.Length; i++)
            {
                if (GUILayout.Toggle(_selected == i,
                    players[i].Data.PlayerName, GUI.skin.button))
                    _selected = i;
            }

            var target = players[_selected];
            GUILayout.Space(8);
            GUILayout.Label("Selected: " + target.Data.PlayerName);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Kick"))
                Moderate(target, false);
            if (GUILayout.Button("Ban"))
                Moderate(target, true);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Warn in lobby chat"))
            {
                if (IsHost && PlayerControl.LocalPlayer != null)
                    PlayerControl.LocalPlayer.RpcSendChat(
                        "[HOST WARNING] " + target.Data.PlayerName +
                        ", please follow the lobby rules.");
            }
        }

        GUILayout.Space(12);
        GUILayout.Label("HOST UTILITIES");
        if (GUILayout.Button("Refresh player list"))
            _selected = 0;

        GUILayout.Space(12);
        GUILayout.Label("PRIVATE LOBBY CHEATS");
        GUILayout.Label(IsPrivateLobby
            ? "Private lobby confirmed. Private-only options can be enabled here."
            : "Locked: cheats are unavailable unless this is a private lobby.");

        GUI.DragWindow(new Rect(0, 0, 10000, 24));
    }

    private static bool IsPrivateLobby
    {
        get
        {
            try
            {
                return GameStartManager.Instance != null &&
                       !GameStartManager.Instance.GamePublic;
            }
            catch { return false; }
        }
    }

    private static void Moderate(PlayerControl target, bool ban)
    {
        // Re-check authority at action time; never trust the window's visibility.
        if (!IsHost || target == null || target.AmOwner) return;

        try
        {
            foreach (var client in AmongUsClient.Instance.allClients)
            {
                if (client.Character == null ||
                    client.Character.PlayerId != target.PlayerId)
                    continue;

                AmongUsClient.Instance.KickPlayer(client.Id, ban);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[Polus] Host moderation action failed: " + ex);
        }
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Start))]
internal static class PolusHostPanelHudPatch
{
    private static void Postfix(HudManager __instance)
    {
        if (__instance == null) return;
        PolusHostPanel.Register();
        if (__instance.GetComponent<PolusHostPanel>() == null)
            __instance.gameObject.AddComponent<PolusHostPanel>();
    }
}

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using MiraAPI;
using HarmonyLib;
using MiraAPI.PluginLoading;
using MiraAPI.Translation;
using Reactor;

namespace Polus.Plugin;

[BepInPlugin(PluginGuid, "Polus Custom Roles", "1.1.0")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(MiraApiPlugin.Id)]
public sealed class PolusPlugin : BasePlugin, IMiraPlugin
{
    public const string PluginGuid = "org.polus.customroles";
    public Harmony Harmony { get; } = new(PluginGuid);
    public string OptionsTitleText => "Polus Custom Roles";
    public string CustomOptionMenuNameTwo => "Polus Role Settings";

    public ConfigFile GetConfigFile() => Config;

    public PolusPlugin()
    {
        MiraLocaleManager.Register(PluginGuid);
    }

    public override void Load()
    {
        Harmony.PatchAll();
        Log.LogInfo("Polus 1.1.0 loaded. Host-side utilities are available without requiring every lobby client to install Polus.");
    }
}
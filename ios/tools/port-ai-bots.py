#!/usr/bin/env python3
"""Overlay the vendored AI bot subsystem onto official Impostor 1.10.9."""

from pathlib import Path
import json
import shutil
import sys

if len(sys.argv) != 2:
    raise SystemExit("usage: port-ai-bots.py <official-impostor-repo>")

root = Path(__file__).resolve().parents[2]
official = Path(sys.argv[1]).resolve()
src = official / "src"
server = src / "Impostor.Server"
api = src / "Impostor.Api"
vendor_root = root / "ios" / "ai-bots" / "vendor"

# Materialize the pinned GPL-3.0 source snapshots stored in this Polus branch.
file_count = 0
for bundle in sorted((root / "ios" / "ai-bots").glob("vendor-*.json")):
    payload = json.loads(bundle.read_text(encoding="utf-8"))
    if payload.get("commit") != "a7b54c8e04d1fcafe56dd302db1557c13f60d506":
        raise SystemExit(f"Unexpected vendor revision in {bundle}")
    for relative, content in payload["files"].items():
        target = vendor_root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8")
        file_count += 1

bot_server = vendor_root / "impostor" / "src" / "Impostor.Server"
if file_count < 62 or not (bot_server / "LlmBots" / "BotManager.cs").is_file():
    raise SystemExit(f"Vendored bot source is incomplete ({file_count} files found).")

shutil.copytree(bot_server / "LlmBots", server / "LlmBots", dirs_exist_ok=True)

# Copy the bot integration partials that extend official server internals.
integration_files = [
    "Impostor.Server/Net/Inner/Objects/Components/InnerCustomNetworkTransform.LlmBots.cs",
    "Impostor.Server/Net/Inner/Objects/InnerMeetingHud.LlmBots.cs",
    "Impostor.Server/Net/Inner/Objects/ShipStatus/InnerShipStatus.LlmBots.cs",
]
for relative in integration_files:
    source = vendor_root / "impostor" / "src" / relative
    destination = src / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)

unknown_role = vendor_root / "impostor" / "src" / "Impostor.Api" / "Innersloth" / "GameOptions" / "RoleOptions" / "UnknownRoleOptions.cs"
shutil.copy2(unknown_role, api / "Innersloth" / "GameOptions" / "RoleOptions" / "UnknownRoleOptions.cs")

def replace_once(path: Path, old: str, new: str) -> None:
    data = path.read_text(encoding="utf-8-sig")
    count = data.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one patch point in {path}, found {count}: {old[:90]!r}")
    path.write_text(data.replace(old, new, 1), encoding="utf-8")

program = server / "Program.cs"
replace_once(program, "using Impostor.Server.Http;\n",
             "using Impostor.Server.Http;\nusing Impostor.Server.LlmBots;\n")
replace_once(
    program,
    "                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));\n",
    "                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));\n"
    "                    services.Configure<LlmBotsConfig>(host.Configuration.GetSection(LlmBotsConfig.Section));\n",
)
replace_once(program, "                    services.AddHostedService<MatchmakerService>();\n",
             "                    services.AddHostedService<MatchmakerService>();\n                    services.AddLlmBots();\n")
replace_once(program, "                            endpoints.MapControllers();\n",
             "                            endpoints.MapControllers();\n                            endpoints.MapLlmBots();\n")

client = server / "Net" / "Client.cs"
replace_once(
    client,
    "            if (Player != null && Player.Game.ModGuid != null)\n",
    "            // Bot mistakes are dropped and logged instead of banning the in-process test players.\n"
    "            if (Connection is Impostor.Server.LlmBots.BotConnection { ExemptFromAntiCheat: true })\n"
    "            {\n"
    "                _logger.LogWarning(\"Bot {Name} ({Id}) sent a message rejected by anti-cheat: [{Context}-{Category}] {Message}\", Name, Id, context.Name, category, message);\n"
    "                return true;\n"
    "            }\n\n"
    "            if (Player != null && Player.Game.ModGuid != null)\n",
)

project = server / "Impostor.Server.csproj"
replace_once(
    project,
    "  <ItemGroup>\n    <ProjectReference Include=\"..\\Impostor.Api\\Impostor.Api.csproj\" />",
    "  <ItemGroup>\n    <EmbeddedResource Include=\"LlmBots\\MapData\\**\\*.json\" />\n  </ItemGroup>\n\n"
    "  <ItemGroup>\n    <ProjectReference Include=\"..\\Impostor.Api\\Impostor.Api.csproj\" />",
)

# Bot partials expose private internals without replacing the official implementation.
ship = server / "Net" / "Inner" / "Objects" / "ShipStatus" / "InnerShipStatus.cs"
replace_once(ship, "    internal abstract class InnerShipStatus : InnerNetObject, IInnerShipStatus",
             "    internal abstract partial class InnerShipStatus : InnerNetObject, IInnerShipStatus")

# Preserve unknown 19.0.x role-option payloads instead of crashing on a newer role enum.
roles = api / "Innersloth" / "GameOptions" / "RoleOptions" / "RoleOptionsCollection.cs"
replace_once(roles, "_ => throw new ArgumentOutOfRangeException(nameof(roleType), roleType, null),",
             "_ => UnknownRoleOptions.Deserialize(roleOptionsReader, roleType),")

# A real client may keep the meeting object alive after voting. Mark it closed from host RPCs.
meeting = server / "Net" / "Inner" / "Objects" / "InnerMeetingHud.cs"
replace_once(meeting, "                    Rpc22Close.Deserialize(reader);\n                    _judgeOverrules.Clear();",
             "                    Rpc22Close.Deserialize(reader);\n                    _judgeOverrules.Clear();\n                    MarkHostClosed();")
replace_once(
    meeting,
    "                case RpcCalls.VotingComplete:\n                {\n                    if (!await ValidateHost(call, sender))\n                    {\n                        return false;\n                    }\n",
    "                case RpcCalls.VotingComplete:\n                {\n                    if (!await ValidateHost(call, sender))\n                    {\n                        return false;\n                    }\n\n                    MarkVotingComplete();\n",
)

print(f"Ported {file_count} vendored source files onto official Impostor 1.10.9.")
print("Retained official 19.0 compatibility entries and added forward-compatible role/meeting handling.")

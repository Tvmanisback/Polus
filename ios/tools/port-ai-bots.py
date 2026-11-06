#!/usr/bin/env python3
"""Overlay the AI bot subsystem onto the official Impostor 1.10.9 source tree."""

from pathlib import Path
import shutil
import sys

if len(sys.argv) != 3:
    raise SystemExit("usage: port-ai-bots.py <official-impostor-repo> <bot-source-repo>")

official = Path(sys.argv[1]).resolve()
bot_source = Path(sys.argv[2]).resolve()
server = official / "src" / "Impostor.Server"
bot_server = bot_source / "impostor" / "src" / "Impostor.Server"

if not (server / "Program.cs").is_file() or not (bot_server / "LlmBots").is_dir():
    raise SystemExit("Could not find the expected Impostor server or upstream LlmBots source.")

shutil.copytree(bot_server / "LlmBots", server / "LlmBots", dirs_exist_ok=True)

def replace_once(path: Path, old: str, new: str) -> None:
    data = path.read_text(encoding="utf-8-sig")
    count = data.count(old)
    if count != 1:
        raise SystemExit(f"Expected exactly one patch point in {path}, found {count}: {old[:90]!r}")
    path.write_text(data.replace(old, new, 1), encoding="utf-8")

program = server / "Program.cs"
replace_once(
    program,
    "using Impostor.Server.Http;\n",
    "using Impostor.Server.Http;\nusing Impostor.Server.LlmBots;\n",
)
replace_once(
    program,
    "                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));\n",
    "                    services.Configure<HttpServerConfig>(host.Configuration.GetSection(HttpServerConfig.Section));\n"
    "                    services.Configure<LlmBotsConfig>(host.Configuration.GetSection(LlmBotsConfig.Section));\n",
)
replace_once(
    program,
    "                    services.AddHostedService<MatchmakerService>();\n",
    "                    services.AddHostedService<MatchmakerService>();\n"
    "                    services.AddLlmBots();\n",
)
replace_once(
    program,
    "                            endpoints.MapControllers();\n",
    "                            endpoints.MapControllers();\n"
    "                            endpoints.MapLlmBots();\n",
)

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

print("AI bot subsystem overlaid onto official Impostor 1.10.9.")
print("Official 19.0 compatibility manager retained; AllowFutureGameVersions is configured separately.")

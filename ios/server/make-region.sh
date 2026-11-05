#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: bash $0 https://your-temporary-host.trycloudflare.com [output-file]"
  exit 2
fi

HTTPS_URL="$1"
OUTPUT="${2:-ios/config/region.json}"

python3 - "${HTTPS_URL}" "${OUTPUT}" <<'PY'
import json
import pathlib
import sys
from urllib.parse import urlparse

url, output = sys.argv[1:3]
parsed = urlparse(url)
if parsed.scheme != "https" or not parsed.hostname:
    raise SystemExit("The matchmaking URL must be a valid https:// URL.")

host = parsed.hostname
regions = [
    {
        "$type": "StaticHttpRegionInfo, Assembly-CSharp",
        "Name": "North America",
        "PingServer": "matchmaker.among.us",
        "Servers": [{"Name": "Http-1", "Ip": "https://matchmaker.among.us", "Port": 443, "UseDtls": True}],
        "TranslateName": 289
    },
    {
        "$type": "StaticHttpRegionInfo, Assembly-CSharp",
        "Name": "Europe",
        "PingServer": "matchmaker-eu.among.us",
        "Servers": [{"Name": "Http-1", "Ip": "https://matchmaker-eu.among.us", "Port": 443, "UseDtls": True}],
        "TranslateName": 290
    },
    {
        "$type": "StaticHttpRegionInfo, Assembly-CSharp",
        "Name": "Asia",
        "PingServer": "matchmaker-as.among.us",
        "Servers": [{"Name": "Http-1", "Ip": "https://matchmaker-as.among.us", "Port": 443, "UseDtls": True}],
        "TranslateName": 291
    },
    {
        "$type": "StaticHttpRegionInfo, Assembly-CSharp",
        "Name": "Polus iOS",
        "PingServer": host,
        "Servers": [{
            "Name": "Polus iOS HTTPS",
            "Ip": f"https://{host}",
            "Port": 443,
            "UseDtls": False,
            "Players": 0,
            "ConnectionFailures": 0
        }],
        "TranslateName": 1003
    }
]
region_info = {"CurrentRegionIdx": 3, "Regions": regions}
pathlib.Path(output).parent.mkdir(parents=True, exist_ok=True)
pathlib.Path(output).write_text(json.dumps(region_info, indent=2) + "\n")
print(f"Wrote {output}")
print(f"Selected region: Polus iOS (https://{host}:443)")
print("The server's UDP game address is configured separately in runtime/config.json.")
PY

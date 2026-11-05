#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 https://your-temporary-host.trycloudflare.com [output-file]"
  exit 2
fi

HTTPS_URL="$1"
OUTPUT="${2:-regionInfo.json}"

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
region = {
    "CurrentRegionIdx": 3,
    "Regions": [{
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
    }]
}
pathlib.Path(output).write_text(json.dumps(region, indent=2) + "\n")
print(f"Wrote {output}")
print(f"HTTPS matchmaking: https://{host}:443")
print("The server's UDP address is configured separately in runtime/config.json.")
PY

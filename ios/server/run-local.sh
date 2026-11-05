#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME="${ROOT}/runtime"
TEMPLATE="${ROOT}/config.template.json"
CONFIG="${RUNTIME}/config.json"

if [[ ! -x "${RUNTIME}/Impostor.Server" ]]; then
  "${ROOT}/download-impostor.sh"
fi

if [[ -z "${POLUS_UDP_HOST:-}" ]]; then
  case "$(uname -s)" in
    Darwin)
      POLUS_UDP_HOST="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || true)"
      ;;
    Linux)
      POLUS_UDP_HOST="$(hostname -I 2>/dev/null | awk '{print $1}')"
      ;;
  esac
fi

if [[ -z "${POLUS_UDP_HOST:-}" ]]; then
  echo "Set POLUS_UDP_HOST to this computer's LAN IPv4 address."
  echo "Example: POLUS_UDP_HOST=192.168.1.25 ./ios/server/run-local.sh"
  exit 1
fi

python3 - "${TEMPLATE}" "${CONFIG}" "${POLUS_UDP_HOST}" <<'PY'
import json
import pathlib
import sys

template, output, host = sys.argv[1:4]
config = json.loads(pathlib.Path(template).read_text())
config["Server"]["PublicIp"] = host
config["Server"]["PublicPort"] = 22023
config["Server"]["ListenIp"] = "0.0.0.0"
config["Server"]["ListenPort"] = 22023
config["HttpServer"]["Enabled"] = True
config["HttpServer"]["ListenIp"] = "127.0.0.1"
config["HttpServer"]["ListenPort"] = 22023
pathlib.Path(output).write_text(json.dumps(config, indent=2) + "\n")
PY

echo "UDP game endpoint: ${POLUS_UDP_HOST}:22023"
echo "HTTP matchmaking endpoint: http://127.0.0.1:22023"
echo "Keep this process running while testing."
cd "${RUNTIME}"
exec ./Impostor.Server

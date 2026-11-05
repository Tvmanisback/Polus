#!/usr/bin/env bash
set -euo pipefail
cd /home/container

PORT="${SERVER_PORT:-${SERVER_PORT_1:-25906}}"
PUBLIC_IP="${SERVER_IP:-103.69.73.55}"
DOTNET_ROOT=/home/container/dotnet
export DOTNET_ROOT
export PATH="$DOTNET_ROOT:$PATH"

if [[ -z "${IONOS_API_TOKEN:-}" ]]; then
  echo "ERROR: Set IONOS_API_TOKEN in the server environment."
  exit 1
fi

if [[ ! -x /home/container/caddy ]]; then
  echo "ERROR: Upload the custom Caddy binary as /home/container/caddy."
  exit 1
fi
if [[ ! -f /home/container/Caddyfile ]]; then
  echo "ERROR: Upload ios/server/Caddyfile as /home/container/Caddyfile."
  exit 1
fi

if ! compgen -G "$DOTNET_ROOT/shared/Microsoft.AspNetCore.App/8.*" >/dev/null; then
  echo "Installing ASP.NET Core 8 runtime..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --runtime aspnetcore --channel 8.0 --install-dir "$DOTNET_ROOT"
fi

cp config.template.json runtime/config.json
python3 - "$PORT" "$PUBLIC_IP" <<'PY'
import json, sys
port = int(sys.argv[1])
public_ip = sys.argv[2]
path = "runtime/config.json"
with open(path, encoding="utf-8") as f:
    cfg = json.load(f)
cfg["Server"]["PublicIp"] = public_ip
cfg["Server"]["PublicPort"] = port
cfg["Server"]["ListenIp"] = "0.0.0.0"
cfg["Server"]["ListenPort"] = port
cfg["HttpServer"]["Enabled"] = True
cfg["HttpServer"]["ListenIp"] = "127.0.0.1"
cfg["HttpServer"]["ListenPort"] = 8080
with open(path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, indent=2)
PY

mkdir -p runtime/plugins
if [[ -d plugins ]]; then
  cp -a plugins/. runtime/plugins/
fi

echo "Starting Impostor UDP on ${PUBLIC_IP}:${PORT}; HTTP upstream on 127.0.0.1:8080"
cd runtime
./Impostor.Server &
IMPOSTOR_PID=$!
trap 'kill "$IMPOSTOR_PID" 2>/dev/null || true' EXIT
sleep 2
cd /home/container
echo "Starting HTTPS proxy at polus.yapyur.org:${PORT}"
/home/container/caddy run --config /home/container/Caddyfile --adapter caddyfile

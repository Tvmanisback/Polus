#!/usr/bin/env bash
set -euo pipefail

if ! command -v cloudflared >/dev/null 2>&1; then
  echo "cloudflared is not installed."
  echo "Install it from https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/"
  exit 1
fi

echo "Starting a temporary HTTPS tunnel to the local Impostor HTTP matchmaking service."
echo "Copy the https://...trycloudflare.com URL from the output."
echo "Then generate your region file with:"
echo "  ./ios/server/make-region.sh https://YOUR-NAME.trycloudflare.com"
echo
exec cloudflared tunnel --url http://127.0.0.1:22023

#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG="${ROOT}/runtime/codespace-smoke-test.log"

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This smoke test is intended for the Linux GitHub Codespaces container."
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1 || ! command -v curl >/dev/null 2>&1; then
  echo "Codespace needs the .NET SDK and curl. Rebuild the Codespace from .devcontainer/devcontainer.json."
  exit 1
fi

mkdir -p "${ROOT}/runtime"
POLUS_UDP_HOST=127.0.0.1 bash "${ROOT}/run-local.sh" >"${LOG}" 2>&1 &
SERVER_PID=$!
cleanup() {
  kill "${SERVER_PID}" 2>/dev/null || true
  wait "${SERVER_PID}" 2>/dev/null || true
}
trap cleanup EXIT

echo "Waiting for Impostor and the Polus plugin to start..."
for attempt in $(seq 1 60); do
  if grep -q "Polus iOS server plugin enabled" "${LOG}"; then
    break
  fi
  if ! kill -0 "${SERVER_PID}" 2>/dev/null; then
    cat "${LOG}"
    echo "Server exited before the plugin loaded."
    exit 1
  fi
  sleep 1
done

if ! grep -q "Polus iOS server plugin enabled" "${LOG}"; then
  cat "${LOG}"
  echo "Polus plugin did not report a successful load."
  exit 1
fi

HTTP_READY=false
for attempt in $(seq 1 15); do
  if curl --silent --fail --max-time 2 -o /dev/null http://127.0.0.1:22023/; then
    HTTP_READY=true
    break
  fi
  sleep 1
done
if [[ "${HTTP_READY}" != true ]]; then
  cat "${LOG}"
  echo "The HTTP matchmaking listener did not respond after 15 retries."
  exit 1
fi

echo "PASS: Impostor started, Polus.ServerPlugin loaded, and the HTTP listener responded."
echo "Codespaces port 22023 can be used to inspect the TCP matchmaking endpoint."
echo "This does NOT test iOS gameplay: Codespaces port forwarding does not forward the UDP game traffic."

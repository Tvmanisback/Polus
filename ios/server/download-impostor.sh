#!/usr/bin/env bash
set -euo pipefail

VERSION="1.10.9"
BASE="https://github.com/Impostor/Impostor/releases/download/v${VERSION}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUNTIME="${ROOT}/runtime"
TMP="$(mktemp -d)"
trap 'rm -rf "${TMP}"' EXIT

OS="$(uname -s)"
ARCH="$(uname -m)"
case "${OS}/${ARCH}" in
  Linux/x86_64)
    ASSET="Impostor-Server_${VERSION}_linux-x64.tar.gz"
    SHA256="54ff18b773fe2cb6458a67d5ae5291b35d5f9f60054ca30e6fc49f07cc784ad8"
    ;;
  Linux/aarch64|Linux/arm64)
    ASSET="Impostor-Server_${VERSION}_linux-arm64.tar.gz"
    SHA256="bf39c29172ade5b19f4ffc53a03df42356662112f05663057bdd80025a2dd499"
    ;;
  Darwin/x86_64)
    ASSET="Impostor-Server_${VERSION}_osx-x64.tar.gz"
    SHA256="736b47cc5bca170711c20d4907962bcc3c3214430ace87465130183bd054a267"
    ;;
  Darwin/arm64)
    echo "The upstream v${VERSION} release does not include an Apple Silicon build."
    echo "Use an x64 Mac with the .NET 8 runtime, or build the upstream source for arm64."
    exit 1
    ;;
  *)
    echo "Unsupported platform: ${OS}/${ARCH}"
    exit 1
    ;;
esac

mkdir -p "${RUNTIME}"
ARCHIVE="${TMP}/${ASSET}"
echo "Downloading Impostor Server v${VERSION}..."
curl --fail --location --retry 3 "${BASE}/${ASSET}" --output "${ARCHIVE}"

if command -v sha256sum >/dev/null 2>&1; then
  echo "${SHA256}  ${ARCHIVE}" | sha256sum --check
else
  echo "${SHA256}  ${ARCHIVE}" | shasum -a 256 --check
fi
tar -xzf "${ARCHIVE}" -C "${RUNTIME}"
chmod +x "${RUNTIME}/Impostor.Server" 2>/dev/null || true

echo "Impostor v${VERSION} installed in ${RUNTIME}"
echo "Upstream compatibility: Among Us 2024.6.18 through 19.0."

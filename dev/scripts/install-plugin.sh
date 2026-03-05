#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PUBLISH_DIR="${ROOT_DIR}/Jellyfin.Plugin.UploadInbox/bin/Release/net9.0/publish"
PLUGIN_DIR="${ROOT_DIR}/dev/jf-config/plugins/UploadInbox"

if [[ ! -d "${PUBLISH_DIR}" ]]; then
  echo "Publish directory not found:"
  echo "  ${PUBLISH_DIR}"
  echo
  echo "Run:"
  echo "  dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release"
  exit 1
fi

mkdir -p "${PLUGIN_DIR}"

echo "Copying plugin build output:"
echo "  from: ${PUBLISH_DIR}"
echo "  to:   ${PLUGIN_DIR}"

cp -R "${PUBLISH_DIR}/"* "${PLUGIN_DIR}/"

echo "Done."
echo
echo "Next:"
echo "  cd dev && docker compose restart jellyfin"

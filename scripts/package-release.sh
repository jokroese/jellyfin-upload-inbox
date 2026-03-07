#!/usr/bin/env bash
# Package the plugin from existing publish output into dist/UploadInbox_<version>.zip
# (files at ZIP root) and generate dist/UploadInbox_<version>.zip.sha256.
# Does not build or publish — run dotnet publish first.
# Usage: VERSION=1.0.0.0 [./scripts/package-release.sh]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/Jellyfin.Plugin.UploadInbox"
PUBLISH_DIR="$PROJECT_DIR/bin/Release/net9.0/publish"
DIST_DIR="$REPO_ROOT/dist"

VERSION="${VERSION:-${1:-}}"
if [[ -z "$VERSION" ]]; then
  echo "Usage: VERSION=x.y.z.w $0  OR  $0 x.y.z.w" >&2
  exit 1
fi

ZIP_NAME="UploadInbox_${VERSION}.zip"
ZIP_PATH="$DIST_DIR/$ZIP_NAME"

# Require existing publish output (caller must run dotnet publish first)
if [[ ! -d "$PUBLISH_DIR" ]]; then
  echo "Publish output directory missing: $PUBLISH_DIR" >&2
  echo "Run 'dotnet publish Jellyfin.Plugin.UploadInbox/Jellyfin.Plugin.UploadInbox.csproj -c Release' first." >&2
  exit 1
fi
if [[ -z "$(ls -A "$PUBLISH_DIR" 2>/dev/null)" ]]; then
  echo "Publish output directory is empty: $PUBLISH_DIR" >&2
  exit 1
fi

# Clean and create dist
rm -rf "$DIST_DIR"
mkdir -p "$DIST_DIR"

# Create ZIP with contents at root (no extra directory layer)
(cd "$PUBLISH_DIR" && zip -r "$ZIP_PATH" .)

# Generate SHA-256 checksum (standard format for sha256sum -c)
if command -v sha256sum >/dev/null 2>&1; then
  (cd "$DIST_DIR" && sha256sum "$ZIP_NAME" > "${ZIP_NAME}.sha256")
else
  (cd "$DIST_DIR" && shasum -a 256 "$ZIP_NAME" | sed 's/  /  /' > "${ZIP_NAME}.sha256")
fi

echo "Created $ZIP_PATH and ${ZIP_PATH}.sha256"

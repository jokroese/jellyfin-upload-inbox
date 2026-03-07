#!/usr/bin/env bash
set -euo pipefail

# Generate a Jellyfin plugin repository manifest in the modern format.
# Top-level plugin metadata and versions[] (version, targetAbi, sourceUrl,
# checksum, timestamp) come from build.yaml and the release environment.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PAGES_DIR="${ROOT_DIR}/pages"
DIST_DIR="${ROOT_DIR}/dist"
BUILD_YAML="${ROOT_DIR}/build.yaml"

VERSION="${VERSION:?VERSION is required}"
TAG="${TAG:-v${VERSION}}"
REPO="${REPO:?REPO is required}"

ZIP_NAME="UploadInbox_${VERSION}.zip"
ZIP_MD5_FILE="${DIST_DIR}/${ZIP_NAME}.md5"

if [[ ! -f "${ZIP_MD5_FILE}" ]]; then
  echo "Missing checksum file: ${ZIP_MD5_FILE}" >&2
  exit 1
fi

cd "${ROOT_DIR}"
python - <<PY
import json
import os
from pathlib import Path

import yaml

build_path = Path("build.yaml")
build = yaml.safe_load(build_path.read_text(encoding="utf-8"))

def flatten(s):
    if s is None:
        return ""
    if isinstance(s, str):
        return s.strip()
    if isinstance(s, list):
        return " ".join(flatten(x) for x in s)
    return str(s).strip()

version = os.environ["VERSION"]
tag = os.environ.get("TAG", f"v{version}")
repo = os.environ["REPO"]
zip_name = f"UploadInbox_{version}.zip"
zip_url = f"https://github.com/{repo}/releases/download/{tag}/{zip_name}"
checksum = Path(f"dist/{zip_name}.md5").read_text(encoding="utf-8").strip()
timestamp = __import__("datetime").datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")

plugin = {
    "guid": build["guid"],
    "name": build["name"],
    "description": flatten(build.get("description")),
    "overview": flatten(build.get("overview")),
    "owner": build["owner"],
    "category": build["category"],
    "versions": [
        {
            "version": version,
            "changelog": flatten(build.get("changelog")),
            "targetAbi": build["targetAbi"],
            "sourceUrl": zip_url,
            "checksum": checksum,
            "timestamp": timestamp,
        }
    ],
}

Path("pages").mkdir(parents=True, exist_ok=True)
Path("pages/manifest.json").write_text(json.dumps([plugin], indent=2) + "\n", encoding="utf-8")
print("Wrote pages/manifest.json")
PY

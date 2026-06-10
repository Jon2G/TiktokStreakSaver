#!/usr/bin/env bash
# Updates dist/altstore/source.json from an IPA file and GitHub release tag.
# Version fields are read from the IPA Info.plist (must match for AltStore).
#
# Usage: ./scripts/update-altstore-source.sh path/to/StreakSaver.ipa ios-v1.0.0
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
JSON="$ROOT/dist/altstore/source.json"
IPA="${1:?IPA path required}"
TAG="${2:?Release tag required (e.g. ios-v1.0.0)}"

if [ ! -f "$IPA" ]; then
  echo "IPA not found: $IPA" >&2
  exit 1
fi

read_version_from_ipa() {
  python3 - "$IPA" <<'PY'
import plistlib, subprocess, sys, zipfile

ipa_path = sys.argv[1]
with zipfile.ZipFile(ipa_path) as zf:
    plist_path = next(
        n for n in zf.namelist()
        if n.startswith("Payload/") and n.endswith(".app/Info.plist")
    )
    data = zf.read(plist_path)

plist = plistlib.loads(data)
version = plist.get("CFBundleShortVersionString", "")
build = str(plist.get("CFBundleVersion", ""))
if not version or not build:
    raise SystemExit("Could not read CFBundleShortVersionString/CFBundleVersion from IPA")
print(version)
print(build)
PY
}

{ read -r VERSION; read -r BUILD_VERSION; } < <(read_version_from_ipa)

SIZE=$(stat -f%z "$IPA" 2>/dev/null || stat -c%s "$IPA")
REPO="${GITHUB_REPOSITORY:-Jon2G/TiktokStreakSaver}"
DOWNLOAD_URL="https://github.com/${REPO}/releases/download/${TAG}/StreakSaver.ipa"
VERSION_DATE=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
SOURCE_URL="https://raw.githubusercontent.com/Jon2G/TiktokStreakSaver/main/dist/altstore/source.json"

python3 - <<PY
import json
from pathlib import Path

path = Path("$JSON")
data = json.loads(path.read_text())
data["sourceURL"] = "$SOURCE_URL"
app = data["apps"][0]
app.pop("marketplaceID", None)  # AltStore Classic rejects notarized/PAL apps
app["version"] = "$VERSION"
app["buildVersion"] = "$BUILD_VERSION"
app["downloadURL"] = "$DOWNLOAD_URL"
app["size"] = int("$SIZE")
app["versionDate"] = "$VERSION_DATE"
path.write_text(json.dumps(data, indent=2) + "\n")
print(f"Updated {path}")
print(f"  version:       $VERSION")
print(f"  buildVersion:  $BUILD_VERSION")
print(f"  size:          $SIZE bytes")
print(f"  downloadURL:   $DOWNLOAD_URL")
PY

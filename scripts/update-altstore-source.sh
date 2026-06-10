#!/usr/bin/env bash
# Updates dist/altstore/source.json from an IPA file and version string.
# Usage: ./scripts/update-altstore-source.sh path/to/StreakSaver.ipa 1.0.0 [v1.0.0]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
JSON="$ROOT/dist/altstore/source.json"
IPA="${1:?IPA path required}"
VERSION="${2:?Version required (e.g. 1.0.0)}"
TAG="${3:-v$VERSION}"

if [ ! -f "$IPA" ]; then
  echo "IPA not found: $IPA" >&2
  exit 1
fi

SIZE=$(stat -f%z "$IPA" 2>/dev/null || stat -c%s "$IPA")
BUILD_VERSION=$(echo "$VERSION" | tr -d '.')
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

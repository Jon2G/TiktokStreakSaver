#!/usr/bin/env bash
# Exit 1 if no provisioning profile exists for the main MAUI bundle ID.
set -euo pipefail

BUNDLE_ID="${1:-com.jon2g.tiktokstreaksaver}"
TEAM_ID="${DEVELOPMENT_TEAM:-}"

search_dirs() {
  echo "${HOME}/Library/MobileDevice/Provisioning Profiles"
  echo "${HOME}/Library/Developer/Xcode/UserData/Provisioning Profiles"
}

python3 - "$BUNDLE_ID" "$TEAM_ID" <<'PY'
import glob, plistlib, subprocess, sys

bundle_id = sys.argv[1]
team_prefix = sys.argv[2] + "." if len(sys.argv) > 2 and sys.argv[2] else ""
want_suffix = team_prefix + bundle_id if team_prefix else bundle_id

dirs = [
    __import__("os").path.expanduser("~/Library/MobileDevice/Provisioning Profiles"),
    __import__("os").path.expanduser("~/Library/Developer/Xcode/UserData/Provisioning Profiles"),
]

found = []
for d in dirs:
    for path in glob.glob(d + "/*.mobileprovision"):
        data = subprocess.check_output(["security", "cms", "-D", "-i", path], stderr=subprocess.DEVNULL)
        pl = plistlib.loads(data)
        app_id = pl.get("Entitlements", {}).get("application-identifier", "")
        if app_id.endswith("." + bundle_id) or app_id == want_suffix:
            found.append((pl.get("Name", path), app_id))

if found:
    for name, app_id in found:
        print(f"OK: {name} ({app_id})")
    sys.exit(0)

print(f"No provisioning profile for {bundle_id}.", file=sys.stderr)
print("Run: export DEVELOPMENT_TEAM=YOUR_TEAM_ID && ./src/ios/build.sh", file=sys.stderr)
sys.exit(1)
PY

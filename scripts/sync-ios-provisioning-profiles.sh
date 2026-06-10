#!/usr/bin/env bash
# Copy Xcode-managed provisioning profiles where MSBuild/dotnet iOS looks for them.
set -euo pipefail

XCODE_PROFILES="${HOME}/Library/Developer/Xcode/UserData/Provisioning Profiles"
LEGACY_PROFILES="${HOME}/Library/MobileDevice/Provisioning Profiles"

if [ ! -d "$XCODE_PROFILES" ]; then
  exit 0
fi

mkdir -p "$LEGACY_PROFILES"
shopt -s nullglob
profiles=("$XCODE_PROFILES"/*.mobileprovision)
if [ "${#profiles[@]}" -eq 0 ]; then
  exit 0
fi

cp -f "${profiles[@]}" "$LEGACY_PROFILES/"

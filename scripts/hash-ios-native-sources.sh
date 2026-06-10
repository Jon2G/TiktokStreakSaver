#!/usr/bin/env bash
# Prints a single SHA-256 fingerprint of all iOS native inputs (Swift, plist, JS, project.yml).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IOS_DIR="$ROOT/src/ios"

if [ ! -d "$IOS_DIR" ]; then
  echo "missing-ios-dir" >&2
  exit 1
fi

# Shared automation script in MAUI — extension bundles a copy under StreakEngine/Resources.
MAUI_JS="$ROOT/src/TiktokStreakSaver/Resources/Raw/tiktok_automation.js"

{
  find "$IOS_DIR" \
    \( -path "$IOS_DIR/artifacts" -o -path "$IOS_DIR/artifacts/*" \) -prune -o \
    \( -path "$IOS_DIR/StreakNative.xcodeproj" -o -path "$IOS_DIR/StreakNative.xcodeproj/*" \) -prune -o \
    -path "$IOS_DIR/StreakEngine/StreakEngine/Resources/tiktok_automation.js" -prune -o \
    -type f \( \
      -name '*.swift' -o -name '*.yml' -o -name '*.yaml' -o \
      -name '*.plist' -o -name '*.h' -o -name '*.js' \
    \) -print0
  if [ -f "$MAUI_JS" ]; then
    printf '%s\0' "$MAUI_JS"
  fi
} | sort -z | xargs -0 shasum -a 256 2>/dev/null | shasum -a 256 | awk '{print $1}'

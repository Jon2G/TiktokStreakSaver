#!/usr/bin/env bash
# Rebuilds StreakEngine.xcframework when source inputs change (hash) or artifacts are missing.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
IOS_DIR="$ROOT/src/ios"
ARTIFACTS="$IOS_DIR/artifacts"
HASH_FILE="$ARTIFACTS/.build-inputs.sha256"
XCFRAMEWORK="$ARTIFACTS/StreakEngine.xcframework/Info.plist"

if [ "${SkipIosNativeBuild:-}" = "true" ] || [ "${SKIP_IOS_NATIVE_BUILD:-}" = "1" ]; then
  echo "Skipping iOS native build (SkipIosNativeBuild=true)."
  exit 0
fi

if ! command -v xcodebuild >/dev/null 2>&1 || ! xcodebuild -version >/dev/null 2>&1; then
  if [ -f "$XCFRAMEWORK" ]; then
    echo "Xcode not available; using existing StreakEngine.xcframework."
    exit 0
  fi
  echo "Xcode is required to build StreakEngine (install full Xcode, not Command Line Tools only)." >&2
  exit 1
fi

mkdir -p "$ARTIFACTS"
CURRENT_HASH="$("$ROOT/scripts/hash-ios-native-sources.sh")"
STORED_HASH=""
if [ -f "$HASH_FILE" ]; then
  STORED_HASH="$(tr -d '[:space:]' < "$HASH_FILE")"
fi

NEEDS_BUILD=0
if [ ! -f "$XCFRAMEWORK" ]; then
  echo "StreakEngine.xcframework missing — building native iOS artifacts."
  NEEDS_BUILD=1
elif [ "$CURRENT_HASH" != "$STORED_HASH" ]; then
  echo "iOS native sources changed — rebuilding StreakEngine.xcframework."
  NEEDS_BUILD=1
else
  echo "iOS native artifacts up to date (hash $CURRENT_HASH)."
fi

if [ "$NEEDS_BUILD" -eq 1 ]; then
  "$ROOT/src/ios/build.sh"
  echo "$CURRENT_HASH" > "$HASH_FILE"
fi

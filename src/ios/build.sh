#!/usr/bin/env bash
# Builds StreakEngine.xcframework and StreakSaverExtension.appex.
# Run from repo root: ./src/ios/build.sh  — or via scripts/build-ios-native.sh
#
# Requires: export DEVELOPMENT_TEAM=XXXXXXXXXX (iOS 26+ SDK disallows ad-hoc signing)
set -eo pipefail
IOS_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$IOS_DIR/../.." && pwd)"
ARTIFACTS="$IOS_DIR/artifacts"
MAUI_JS="$ROOT/src/TiktokStreakSaver/Resources/Raw/tiktok_automation.js"
ENGINE_JS="$IOS_DIR/StreakEngine/StreakEngine/Resources/tiktok_automation.js"

mkdir -p "$ARTIFACTS"
mkdir -p "$(dirname "$ENGINE_JS")"
if [ -f "$MAUI_JS" ]; then
  cp "$MAUI_JS" "$ENGINE_JS"
fi

if ! command -v xcodebuild >/dev/null 2>&1 || ! xcodebuild -version >/dev/null 2>&1; then
  echo "Full Xcode is required (xcode-select -s /Applications/Xcode.app/Contents/Developer)" >&2
  exit 1
fi

if [ -z "${DEVELOPMENT_TEAM:-}" ]; then
  echo "DEVELOPMENT_TEAM is required for native iOS builds." >&2
  echo "  export DEVELOPMENT_TEAM=XXXXXXXXXX  # Xcode → Settings → Accounts → Team ID" >&2
  echo "  ./src/ios/build.sh" >&2
  exit 1
fi

cd "$IOS_DIR"
if command -v xcodegen >/dev/null 2>&1; then
  xcodegen generate
else
  echo "Install xcodegen: brew install xcodegen" >&2
  exit 1
fi

echo "Using DEVELOPMENT_TEAM=$DEVELOPMENT_TEAM"

# CI has no Apple ID in Xcode. Use automatic signing with pre-installed cert + profiles
# (Personal Team profiles are Xcode-managed and cannot be used with Manual signing).
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  BASE_SIGNING=(
    "DEVELOPMENT_TEAM=$DEVELOPMENT_TEAM"
    CODE_SIGN_STYLE=Automatic
    CODE_SIGN_IDENTITY="Apple Development"
  )
  EXTENSION_SIGNING=("${BASE_SIGNING[@]}")
  HOST_SIGNING=("${BASE_SIGNING[@]}")
  ENGINE_SIGNING=("${BASE_SIGNING[@]}")
else
  BASE_SIGNING=(
    "DEVELOPMENT_TEAM=$DEVELOPMENT_TEAM"
    CODE_SIGN_STYLE=Automatic
    -allowProvisioningUpdates
  )
  EXTENSION_SIGNING=("${BASE_SIGNING[@]}")
  HOST_SIGNING=("${BASE_SIGNING[@]}")
  ENGINE_SIGNING=("${BASE_SIGNING[@]}")
fi

archive_engine() {
  local dest="$1"
  local path="$2"
  xcodebuild archive \
    -project StreakNative.xcodeproj \
    -scheme StreakEngine \
    -destination "$dest" \
    -archivePath "$path" \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    "${ENGINE_SIGNING[@]}"
}

archive_app_intents() {
  local dest="$1"
  local path="$2"
  xcodebuild archive \
    -project StreakNative.xcodeproj \
    -scheme StreakAppIntents \
    -destination "$dest" \
    -archivePath "$path" \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    "${ENGINE_SIGNING[@]}"
}

archive_engine "generic/platform=iOS" "$ARTIFACTS/StreakEngine-iOS"
archive_engine "generic/platform=iOS Simulator" "$ARTIFACTS/StreakEngine-Sim"

archive_app_intents "generic/platform=iOS" "$ARTIFACTS/StreakAppIntents-iOS"
archive_app_intents "generic/platform=iOS Simulator" "$ARTIFACTS/StreakAppIntents-Sim"

rm -rf "$ARTIFACTS/StreakEngine.xcframework"
xcodebuild -create-xcframework \
  -framework "$ARTIFACTS/StreakEngine-iOS.xcarchive/Products/Library/Frameworks/StreakEngine.framework" \
  -framework "$ARTIFACTS/StreakEngine-Sim.xcarchive/Products/Library/Frameworks/StreakEngine.framework" \
  -output "$ARTIFACTS/StreakEngine.xcframework"

echo "Built: $ARTIFACTS/StreakEngine.xcframework"

rm -rf "$ARTIFACTS/StreakAppIntents.xcframework"
xcodebuild -create-xcframework \
  -framework "$ARTIFACTS/StreakAppIntents-iOS.xcarchive/Products/Library/Frameworks/StreakAppIntents.framework" \
  -framework "$ARTIFACTS/StreakAppIntents-Sim.xcarchive/Products/Library/Frameworks/StreakAppIntents.framework" \
  -output "$ARTIFACTS/StreakAppIntents.xcframework"

echo "Built: $ARTIFACTS/StreakAppIntents.xcframework"

build_extension() {
  local destination="$1"
  local configuration="$2"
  local build_dir="$3"
  local output_appex="$4"
  local signing_args=()
  if [[ "$destination" == *"iOS Simulator"* ]]; then
    signing_args=(
      CODE_SIGN_IDENTITY="-"
    )
  else
    signing_args=("${EXTENSION_SIGNING[@]}")
  fi

  rm -rf "$build_dir"
  mkdir -p "$build_dir"

  echo "Building extension ($configuration) for $destination ..."
  xcodebuild -project StreakNative.xcodeproj \
    -scheme StreakSaverExtension \
    -destination "$destination" \
    -configuration "$configuration" \
    build \
    "CONFIGURATION_BUILD_DIR=$build_dir" \
    "${signing_args[@]}"

  if [ ! -d "$build_dir/StreakSaverExtension.appex" ]; then
    echo "Extension build finished but StreakSaverExtension.appex was not found in $build_dir" >&2
    exit 1
  fi

  rm -rf "$output_appex"
  cp -R "$build_dir/StreakSaverExtension.appex" "$output_appex"
  echo "Built: $output_appex"
}

build_extension \
  "generic/platform=iOS Simulator" \
  Debug \
  "$ARTIFACTS/extension-build-sim" \
  "$ARTIFACTS/StreakSaverExtension-Sim.appex"

build_extension \
  "generic/platform=iOS" \
  Release \
  "$ARTIFACTS/extension-build-ios" \
  "$ARTIFACTS/StreakSaverExtension.appex"

echo "Building StreakSaverHost (main app provisioning profile for MAUI publish) ..."
xcodebuild -project StreakNative.xcodeproj \
  -scheme StreakSaverHost \
  -destination "generic/platform=iOS" \
  -configuration Release \
  build \
  "${HOST_SIGNING[@]}"

ROOT_SCRIPTS="$(cd "$IOS_DIR/../.." && pwd)/scripts"
if [ -x "$ROOT_SCRIPTS/sync-ios-provisioning-profiles.sh" ]; then
  "$ROOT_SCRIPTS/sync-ios-provisioning-profiles.sh"
fi

#!/usr/bin/env bash
# Build a signed StreakSaver.ipa for AltStore + refresh dist/altstore/source.json
#
# Prerequisites:
#   - macOS, full Xcode, Apple ID signed in (Xcode → Settings → Accounts)
#   - At least one "Apple Development" certificate in Keychain (create via Xcode if missing)
#   - App Group enabled for com.jon2g.tiktokstreaksaver (+ extension) in developer.apple.com
#
# Usage:
#   export DEVELOPMENT_TEAM=XXXXXXXXXX
#   ./scripts/build-ios-release.sh [version] [git-tag]
#
# Examples:
#   ./scripts/build-ios-release.sh 1.0.0 v1.0.0
#   ./scripts/build-ios-release.sh 2.2.0 v2.2.0
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-1.0.0}"
TAG="${2:-v$VERSION}"
OUT="$ROOT/release-out"
PROJECT="$ROOT/src/TiktokStreakSaver/TiktokStreakSaver.csproj"

if ! command -v xcodebuild >/dev/null 2>&1; then
  echo "Full Xcode is required." >&2
  exit 1
fi

if [ -z "${DEVELOPMENT_TEAM:-}" ]; then
  echo "DEVELOPMENT_TEAM is not set." >&2
  echo "" >&2
  echo "Find it in https://developer.apple.com/account → Membership (Team ID)," >&2
  echo "or Xcode → Settings → Accounts → [your team] → Team ID." >&2
  echo "" >&2
  echo "  export DEVELOPMENT_TEAM=XXXXXXXXXX" >&2
  exit 1
fi

if ! security find-identity -v -p codesigning 2>/dev/null | grep -q "Apple Development\|Apple Distribution\|iPhone Developer"; then
  echo "No iOS code signing certificate in Keychain." >&2
  echo "Open Xcode → Settings → Accounts → your Apple ID → Manage Certificates…" >&2
  echo "→ + → Apple Development. Then run this script again." >&2
  exit 1
fi

echo "==> Native (StreakEngine + device widget extension)"
chmod +x "$ROOT/src/ios/build.sh"
"$ROOT/src/ios/build.sh"

if [ ! -d "$ROOT/src/ios/artifacts/StreakSaverExtension.appex" ]; then
  echo "Device extension missing: src/ios/artifacts/StreakSaverExtension.appex" >&2
  echo "Ensure DEVELOPMENT_TEAM is valid and ./src/ios/build.sh succeeded." >&2
  exit 1
fi

echo "==> Provisioning profiles for main app"
chmod +x "$ROOT/scripts/sync-ios-provisioning-profiles.sh" "$ROOT/scripts/ensure-main-ios-profile.sh"
"$ROOT/scripts/sync-ios-provisioning-profiles.sh"
if ! "$ROOT/scripts/ensure-main-ios-profile.sh"; then
  echo "Run ./src/ios/build.sh with DEVELOPMENT_TEAM set (creates StreakSaverHost profile), then retry." >&2
  exit 1
fi

echo "==> dotnet publish (Release, ios-arm64)"
mkdir -p "$OUT"
dotnet publish "$PROJECT" -f net9.0-ios -c Release \
  -r ios-arm64 \
  -p:ArchiveOnBuild=true \
  -p:DevelopmentTeam="$DEVELOPMENT_TEAM" \
  -p:CodesignEntitlements=Platforms/iOS/Entitlements.plist \
  -p:SkipIosNativeBuild=true \
  -o "$OUT/build"

IPA_SRC=$(find "$OUT" "$ROOT/src/TiktokStreakSaver/bin" "$ROOT/src/TiktokStreakSaver" -name "*.ipa" 2>/dev/null | head -1)
if [ -z "$IPA_SRC" ]; then
  echo "No .ipa produced. Check publish output above." >&2
  exit 1
fi

cp -f "$IPA_SRC" "$OUT/StreakSaver.ipa"
echo "==> IPA: $OUT/StreakSaver.ipa ($(stat -f%z "$OUT/StreakSaver.ipa" 2>/dev/null || stat -c%s "$OUT/StreakSaver.ipa") bytes)"

echo "==> AltStore source.json"
"$ROOT/scripts/update-altstore-source.sh" "$OUT/StreakSaver.ipa" "$VERSION" "$TAG"

echo ""
echo "Done. Next steps:"
echo "  1. Commit: git add dist/altstore/source.json && git commit -m \"chore(ios): AltStore source $VERSION\""
echo "  2. Push to main (so raw source.json URL is live)"
echo "  3. Create release:"
echo "       git tag $TAG && git push origin $TAG"
echo "       gh release create $TAG $OUT/StreakSaver.ipa --title \"Streak Saver $VERSION (iOS)\" --notes \"AltStore sideload. See Profile → Shortcut setup guide after install.\""
echo "  4. In AltStore, add source:"
echo "       https://raw.githubusercontent.com/Jon2G/TiktokStreakSaver/main/dist/altstore/source.json"

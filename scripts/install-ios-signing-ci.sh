#!/usr/bin/env bash
# Install iOS signing certificate + provisioning profiles for GitHub Actions.
# Expects secrets as env vars (see .github/workflows/ios-release.yml).
set -euo pipefail

require_env() {
  local name="$1"
  if [ -z "${!name:-}" ]; then
    echo "Missing required secret/env: $name" >&2
    exit 1
  fi
}

require_env IOS_DISTRIBUTION_CERTIFICATE_BASE64
require_env IOS_DISTRIBUTION_CERTIFICATE_PASSWORD
require_env IOS_PROVISIONING_PROFILE_BASE64
require_env IOS_EXTENSION_PROVISIONING_PROFILE_BASE64
require_env IOS_DEVELOPMENT_TEAM

install_profile() {
  local b64="$1"
  local label="$2"
  local tmp
  tmp="$(mktemp)"
  echo "$b64" | base64 --decode > "$tmp"

  local uuid name
  uuid="$(security cms -D -i "$tmp" | plutil -extract UUID raw -)"
  name="$(security cms -D -i "$tmp" | plutil -extract Name raw -)"

  local dir
  for dir in \
    "$HOME/Library/MobileDevice/Provisioning Profiles" \
    "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"; do
    mkdir -p "$dir"
    cp "$tmp" "$dir/$uuid.mobileprovision"
  done
  rm -f "$tmp"

  echo "Installed $label profile: $name ($uuid)" >&2
  echo "$name"
}

echo "==> Import signing certificate"
CERT_PATH="$RUNNER_TEMP/build_certificate.p12"
KEYCHAIN_PATH="$RUNNER_TEMP/app-signing.keychain-db"
echo "$IOS_DISTRIBUTION_CERTIFICATE_BASE64" | base64 --decode > "$CERT_PATH"

security create-keychain -p "" "$KEYCHAIN_PATH"
security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
security unlock-keychain -p "" "$KEYCHAIN_PATH"
security import "$CERT_PATH" -P "$IOS_DISTRIBUTION_CERTIFICATE_PASSWORD" -A -t cert -f pkcs12 -k "$KEYCHAIN_PATH"
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "" "$KEYCHAIN_PATH"
security list-keychain -d user -s "$KEYCHAIN_PATH" "$HOME/Library/Keychains/login.keychain-db"

echo "==> Install provisioning profiles"
IOS_MAIN_PROFILE_NAME="$(install_profile "$IOS_PROVISIONING_PROFILE_BASE64" "main app")"
IOS_EXTENSION_PROFILE_NAME="$(install_profile "$IOS_EXTENSION_PROVISIONING_PROFILE_BASE64" "extension")"

export IOS_MAIN_PROFILE_NAME IOS_EXTENSION_PROFILE_NAME

{
  echo "IOS_MAIN_PROFILE_NAME=$IOS_MAIN_PROFILE_NAME"
  echo "IOS_EXTENSION_PROFILE_NAME=$IOS_EXTENSION_PROFILE_NAME"
} >> "${GITHUB_ENV:-/dev/null}"

echo "==> Verify code signing identity"
if ! security find-identity -v -p codesigning | grep -q "Apple Development\|Apple Distribution"; then
  echo "No valid code signing identity after import." >&2
  security find-identity -v -p codesigning || true
  exit 1
fi

security find-identity -v -p codesigning

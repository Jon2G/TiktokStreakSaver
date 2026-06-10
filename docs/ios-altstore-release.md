# iOS AltStore release (IPA + source.json)

This is the **Option A** flow: publish `StreakSaver.ipa` on GitHub Releases and install via AltStore.

## One-time setup

### 1. Apple signing (required)

1. Install **Xcode** and sign in: **Xcode → Settings → Accounts** → your Apple ID.
2. **Manage Certificates…** → **+** → **Apple Development** (creates cert **and** private key in Keychain).

#### Free Personal Team (no paid $99 program)

You **cannot** revoke or manage certificates on [developer.apple.com](https://developer.apple.com) — that’s normal. Use Xcode + Keychain only:

1. **Keychain Access** → **login** keychain → category **My Certificates**
2. Find **Apple Development: …** — expand it. If there is **no** small key icon under the cert, the private key is missing → select the cert → **Delete** (certificate only).
3. **Xcode → Settings → Accounts → Manage Certificates…**
4. Select any **Apple Development** row → **−** (minus) to remove from Xcode’s list
5. **+** → **Apple Development** again (creates a fresh cert + key on **this** Mac)
6. Verify in Terminal:
   ```bash
   security find-identity -v -p codesigning
   ```
   You must see `Apple Development: Your Name`.

**App Groups:** a free Personal Team often **cannot** use App Groups or widget extensions in provisioning. The main app may still build for AltStore; Shortcuts/widget may not work until you join the paid Apple Developer Program ($99/year). Try the build — if signing fails on App Group entitlements, see [ios-signing.md](ios-signing.md).

#### Paid Apple Developer Program

Register App IDs with **App Groups** (`group.com.jon2g.tiktokstreaksaver`) for the main app and `com.jon2g.tiktokstreaksaver.StreakSaverExtension`.

4. Note your **Team ID** (10 characters): **Xcode → Settings → Accounts** → your team → **Team ID** (Personal Team works too).

### 2. Mac build tools

```bash
xcode-select -s /Applications/Xcode.app/Contents/Developer
brew install xcodegen
dotnet workload install maui-ios
```

## Build IPA + update AltStore JSON

```bash
cd TiktokStreakSaver
chmod +x scripts/build-ios-release.sh scripts/update-altstore-source.sh

export DEVELOPMENT_TEAM=YOUR_TEAM_ID
./scripts/build-ios-release.sh 1.0.0 ios-v1.0.0
```

Outputs:

| File | Purpose |
|------|---------|
| `release-out/StreakSaver.ipa` | Upload to GitHub Release |
| `dist/altstore/source.json` | AltStore catalog (version, size, download URL) |

## Publish to GitHub

```bash
# 1. Commit updated source.json (use raw.githubusercontent.com URL — works without extra Pages config)
git add dist/altstore/source.json
git commit -m "chore(ios): AltStore source 1.0.0"
git push origin main

# 2. Tag + release with the IPA
git tag ios-v1.0.0
git push origin ios-v1.0.0

gh release create ios-v1.0.0 release-out/StreakSaver.ipa \
  --title "Streak Saver 1.0.0 (iOS)" \
  --notes "iOS build for AltStore. After install: log in to TikTok, then Profile → Shortcut setup guide."
```

The `downloadURL` in `source.json` must match the iOS release tag:  
`https://github.com/Jon2G/TiktokStreakSaver/releases/download/ios-v1.0.0/StreakSaver.ipa`

iOS releases use the `ios-v*` tag prefix so they do not collide with Android `v*` releases.

## Install on iPhone (AltStore)

1. **AltStore** → **Sources** → **+**
2. Add: `https://raw.githubusercontent.com/Jon2G/TiktokStreakSaver/main/dist/altstore/source.json`
3. Install **Streak Saver**
4. Open app → log in → **Profile → Shortcut setup guide**

Refresh the app in AltStore about every **7 days** (free Apple ID limit).

## CI alternative

Push a tag `v*` to run [`.github/workflows/ios-release.yml`](../.github/workflows/ios-release.yml). Set repository secrets:

- `IOS_DEVELOPMENT_TEAM`
- `IOS_DISTRIBUTION_CERTIFICATE_BASE64` + password (optional, for distribution signing)
- `IOS_PROVISIONING_PROFILE_BASE64` + extension profile (optional)

Without distribution secrets, use the local script above with **Apple Development** signing (fine for AltStore).

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `No valid iOS code signing keys` | Create **Apple Development** cert in Xcode |
| `StreakSaverExtension.appex` missing | `export DEVELOPMENT_TEAM=…` then `./src/ios/build.sh` |
| AltStore source fails to load | Push `source.json` to `main`; use **raw** URL above |
| `size: 0` in JSON | Re-run `update-altstore-source.sh` after IPA exists |
| Cert + key in Keychain but `0 valid identities` | Install [Apple WWDR G3](https://www.apple.com/certificateauthority/AppleWWDRCAG3.cer) (double-click), then run `security find-identity -v -p codesigning` |
| `Ad Hoc code signing is not allowed with SDK` | Set `export DEVELOPMENT_TEAM=…` before `./src/ios/build.sh` (do not use `CODE_SIGN_IDENTITY=-`) |

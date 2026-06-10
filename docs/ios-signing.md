# iOS build & signing (AltStore)

## Prerequisites

- macOS with **full Xcode** (not Command Line Tools only)
- `xcode-select -s /Applications/Xcode.app/Contents/Developer`
- .NET 9 SDK with **MAUI iOS workload**: `dotnet workload install maui-ios`
- [xcodegen](https://github.com/yonaskolb/XcodeGen): `brew install xcodegen`

## Native streak engine & widget extension

See **[`src/ios/README.md`](../src/ios/README.md)** for full details. Quick start:

```bash
chmod +x src/ios/build.sh
export DEVELOPMENT_TEAM=XXXXXXXXXX
./src/ios/build.sh
```

On macOS, `dotnet build` for `net9.0-ios` **automatically** rebuilds native code when inputs change (SHA-256 hash). Use `-p:SkipIosNativeBuild=true` in CI if you already built artifacts.

Outputs:

- `src/ios/artifacts/StreakEngine.xcframework`
- `src/ios/artifacts/StreakSaverExtension.appex` (with `DEVELOPMENT_TEAM`)

## Build the MAUI iOS app

```bash
cd src/TiktokStreakSaver
dotnet build TiktokStreakSaver.csproj -f net9.0-ios -c Release -r ios-arm64
dotnet publish TiktokStreakSaver.csproj -f net9.0-ios -c Release -r ios-arm64 -p:ArchiveOnBuild=true
```

## Signing secrets (GitHub Actions)

Configure repository secrets for CI:

| Secret | Purpose |
|--------|---------|
| `IOS_DISTRIBUTION_CERTIFICATE_BASE64` | Base64 `.p12` distribution certificate |
| `IOS_DISTRIBUTION_CERTIFICATE_PASSWORD` | P12 password |
| `IOS_PROVISIONING_PROFILE_BASE64` | Base64 `.mobileprovision` for `com.jon2g.tiktokstreaksaver` |
| `IOS_EXTENSION_PROVISIONING_PROFILE_BASE64` | Profile for `com.jon2g.tiktokstreaksaver.StreakSaverExtension` |

Entitlements must include App Group `group.com.jon2g.tiktokstreaksaver` on **both** the main app and the widget extension.

## AltStore source

Users add this source URL in AltStore:

`https://raw.githubusercontent.com/Jon2G/TiktokStreakSaver/main/dist/altstore/source.json`

**Release checklist:** [ios-altstore-release.md](ios-altstore-release.md) — run `./scripts/build-ios-release.sh`, commit `dist/altstore/source.json`, tag, and `gh release create` with `StreakSaver.ipa`.

Or run `./scripts/update-altstore-source.sh release-out/StreakSaver.ipa 1.0.0 v1.0.0` after building the IPA manually.

# iOS native layer (StreakEngine + widget extension)

Swift/Xcode code used by the .NET MAUI app for **Shortcuts App Intents**, **WidgetKit**, and **WKWebView streak automation**. The main app lives in [`../TiktokStreakSaver`](../TiktokStreakSaver).

## Layout

```
src/ios/
├── README.md                 ← this file
├── build.sh                  ← build StreakEngine.xcframework (+ optional .appex)
├── project.yml               ← XcodeGen spec → StreakNative.xcodeproj
├── artifacts/                ← build outputs (gitignored)
│   ├── StreakEngine.xcframework
│   ├── StreakAppIntents.xcframework   ← App Shortcuts metadata (force-loaded into MAUI app)
│   ├── StreakSaverExtension-Sim.appex
│   ├── StreakSaverExtension.appex
│   └── .build-inputs.sha256         ← fingerprint of last successful native build
├── SharedAppIntents/         ← MaintainStreakIntent + StreakShortcuts (main app + widget)
├── StreakEngine/             ← shared framework (WebView runner, App Group I/O)
│   └── …/Resources/tiktok_automation.js   ← copied from MAUI at build (gitignored)
├── StreakSaverHost/            ← minimal app (provisioning only; not shipped)
└── StreakSaverExtension/     ← widget + MaintainStreakIntent
```

## Prerequisites

- macOS with **full Xcode** (not Command Line Tools only)
- `xcode-select -s /Applications/Xcode.app/Contents/Developer`
- [XcodeGen](https://github.com/yonaskolb/XcodeGen): `brew install xcodegen`
- .NET 9 + MAUI iOS workload for the C# app: `dotnet workload install maui-ios`

## Build native artifacts (manual)

From the **repository root**:

```bash
chmod +x src/ios/build.sh scripts/build-ios-native.sh scripts/ensure-ios-native-built.sh

# Requires Team ID (iOS 26+ SDK disallows ad-hoc signing)
export DEVELOPMENT_TEAM=XXXXXXXXXX
./src/ios/build.sh
```

`scripts/build-ios-native.sh` is a thin wrapper around `src/ios/build.sh`.

The build also compiles **StreakSaverHost** (`com.jon2g.tiktokstreaksaver`) so Xcode creates the main-app provisioning profile required by `dotnet publish`. Profiles are synced to `~/Library/MobileDevice/Provisioning Profiles` for MSBuild.

After a successful build, `tiktok_automation.js` is copied from the MAUI project into the framework bundle so it stays in sync with Android.

## Automatic rebuild on `dotnet build` (hash)

When you build the MAUI project for iOS **on macOS**, MSBuild runs `scripts/ensure-ios-native-built.sh` first. It:

1. Computes a SHA-256 hash of all native inputs (Swift, `project.yml`, plists, bundled JS, and the shared MAUI `tiktok_automation.js`).
2. Compares it to `artifacts/.build-inputs.sha256`.
3. Rebuilds via `src/ios/build.sh` if the hash changed or `StreakEngine.xcframework` is missing.

Skip automatic native builds (e.g. CI already built artifacts):

```bash
dotnet build ... -p:SkipIosNativeBuild=true
# or
export SKIP_IOS_NATIVE_BUILD=1
```

Force a clean native rebuild:

```bash
rm -f src/ios/artifacts/.build-inputs.sha256 src/ios/artifacts/StreakEngine.xcframework
./src/ios/build.sh
```

## Build the MAUI iOS app

Simulator (no device signing):

```bash
cd src/TiktokStreakSaver
dotnet build TiktokStreakSaver.csproj -f net9.0-ios -c Debug -r iossimulator-arm64 -p:CodesignEntitlements=
```

Without a development team, the App Group is unavailable on Simulator (settings/cookies use the app Library instead; Shortcuts/widget sharing may be limited). For full App Group + extension behavior, set `DEVELOPMENT_TEAM` and rebuild native artifacts. Harmless `[open] unable to make sandbox extension` lines from WebKit may still appear in the Xcode console.

Device / Release:

```bash
dotnet build TiktokStreakSaver.csproj -f net9.0-ios -c Release -r ios-arm64
dotnet publish TiktokStreakSaver.csproj -f net9.0-ios -c Release -r ios-arm64 -p:ArchiveOnBuild=true
```

Native artifacts are built automatically on macOS unless `SkipIosNativeBuild=true`.

## App Group

Both the MAUI app and `StreakEngine` use:

- App Group: `group.com.jon2g.tiktokstreaksaver`
- Shared cookies file: `cookies.json` in the group container
- Shared settings: `NSUserDefaults` suite with the same keys as Android `Preferences`

## Shortcuts troubleshooting

Apple requires **`AppShortcutsProvider` in the main app executable**, not only in the widget extension. This repo ships `StreakAppIntents` as a **static archive force-loaded** into the MAUI binary (`Kind=Static`, `ForceLoad=true`) plus copies **`Metadata.appintents`** into the main `.app` bundle during `dotnet build`.

If Shortcuts logs `Couldn't find AppShortcutsProvider`, `LNContextErrorDomain Code=2001`, or `Failed to load a definition for com.jon2g.tiktokstreaksaver.<TeamID>`:

1. Rebuild native artifacts: `export DEVELOPMENT_TEAM=… && ./src/ios/build.sh`
2. Rebuild/reinstall the MAUI app on device
3. Verify metadata is present: `ls TiktokStreakSaver.app/Metadata.appintents/` (should contain `extract.actionsdata`, not only under `PlugIns/…`)
4. Delete the old shortcut/automation and add **Maintain TikTok Streaks** again from the Shortcuts action list

## More documentation

- Signing, CI, AltStore: [`../../docs/ios-signing.md`](../../docs/ios-signing.md)
- Product architecture: [`.cursor/ios_plan.md`](../../.cursor/ios_plan.md)

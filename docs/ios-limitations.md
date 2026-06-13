# iOS platform limitations (Streak Saver)

**Status: experimental — not ready for production use.**

Streak Saver on iPhone is a **best-effort experimental port**. Despite substantial engineering (Shortcuts App Intents, native WKWebView automation, widget integration, App Group sharing, and MAUI bridging), **iOS platform policy prevents the same reliability as Android** for automated TikTok streak messaging.

**For daily streak protection, use the [Android app](https://github.com/Jon2G/TiktokStreakSaver/releases).** Do not rely on the iOS build as your primary streak saver today.

---

## Why Android works and iOS does not (for this app)

| Capability | Android (supported) | iOS (experimental) |
|---|---|---|
| Run while phone is **locked** | Yes — foreground service + WebView | **No** — Shortcuts cannot launch the app when locked |
| Run with screen off / app closed | Yes — scheduled alarm + background service | **No** — must foreground the app briefly |
| In-app scheduling | AlarmManager (1–23 h interval) | **No** — only Shortcuts personal automations |
| TikTok automation | WKWebView in background context | WKWebView only while app is **foreground** |
| Distribution | APK sideload / install | AltStore; free certs expire every **7 days** |

This is not a bug in Streak Saver. It follows from how Apple allows third-party apps to run code, launch UI, and use WebKit.

---

## What iOS *can* do (best effort)

- **Run Now** on the Dashboard when you open the app manually.
- **Shortcuts automation** when your iPhone is **unlocked** at the scheduled time (app opens briefly; TikTok runs in an invisible browser).
- **Home screen widget** tap after unlock (queues the same run).
- **Notifications** when a run finishes or login is required.

---

## What iOS *cannot* do (platform limits)

### 1. Locked iPhone

Our App Shortcut uses `openAppWhenRun = true` so the app can start WKWebView automation. iOS **refuses to launch or foreground the app from Shortcuts while the device is locked**.

Typical Shortcuts error:

> **The operation couldn't be completed. Unable to launch because the device couldn't be unlocked.**

Turning off **Ask Before Running** only removes the confirmation tap; it does **not** allow runs on a locked phone.

### 2. Silent / fully background automation

TikTok messaging uses **WKWebView** and JavaScript automation (same approach as Android). On iOS:

- The runner needs a **foreground window scene** to attach the WebView.
- Apple **suspends WebView JavaScript** when the app is backgrounded — automation stalls or fails.

There is no Android-style “foreground service for WebView” API for third-party App Store / sideload apps.

### 3. In-app timer

iOS does not expose an equivalent to Android’s exact alarms + foreground service for this use case. Scheduling is delegated to the **Shortcuts** app (user-created personal automation).

### 4. Sideload fragility (AltStore)

Free Apple development certificates expire about every **7 days**. If AltStore does not refresh, the app and Shortcuts actions stop working until re-signed.

---

## Common messages (not always bugs)

| Message | Meaning |
|---|---|
| *Unable to launch because the device couldn't be unlocked* | Automation ran while iPhone was locked. Schedule for a time you are usually **awake and unlocked**, or run manually after unlock. |
| *Couldn't find AppShortcutsProvider* | Shortcuts metadata out of date after an app update. Reinstall, open app once, **delete and recreate** the automation. See [src/ios/README.md](../src/ios/README.md). |
| Run completes with **0 messages sent** | Scheduled mode only messages friends **not yet sent today**. Use **Run Now** to force all enabled friends. |

---

## Practical tips (if you still test on iOS)

1. Pick a daily Shortcut time when your phone is **usually unlocked** (e.g. morning after waking up).
2. Keep **Ask Before Running** off, but accept that **lock screen still blocks** the run.
3. Use **Run Now** on the Dashboard as a fallback after you unlock.
4. After app updates, **recreate** the Shortcuts automation.
5. Keep AltStore refreshing so the certificate does not expire.

---

## What we built on iOS (still blocked by the OS)

- Shortcuts **App Intents** with `AppShortcutsProvider` in the main app binary
- Static **StreakAppIntents** framework force-loaded into MAUI + `Metadata.appintents` copy
- Native **StreakEngine** WKWebView runner (off-screen window)
- Swift → C# bridge so Shortcuts invoke `IosStreakRunner` with session refresh
- Widget extension + pending-run retries for widget taps
- Foreground scene wait before WebView attach

None of this enables locked-phone or silent background WebView automation. A different architecture (e.g. unofficial HTTP APIs without WebView) would be a **separate product**, fragile, and out of scope for this project.

---

## Official Apple documentation

Apple does not publish one page titled “Shortcuts cannot run while locked.” The behavior follows from **foreground launch requirements**, **App Intents** design, and **background execution limits**. Relevant official references:

| Topic | Documentation |
|---|---|
| App Intents — app opens when intent runs | [AppIntent.openAppWhenRun](https://developer.apple.com/documentation/appintents/appintent/openappwhenrun) |
| App Intents — foreground vs background modes | [AppIntent.supportedModes](https://developer.apple.com/documentation/appintents/appintent/supportedmodes) |
| Background Tasks (limited background work) | [BackgroundTasks](https://developer.apple.com/documentation/backgroundtasks) |
| Long-running background tasks (constraints) | [Performing long-running tasks on iOS and iPadOS](https://developer.apple.com/documentation/backgroundtasks/performing_long-running_tasks_on_ios_and_ipados) |
| UI and background execution windows | [Preparing your UI to run in the background](https://developer.apple.com/documentation/uikit/app_and_environment/scenes/preparing_your_ui_to_run_in_the_background) |
| WKWebView (WebKit) | [WKWebView](https://developer.apple.com/documentation/webkit/wkwebview) |
| Shortcuts (user guide) | [Shortcuts User Guide](https://support.apple.com/guide/shortcuts/welcome/ios) |

---

## Summary

**Streak Saver for iOS is experimental and not stable for the same use cases as Android.** We document these limits openly so users are not surprised by locked-device failures or missed streaks. For reliable automatic streak messaging, **use Android**.

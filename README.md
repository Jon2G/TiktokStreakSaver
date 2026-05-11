# 🔥 Streak Saver

**Automatically send TikTok messages to keep your streaks alive!**

Streak Saver is an open-source Android app that runs in the background and automatically sends messages to your TikTok friends every 23 hours, ensuring you never lose your streaks.

<p align="center">
  <img src="docs/imgs/main_screen.png" alt="Streak Saver Main Screen" width="300"/>
</p>

## ✨ Features

- **🕐 Automatic Scheduling** — Hours-only run interval (1–23 h, default 23 h)
- **👥 Multiple Friends** — Add, edit, import, and export your streak list
- **📱 Background Service** — Runs even when the app is closed
- **🔔 Smart Notifications** — Foreground progress notification while sending; separate completion / offline alert
- **🌐 Offline Resilience** — If the device has no Wi-Fi or mobile data when an alarm fires, the run is skipped, the user is notified, and a retry is automatically scheduled in 1 hour (no streak slot wasted)
- **🔄 Boot Persistence** — Automatically reschedules the next run after device restart
- **🔐 Cookie-Based Session** — Login once via WebView; the captured user agent is reused for every background run so cookies stay valid
- **⚡ Battery Optimized** — Requests battery-optimization exemption for reliable background execution
- **🎲 Randomized Messages** — Optional pool of 50 short built-in streak variants ("streak", "yo streak", "hey", …) so you don't always send the same text
- **🧭 Skip Unreachable** — Optional behavior (on by default for new installs) to continue with other friends when a chat cannot be opened
- **🗂 Multi-Page UI** — Dashboard, Friends, History, and Profile pages with a custom floating nav bar
- **📊 Run History & Stats** — Per-run success rate, last-run summary, and exportable run logs
- **📦 In-App Updates** — Check for updates from the app; downloaded APKs install via the system installer (FileProvider on Android)
- **🔄 Up-to-Date Automation** — TikTok WebView automation script maintained for current inbox behavior

## 📋 Requirements

- Android 7.0 (API 24) or higher
- TikTok account
- Internet connection

## 📥 Installation

### Option 1: Download APK (Recommended)

1. Go to the [Releases](../../releases) page
2. Download the latest `StreakSaver-vX.X.X.apk`
3. Enable "Install from unknown sources" on your Android device
4. Install the APK

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/Jon2G/TiktokStreakSaver.git
cd TiktokStreakSaver

# Build for Android
cd src/TiktokStreakSaver
dotnet build -f net9.0-android -c Release
```

## 🚀 Getting Started

1. **Open the app** and tap **Login Required** to sign in to TikTok in the WebView
2. **Add friends** on the **Friends** page (TikTok username; optional display name)
3. **Pick a message** on the **Dashboard**: type your own, or enable **Randomize messages** in **Profile** to use the built-in variants
4. **Configure scheduling** on the **Profile** page: enable **Background automation** and pick a **Run interval** (1–23 hours)
5. **Grant permissions** when prompted (exact alarms, battery exemption, notifications)
6. Use **Run Now** on the Dashboard for an immediate send, or rely on background automation when scheduled

## ⚙️ How It Works

```
┌─────────────────────────────────────────────────────────────┐
│                      Streak Saver                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [App Start] → Schedule next-run alarm (1–23 h)             │
│        ↓                                                    │
│  [Alarm fires] → AlarmReceiver triggers                     │
│        ↓                                                    │
│  [StreakService] → Start foreground service                 │
│        ↓                                                    │
│  [Network check] → No internet? Skip + retry in 1 h         │
│        ↓                                                    │
│  [WebView] → Load tiktok.com/messages                       │
│        ↓                                                    │
│  [For each friend] → Find chat → Send message               │
│        ↓                                                    │
│  [Complete] → Schedule next run → Stop service              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 🛠️ Tech Stack

- **.NET 9 MAUI** — Cross-platform framework (Android-only target)
- **Android WebView** — TikTok web automation
- **AlarmManager** — Exact-alarm scheduling for precise hourly triggers
- **Foreground Service** — Reliable background execution with wake-lock
- **JavaScript Injection** — Web page automation
- **Shell + custom FloatingNavBar** — Multi-page navigation

## 📁 Project Structure

```
TiktokStreakSaver/
├── src/TiktokStreakSaver/
│   ├── Models/                    # Data models (FriendConfig, StreakRunResult)
│   ├── Services/                  # SettingsService, SessionService,
│   │                              # TikTokWebViewHelper, UpdateService
│   ├── Pages/                     # DashboardPage, FriendsPage,
│   │                              # HistoryPage, ProfilePage
│   ├── Views/                     # FloatingNavBar + custom drawables
│   ├── Platforms/Android/
│   │   ├── Services/              # StreakService (foreground)
│   │   ├── Receivers/             # AlarmReceiver, BootReceiver
│   │   └── Resources/             # Android resources, splash, themes
│   ├── Resources/                 # MAUI resources (icons, fonts, styles)
│   ├── AppShell.xaml              # Multi-tab Shell host (TabBar hidden)
│   ├── LoginPage.xaml             # TikTok login WebView
│   └── AboutPopupPage.xaml        # About / changelog / update modal
├── .github/workflows/             # CI/CD pipelines (see below)
├── .github/dependabot.yml         # Dependency update PRs (Actions + NuGet)
└── docs/                          # Documentation & screenshots
```

### Continuous integration

| Workflow | When it runs | What it does |
|----------|----------------|----------------|
| [`ci.yml`](.github/workflows/ci.yml) | Push to `main` / `master`, all pull requests, manual | `dotnet build` for **Android** (Debug, no signing) |
| [`android-release.yml`](.github/workflows/android-release.yml) | Git tag `v*`, manual dispatch | Signed Release APK, artifact upload, GitHub Release |

## 🔧 Configuration

### Run interval

On the **Profile** page, the **Run interval** stepper sets how many hours to wait after each successful run before the next automatic background run. Allowed range is **1–23 hours**, default **23 h** (the classic streak window).

### Randomized messages

On **Profile**, toggle **Randomize messages** to send a random short variant from a built-in pool of 50 streak phrases instead of your custom text. The pool is reshuffled when exhausted so a sequence is never repeated within a run.

### Custom message

On **Dashboard**, the **Run Configuration** card lets you type any message. The default is:

```
Hey! Keeping our streak alive! 🔥
```

## 🤝 Contributing

Contributions are welcome! Here's how you can help:

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** your changes (`git commit -m 'Add amazing feature'`)
4. **Push** to the branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Setup

```bash
# Prerequisites
- .NET 9 SDK
- Visual Studio 2022 or VS Code with C# extension
- Android SDK (API 24+)

# Install MAUI workload
dotnet workload install maui-android

# Restore and build
cd src/TiktokStreakSaver
dotnet restore
dotnet build -f net9.0-android
```

## ⚠️ Disclaimer

This app is for educational purposes only. Use responsibly and in accordance with TikTok's Terms of Service. The developers are not responsible for any account restrictions or bans that may result from using this application.

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET MAUI](https://dotnet.microsoft.com/apps/maui)
- Inspired by the need to never lose a streak again

### Special thanks to [eulfn](https://github.com/eulfn) (Feener / [streak-tiktok](https://github.com/eulfn/streak-tiktok))

The current architecture of this app is a direct port of the rewrite that **eulfn** did in his fork [`streak-tiktok`](https://github.com/eulfn/streak-tiktok) (project name "Feener"). Massive credit to him and contributors for designing and implementing essentially the entire current UX and most of the stability fixes that ship in this version, including:

- **Multi-page architecture** — Dashboard / Friends / History / Profile pages hosted in a hidden Shell `TabBar`, replacing the original single-page layout
- **Custom `FloatingNavBar`** — Pill-style bottom navigation with active-state indicators
- **Custom `IDrawable`s** — `NormalProgressDrawable` (linear daily-progress bar) and `SuccessRateDrawable` (donut-style success-rate chart) used on Dashboard and History
- **Theme system** — Inter font family registration, expanded color palette (`PrimaryLight`, `PrimarySubtle`, warm `OffBlack`, semantic gray ramp, card/border tokens), and `SectionCard` / typography styles
- **Android 12+ splash screen** — `App.StartingTheme` with `windowSplashScreenAnimatedIcon` + theme-aware splash background
- **WebView viewport fix** — Headless WebView is sized to a real 1920×1080 viewport so TikTok's virtualized chat list actually renders its lazy children
- **Session-stored user agent** — The user agent captured at login is reused for every background run so TikTok cookies stay valid (with a modern Chrome desktop fallback)
- **Cookie-based session helper** — `TikTokWebViewHelper` centralizes WebView configuration, cookie checks, and login-status detection
- **Randomized normal messages** — Built-in pool of 50 short streak variants with shuffle / reshuffle-on-exhaust logic
- **Bounded per-friend retry** — Replaces the original "advance index on null friend" with a retry budget so a transient mismatch doesn't silently skip a friend
- **`OnDestroy` mutex reset** — Safety net so a system-killed service doesn't leave the static `_isRunning` lock stuck on
- **Improved automation `tiktok_automation.js`** — Virtualized list scrolling, checkpoint retry logic, exact-username matching, and pre-click extraction
- **Friends import / export, search, and bulk enable / disable** UI on the Friends page
- **Run history with success-rate stats** and exportable run logs on the History page
- **Profile page** consolidating account, display name / photo, settings toggles, and the explicit *Check for updates* button
- **CSS-style `Focused` visual states** on `Entry` and `Editor`, and `Android` handler mappers that strip the default Entry/Editor underline

If you like the look and feel of this app, please go star the upstream fork as well: <https://github.com/eulfn/streak-tiktok>.

---

<p align="center">
  Made with ❤️ and 🔥
</p>

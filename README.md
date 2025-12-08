# 🔥 Streak Saver

**Automatically send TikTok messages to keep your streaks alive!**

Streak Saver is an open-source Android app that runs in the background and automatically sends messages to your TikTok friends every 23 hours, ensuring you never lose your streaks.

<p align="center">
  <img src="docs/imgs/main_screen.jpeg" alt="Streak Saver Main Screen" width="300"/>
</p>

## ✨ Features

- **🕐 Automatic Scheduling** - Sends messages every 23 hours automatically
- **👥 Multiple Friends** - Configure multiple friends to maintain streaks with
- **📱 Background Service** - Works even when the app is closed
- **🔔 Smart Notifications** - Shows progress only while sending, then disappears
- **🔄 Boot Persistence** - Automatically reschedules after device restart
- **🔐 Session Management** - Login once, stays logged in
- **⚡ Battery Optimized** - Requests battery optimization exemption for reliability

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
git clone https://github.com/yourusername/TiktokStreakSaver.git
cd TiktokStreakSaver

# Build for Android
cd src/TiktokStreakSaver
dotnet build -f net9.0-android -c Release
```

## 🚀 Getting Started

1. **Open the app** and tap "Login to TikTok"
2. **Sign in** to your TikTok account in the WebView
3. **Add friends** by tapping "+ Add" and entering their TikTok username
4. **Set your message** in the "Message to Send" field
5. **Enable scheduling** by toggling the switch
6. **Grant permissions** by tapping "Permissions" and allowing:
   - Battery optimization exemption
   - Exact alarm permission
   - Notification permission

## ⚙️ How It Works

```
┌─────────────────────────────────────────────────────────────┐
│                      Streak Saver                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  [App Start] → Schedule 23hr Alarm                          │
│        ↓                                                    │
│  [Every 23hrs] → AlarmReceiver triggers                     │
│        ↓                                                    │
│  [StreakService] → Start Foreground Service                 │
│        ↓                                                    │
│  [WebView] → Load TikTok Messages                           │
│        ↓                                                    │
│  [For each friend] → Find chat → Send message               │
│        ↓                                                    │
│  [Complete] → Schedule next run → Stop service              │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 🛠️ Tech Stack

- **.NET 9 MAUI** - Cross-platform framework
- **Android WebView** - TikTok web automation
- **AlarmManager** - Precise 23-hour scheduling
- **Foreground Service** - Reliable background execution
- **JavaScript Injection** - Web page automation

## 📁 Project Structure

```
TiktokStreakSaver/
├── src/TiktokStreakSaver/
│   ├── Models/                    # Data models
│   ├── Services/                  # Business logic services
│   ├── Platforms/Android/
│   │   ├── Services/              # Android foreground service
│   │   ├── Receivers/             # Alarm & boot receivers
│   │   └── Resources/             # Android resources
│   ├── Resources/                 # MAUI resources (icons, fonts)
│   ├── MainPage.xaml              # Main UI
│   └── LoginPage.xaml             # TikTok login WebView
├── .github/workflows/             # CI/CD pipelines
└── docs/                          # Documentation & screenshots
```

## 🔧 Configuration

### Changing the Interval

The default interval is 23 hours. To modify, update the `DefaultIntervalHours` constant in `Services/SettingsService.cs`:

```csharp
public const int DefaultIntervalHours = 23;
```

### Custom Message

You can set any message in the app's UI. The default message is:
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

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [.NET MAUI](https://dotnet.microsoft.com/apps/maui)
- Inspired by the need to never lose a streak again

---

<p align="center">
  Made with ❤️ and 🔥
</p>

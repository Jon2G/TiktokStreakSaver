# Privacy Policy — Streak Saver

**Effective date:** June 9, 2026

Streak Saver ("the app") is an open-source Android application maintained by
Jon2G and contributors. This policy explains what data the app handles and
what it does NOT do.

## TL;DR

The app does not collect, transmit, sell, or share any personal data about
you. There are no analytics, no advertising, no third-party tracking SDKs,
and no developer-owned servers receiving anything about you or your usage.
Everything you configure stays on your device.

## What the app stores on your device

The following information is saved on your device using Android's standard
preferences and the system WebView cookie store. It never leaves your device
unless you back it up or export it yourself:

- TikTok session data: cookies and the browser user-agent string captured at
  login, so background runs can send streak messages without re-logging in.
- The friends/groups list you add: TikTok usernames, optional display names,
  group names, enable/disable state, and counters (last-sent timestamp,
  success/failure counts).
- Your custom message text and the randomized-messages toggle.
- Scheduling preferences: interval or fixed daily time, retry counter for
  the day, the "Send streaks if battery is running low" toggle, and similar
  app settings.
- Run history: per-run results (success/failure per friend), capped to the
  most recent 50 runs.
- Optional display name and profile photo you set in the app. The photo,
  if any, is stored as a file in the app's private storage.

You can erase all of this at any time by:

- Tapping "Logout / Clear Session" on the Profile page (clears TikTok
  cookies and session flag), and/or
- Android Settings → Apps → Streak Saver → Storage → Clear data, or by
  uninstalling the app.

## Network connections

The app communicates directly with the following services from your device.
The developer does not operate any intermediary server and does not see
this traffic:

1. **TikTok** (`tiktok.com` and subdomains) — when you log in and whenever a
   scheduled run starts. The app loads TikTok in an Android WebView and uses
   your own cookies to open chat threads and send the message you
   configured. TikTok's own privacy policy applies to whatever happens
   inside that WebView.
2. **GitHub** (`api.github.com`, `github.com`, and release-asset download
   URLs) — for the in-app "Check for updates" feature. The app calls the
   public GitHub Releases API for repo `Jon2G/TiktokStreakSaver` and
   downloads APK files from release assets. The only header sent is
   `User-Agent: TiktokStreakSaver/1.0`. No account, device, or personal
   identifiers are sent.

That is the entire set of outbound connections. There are no analytics
endpoints, no crash-reporting endpoints, no advertising endpoints.

## Permissions and why they are requested

| Permission | Purpose |
|---|---|
| `INTERNET`, `ACCESS_NETWORK_STATE` | Connect to TikTok to send streaks and to GitHub for update checks; detect Wi-Fi / cellular availability before starting a run |
| `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_DATA_SYNC`, `WAKE_LOCK` | Keep the streak-sender service alive while a run is in progress |
| `SCHEDULE_EXACT_ALARM`, `USE_EXACT_ALARM` | Trigger the next streak run at the configured time |
| `RECEIVE_BOOT_COMPLETED` | Reschedule the alarm after the device restarts |
| `REQUEST_IGNORE_BATTERY_OPTIMIZATIONS` | Ask Android to keep the app reliable in the background |
| `POST_NOTIFICATIONS` | Show progress and completion notifications |
| `REQUEST_INSTALL_PACKAGES` | Install updated APKs downloaded from GitHub Releases |

The app uses these permissions only for the purposes listed above.

## Minimum Age

The app is not intended for users below the age of 13 or your region's age
requirements for TikTok.

## Third parties

The app does not embed any third-party advertising, analytics, attribution,
marketing, or social SDKs. The only services it talks to are TikTok (your
own account) and GitHub (public release metadata), each with their own
privacy practices, which apply to your direct interaction with them.

## Security

Local data is kept inside the app's private storage, protected by Android's
standard app-sandboxing model. Because nothing is transmitted to a
developer-controlled server, there is no remote breach risk on the
developer's side.

## Changes

Updates to this policy are committed to `PRIVACY.md` in the repository and
reflected in the in-app Privacy screen on the next app update. The
"Effective date" above changes whenever the text changes.

## Contact

This app is maintained on GitHub. To report a privacy concern, open an
issue at <https://github.com/Jon2G/TiktokStreakSaver/issues>.

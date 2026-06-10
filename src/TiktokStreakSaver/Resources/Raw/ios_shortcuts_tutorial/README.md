# iOS Shortcuts tutorial media

Drop screen recordings, GIFs, or stills here so they appear in the in-app **Shortcuts setup** guide.

## File names (expected by the app)

| File | Step |
|------|------|
| `step_01.gif` | Open Shortcuts → Automation tab |
| `step_02.gif` | Create personal automation → Time of Day |
| `step_03.gif` | Daily schedule + time |
| `step_04.gif` | Add “Maintain TikTok Streaks” action |
| `step_05.gif` | Disable “Ask Before Running” → Done |
| `step_06.gif` | Optional home screen widget |

You can use **`.gif`**, **`.png`**, **`.jpg`**, or **`.mp4`** instead of `.gif` — update `MediaFileName` in `Services/IosShortcutTutorialCatalog.cs` if you change extensions.

## Tips

- Keep clips short (3–8 seconds); the UI loops videos automatically.
- Portrait phone recordings work best (9:16).
- After adding files, rebuild the MAUI app (`dotnet build` with `net9.0-ios`).

Until files are added, the tutorial shows a placeholder with the expected path.

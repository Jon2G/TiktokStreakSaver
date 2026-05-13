export interface Screenshot {
  /** File under `public/imgs/`. */
  src: string
  alt: string
  caption: string
}

// Build-time base URL injected by Vite (e.g. `/TiktokStreakSaver/`) so deployed
// links resolve correctly under the GitHub Pages project path.
const base = `${import.meta.env.BASE_URL}imgs/`

export const screenshots: Screenshot[] = [
  { src: base + 'Welcome.jpeg', alt: 'Welcome / login screen', caption: 'One-tap TikTok login' },
  { src: base + 'Main_Secondary.jpeg', alt: 'Dashboard', caption: 'Daily progress at a glance' },
  { src: base + 'Burst_Main.jpeg', alt: 'Run in progress', caption: 'Run streaks in seconds' },
  { src: base + 'Burst_Secondary.jpeg', alt: 'Per-friend feedback', caption: 'Live per-friend status' },
  { src: base + 'Settings.jpeg', alt: 'Settings', caption: 'Schedule, retries, battery toggle' },
  { src: base + 'Update_Screen.jpeg', alt: 'Update screen', caption: 'In-app update checks' }
]

export interface HowStep {
  number: number
  title: string
  description: string
  image: string
  alt: string
}

export const howItWorks: HowStep[] = [
  {
    number: 1,
    title: 'Log in to TikTok',
    description: 'Sign in once inside the in-app WebView. Cookies stay on your device.',
    image: base + 'Welcome.jpeg',
    alt: 'Welcome screen with login button'
  },
  {
    number: 2,
    title: 'Add your friends',
    description: 'Drop in usernames or group names. Toggle individuals on or off any time.',
    image: base + 'Main_Secondary.jpeg',
    alt: 'Friends management screen'
  },
  {
    number: 3,
    title: 'Pick a schedule',
    description: 'Run every N hours, or at a fixed daily time. Configure retries and the battery rule.',
    image: base + 'Settings.jpeg',
    alt: 'Settings screen'
  },
  {
    number: 4,
    title: 'Streaks send themselves',
    description: 'A foreground service quietly opens TikTok and sends each message. You just check the notification.',
    image: base + 'Burst_Main.jpeg',
    alt: 'Streak run notification and progress'
  }
]

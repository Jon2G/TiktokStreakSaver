export interface Feature {
  /** Inline SVG path data (24x24 viewbox) for a lightweight per-feature icon. */
  iconPath: string
  title: string
  description: string
}

/** Stable list of feature highlights shown on the marketing page. */
export const features: Feature[] = [
  {
    iconPath:
      'M12 8v4l2.5 2.5M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
    title: 'Automatic scheduling',
    description:
      'Set a rolling interval (1-23 h) or pin a fixed daily time. Streaks fire on their own.'
  },
  {
    iconPath:
      'M16 11a4 4 0 1 0-8 0 4 4 0 0 0 8 0Zm6 9a8 8 0 0 0-16 0',
    title: 'Friends and groups',
    description:
      'Add as many TikTok friends and group chats as you want. Enable, disable, import and export.'
  },
  {
    iconPath:
      'M13 2 4 14h7l-1 8 9-12h-7l1-8Z',
    title: 'Smart resilience',
    description:
      'Hourly retries when Wi-Fi or send fails (up to 3/day), auto-recovery when the network is back, and an early-fire if the battery is running low.'
  },
  {
    iconPath:
      'M4 7h16M4 12h16M4 17h10M19 17l2 2-2 2',
    title: 'Background and boot-safe',
    description:
      'Foreground service + exact alarms. Reschedules itself after every reboot so you never lose a slot.'
  },
  {
    iconPath:
      'M4 5h16v14H4zM4 9h16M9 5v14',
    title: 'Randomized messages',
    description:
      '50 built-in short streak variants, reshuffled when exhausted, so you do not always send the exact same line.'
  },
  {
    iconPath:
      'M12 2 4 5v6c0 5 3.5 9 8 11 4.5-2 8-6 8-11V5l-8-3Zm-1 13-3-3 1.4-1.4L11 12.2l4.6-4.6L17 9l-6 6Z',
    title: 'Privacy first',
    description:
      'No servers, no ads, no analytics, no third-party SDKs. Your data never leaves your device.'
  }
]

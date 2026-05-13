<script setup lang="ts">
import { PRIVACY_URL } from '@/data/links'

const promises = [
  'No analytics, no tracking SDKs',
  'No advertising, ever',
  'No developer-controlled backend',
  'Your TikTok cookies stay on your device'
]
</script>

<template>
  <section id="privacy" class="py-20 md:py-28">
    <div class="mx-auto max-w-5xl px-4 sm:px-6">
      <div
        class="relative overflow-hidden rounded-3xl border border-slate-200/70 bg-gradient-to-br from-white to-brand-50/40 p-8 shadow-sm dark:border-slate-800 dark:from-slate-900 dark:to-brand-900/20 sm:p-12"
      >
        <div class="grid items-center gap-10 lg:grid-cols-[1.4fr_1fr]">
          <div>
            <span class="text-sm font-semibold uppercase tracking-wider text-brand-500">Privacy</span>
            <h2 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">
              Your streaks. Your data. Your phone.
            </h2>
            <p class="mt-4 text-lg text-slate-600 dark:text-slate-300">
              Streak Saver is local-first by design. The only services the app contacts are TikTok
              itself (using your own session) and GitHub for update checks. There is no
              developer-controlled server in the middle.
            </p>
            <a
              :href="PRIVACY_URL"
              target="_blank"
              rel="noopener"
              class="mt-6 inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-5 py-3 text-sm font-semibold text-slate-800 transition hover:border-slate-300 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:hover:border-slate-600 dark:hover:bg-slate-800"
            >
              Read the full Privacy Policy
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="h-4 w-4">
                <path d="M7 17 17 7M9 7h8v8" />
              </svg>
            </a>
          </div>

          <ul class="grid gap-3">
            <li
              v-for="(p, i) in promises"
              :key="i"
              class="flex items-center gap-3 rounded-xl border border-slate-200/70 bg-white/70 px-4 py-3 backdrop-blur dark:border-slate-800 dark:bg-slate-900/70"
            >
              <span
                class="flex h-7 w-7 items-center justify-center rounded-full bg-brand-500/15 text-brand-600 dark:text-brand-300"
              >
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" class="h-4 w-4">
                  <path d="m5 13 4 4 10-10" />
                </svg>
              </span>
              <span class="text-sm font-medium text-slate-700 dark:text-slate-200">{{ p }}</span>
            </li>
          </ul>
        </div>
      </div>
    </div>
  </section>
</template>

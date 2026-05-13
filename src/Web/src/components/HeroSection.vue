<script setup lang="ts">
import { APK_URL, REPO_URL, RELEASES_URL } from '@/data/links'

const iconUrl = `${import.meta.env.BASE_URL}imgs/icon.png`
const heroShotUrl = `${import.meta.env.BASE_URL}imgs/Main_Secondary.jpeg`
</script>

<template>
  <section
    id="top"
    class="relative overflow-hidden"
  >
    <!-- Soft brand-tinted glow behind the hero. -->
    <div
      aria-hidden="true"
      class="pointer-events-none absolute inset-0 -z-10 bg-gradient-to-b from-brand-50/70 via-transparent to-transparent dark:from-brand-900/30"
    />
    <div
      aria-hidden="true"
      class="pointer-events-none absolute -top-32 left-1/2 -z-10 h-[420px] w-[420px] -translate-x-1/2 rounded-full bg-brand-500/15 blur-3xl dark:bg-brand-500/20"
    />

    <div class="mx-auto grid max-w-6xl items-center gap-12 px-4 py-20 sm:px-6 md:grid-cols-2 md:py-28">
      <div>
        <div class="mb-6 flex items-center gap-3">
          <img
            :src="iconUrl"
            alt="Streak Saver"
            class="h-14 w-14 rounded-2xl shadow-md"
            width="56"
            height="56"
          />
          <span
            class="inline-flex items-center gap-1.5 rounded-full border border-brand-200/70 bg-brand-50 px-3 py-1 text-xs font-medium text-brand-700 dark:border-brand-800 dark:bg-brand-900/40 dark:text-brand-200"
          >
            Free &amp; open source for Android
          </span>
        </div>

        <h1 class="text-4xl font-extrabold tracking-tight sm:text-5xl md:text-6xl">
          Never lose a
          <span class="text-brand-500">TikTok</span> streak again.
        </h1>

        <p class="mt-6 max-w-xl text-lg text-slate-600 dark:text-slate-300">
          Streak Saver runs quietly in the background and sends your streak messages every day,
          on schedule, with retries when the network drops or the battery is running low.
        </p>

        <div class="mt-8 flex flex-wrap items-center gap-3">
          <a
            :href="APK_URL"
            class="inline-flex items-center gap-2 rounded-xl bg-brand-500 px-5 py-3 text-base font-semibold text-white shadow-lg shadow-brand-500/20 transition hover:bg-brand-600"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="h-5 w-5">
              <path d="M12 3v12" />
              <path d="m7 10 5 5 5-5" />
              <path d="M5 21h14" />
            </svg>
            Download the APK
          </a>
          <a
            :href="REPO_URL"
            target="_blank"
            rel="noopener"
            class="inline-flex items-center gap-2 rounded-xl border border-slate-200 bg-white px-5 py-3 text-base font-semibold text-slate-800 transition hover:border-slate-300 hover:bg-slate-50 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 dark:hover:border-slate-600 dark:hover:bg-slate-800"
          >
            <svg viewBox="0 0 24 24" fill="currentColor" class="h-5 w-5">
              <path
                d="M12 .5a11.5 11.5 0 0 0-3.63 22.41c.57.1.79-.25.79-.55v-2c-3.21.7-3.89-1.37-3.89-1.37a3.06 3.06 0 0 0-1.28-1.69c-1.05-.71.08-.7.08-.7a2.42 2.42 0 0 1 1.77 1.19 2.46 2.46 0 0 0 3.37.96 2.47 2.47 0 0 1 .74-1.55c-2.56-.29-5.26-1.28-5.26-5.69a4.45 4.45 0 0 1 1.19-3.09 4.13 4.13 0 0 1 .11-3.05s.97-.31 3.18 1.18a11 11 0 0 1 5.78 0c2.21-1.49 3.18-1.18 3.18-1.18a4.13 4.13 0 0 1 .11 3.05 4.45 4.45 0 0 1 1.19 3.09c0 4.42-2.7 5.4-5.27 5.69a2.76 2.76 0 0 1 .79 2.14v3.17c0 .31.21.66.8.55A11.5 11.5 0 0 0 12 .5Z"
              />
            </svg>
            View on GitHub
          </a>
        </div>

        <p class="mt-4 text-xs text-slate-500 dark:text-slate-400">
          Android 7.0+ &middot; APK install &middot;
          <a :href="RELEASES_URL" target="_blank" rel="noopener" class="underline underline-offset-2 hover:text-brand-500">
            see all releases
          </a>
        </p>
      </div>

      <div class="relative mx-auto w-full max-w-[300px] md:max-w-sm">
        <!-- Phone frame -->
        <div
          class="relative rounded-[2.25rem] border border-slate-200/70 bg-white p-2 shadow-2xl shadow-slate-900/20 dark:border-slate-800 dark:bg-slate-900"
        >
          <img
            :src="heroShotUrl"
            alt="Streak Saver dashboard"
            class="aspect-[9/19.5] w-full rounded-[1.75rem] object-cover"
          />
        </div>
        <div
          aria-hidden="true"
          class="pointer-events-none absolute -bottom-6 left-1/2 -z-10 h-12 w-3/4 -translate-x-1/2 rounded-full bg-brand-500/30 blur-2xl"
        />
      </div>
    </div>
  </section>
</template>

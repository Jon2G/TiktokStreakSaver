<script setup lang="ts">
import { REPO_URL, PRIVACY_URL, ISSUES_URL, LICENSE_URL } from '@/data/links'

const year = new Date().getFullYear()
const iconUrl = `${import.meta.env.BASE_URL}imgs/icon.png`
</script>

<template>
  <footer class="border-t border-slate-200/70 bg-white py-10 dark:border-slate-800/70 dark:bg-slate-950">
    <div class="mx-auto flex max-w-6xl flex-col items-center gap-6 px-4 sm:px-6 md:flex-row md:justify-between">
      <div class="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
        <img
          :src="iconUrl"
          alt=""
          aria-hidden="true"
          class="h-6 w-6 rounded-md"
          width="24"
          height="24"
        />
        <span>&copy; {{ year }} Streak Saver. MIT-licensed open source.</span>
      </div>

      <nav class="flex flex-wrap items-center justify-center gap-x-6 gap-y-2 text-sm font-medium text-slate-600 dark:text-slate-300">
        <a :href="REPO_URL" target="_blank" rel="noopener" class="transition hover:text-brand-500">GitHub</a>
        <a :href="PRIVACY_URL" target="_blank" rel="noopener" class="transition hover:text-brand-500">Privacy</a>
        <a :href="LICENSE_URL" target="_blank" rel="noopener" class="transition hover:text-brand-500">License</a>
        <a :href="ISSUES_URL" target="_blank" rel="noopener" class="transition hover:text-brand-500">Report an issue</a>
      </nav>
    </div>

    <p class="mx-auto mt-6 max-w-3xl px-4 text-center text-xs text-slate-500 sm:px-6 dark:text-slate-500">
      Streak Saver is an unofficial open-source tool and is not affiliated with, endorsed by, or
      sponsored by TikTok Inc. Use in accordance with TikTok's Terms of Service.
    </p>
  </footer>
</template>

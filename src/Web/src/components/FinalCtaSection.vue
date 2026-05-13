<script setup lang="ts">
import { APK_URL, REPO_URL } from '@/data/links'
</script>

<template>
  <section class="py-20 md:py-28">
    <div class="mx-auto max-w-4xl px-4 sm:px-6">
      <div
        class="relative overflow-hidden rounded-3xl bg-brand-500 px-8 py-14 text-center text-white shadow-2xl shadow-brand-500/30 sm:px-12 sm:py-20"
      >
        <div
          aria-hidden="true"
          class="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.25),transparent_60%),radial-gradient(circle_at_70%_80%,rgba(255,255,255,0.15),transparent_55%)]"
        />
        <h2 class="text-3xl font-extrabold tracking-tight sm:text-4xl">
          Ready to never lose another streak?
        </h2>
        <p class="mx-auto mt-4 max-w-xl text-base text-white/90">
          Install the latest signed APK. No store needed, no account to create, no payment.
        </p>
        <div class="mt-8 flex flex-wrap items-center justify-center gap-3">
          <a
            :href="APK_URL"
            class="inline-flex items-center gap-2 rounded-xl bg-white px-6 py-3 text-base font-semibold text-brand-700 shadow-lg transition hover:bg-slate-100"
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
            class="inline-flex items-center gap-2 rounded-xl border border-white/30 bg-white/10 px-6 py-3 text-base font-semibold text-white backdrop-blur transition hover:bg-white/20"
          >
            <svg viewBox="0 0 24 24" fill="currentColor" class="h-5 w-5">
              <path d="M12 2 14.39 9.34h7.61L15.79 13.7l2.39 7.34L12 16.5l-6.18 4.54 2.39-7.34L2.01 9.34h7.6L12 2Z" />
            </svg>
            Star on GitHub
          </a>
        </div>
      </div>
    </div>
  </section>
</template>

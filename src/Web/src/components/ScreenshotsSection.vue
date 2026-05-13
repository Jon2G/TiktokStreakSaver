<script setup lang="ts">
import { screenshots } from '@/data/screenshots'
</script>

<template>
  <section id="screenshots" class="py-20 md:py-28">
    <div class="mx-auto max-w-6xl px-4 sm:px-6">
      <div class="mx-auto max-w-2xl text-center">
        <span class="text-sm font-semibold uppercase tracking-wider text-brand-500">Inside the app</span>
        <h2 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">
          A clean, fast Android UI.
        </h2>
        <p class="mt-4 text-lg text-slate-600 dark:text-slate-300">
          Dashboard, friends, settings, and run history &mdash; everything you need to keep streaks alive.
        </p>
      </div>

      <div class="scroll-rail mt-14 flex snap-x snap-mandatory gap-6 overflow-x-auto px-2 pb-6">
        <figure
          v-for="(shot, i) in screenshots"
          :key="i"
          class="snap-center"
        >
          <div
            class="relative w-[220px] shrink-0 overflow-hidden rounded-[2rem] border border-slate-200/70 bg-white p-1.5 shadow-xl shadow-slate-900/10 transition hover:-translate-y-1 hover:shadow-2xl dark:border-slate-800 dark:bg-slate-900 sm:w-[260px]"
          >
            <img
              :src="shot.src"
              :alt="shot.alt"
              class="aspect-[9/19.5] w-full rounded-[1.5rem] object-cover"
              loading="lazy"
            />
          </div>
          <figcaption class="mt-3 text-center text-sm font-medium text-slate-600 dark:text-slate-300">
            {{ shot.caption }}
          </figcaption>
        </figure>
      </div>
    </div>
  </section>
</template>

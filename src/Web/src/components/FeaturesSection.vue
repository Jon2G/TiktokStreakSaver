<script setup lang="ts">
import { features } from '@/data/features'
</script>

<template>
  <section id="features" class="border-y border-slate-200/70 bg-white py-20 dark:border-slate-800/70 dark:bg-slate-900/40 md:py-28">
    <div class="mx-auto max-w-6xl px-4 sm:px-6">
      <div class="mx-auto max-w-2xl text-center">
        <span class="text-sm font-semibold uppercase tracking-wider text-brand-500">Why Streak Saver</span>
        <h2 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">
          Built so a streak never slips through.
        </h2>
        <p class="mt-4 text-lg text-slate-600 dark:text-slate-300">
          A focused tool with the reliability features the original task actually needs &mdash;
          and nothing it doesn't.
        </p>
      </div>

      <div class="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        <article
          v-for="(feature, i) in features"
          :key="i"
          class="group relative overflow-hidden rounded-2xl border border-slate-200/70 bg-slate-50 p-6 transition hover:-translate-y-0.5 hover:border-brand-200 hover:shadow-lg hover:shadow-slate-900/5 dark:border-slate-800 dark:bg-slate-900/50 dark:hover:border-brand-800"
        >
          <div
            class="mb-4 inline-flex h-11 w-11 items-center justify-center rounded-xl bg-brand-50 text-brand-600 transition group-hover:bg-brand-500 group-hover:text-white dark:bg-brand-900/40 dark:text-brand-300"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
              class="h-5 w-5"
            >
              <path :d="feature.iconPath" />
            </svg>
          </div>
          <h3 class="text-lg font-semibold">{{ feature.title }}</h3>
          <p class="mt-2 text-sm leading-relaxed text-slate-600 dark:text-slate-300">
            {{ feature.description }}
          </p>
        </article>
      </div>
    </div>
  </section>
</template>

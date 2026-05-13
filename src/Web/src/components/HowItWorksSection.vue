<script setup lang="ts">
import { howItWorks } from '@/data/screenshots'
</script>

<template>
  <section id="how" class="border-y border-slate-200/70 bg-white py-20 dark:border-slate-800/70 dark:bg-slate-900/40 md:py-28">
    <div class="mx-auto max-w-6xl px-4 sm:px-6">
      <div class="mx-auto max-w-2xl text-center">
        <span class="text-sm font-semibold uppercase tracking-wider text-brand-500">How it works</span>
        <h2 class="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">
          Four steps. Then you forget about it.
        </h2>
      </div>

      <ol class="mt-14 grid gap-8 sm:grid-cols-2 lg:grid-cols-4">
        <li
          v-for="step in howItWorks"
          :key="step.number"
          class="relative flex flex-col items-center text-center"
        >
          <div
            class="relative mb-5 w-[160px] overflow-hidden rounded-[1.75rem] border border-slate-200/70 bg-slate-50 p-1.5 shadow-lg shadow-slate-900/5 dark:border-slate-800 dark:bg-slate-900"
          >
            <img
              :src="step.image"
              :alt="step.alt"
              class="aspect-[9/19.5] w-full rounded-[1.25rem] object-cover"
              loading="lazy"
            />
            <div
              class="absolute -top-3 -left-3 flex h-9 w-9 items-center justify-center rounded-full bg-brand-500 text-sm font-bold text-white shadow-md"
            >
              {{ step.number }}
            </div>
          </div>
          <h3 class="text-lg font-semibold">{{ step.title }}</h3>
          <p class="mt-2 max-w-xs text-sm text-slate-600 dark:text-slate-300">
            {{ step.description }}
          </p>
        </li>
      </ol>
    </div>
  </section>
</template>

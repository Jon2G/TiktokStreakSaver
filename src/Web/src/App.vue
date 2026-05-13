<script setup lang="ts">
import { onMounted, onBeforeUnmount } from 'vue'
import SiteHeader from '@/components/SiteHeader.vue'
import HeroSection from '@/components/HeroSection.vue'
import FeaturesSection from '@/components/FeaturesSection.vue'
import ScreenshotsSection from '@/components/ScreenshotsSection.vue'
import HowItWorksSection from '@/components/HowItWorksSection.vue'
import PrivacySection from '@/components/PrivacySection.vue'
import FinalCtaSection from '@/components/FinalCtaSection.vue'
import SiteFooter from '@/components/SiteFooter.vue'

// Mirror the system color-scheme onto the <html class="dark"> toggle so
// Tailwind's `dark:` modifier picks it up.
const darkMq = window.matchMedia('(prefers-color-scheme: dark)')

function applyTheme(isDark: boolean) {
  document.documentElement.classList.toggle('dark', isDark)
}

function onMqChange(e: MediaQueryListEvent) {
  applyTheme(e.matches)
}

onMounted(() => {
  applyTheme(darkMq.matches)
  darkMq.addEventListener('change', onMqChange)
})

onBeforeUnmount(() => {
  darkMq.removeEventListener('change', onMqChange)
})
</script>

<template>
  <SiteHeader />
  <main>
    <HeroSection />
    <FeaturesSection />
    <ScreenshotsSection />
    <HowItWorksSection />
    <PrivacySection />
    <FinalCtaSection />
  </main>
  <SiteFooter />
</template>

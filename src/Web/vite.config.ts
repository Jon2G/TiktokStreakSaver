import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

// Base path matches the GitHub Pages project URL:
// https://jon2g.github.io/TiktokStreakSaver/
export default defineConfig({
  plugins: [vue(), tailwindcss()],
  base: '/TiktokStreakSaver/',
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  }
})

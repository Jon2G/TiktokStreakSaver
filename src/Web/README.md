# Streak Saver — Web showcase

Single-page Vue 3 + TypeScript + Tailwind CSS marketing site that lives at
[https://jon2g.github.io/TiktokStreakSaver/](https://jon2g.github.io/TiktokStreakSaver/).

The hero CTA links directly to the latest signed APK:

```
https://github.com/Jon2G/TiktokStreakSaver/releases/latest/download/StreakSaver.apk
```

That stable filename is produced by the Android release workflow
([`.github/workflows/android-release.yml`](../../.github/workflows/android-release.yml)) — each
release uploads both `StreakSaver-v<version>.apk` (archive history) and `StreakSaver.apk` (the
URL above) so this link does not break between releases.

## Stack

- [Vue 3](https://vuejs.org/) (Composition API, `<script setup>`)
- [TypeScript](https://www.typescriptlang.org/)
- [Vite 5](https://vitejs.dev/)
- [Tailwind CSS v4](https://tailwindcss.com/) via the `@tailwindcss/vite` plugin (CSS-first
  configuration in [`src/styles.css`](src/styles.css), no `tailwind.config.js`).

No vue-router, no Pinia, no PWA: it is a single page with in-page anchor navigation.

## Local development

Requires Node.js 20+ and npm.

```bash
cd src/Web
npm install
npm run dev
```

Then open the URL printed by Vite (it serves at the project base
`/TiktokStreakSaver/`, e.g. `http://localhost:5173/TiktokStreakSaver/`).

## Production build

```bash
npm run build
npm run preview
```

- `npm run build` runs `vue-tsc -b` and then `vite build`, producing static files in
  `src/Web/dist/`.
- `npm run preview` serves that output locally so you can sanity-check the base path
  (`http://localhost:4173/TiktokStreakSaver/`).

## Deployment

Pushes to `main` / `master` that touch `src/Web/**` (or the workflow file itself) trigger
[`.github/workflows/web-pages.yml`](../../.github/workflows/web-pages.yml). That workflow:

1. Builds the site with `npm ci && npm run build`.
2. Uploads `src/Web/dist` as a GitHub Pages artifact.
3. Deploys it via `actions/deploy-pages@v4`.

**One-time setup:** in the repo **Settings → Pages**, set **Source** to **GitHub Actions**.
After the first successful deploy, the page URL appears in the workflow run summary and the
`github-pages` environment.

## Layout

```
src/Web/
├── public/
│   ├── favicon.png
│   ├── og-image.png
│   └── imgs/            # screenshots copied from docs/imgs/
└── src/
    ├── App.vue
    ├── main.ts
    ├── styles.css
    ├── data/            # links, feature copy, screenshot list
    └── components/      # SiteHeader, HeroSection, FeaturesSection, ...
```

When you tweak feature copy or swap a screenshot, you only need to edit `src/data/*.ts` and the
public image — every section is data-driven.

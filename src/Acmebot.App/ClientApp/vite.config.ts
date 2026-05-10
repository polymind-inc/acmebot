import { fileURLToPath, URL } from 'node:url';

import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';

const apiOrigin = process.env.ACMEBOT_API_ORIGIN;
const buildSourceMap = process.env.ACMEBOT_BUILD_SOURCEMAP === 'true';
const dashboardVersion = normalizeVersion(process.env.ACMEBOT_DASHBOARD_VERSION);
const dashboardCommitHash = normalizeCommitHash(process.env.ACMEBOT_DASHBOARD_COMMIT ?? process.env.GITHUB_SHA);

function normalizeVersion(value: string | undefined): string {
  const version = value?.trim();

  if (!version) {
    return 'dev';
  }

  if (/^v?\d/i.test(version)) {
    return version.startsWith('v') || version.startsWith('V') ? `v${version.slice(1)}` : `v${version}`;
  }

  return version;
}

function normalizeCommitHash(value: string | undefined): string {
  return value?.trim() || 'local';
}

export default defineConfig({
  base: '/dashboard-vnext/',
  plugins: [vue()],
  define: {
    __ACMEBOT_DASHBOARD_VERSION__: JSON.stringify(dashboardVersion),
    __ACMEBOT_DASHBOARD_COMMIT_HASH__: JSON.stringify(dashboardCommitHash)
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  },
  build: {
    outDir: '../wwwroot/dashboard-vnext',
    emptyOutDir: true,
    sourcemap: buildSourceMap
  },
  server: {
    port: 5173,
    proxy: apiOrigin
      ? {
          '/api': {
            target: apiOrigin,
            changeOrigin: true
          }
        }
      : undefined
  }
});

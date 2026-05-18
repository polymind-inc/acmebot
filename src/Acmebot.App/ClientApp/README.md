# Acmebot Dashboard

This folder contains the Vite + Vue 3 + TypeScript implementation of the Acmebot dashboard.

## Commands

```powershell
npm install
npm run dev
npm run build
npm run lint
```

Linting is handled by ESLint flat config with ESLint Stylistic. `npm run lint` checks Vue and TypeScript files.

`npm run build` writes the static dashboard assets directly to `../wwwroot` so they can be served from the Azure Functions app root.

Production builds always call the same-origin `/api/*` Azure Functions endpoints. The mock API switch is limited to the Vite development server, so files generated under `../wwwroot` are safe to deploy with the Function app package.

Source maps are disabled by default for deployable output. Set `ACMEBOT_BUILD_SOURCEMAP=true` before `npm run build` when you need browser debugging symbols.

The dashboard footer displays Acmebot build metadata embedded by Vite at build time. Set `ACMEBOT_VERSION` to the Acmebot release version and `ACMEBOT_COMMIT_HASH` to the source commit hash before `npm run build`; when the version variable is omitted, `v1.0.0` is used, and when the commit hash variable is omitted, GitHub Actions `GITHUB_SHA` is used if available. Version and commit values link to the matching GitHub release and commit when possible.

When the embedded Acmebot version is a release-like value such as `v5.0.0`, the dashboard checks Acmebot GitHub releases, including prereleases, and shows an upgrade notification when a newer version is available. Non-version values such as `dev` skip this check.

For local API proxying during Vite development, set `ACMEBOT_API_ORIGIN` to an Azure Functions host, for example `http://localhost:7071`.

To run the dashboard locally without an Azure Functions host, start Vite with mock data:

```powershell
$env:VITE_ACMEBOT_USE_MOCKS = 'true'
npm run dev
```

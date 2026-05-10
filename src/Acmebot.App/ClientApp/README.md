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

`npm run build` writes the static dashboard assets to `../wwwroot/dashboard-vnext` so they can be served by the existing Azure Functions static page endpoint while the current `/dashboard` UI remains available.

Production builds always call the same-origin `/api/*` Azure Functions endpoints. The mock API switch is limited to the Vite development server, so files generated under `../wwwroot/dashboard-vnext` are safe to deploy with the Function app package.

Source maps are disabled by default for deployable output. Set `ACMEBOT_BUILD_SOURCEMAP=true` before `npm run build` when you need browser debugging symbols.

The dashboard footer displays build metadata embedded by Vite at build time. Set `ACMEBOT_DASHBOARD_VERSION` to the release version and `ACMEBOT_DASHBOARD_COMMIT` to the source commit hash before `npm run build`; when the commit variable is omitted, GitHub Actions `GITHUB_SHA` is used if available.

For local API proxying during Vite development, set `ACMEBOT_API_ORIGIN` to an Azure Functions host, for example `http://localhost:7071`.

To run the dashboard locally without an Azure Functions host, start Vite with mock data:

```powershell
$env:VITE_ACMEBOT_USE_MOCKS = 'true'
npm run dev
```

<script setup lang="ts">
function scrollToSection(event: MouseEvent, selector: string) {
  event.preventDefault();

  document.querySelector(selector)?.scrollIntoView({
    behavior: "smooth",
    block: "start"
  });
}
</script>

<template>
  <div class="landing-page">
    <section class="hero">
      <div class="container">
        <div class="hero-badge">v5 Generally Available</div>
        <h1 class="hero-title">Automated SSL/TLS Certificate Management<br>for <span class="hero-highlight">Microsoft Azure</span></h1>
        <p class="hero-description">Securely manage ACME certificates with Azure Key Vault.<br class="hide-mobile"> Issue, renew, and deploy certificates across your Azure services with production-ready automation.</p>
        <div class="hero-actions">
          <a href="#deploy" class="btn btn-primary" @click="scrollToSection($event, '#deploy')">Deploy to Azure</a>
          <a href="/guide/getting-started.html" class="btn btn-outline">Get Started</a>
        </div>
        <div class="hero-stats">
          <div class="hero-stat">
            <span class="hero-stat-value">5+</span>
            <span class="hero-stat-label">Certificate Authorities</span>
          </div>
          <div class="hero-stat">
            <span class="hero-stat-value">12+</span>
            <span class="hero-stat-label">DNS Providers</span>
          </div>
          <div class="hero-stat">
            <span class="hero-stat-value">7+</span>
            <span class="hero-stat-label">Azure Services</span>
          </div>
        </div>
      </div>
    </section>

    <section id="features" class="section">
      <div class="container">
        <h2 class="section-title">Why Acmebot?</h2>
        <p class="section-subtitle">Everything you need for automated certificate management on Azure</p>
        <div class="features-grid">
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0110 0v4" /></svg>
            </div>
            <h3>Secure by Design</h3>
            <p>Certificates are stored in Azure Key Vault with managed identity authentication. No secrets to manage.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2v4m0 12v4M4.93 4.93l2.83 2.83m8.48 8.48l2.83 2.83M2 12h4m12 0h4M4.93 19.07l2.83-2.83m8.48-8.48l2.83-2.83" /></svg>
            </div>
            <h3>Fully Automated</h3>
            <p>Automatic certificate issuance and renewal. Set it up once and forget about certificate expiration.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="3" width="20" height="14" rx="2" /><path d="M8 21h8m-4-4v4" /></svg>
            </div>
            <h3>Dashboard</h3>
            <p>Dedicated web-based dashboard for certificate management. View status, issue and revoke certificates with ease.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2L2 7l10 5 10-5-10-5z" /><path d="M2 17l10 5 10-5" /><path d="M2 12l10 5 10-5" /></svg>
            </div>
            <h3>Multi-Domain Support</h3>
            <p>Issue certificates for Zone Apex, Wildcard and SANs. Manage multiple domains from a single deployment.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9" /><path d="M13.73 21a2 2 0 01-3.46 0" /></svg>
            </div>
            <h3>Webhook Notifications</h3>
            <p>Get notified via Slack, Microsoft Teams, or custom webhooks when certificates are issued or renewed.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10" /><path d="M2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z" /></svg>
            </div>
            <h3>Multiple CAs</h3>
            <p>Support for Let's Encrypt, ZeroSSL, Google Trust Services, SSL.com, Entrust, and more ACME v2 CAs.</p>
          </div>
        </div>
      </div>
    </section>

    <section class="section section-alt">
      <div class="container">
        <h2 class="section-title">What's New in v5</h2>
        <p class="section-subtitle">Production-ready on Azure Functions Flex Consumption for modern cloud workloads</p>
        <div class="v5-grid">
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
            </div>
            <h3>Flex Consumption</h3>
            <p>Built on Azure Functions Flex Consumption for cost-effective, event-driven scaling in production environments.</p>
          </div>
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /></svg>
            </div>
            <h3>Built-in VNET Integration</h3>
            <p>Native virtual network integration support for secure private connectivity to internal Azure resources.</p>
          </div>
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z" /></svg>
            </div>
            <h3>.NET 10</h3>
            <p>Runs on .NET 10 for the latest platform improvements, performance updates, and long-term maintainability.</p>
          </div>
        </div>
      </div>
    </section>

    <section id="services" class="section">
      <div class="container">
        <h2 class="section-title">Supported Azure Services</h2>
        <p class="section-subtitle">Deploy certificates across your entire Azure infrastructure</p>
        <div class="services-grid">
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-app-service.svg" alt="App Service" width="36" height="36">
            </div>
            <span>App Service</span>
            <small>Web Apps / Functions / Containers</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-container-apps.svg" alt="Container Apps" width="36" height="36">
            </div>
            <span>Container Apps</span>
            <small>Including custom DNS suffix</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-front-door.svg" alt="Front Door" width="36" height="36">
            </div>
            <span>Front Door</span>
            <small>Standard / Premium</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-application-gateway.svg" alt="Application Gateway" width="36" height="36">
            </div>
            <span>Application Gateway</span>
            <small>v2</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-api-management.svg" alt="API Management" width="36" height="36">
            </div>
            <span>API Management</span>
            <small>Custom domains</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-signalr.svg" alt="SignalR Service" width="36" height="36">
            </div>
            <span>SignalR Service</span>
            <small>Premium tier</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-virtual-machines.svg" alt="Virtual Machines" width="36" height="36">
            </div>
            <span>Virtual Machines</span>
            <small>Custom certificate deployment</small>
          </div>
        </div>
      </div>
    </section>

    <section id="providers" class="section section-alt">
      <div class="container">
        <h2 class="section-title">DNS Providers</h2>
        <p class="section-subtitle">Automatic DNS-01 challenge validation with built-in integrations and custom extensibility</p>
        <div class="providers-grid">
          <div class="provider-card">Azure DNS</div>
          <div class="provider-card">Azure Private DNS</div>
          <div class="provider-card">Cloudflare</div>
          <div class="provider-card">Amazon Route 53</div>
          <div class="provider-card">Google Cloud DNS</div>
          <div class="provider-card">GoDaddy</div>
          <div class="provider-card">Akamai Edge DNS</div>
          <div class="provider-card">DNS Made Easy</div>
          <div class="provider-card">Gandi LiveDNS</div>
          <div class="provider-card">OVH</div>
          <div class="provider-card">TransIP DNS</div>
          <div class="provider-card">PowerDNS</div>
          <div class="provider-card">Custom DNS</div>
        </div>
        <p class="providers-note">Need another provider? Custom DNS support lets you integrate virtually any DNS service through Azure Functions.</p>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <h2 class="section-title">Certificate Authorities</h2>
        <p class="section-subtitle">Includes first-class support for popular ACME v2 Certificate Authorities</p>
        <div class="ca-grid">
          <div class="ca-card">
            <strong>Let's Encrypt</strong>
            <span class="ca-badge ca-free">Free</span>
            <p>The most widely used free CA. No additional credentials required.</p>
          </div>
          <div class="ca-card">
            <strong>ZeroSSL</strong>
            <span class="ca-badge">EAB</span>
            <p>Free SSL certificates with ACME support and REST API.</p>
          </div>
          <div class="ca-card">
            <strong>Google Trust Services</strong>
            <span class="ca-badge">EAB</span>
            <p>Google's public Certificate Authority for trusted certificates.</p>
          </div>
          <div class="ca-card">
            <strong>SSL.com</strong>
            <span class="ca-badge">EAB</span>
            <p>Commercial CA offering free 90-day certificates via ACME.</p>
          </div>
          <div class="ca-card">
            <strong>Entrust</strong>
            <span class="ca-badge">EAB</span>
            <p>Enterprise-grade certificates with ACME automation support.</p>
          </div>
        </div>
      </div>
    </section>

    <section id="deploy" class="section section-alt">
      <div class="container">
        <h2 class="section-title">Deploy to Azure</h2>
        <p class="section-subtitle">One-click deployment to your Azure subscription</p>
        <div class="deploy-grid">
          <div class="deploy-card deploy-card-primary">
            <div class="deploy-version">v5<span class="deploy-tag">GA</span></div>
            <p>Production-ready deployment on Azure Functions Flex Consumption with built-in VNET integration support.</p>
            <ul class="deploy-features">
              <li>Flex Consumption plan</li>
              <li>Built-in VNET integration</li>
              <li>.NET 10 runtime</li>
            </ul>
            <div class="deploy-buttons">
              <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json" class="btn btn-primary btn-deploy">Azure Public</a>
              <a href="https://portal.azure.cn/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json" class="btn btn-outline btn-deploy">Azure China</a>
              <a href="https://portal.azure.us/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy_ui.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json" class="btn btn-outline btn-deploy">Azure Government</a>
            </div>
          </div>
        </div>
        <div class="deploy-alt">
          <p>Also available on <a href="https://registry.terraform.io/modules/polymind-inc/acmebot/azurerm/latest">Terraform Registry</a></p>
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <h2 class="section-title">Get Started in Minutes</h2>
        <p class="section-subtitle">Three simple steps to automated certificate management</p>
        <div class="steps-grid">
          <div class="step-card">
            <div class="step-number">1</div>
            <h3>Deploy</h3>
            <p>Choose the v5 Deploy to Azure option and configure your deployment with the Azure Portal wizard.</p>
          </div>
          <div class="step-card">
            <div class="step-number">2</div>
            <h3>Configure DNS</h3>
            <p>Grant the managed identity access to your DNS zone for automatic DNS-01 challenge validation.</p>
          </div>
          <div class="step-card">
            <div class="step-number">3</div>
            <h3>Issue Certificates</h3>
            <p>Use the built-in dashboard to issue your first certificate. Renewals happen automatically.</p>
          </div>
        </div>
        <div class="steps-cta">
          <a href="/guide/getting-started.html" class="btn btn-primary">Read the Documentation</a>
        </div>
      </div>
    </section>

    <section class="section section-alt">
      <div class="container">
        <h2 class="section-title">Sponsors</h2>
        <p class="section-subtitle">Thank you for your support of our development</p>
        <div class="sponsors-grid">
          <a href="https://zenarchitects.co.jp" class="sponsor-card">
            <img src="/images/zenarchitects.png" alt="ZEN Architects" loading="lazy">
          </a>
        </div>
        <div class="sponsors-cta">
          <p>Interested in supporting the project?</p>
          <a href="https://github.com/sponsors/shibayan" class="btn btn-outline">Become a Sponsor</a>
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <h2 class="section-title">Open Source</h2>
        <p class="section-subtitle">Apache License 2.0 &mdash; free to use, forever</p>
        <div class="community-grid">
          <a href="https://github.com/polymind-inc/acmebot" class="community-card">
            <svg viewBox="0 0 16 16" width="24" height="24" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" /></svg>
            <strong>GitHub</strong>
            <p>Source code, issues, and contributions</p>
          </a>
          <a href="/guide/" class="community-card">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z" /><path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z" /></svg>
            <strong>Documentation</strong>
            <p>Setup guides and configuration reference</p>
          </a>
          <a href="https://github.com/polymind-inc/acmebot/discussions" class="community-card">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z" /></svg>
            <strong>Discussions</strong>
            <p>Questions, ideas, and community support</p>
          </a>
        </div>
      </div>
    </section>

    <footer class="footer">
      <div class="container">
        <div class="footer-content">
          <div class="footer-brand">
            <strong>Acmebot</strong>
            <p>Automated ACME SSL/TLS certificate management for Microsoft Azure</p>
          </div>
          <div class="footer-links">
            <div class="footer-col">
              <h4>Project</h4>
              <a href="https://github.com/polymind-inc/acmebot">GitHub</a>
              <a href="https://github.com/polymind-inc/acmebot/releases">Releases</a>
              <a href="https://github.com/polymind-inc/acmebot/blob/master/LICENSE">License</a>
            </div>
            <div class="footer-col">
              <h4>Resources</h4>
              <a href="/guide/">Documentation</a>
              <a href="/guide/getting-started.html">Getting Started</a>
              <a href="https://github.com/polymind-inc/acmebot/discussions">Discussions</a>
            </div>
            <div class="footer-col">
              <h4>Support</h4>
              <a href="https://github.com/sponsors/shibayan">Sponsor</a>
              <a href="https://github.com/polymind-inc">Commercial Support</a>
            </div>
          </div>
        </div>
        <div class="footer-bottom">
          <p>&copy; 2026 Acmebot Project. Licensed under Apache License 2.0.</p>
        </div>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.landing-page,
.landing-page *,
.landing-page *::before,
.landing-page *::after {
  box-sizing: border-box;
  margin: 0;
  padding: 0;
}

.landing-page {
  --primary: #0078d4;
  --primary-dark: #005a9e;
  --primary-light: #deecf9;
  --primary-gradient: linear-gradient(135deg, #0078d4, #005a9e);
  --success: #107c10;
  --warning: #ff8c00;
  --neutral-bg: #f8f9fc;
  --card-bg: #ffffff;
  --text-primary: #1b1b1f;
  --text-secondary: #5c5c6d;
  --text-muted: #8e8ea0;
  --border-color: #e1e5eb;
  --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.06), 0 1px 2px rgba(0, 0, 0, 0.04);
  --shadow-md: 0 4px 12px rgba(0, 0, 0, 0.08);
  --radius: 8px;
  --radius-lg: 12px;
  --radius-xl: 16px;
  min-height: 100vh;
  color: var(--text-primary);
  font-family: Inter, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
  line-height: 1.6;
  background: #ffffff;
  -webkit-font-smoothing: antialiased;
}

.landing-page a {
  color: var(--primary);
  text-decoration: none;
}

.landing-page a:hover {
  color: var(--primary-dark);
}

.landing-page a:focus-visible,
.landing-page .btn:focus-visible {
  outline: 2px solid var(--primary);
  outline-offset: 2px;
  border-radius: 4px;
}

.landing-page img {
  max-width: 100%;
}

.container {
  max-width: 1140px;
  margin: 0 auto;
  padding: 0 24px;
}

.section {
  padding: 96px 0;
  scroll-margin-top: 80px;
}

.section-alt {
  background: var(--neutral-bg);
}

.section-title {
  font-size: 2.25rem;
  font-weight: 800;
  text-align: center;
  margin-bottom: 12px;
  letter-spacing: 0;
}

.section-subtitle {
  font-size: 1.125rem;
  color: var(--text-secondary);
  text-align: center;
  margin-bottom: 56px;
  max-width: 600px;
  margin-left: auto;
  margin-right: auto;
}

.hero {
  padding: 96px 0;
  text-align: center;
  background: linear-gradient(180deg, #f0f6ff 0%, #ffffff 100%);
}

.hero-badge {
  display: inline-block;
  padding: 6px 16px;
  background: var(--primary-light);
  color: var(--primary-dark);
  font-size: 0.85rem;
  font-weight: 600;
  border-radius: 20px;
  margin-bottom: 24px;
}

.hero-title {
  font-size: 3.25rem;
  font-weight: 800;
  line-height: 1.15;
  letter-spacing: 0;
  margin-bottom: 24px;
  max-width: 800px;
  margin-left: auto;
  margin-right: auto;
}

.hero-highlight {
  background: var(--primary-gradient);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.hero-description {
  font-size: 1.2rem;
  color: var(--text-secondary);
  line-height: 1.7;
  max-width: 640px;
  margin: 0 auto 40px;
}

.hero-actions {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-bottom: 64px;
}

.hero-stats {
  display: flex;
  gap: 64px;
  justify-content: center;
}

.hero-stat {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.hero-stat-value {
  font-size: 2rem;
  font-weight: 800;
  color: var(--primary);
}

.hero-stat-label {
  font-size: 0.85rem;
  color: var(--text-muted);
  font-weight: 500;
}

.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 12px 28px;
  font-size: 0.95rem;
  font-weight: 600;
  border-radius: var(--radius);
  border: 2px solid transparent;
  cursor: pointer;
  transition: all 0.15s;
  font-family: inherit;
  text-decoration: none;
}

.btn-primary {
  background: var(--primary);
  color: #ffffff !important;
  border-color: var(--primary);
}

.btn-primary:hover {
  background: var(--primary-dark);
  border-color: var(--primary-dark);
  color: #ffffff !important;
}

.btn-secondary {
  background: var(--text-secondary);
  color: #ffffff !important;
  border-color: var(--text-secondary);
}

.btn-secondary:hover {
  background: var(--text-primary);
  border-color: var(--text-primary);
  color: #ffffff !important;
}

.btn-outline {
  background: transparent;
  color: var(--text-primary);
  border-color: var(--border-color);
}

.btn-outline:hover {
  border-color: var(--primary);
  color: var(--primary);
}

.btn:active {
  transform: scale(0.97);
}

.features-grid,
.v5-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 32px;
}

.feature-card,
.v5-card,
.step-card,
.community-card,
.service-card,
.ca-card,
.deploy-card,
.sponsor-card {
  background: var(--card-bg);
}

.feature-card {
  padding: 32px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  transition: box-shadow 0.2s, transform 0.2s;
}

.feature-card:hover,
.v5-card:hover,
.service-card:hover,
.step-card:hover,
.community-card:hover,
.sponsor-card:hover {
  box-shadow: var(--shadow-md);
  transform: translateY(-2px);
}

.feature-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--primary-light);
  border-radius: var(--radius);
  margin-bottom: 20px;
  color: var(--primary);
}

.feature-icon svg {
  width: 24px;
  height: 24px;
}

.feature-card h3,
.v5-card h3,
.step-card h3 {
  font-size: 1.1rem;
  font-weight: 700;
  margin-bottom: 8px;
}

.feature-card p,
.v5-card p,
.step-card p {
  font-size: 0.9rem;
  color: var(--text-secondary);
  line-height: 1.6;
}

.v5-card {
  padding: 32px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  text-align: center;
  transition: box-shadow 0.2s, transform 0.2s;
}

.v5-icon {
  width: 56px;
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--primary-light);
  border-radius: 50%;
  margin: 0 auto 20px;
  color: var(--primary);
}

.v5-icon svg {
  width: 28px;
  height: 28px;
}

.services-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 20px;
  justify-content: center;
}

.service-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 28px 20px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  transition: box-shadow 0.2s, transform 0.2s;
  width: calc(25% - 15px);
  min-width: 200px;
}

.service-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--primary);
  margin-bottom: 12px;
}

.service-icon svg,
.service-icon img {
  width: 36px;
  height: 36px;
}

.service-card span {
  font-weight: 600;
  font-size: 0.95rem;
}

.service-card small {
  font-size: 0.8rem;
  color: var(--text-muted);
  margin-top: 4px;
}

.providers-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  justify-content: center;
  margin-bottom: 24px;
}

.provider-card {
  padding: 12px 24px;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  font-size: 0.9rem;
  font-weight: 600;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.provider-card:hover {
  border-color: var(--primary);
  box-shadow: var(--shadow-sm);
}

.providers-note {
  text-align: center;
  font-size: 0.85rem;
  color: var(--text-muted);
}

.ca-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 20px;
}

.ca-card {
  padding: 24px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
}

.ca-card strong {
  display: block;
  font-size: 1rem;
  margin-bottom: 6px;
}

.ca-badge {
  display: inline-block;
  padding: 2px 10px;
  font-size: 0.75rem;
  font-weight: 600;
  border-radius: 12px;
  background: #fff3e0;
  color: #e65100;
  margin-bottom: 8px;
}

.ca-free {
  background: #e8f5e9;
  color: #2e7d32;
}

.ca-card p {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.5;
}

.deploy-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 32px;
  max-width: 760px;
  margin: 0 auto 32px;
}

.deploy-card {
  padding: 36px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-xl);
}

.deploy-card-primary {
  border: 2px solid var(--primary);
}

.deploy-version {
  font-size: 1.5rem;
  font-weight: 800;
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.deploy-tag {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 3px 10px;
  border-radius: 12px;
  background: var(--primary);
  color: #ffffff;
}

.deploy-card > p {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin-bottom: 20px;
}

.deploy-features {
  list-style: none;
  margin-bottom: 24px;
}

.deploy-features li {
  padding: 6px 0;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.deploy-features li::before {
  content: "\2713";
  color: var(--success);
  font-weight: 700;
  margin-right: 8px;
}

.deploy-buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.btn-deploy {
  padding: 10px 20px;
  font-size: 0.85rem;
}

.deploy-alt {
  text-align: center;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.deploy-alt a {
  font-weight: 600;
}

.steps-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 32px;
  margin-bottom: 48px;
}

.step-card {
  text-align: center;
  padding: 32px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  transition: box-shadow 0.2s, transform 0.2s;
}

.step-number {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--primary-gradient);
  color: #ffffff;
  font-size: 1.25rem;
  font-weight: 800;
  border-radius: 50%;
  margin: 0 auto 20px;
}

.steps-cta {
  text-align: center;
}

.sponsors-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 32px;
  justify-content: center;
  margin-bottom: 40px;
}

.sponsor-card {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px 40px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  transition: box-shadow 0.2s, transform 0.2s;
}

.sponsor-card img {
  max-height: 48px;
  width: auto;
}

.sponsors-cta {
  text-align: center;
}

.sponsors-cta p {
  font-size: 0.95rem;
  color: var(--text-secondary);
  margin-bottom: 16px;
}

.community-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 24px;
}

.community-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 32px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  transition: box-shadow 0.2s, transform 0.2s;
}

.community-card:hover {
  color: var(--text-primary);
}

.community-card svg {
  margin-bottom: 16px;
  color: var(--primary);
}

.community-card strong {
  font-size: 1.05rem;
  margin-bottom: 4px;
}

.community-card p {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.footer {
  background: var(--text-primary);
  color: #ffffff;
  padding: 64px 0 0;
}

.footer-content {
  display: flex;
  justify-content: space-between;
  gap: 48px;
  padding-bottom: 48px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.footer-brand strong {
  font-size: 1.25rem;
}

.footer-brand p {
  font-size: 0.85rem;
  color: rgba(255, 255, 255, 0.6);
  margin-top: 8px;
  max-width: 300px;
}

.footer-links {
  display: flex;
  gap: 56px;
}

.footer-col h4 {
  font-size: 0.85rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: rgba(255, 255, 255, 0.5);
  margin-bottom: 16px;
}

.footer-col a {
  display: block;
  font-size: 0.9rem;
  color: rgba(255, 255, 255, 0.8);
  padding: 4px 0;
}

.footer-col a:hover {
  color: #ffffff;
}

.footer-bottom {
  padding: 24px 0;
  text-align: center;
}

.footer-bottom p {
  font-size: 0.8rem;
  color: rgba(255, 255, 255, 0.4);
}

.hide-mobile {
  display: inline;
}

@media (max-width: 900px) {
  .section {
    padding: 64px 0;
  }

  .section-title {
    font-size: 1.75rem;
  }

  .section-subtitle {
    font-size: 1rem;
    margin-bottom: 40px;
  }

  .hero {
    padding: 72px 0 64px;
  }

  .hero-title {
    font-size: 2.25rem;
  }

  .hero-description {
    font-size: 1.05rem;
  }

  .hero-stats {
    gap: 32px;
  }

  .hide-mobile {
    display: none;
  }

  .features-grid,
  .v5-grid,
  .steps-grid,
  .community-grid {
    grid-template-columns: repeat(2, 1fr);
    gap: 16px;
  }

  .ca-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .services-grid {
    gap: 12px;
  }

  .service-card {
    width: calc(50% - 6px);
    min-width: 0;
  }

  .deploy-grid {
    grid-template-columns: 1fr;
    gap: 20px;
  }

  .footer-content {
    flex-direction: column;
    gap: 32px;
  }

  .footer-links {
    flex-wrap: wrap;
    gap: 32px;
  }
}

@media (max-width: 640px) {
  .hero-title {
    font-size: 1.75rem;
  }

  .hero-actions {
    flex-direction: column;
    align-items: center;
  }

  .hero-stats {
    flex-direction: column;
    gap: 16px;
  }

  .service-card {
    width: 100%;
  }

  .features-grid,
  .v5-grid,
  .steps-grid,
  .community-grid {
    grid-template-columns: 1fr;
  }

  .ca-grid {
    grid-template-columns: 1fr;
  }

  .deploy-buttons {
    flex-direction: column;
  }
}

@media (prefers-reduced-motion: reduce) {
  .landing-page *,
  .landing-page *::before,
  .landing-page *::after {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
</style>

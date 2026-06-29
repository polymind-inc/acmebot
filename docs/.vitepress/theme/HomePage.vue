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
        <div class="hero-badge">Acmebot v5 &mdash; Production Ready</div>
        <h1 class="hero-title">Automated TLS certificates<br>for <span class="hero-highlight">Microsoft Azure</span></h1>
        <p class="hero-description">Acmebot issues and renews ACME certificates with DNS-01 validation, ARI-aware renewal scheduling, and Azure Key Vault private key storage.</p>
        <div class="hero-actions">
          <a href="#deploy" class="btn btn-primary" @click="scrollToSection($event, '#deploy')">Deploy to Azure</a>
          <a href="/guide/getting-started" class="btn btn-outline">Get Started</a>
        </div>
        <div class="hero-stats">
          <div class="hero-stat">
            <span class="hero-stat-value">5+</span>
            <span class="hero-stat-label">Verified ACME CAs</span>
          </div>
          <div class="hero-stat">
            <span class="hero-stat-value">15+</span>
            <span class="hero-stat-label">DNS provider integrations</span>
          </div>
          <div class="hero-stat">
            <span class="hero-stat-value">9+</span>
            <span class="hero-stat-label">Azure service integrations</span>
          </div>
        </div>
      </div>
    </section>

    <section id="features" class="section">
      <div class="container">
        <h2 class="section-title">Why teams choose Acmebot</h2>
        <p class="section-subtitle">Reduce expiry risk, retire one-off renewal scripts, and keep certificate work inside Azure-native security and monitoring.</p>
        <div class="features-grid">
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="11" width="18" height="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0110 0v4" /></svg>
            </div>
            <h3>Keep private keys in Key Vault</h3>
            <p>Key Vault generates or reuses the private key, and Acmebot merges the ACME-issued chain back into the same certificate.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2v4m0 12v4M4.93 4.93l2.83 2.83m8.48 8.48l2.83 2.83M2 12h4m12 0h4M4.93 19.07l2.83-2.83m8.48-8.48l2.83-2.83" /></svg>
            </div>
            <h3>Operate certificate fleets</h3>
            <p>Each managed certificate keeps its own renewal state, next check time, CA-guided timing when available, and fallback behavior.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="3" width="20" height="14" rx="2" /><path d="M8 21h8m-4-4v4" /></svg>
            </div>
            <h3>Replace one-off scripts</h3>
            <p>Use the dashboard for daily work, the HTTP API for integrations, and the CLI for repeatable automation.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 2L2 7l10 5 10-5-10-5z" /><path d="M2 17l10 5 10-5" /><path d="M2 12l10 5 10-5" /></svg>
            </div>
            <h3>Issue apex, wildcard, and SAN names</h3>
            <p>Cover zone apex, wildcard, and multi-SAN certificates across multiple DNS zones and providers from one deployment.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 8A6 6 0 006 8c0 7-3 9-3 9h18s-3-2-3-9" /><path d="M13.73 21a2 2 0 01-3.46 0" /></svg>
            </div>
            <h3>Route results to your team</h3>
            <p>Send operation results to Slack, Microsoft Teams, Logic Apps, Power Automate, or your own webhook endpoint.</p>
          </div>
          <div class="feature-card">
            <div class="feature-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10" /><path d="M2 12h20M12 2a15.3 15.3 0 014 10 15.3 15.3 0 01-4 10 15.3 15.3 0 01-4-10 15.3 15.3 0 014-10z" /></svg>
            </div>
            <h3>Use the CA your policy requires</h3>
            <p>Start with Let's Encrypt, or use GlobalSign, Google Trust Services, SSL.com, ZeroSSL, or a custom ACME v2 endpoint.</p>
          </div>
        </div>
      </div>
    </section>

    <section class="section section-alt">
      <div class="container">
        <h2 class="section-title">Designed for production rollout</h2>
        <p class="section-subtitle">v5 focuses on making adoption safer for existing deployments and easier to operate after the first certificate is issued.</p>
        <div class="v5-grid">
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 00-1-1.73l-7-4a2 2 0 00-2 0l-7 4A2 2 0 003 8v8a2 2 0 001 1.73l7 4a2 2 0 002 0l7-4A2 2 0 0021 16z" /></svg>
            </div>
            <h3>Preserve what already works</h3>
            <p>Migrate from v4 while keeping existing Key Vault certificates, DNS provider settings, managed identity, and monitoring resources.</p>
          </div>
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 12h-4l-3 9L9 3l-3 9H2" /></svg>
            </div>
            <h3>Use one operation model</h3>
            <p>Issue, renew, revoke, and track operations through the same model whether you use the dashboard, HTTP API, CLI, or scheduled renewal.</p>
          </div>
          <div class="v5-card">
            <div class="v5-icon">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /></svg>
            </div>
            <h3>Keep access and visibility clear</h3>
            <p>Protect the dashboard with authentication and app roles, scope Azure access with managed identity, and monitor with telemetry and webhooks.</p>
          </div>
        </div>
      </div>
    </section>

    <section id="services" class="section">
      <div class="container">
        <h2 class="section-title">Bring renewed certificates to Azure services</h2>
        <p class="section-subtitle">Acmebot owns issuance and renewal; each Azure service consumes the current Key Vault certificate through its supported pattern.</p>
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
              <img src="/images/azure-web-pubsub.svg" alt="Azure Web PubSub" width="36" height="36">
            </div>
            <span>Web PubSub</span>
            <small>Premium tier</small>
          </div>
          <div class="service-card">
            <div class="service-icon">
              <img src="/images/azure-event-grid.svg" alt="Azure Event Grid Namespaces" width="36" height="36">
            </div>
            <span>Event Grid Namespaces</span>
            <small>HTTP / MQTT custom domains</small>
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
        <h2 class="section-title">DNS providers</h2>
        <p class="section-subtitle">Use the DNS provider you already run, or connect Acmebot to your own DNS automation API.</p>
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
        <p class="providers-note">Need another provider? The Custom DNS provider can connect Acmebot to your own DNS automation API.</p>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <h2 class="section-title">Certificate authorities</h2>
        <p class="section-subtitle">Use a verified ACME v2 endpoint, choose a CA with EAB, or point Acmebot at your own ACME directory URL.</p>
        <div class="ca-grid">
          <div class="ca-card">
            <strong>Let's Encrypt</strong>
            <span class="ca-badge ca-free">Free</span>
            <p>Common default for public certificates. No external account binding credentials are required.</p>
          </div>
          <div class="ca-card">
            <strong>GlobalSign</strong>
            <span class="ca-badge">EAB</span>
            <p>Commercial CA option for automated certificate issuance through ACME.</p>
          </div>
          <div class="ca-card">
            <strong>Google Trust Services</strong>
            <span class="ca-badge">EAB</span>
            <p>Google public CA endpoint for ACME-issued certificates.</p>
          </div>
          <div class="ca-card">
            <strong>SSL.com</strong>
            <span class="ca-badge">EAB</span>
            <p>Commercial CA endpoint for 90-day certificates through ACME.</p>
          </div>
          <div class="ca-card">
            <strong>ZeroSSL</strong>
            <span class="ca-badge">EAB</span>
            <p>ACME CA option for teams that already use ZeroSSL accounts.</p>
          </div>
        </div>
      </div>
    </section>

    <section id="deploy" class="section section-alt">
      <div class="container">
        <h2 class="section-title">Deploy to Azure</h2>
        <p class="section-subtitle">Start with the Azure Public template, then connect DNS permissions and dashboard authentication for your environment.</p>
        <div class="deploy-grid">
          <div class="deploy-card deploy-card-primary">
            <h3 class="deploy-version">Acmebot v5 <span class="deploy-tag">GA</span></h3>
            <p>Provision the Function App, storage, monitoring resources, application settings, and optional Key Vault for a new Acmebot environment.</p>
            <ul class="deploy-features">
              <li>Scheduled renewals</li>
              <li>ARI-aware timing</li>
              <li>Key Vault storage</li>
              <li>Private network support</li>
              <li>Azure Monitor telemetry</li>
            </ul>
            <div class="deploy-buttons">
              <a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2Fazuredeploy.json/uiFormDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2Fpolymind-inc%2Facmebot%2Fmaster%2Fdeploy%2FuiFormDefinition.json" class="btn btn-primary btn-deploy">Azure Public</a>
            </div>
          </div>
        </div>
        <div class="deploy-alt">
          <p>Also available from the <a href="https://registry.terraform.io/modules/polymind-inc/acmebot/azurerm/latest">Terraform Registry</a></p>
        </div>
      </div>
    </section>

    <section class="section">
      <div class="container">
        <h2 class="section-title">From deployment to first certificate</h2>
        <p class="section-subtitle">A small amount of Azure setup gives your team an automated certificate path it can keep using.</p>
        <div class="steps-grid">
          <div class="step-card">
            <div class="step-number">1</div>
            <h3>Deploy</h3>
            <p>Deploy the v5 template and choose the ACME endpoint, managed identity, Key Vault, and monitoring settings.</p>
          </div>
          <div class="step-card">
            <div class="step-number">2</div>
            <h3>Configure DNS</h3>
            <p>Grant the selected identity or provider credential permission to create and delete DNS-01 TXT records.</p>
          </div>
          <div class="step-card">
            <div class="step-number">3</div>
            <h3>Issue Certificates</h3>
            <p>Use the dashboard to issue a certificate, confirm the Key Vault version, and let scheduled renewals take over.</p>
          </div>
        </div>
        <div class="steps-cta">
          <a href="/guide/getting-started" class="btn btn-primary">Read the Documentation</a>
        </div>
      </div>
    </section>

    <section class="section section-alt">
      <div class="container">
        <h2 class="section-title">Sponsors</h2>
        <p class="section-subtitle">Thank you to the organizations and individuals who support Acmebot development.</p>
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
        <h2 class="section-title">Open source</h2>
        <p class="section-subtitle">Apache License 2.0 &mdash; open source and free to use.</p>
        <div class="community-grid">
          <a href="https://github.com/polymind-inc/acmebot" class="community-card">
            <svg viewBox="0 0 16 16" width="24" height="24" fill="currentColor"><path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z" /></svg>
            <strong>GitHub</strong>
            <p>Source code, issues, releases, and contributions</p>
          </a>
          <a href="/guide/" class="community-card">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 014 4v14a3 3 0 00-3-3H2z" /><path d="M22 3h-6a4 4 0 00-4 4v14a3 3 0 013-3h7z" /></svg>
            <strong>Documentation</strong>
            <p>Deployment guides, operations guidance, and reference material</p>
          </a>
          <a href="https://github.com/polymind-inc/acmebot/discussions" class="community-card">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15a2 2 0 01-2 2H7l-4 4V5a2 2 0 012-2h14a2 2 0 012 2z" /></svg>
            <strong>Discussions</strong>
            <p>Usage questions, design discussions, and community support</p>
          </a>
        </div>
      </div>
    </section>

    <footer class="footer">
      <div class="container">
        <div class="footer-content">
          <div class="footer-brand">
            <strong>Acmebot</strong>
            <p>ACME SSL/TLS certificate automation for Microsoft Azure</p>
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
              <a href="/guide/getting-started">Getting Started</a>
              <a href="https://github.com/polymind-inc/acmebot/discussions">Discussions</a>
            </div>
            <div class="footer-col">
              <h4>Support</h4>
              <a href="https://github.com/sponsors/shibayan">Sponsor</a>
              <a href="https://polymind.jp/acmebot">Commercial Support</a>
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
  padding: 44px 0 24px;
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
  margin-bottom: 20px;
}

.hero-title {
  font-size: 3.25rem;
  font-weight: 800;
  line-height: 1.15;
  letter-spacing: 0;
  margin-bottom: 20px;
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
  margin: 0 auto 32px;
}

.hero-actions {
  display: flex;
  gap: 16px;
  justify-content: center;
  margin-bottom: 28px;
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
  border-radius: var(--radius-lg);}

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
  text-align: center;}

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
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  max-width: 980px;
  margin: 0 auto;
}

.service-card {
  display: grid;
  grid-template-columns: 36px minmax(0, 1fr);
  grid-template-rows: auto auto;
  align-items: center;
  column-gap: 12px;
  text-align: left;
  padding: 14px 16px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius);
  min-width: 0;
  min-height: 76px;
}

.service-icon {
  grid-row: 1 / span 2;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--primary);
  margin: 0;
}

.service-icon svg,
.service-icon img {
  width: 30px;
  height: 30px;
}

.service-card span {
  font-weight: 600;
  font-size: 0.92rem;
  line-height: 1.25;
}

.service-card small {
  font-size: 0.76rem;
  color: var(--text-muted);
  line-height: 1.35;
  margin-top: 2px;
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
  max-width: 640px;
  margin: 0 auto 32px;
}

.deploy-card {
  padding: 40px 32px;
  border: 1px solid var(--border-color);
  border-radius: var(--radius-xl);
  text-align: center;
}

.deploy-card-primary {
  border: 2px solid var(--primary);
}

.deploy-version {
  font-size: 1.5rem;
  font-weight: 800;
  margin: 0 0 12px;
  display: flex;
  align-items: center;
  justify-content: center;
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
  max-width: 520px;
  margin: 0 auto 20px;
}

.deploy-features {
  list-style: none;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 24px;
  max-width: 440px;
  margin: 0 auto 28px;
}

.deploy-features li {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0;
  font-size: 0.9rem;
  color: var(--text-secondary);
  text-align: left;
}

.deploy-features li::before {
  content: "\2713";
  flex: 0 0 auto;
  color: var(--success);
  font-weight: 700;
}

.deploy-buttons {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px;
}

.btn-deploy {
  padding: 10px 20px;
  font-size: 0.85rem;
  min-width: 148px;
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
  border-radius: var(--radius-lg);}

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
  border-radius: var(--radius-lg);}

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
  color: var(--text-primary);}

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
    padding: 56px 0 48px;
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
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 12px;
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
    font-size: 1.65rem;
  }

  .hero-description {
    font-size: 0.98rem;
    line-height: 1.55;
  }

  .hero-actions {
    flex-direction: column;
    align-items: center;
    margin-bottom: 32px;
  }

  .hero-stats {
    flex-direction: row;
    align-items: flex-start;
    gap: 8px;
  }

  .hero-stat {
    flex: 1 1 0;
    min-width: 0;
  }

  .hero-stat-value {
    font-size: 1.5rem;
  }

  .hero-stat-label {
    font-size: 0.72rem;
    line-height: 1.3;
  }

  .service-card {
    min-height: 0;
    padding: 12px 14px;
  }

  .services-grid {
    grid-template-columns: 1fr;
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

  .deploy-features {
    grid-template-columns: 1fr;
    max-width: 260px;
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

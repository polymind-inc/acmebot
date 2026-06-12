import { defineConfig } from "vitepress";

const guideSidebar = [
  {
    text: "Start",
    items: [
      { text: "Overview", link: "/guide/" },
      { text: "Getting Started", link: "/guide/getting-started" }
    ]
  },
  {
    text: "Deploy",
    items: [
      { text: "Deployment", link: "/guide/deployment" },
      { text: "Migrating from v4 to v5", link: "/guide/migration-v5" }
    ]
  },
  {
    text: "Configure",
    items: [
      { text: "DNS Providers", link: "/guide/dns-providers" },
      { text: "Certificate Authorities", link: "/guide/certificate-authorities" },
      { text: "Azure Service Integration", link: "/guide/service-integration" }
    ]
  },
  {
    text: "Operate",
    items: [
      { text: "Dashboard", link: "/guide/dashboard" },
      { text: "Operations", link: "/guide/operations" },
      { text: "Troubleshooting", link: "/guide/troubleshooting" },
      { text: "FAQ", link: "/guide/faq" },
      { text: "Support", link: "/guide/support" }
    ]
  },
  {
    text: "Reference",
    items: [
      { text: "Configuration", link: "/reference/configuration" },
      { text: "Architecture", link: "/reference/architecture" },
      { text: "HTTP API", link: "/reference/api" },
      { text: "Security", link: "/reference/security" }
    ]
  }
];

export default defineConfig({
  lang: "en-US",
  title: "Acmebot",
  description: "Automated ACME SSL/TLS certificate management for Microsoft Azure.",
  lastUpdated: true,
  srcExclude: ["README.md"],
  head: [
    ["meta", { name: "theme-color", content: "#0078d4" }],
    ["link", { rel: "preconnect", href: "https://fonts.googleapis.com" }],
    ["link", { rel: "preconnect", href: "https://fonts.gstatic.com", crossorigin: "" }],
    ["link", { rel: "stylesheet", href: "https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&display=swap" }],
    ["meta", { property: "og:title", content: "Acmebot for Microsoft Azure" }],
    ["meta", { property: "og:description", content: "Automated ACME SSL/TLS certificate management built around Azure Key Vault." }],
    ["meta", { property: "og:image", content: "https://acmebot.dev/images/ogp.png" }],
    ["meta", { property: "og:type", content: "website" }],
    ["meta", { name: "twitter:card", content: "summary_large_image" }]
  ],
  themeConfig: {
    nav: [
      { text: "Guide", link: "/guide/" },
      { text: "Reference", link: "/reference/configuration" },
      { text: "Deploy", link: "/guide/deployment" },
      { text: "Support", link: "/guide/support" },
      { text: "GitHub", link: "https://github.com/polymind-inc/acmebot" }
    ],
    sidebar: {
      "/guide/": guideSidebar,
      "/reference/": guideSidebar
    },
    socialLinks: [
      { icon: "github", link: "https://github.com/polymind-inc/acmebot" }
    ],
    search: {
      provider: "local"
    },
    editLink: {
      pattern: "https://github.com/polymind-inc/acmebot/edit/master/docs/:path",
      text: "Edit this page on GitHub"
    },
    footer: {
      message: "Released under the Apache License 2.0.",
      copyright: "Copyright (c) 2026 Acmebot Project"
    }
  }
});

import { defineConfig } from "vitepress";

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
      { text: "GitHub", link: "https://github.com/polymind-inc/acmebot" }
    ],
    sidebar: {
      "/guide/": [
        {
          text: "Guide",
          items: [
            { text: "Overview", link: "/guide/" },
            { text: "Getting Started", link: "/guide/getting-started" },
            { text: "Deployment", link: "/guide/deployment" },
            { text: "Dashboard", link: "/guide/dashboard" },
            { text: "DNS Providers", link: "/guide/dns-providers" },
            { text: "Certificate Authorities", link: "/guide/certificate-authorities" },
            { text: "Operations", link: "/guide/operations" },
            { text: "Azure Service Integration", link: "/guide/service-integration" },
            { text: "Troubleshooting", link: "/guide/troubleshooting" },
            { text: "FAQ", link: "/guide/faq" }
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
      ],
      "/reference/": [
        {
          text: "Guide",
          items: [
            { text: "Overview", link: "/guide/" },
            { text: "Getting Started", link: "/guide/getting-started" },
            { text: "Deployment", link: "/guide/deployment" },
            { text: "Dashboard", link: "/guide/dashboard" },
            { text: "DNS Providers", link: "/guide/dns-providers" },
            { text: "Certificate Authorities", link: "/guide/certificate-authorities" },
            { text: "Operations", link: "/guide/operations" },
            { text: "Azure Service Integration", link: "/guide/service-integration" },
            { text: "Troubleshooting", link: "/guide/troubleshooting" },
            { text: "FAQ", link: "/guide/faq" }
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
      ]
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

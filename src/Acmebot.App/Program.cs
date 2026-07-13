using System.Text.Json;

using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Infrastructure;
using Acmebot.App.Notifications;
using Acmebot.App.Options;
using Acmebot.App.Providers;
using Acmebot.App.Services;

using Azure.Core;
using Azure.Functions.Worker.Extensions.HttpApi.Config;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Security.KeyVault.Certificates;
using Azure.Storage.Blobs;

using DnsClient;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication()
       .AddHttpApi();

builder.Services.AddOpenTelemetry()
       .WithMetrics(metrics => metrics.AddHttpClientInstrumentation())
       .WithTracing(tracing => tracing.AddHttpClientInstrumentation())
       .UseFunctionsWorkerDefaults()
       .UseAzureMonitorExporter();

builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.IncludeFields = true;
});

// Add Options
builder.Services.AddOptions<AcmebotOptions>()
       .Bind(builder.Configuration.GetSection("Acmebot"))
       .ValidateDataAnnotations();

builder.Services.AddWebhookOptions(builder.Configuration.GetSection("Acmebot:Webhook"));

// Add Services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AppRoleService>();
builder.Services.AddSingleton<CertificateQueryService>();
builder.Services.AddSingleton<DnsZoneQueryService>();
builder.Services.AddSingleton<CertificateOperationService>();

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;

    var lookupClientOptions = options.UseSystemNameServer ? new LookupClientOptions() : new LookupClientOptions(NameServer.GooglePublicDns, NameServer.GooglePublicDns2);

    lookupClientOptions.UseCache = false;
    lookupClientOptions.UseRandomNameServer = true;

    return new LookupClient(lookupClientOptions);
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;

    return AzureEnvironment.Get(options.Environment);
});

builder.Services.AddSingleton<TokenCredential>(provider =>
{
    var environment = provider.GetRequiredService<AzureEnvironment>();
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;

    var managedIdentityId = string.IsNullOrWhiteSpace(options.ManagedIdentityClientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId);

    return new ManagedIdentityCredential(new ManagedIdentityCredentialOptions(managedIdentityId)
    {
        AuthorityHost = environment.AuthorityHost
    });
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
    var credential = provider.GetRequiredService<TokenCredential>();

    return new CertificateClient(new Uri(options.VaultBaseUrl), credential);
});

builder.Services.AddSingleton(provider =>
{
    const string acmeStateContainerName = "acmebot-state";

    var environment = provider.GetRequiredService<AzureEnvironment>();
    var configuration = provider.GetRequiredService<IConfiguration>();

    var storageSettings = AzureWebJobsStorageSettings.FromConfiguration(configuration);
    var connectionString = storageSettings.ConnectionString;

    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return new BlobContainerClient(connectionString, acmeStateContainerName);
    }

    var blobServiceUri = storageSettings.GetBlobServiceUri();
    var clientId = storageSettings.ClientId;

    var managedIdentityId = string.IsNullOrWhiteSpace(clientId) ? ManagedIdentityId.SystemAssigned : ManagedIdentityId.FromUserAssignedClientId(clientId);

    var credential = new ManagedIdentityCredential(new ManagedIdentityCredentialOptions(managedIdentityId)
    {
        AuthorityHost = environment.AuthorityHost
    });

    return new BlobContainerClient(new Uri(new Uri(blobServiceUri.TrimEnd('/') + "/"), acmeStateContainerName), credential);
});

builder.Services.AddSingleton<BlobAcmeStateStore>();
builder.Services.AddSingleton<FileSystemAcmeStateStore>();
builder.Services.AddSingleton<IAcmeStateStore>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();

    return HasAzureFilesContentShare(configuration) ? provider.GetRequiredService<FileSystemAcmeStateStore>() : provider.GetRequiredService<BlobAcmeStateStore>();
});
builder.Services.AddSingleton<AcmeClientFactory>();

// Add Webhook invoker
builder.Services.AddSingleton<WebhookInvoker>();

builder.Services.AddSingleton<IWebhookPayloadBuilder>(provider =>
{
    var acmebotOptions = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
    var webhookOptions = provider.GetRequiredService<IOptions<WebhookOptions>>().Value;

    return WebhookPayloadBuilderFactory.Create(acmebotOptions, webhookOptions);
});

// Add DNS Providers
builder.Services.AddSingleton<IEnumerable<IDnsProvider>>(provider =>
{
    var options = provider.GetRequiredService<IOptions<AcmebotOptions>>().Value;
    var environment = provider.GetRequiredService<AzureEnvironment>();
    var tokenCredential = provider.GetRequiredService<TokenCredential>();

    var dnsProviders = new List<IDnsProvider>();

    dnsProviders.TryAdd(options.Akamai, o => new AkamaiEdgeDnsProvider(o));
    dnsProviders.TryAdd(options.AzureDns, o => new AzureDnsProvider(
        o,
        environment,
        ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
    dnsProviders.TryAdd(options.AzurePrivateDns, o => new AzurePrivateDnsProvider(
        o,
        environment,
        ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
    dnsProviders.TryAdd(options.Cloudflare, o => new CloudflareProvider(o));
    dnsProviders.TryAdd(options.CustomDns, o => new CustomDnsProvider(o));
    dnsProviders.TryAdd(options.DnsMadeEasy, o => new DnsMadeEasyProvider(o));
    dnsProviders.TryAdd(options.GandiLiveDns, o => new GandiLiveDnsProvider(o));
    dnsProviders.TryAdd(options.GoDaddy, o => new GoDaddyProvider(o));
    dnsProviders.TryAdd(options.GoogleDns, o => new GoogleDnsProvider(o, ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
    dnsProviders.TryAdd(options.IonosDns, o => new IonosDnsProvider(o));
    dnsProviders.TryAdd(options.Ovh, o => new OvhProvider(o));
    dnsProviders.TryAdd(options.PowerDns, o => new PowerDnsProvider(o));
    dnsProviders.TryAdd(options.Regfish, o => new RegfishProvider(o));
    dnsProviders.TryAdd(options.Route53, o => new Route53Provider(o, ResolveCredential(environment, tokenCredential, o.ManagedIdentityClientId)));
    dnsProviders.TryAdd(options.TransIp, o => new TransIpProvider(options, o, tokenCredential));
    dnsProviders.TryAdd(options.UnitedDomains, o => new UnitedDomainsProvider(o));

    if (dnsProviders.Count == 0)
    {
        throw new NotSupportedException("No DNS provider is configured. Configure one before starting Acmebot.");
    }

    return dnsProviders;
});

builder.Build().Run();

return;

static bool HasAzureFilesContentShare(IConfiguration configuration)
    => !string.IsNullOrEmpty(configuration["WEBSITE_CONTENTAZUREFILECONNECTIONSTRING"]) || !string.IsNullOrEmpty(configuration["WEBSITE_CONTENTSHARE"]);

static TokenCredential ResolveCredential(AzureEnvironment environment, TokenCredential tokenCredential, string? managedIdentityClientId)
{
    if (string.IsNullOrWhiteSpace(managedIdentityClientId))
    {
        return tokenCredential;
    }

    var managedIdentityId = ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId);

    return new ManagedIdentityCredential(new ManagedIdentityCredentialOptions(managedIdentityId)
    {
        AuthorityHost = environment.AuthorityHost
    });
}

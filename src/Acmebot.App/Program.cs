using System.Text.Json;

using Acmebot.App.Acme;
using Acmebot.App.Extensions;
using Acmebot.App.Infrastructure;
using Acmebot.App.Notifications;
using Acmebot.App.Options;
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
       .ValidateDataAnnotations()
       .ValidateOnStart();

builder.Services.AddWebhookOptions(builder.Configuration.GetSection("Acmebot:Webhook"));

// Add Services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AppRoleService>();
builder.Services.AddSingleton<CertificateQueryService>();
builder.Services.AddSingleton<DnsZoneQueryService>();
builder.Services.AddSingleton<CertificateOperationService>();

builder.Services.AddSingleton<ILookupClient>(provider =>
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

    var blobServiceUri = storageSettings.GetBlobServiceUri(environment.StorageEndpointSuffix);
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
builder.Services.AddDnsProviders();

builder.Build().Run();

return;

static bool HasAzureFilesContentShare(IConfiguration configuration)
    => !string.IsNullOrEmpty(configuration["WEBSITE_CONTENTAZUREFILECONNECTIONSTRING"]) || !string.IsNullOrEmpty(configuration["WEBSITE_CONTENTSHARE"]);

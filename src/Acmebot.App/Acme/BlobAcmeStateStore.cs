using System.Text.Json;

using Acmebot.App.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Options;

namespace Acmebot.App.Acme;

internal sealed class BlobAcmeStateStore(BlobContainerClient containerClient, IOptions<AcmebotOptions> options) : IAcmeStateStore
{
    private const string ContentType = "application/json";

    private readonly Uri _endpoint = options.Value.Endpoint;

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<TState?> LoadAsync<TState>(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobClient = containerClient.GetBlobClient(CreateBlobName(path));
            var response = await blobClient.DownloadContentAsync(cancellationToken);

            return response.Value.Content.ToObjectFromJson<TState>(s_jsonSerializerOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return default;
        }
    }

    public async Task SaveAsync<TState>(TState value, string path, CancellationToken cancellationToken = default)
    {
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(CreateBlobName(path));
        var content = BinaryData.FromObjectAsJson(value, s_jsonSerializerOptions);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentType
                }
            },
            cancellationToken);
    }

    private string CreateBlobName(string path) => $"{_endpoint.Host}/{path}";
}

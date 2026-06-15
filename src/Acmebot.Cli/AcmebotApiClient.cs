using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Azure.Core;

namespace Acmebot.Cli;

internal sealed class AcmebotApiClient(HttpClient httpClient, Uri endpoint, TokenCredential credential, string[] scopes) : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly HttpClient _httpClient = httpClient;
    private readonly Uri _endpoint = endpoint;
    private readonly TokenCredential _credential = credential;
    private readonly string[] _scopes = scopes;
    private AccessToken? _accessToken;

    public async Task<IReadOnlyList<CertificateItem>> GetCertificatesAsync(CancellationToken cancellationToken) => await GetJsonAsync<IReadOnlyList<CertificateItem>>("api/certificates", cancellationToken);

    public async Task<IReadOnlyList<DnsZoneGroup>> GetDnsZonesAsync(CancellationToken cancellationToken) => await GetJsonAsync<IReadOnlyList<DnsZoneGroup>>("api/dns-zones", cancellationToken);

    public async Task<Uri> IssueCertificateAsync(CertificatePolicyItem policy, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("api/certificates"))
        {
            Content = JsonContent.Create(policy, options: s_jsonOptions)
        };

        return await StartOperationAsync(request, cancellationToken);
    }

    public async Task<Uri> RenewCertificateAsync(string certificateName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri($"api/certificates/{Uri.EscapeDataString(certificateName)}/renew"));

        return await StartOperationAsync(request, cancellationToken);
    }

    public async Task RevokeCertificateAsync(string certificateName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri($"api/certificates/{Uri.EscapeDataString(certificateName)}/revoke"));
        using var response = await SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task WaitForOperationAsync(Uri operationLocation, TimeSpan pollInterval, TimeSpan timeout, TextWriter progress, CancellationToken cancellationToken)
    {
        using var timeoutTokenSource = new CancellationTokenSource(timeout);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutTokenSource.Token);

        try
        {
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                using var response = await SendAsync(request, linkedTokenSource.Token);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    await progress.WriteLineAsync("Operation is still running...");
                    await Task.Delay(pollInterval, linkedTokenSource.Token);
                    continue;
                }

                await EnsureSuccessAsync(response, linkedTokenSource.Token);
            }
        }
        catch (OperationCanceledException) when (timeoutTokenSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation did not complete within {timeout.TotalSeconds:N0} seconds.");
        }
    }

    public void Dispose() => _httpClient.Dispose();

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri(path));
        using var response = await SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var value = await response.Content.ReadFromJsonAsync<T>(s_jsonOptions, cancellationToken);

        return value ?? throw new AcmebotApiException(response.StatusCode, "The API returned an empty response.");
    }

    private async Task<Uri> StartOperationAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        if (response.Headers.Location is null)
        {
            throw new AcmebotApiException(response.StatusCode, "The operation response did not include a Location header.");
        }

        return BuildLocationUri(response.Headers.Location);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is { ExpiresOn: var expiresOn, Token: var token } && expiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return token;
        }

        var accessToken = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken);
        _accessToken = accessToken;

        return accessToken.Token;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = response.Content is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);
        ProblemDetails? problemDetails = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                problemDetails = JsonSerializer.Deserialize<ProblemDetails>(text, s_jsonOptions);
            }
            catch (JsonException)
            {
                // Fall back to the raw response text below.
            }
        }

        var message = GetProblemMessage(response.StatusCode, problemDetails, text);

        throw new AcmebotApiException(response.StatusCode, message, problemDetails);
    }

    private static string GetProblemMessage(HttpStatusCode statusCode, ProblemDetails? problemDetails, string? rawText)
    {
        if (problemDetails?.Errors is { Count: > 0 })
        {
            return string.Join(Environment.NewLine, problemDetails.Errors.SelectMany(error => error.Value));
        }

        return problemDetails?.Detail
            ?? problemDetails?.Output
            ?? problemDetails?.Title
            ?? rawText
            ?? $"HTTP {(int)statusCode} {statusCode}";
    }

    private Uri BuildUri(string path) => new(_endpoint, path);

    private Uri BuildLocationUri(Uri location)
    {
        if (location.IsAbsoluteUri)
        {
            return location;
        }

        if (location.OriginalString.StartsWith("/", StringComparison.Ordinal))
        {
            return new Uri(new Uri(_endpoint.GetLeftPart(UriPartial.Authority)), location.OriginalString.TrimStart('/'));
        }

        return new Uri(_endpoint, location);
    }
}

using System.Net;

using Acmebot.App.Extensions;

using Xunit;

namespace Acmebot.App.Tests;

public sealed class HttpClientJsonExtensionsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteAsJsonAsync_DisposesRequestAfterSendCompletes(bool fail)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var handler = new DeleteHandler(fail);
        using var client = new HttpClient(handler);

        var sendTask = client.DeleteAsJsonAsync("https://dns.example/records", new { name = "example" }, cancellationToken);
        handler.CompleteSend();

        if (fail)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => sendTask);
        }
        else
        {
            using var response = await sendTask;
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var content = Assert.IsAssignableFrom<HttpContent>(handler.RequestContent);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => content.ReadAsStringAsync(cancellationToken));
    }

    private sealed class DeleteHandler(bool fail) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _sendGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HttpContent? RequestContent { get; private set; }

        public void CompleteSend() => _sendGate.SetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = Assert.IsAssignableFrom<HttpContent>(request.Content);
            RequestContent = content;

            await _sendGate.Task.WaitAsync(cancellationToken);
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
            Assert.Equal("""{"name":"example"}""", await content.ReadAsStringAsync(cancellationToken));

            if (fail)
            {
                throw new HttpRequestException("Simulated send failure.");
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
